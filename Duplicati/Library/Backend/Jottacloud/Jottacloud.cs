// Copyright (C) 2025, The Duplicati Team
// https://duplicati.com, hello@duplicati.com
// 
// Permission is hereby granted, free of charge, to any person obtaining a 
// copy of this software and associated documentation files (the "Software"), 
// to deal in the Software without restriction, including without limitation 
// the rights to use, copy, modify, merge, publish, distribute, sublicense, 
// and/or sell copies of the Software, and to permit persons to whom the 
// Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in 
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS 
// OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
// DEALINGS IN THE SOFTWARE.

using Duplicati.Library.Common.IO;
using Duplicati.Library.Interface;
using Duplicati.Library.Localization.Short;
using Duplicati.Library.Utility;
using Duplicati.Library.Utility.Options;
using System.Net;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Tasks;
namespace Duplicati.Library.Backend
{
    // ReSharper disable once UnusedMember.Global
    // This class is instantiated dynamically in the BackendLoader.
    public class Jottacloud : IBackend, IStreamingBackend
    {
        private static readonly string TOKEN_URL = OAuthHelper.OAUTH_LOGIN_URL("jottacloud");
        private const string JFS_ROOT = "https://jfs.jottacloud.com/jfs";
        private const string JFS_ROOT_UPLOAD = "https://up.jottacloud.com/jfs"; // Separate host for uploading files
        private const string JFS_BUILTIN_DEVICE = "Jotta"; // The built-in device used for the built-in Sync and Archive mount points.
        private static readonly string JFS_DEFAULT_BUILTIN_MOUNT_POINT = "Archive"; // When using the built-in device we pick this mount point as our default.
        private static readonly string JFS_DEFAULT_CUSTOM_MOUNT_POINT = "Duplicati"; // When custom device is specified then we pick this mount point as our default.
        private static readonly string[] JFS_BUILTIN_MOUNT_POINTS = { "Archive", "Sync" }; // Name of built-in mount points that we can use.
        private static readonly string[] JFS_BUILTIN_ILLEGAL_MOUNT_POINTS = { "Trash", "Links", "Latest", "Shared" }; // Name of built-in mount points that we can not use. These are treated as mount points in the API, but they are for used for special functionality and we cannot upload files to them!
        private const string JFS_DEVICE_OPTION = "jottacloud-device";
        private const string JFS_MOUNT_POINT_OPTION = "jottacloud-mountpoint";
        private const string JFS_THREADS = "jottacloud-threads";
        private const string JFS_CHUNKSIZE = "jottacloud-chunksize";
        private const string JFS_DATE_FORMAT = "yyyy'-'MM'-'dd-'T'HH':'mm':'ssK";
        private readonly string m_device;
        private readonly bool m_device_builtin;
        private readonly string m_mountPoint;
        private readonly string m_path;
        private readonly string m_url_device;
        private readonly string m_url;
        private readonly string m_url_upload;

        private static readonly string JFS_DEFAULT_CHUNKSIZE = "5mb";
        private static readonly string JFS_DEFAULT_THREADS = "4";
        private readonly int m_threads;
        private readonly long m_chunksize;
        private readonly TimeoutOptionsHelper.Timeouts m_timeouts;

        private readonly JottacloudAuthHelper m_oauth;

        /// <summary>
        /// The default maximum number of concurrent connections allowed by a ServicePoint object is 2.
        /// It should be increased to allow multiple download threads.
        /// https://stackoverflow.com/a/44637423/1105812
        /// </summary>
        static Jottacloud()
        {
            // TODO: Remove this when HttpClient migration is done
            ServicePointManager.DefaultConnectionLimit = 1000;
        }

        public Jottacloud()
        {
            m_device = null!;
            m_mountPoint = null!;
            m_path = null!;
            m_url_device = null!;
            m_url = null!;
            m_url_upload = null!;
            m_timeouts = null!;
            m_oauth = null!;
        }

        public Jottacloud(string url, Dictionary<string, string?> options)
        {
            // Duplicati back-end url for Jottacloud is in format "jottacloud://folder/subfolder", we transform them to
            // the Jottacloud REST API (JFS) url format "https://jfs.jottacloud.com/jfs/[username]/[device]/[mountpoint]/[folder]/[subfolder]".

            // Find out what JFS device to use.
            var device = options.GetValueOrDefault(JFS_DEVICE_OPTION);
            if (!string.IsNullOrWhiteSpace(device))
            {
                // Custom device specified.
                m_device = device;
                if (string.Equals(m_device, JFS_BUILTIN_DEVICE, StringComparison.OrdinalIgnoreCase))
                {
                    m_device_builtin = true; // Device is configured, but value set to the built-in device!
                    m_device = JFS_BUILTIN_DEVICE; // Ensure correct casing (doesn't seem to matter, but in theory it could).
                }
                else
                {
                    m_device_builtin = false;
                }
            }
            else
            {
                // Use default: The built-in device.
                m_device = JFS_BUILTIN_DEVICE;
                m_device_builtin = true;
            }

            // Find out what JFS mount point to use on the device.
            var mountPoint = options.GetValueOrDefault(JFS_MOUNT_POINT_OPTION);
            if (!string.IsNullOrWhiteSpace(mountPoint))
            {
                // Custom mount point specified.
                m_mountPoint = mountPoint;

                // If we are using the built-in device make sure we have picked a mount point that we can use.
                if (m_device_builtin)
                {
                    // Check that it is not set to one of the special built-in mount points that we definitely cannot make use of.
                    if (Array.FindIndex(JFS_BUILTIN_ILLEGAL_MOUNT_POINTS, x => x.Equals(m_mountPoint, StringComparison.OrdinalIgnoreCase)) != -1)
                        throw new UserInformationException(Strings.Jottacloud.IllegalMountPoint, "JottaIllegalMountPoint");
                    // Check if it is one of the legal built-in mount points.
                    // What to do if it is not is open for discussion: The JFS API supports creation of custom mount points not only
                    // for custom (backup) devices, but also for the built-in device. But this will not be visible via the official
                    // web interface, so you are kind of working in the dark and need to use the REST API to delete it etc. Therefore
                    // we do not allow this for now, although in future maybe we could consider it, as a "hidden" location?
                    var i = Array.FindIndex(JFS_BUILTIN_MOUNT_POINTS, x => x.Equals(m_mountPoint, StringComparison.OrdinalIgnoreCase));
                    if (i != -1)
                        m_mountPoint = JFS_BUILTIN_MOUNT_POINTS[i]; // Ensure correct casing (doesn't seem to matter, but in theory it could).
                    else
                        throw new UserInformationException(Strings.Jottacloud.IllegalMountPoint, "JottaIllegalMountPoint"); // User defined mount points on built-in device currently not allowed.
                }
            }
            else
            {
                if (m_device_builtin)
                    m_mountPoint = JFS_DEFAULT_BUILTIN_MOUNT_POINT; // Set a suitable built-in mount point for the built-in device.
                else
                    m_mountPoint = JFS_DEFAULT_CUSTOM_MOUNT_POINT; // Set a suitable default mount point for custom (backup) devices.
            }

            var authId = AuthIdOptionsHelper.Parse(options)
                .RequireCredentials(TOKEN_URL);

            m_oauth = new JottacloudAuthHelper(authId.AuthId!);

            // Build URL
            var u = new Utility.Uri(url);
            m_path = u.HostAndPath; // Host and path of "jottacloud://folder/subfolder" is "folder/subfolder", so the actual folder path within the mount point.
            if (string.IsNullOrEmpty(m_path)) // Require a folder. Actually it is possible to store files directly on the root level of the mount point, but that does not seem to be a good option.
                throw new UserInformationException(Strings.Jottacloud.NoPathError, "JottaNoPath");
            m_path = Util.AppendDirSeparator(m_path, "/");

            m_url_device = JFS_ROOT + "/" + m_oauth.Username + "/" + m_device;
            m_url = m_url_device + "/" + m_mountPoint + "/" + m_path;
            m_url_upload = JFS_ROOT_UPLOAD + "/" + m_oauth.Username + "/" + m_device + "/" + m_mountPoint + "/" + m_path; // Different hostname, else identical to m_url.

            var jfsThreads = options.GetValueOrDefault(JFS_THREADS);
            if (string.IsNullOrWhiteSpace(jfsThreads))
                jfsThreads = JFS_DEFAULT_THREADS;

            m_threads = int.Parse(jfsThreads);

            var jfsChunksize = options.GetValueOrDefault(JFS_CHUNKSIZE);
            if (string.IsNullOrWhiteSpace(jfsChunksize))
                jfsChunksize = JFS_DEFAULT_CHUNKSIZE;

            var chunksize = Sizeparser.ParseSize(jfsChunksize, "mb");

            // Chunk size is bound by BinaryReader.ReadBytes(length) where length is an int.

            if (chunksize > int.MaxValue || chunksize < 1024)
                throw new ArgumentOutOfRangeException(nameof(chunksize), $"The chunk size cannot be less than {Utility.Utility.FormatSizeString(1024)}, nor larger than {Utility.Utility.FormatSizeString(int.MaxValue)}");

            m_chunksize = chunksize;
            m_timeouts = TimeoutOptionsHelper.Parse(options);
        }

        #region IBackend Members

        public string DisplayName => Strings.Jottacloud.DisplayName;

        public string ProtocolKey => "jottacloud";

        /// <inheritdoc />
        public async IAsyncEnumerable<IFileEntry> ListAsync([EnumeratorCancellation] CancellationToken cancelToken)
        {
            // Remove warning until this backend is rewritten with HttpClient
            await Task.CompletedTask;

            var doc = new System.Xml.XmlDocument();
            try
            {
                // Send request and load XML response.
                var req = CreateRequest(WebRequestMethods.Http.Get, "", "", false);
                var areq = new AsyncHttpRequest(req);
                await Utility.Utility.WithTimeout(m_timeouts.ListTimeout, cancelToken, _ =>
                {
                    using (var rs = areq.GetResponseStream())
                        doc.Load(rs);
                }).ConfigureAwait(false);
            }
            catch (WebException wex)
            {
                if (wex.Response is HttpWebResponse response && response.StatusCode == HttpStatusCode.NotFound)
                    throw new FolderMissingException(wex);
                throw;
            }
            // Handle XML response. Since we in the constructor demand a folder below the mount point we know the root
            // element must be a "folder", else it could also have been a "mountPoint" (which has a very similar structure).
            // We must check for "deleted" attribute, because files/folders which has it is deleted (attribute contains the timestamp of deletion)
            // so we treat them as non-existent here.
            var xRoot = doc.DocumentElement;
            if (xRoot == null)
                throw new FolderMissingException();
            if (xRoot.Attributes["deleted"] != null)
                throw new FolderMissingException();

            var folders = xRoot.SelectNodes("folders/folder[not(@deleted)]");
            if (folders != null)
                foreach (System.Xml.XmlNode xFolder in folders)
                {
                    // Subfolders are only listed with name. We can get a timestamp by sending a request for each folder, but that is probably not necessary?
                    var name = xFolder.Attributes?["name"]?.Value;
                    if (string.IsNullOrEmpty(name))
                        continue;

                    yield return new FileEntry(name) { IsFolder = true };
                }

            var files = xRoot.SelectNodes("files/file[not(@deleted)]");
            if (files != null)
                foreach (System.Xml.XmlNode xFile in files)
                {
                    var fe = ToFileEntry(xFile);
                    if (fe != null)
                        yield return fe;
                }
        }

        public static IFileEntry? ToFileEntry(System.Xml.XmlNode? xFile)
        {
            if (xFile == null)
                return null;

            var name = xFile.Attributes?["name"]?.Value;
            if (string.IsNullOrWhiteSpace(name))
                return null;

            // Normal files have an "currentRevision", which represent the most recent successfully upload
            // (could also checked that currentRevision/state is "COMPLETED", but should not be necessary).
            // There might also be a newer "latestRevision" coming from an incomplete or corrupt upload,
            // but we ignore that here and use the information about the last valid version.
            var xRevision = xFile.SelectSingleNode("currentRevision");
            if (xRevision != null)
            {
                var xState = xRevision.SelectSingleNode("state");
                if (xState != null && xState.InnerText == "COMPLETED") // Think "currentRevision" always is a complete version, but just to be on the safe side..
                {
                    var xSize = xRevision.SelectSingleNode("size");
                    long size;
                    if (xSize == null || !long.TryParse(xSize.InnerText, out size))
                        size = -1;

                    var xModified = xRevision.SelectSingleNode("modified"); // There is created, modified and updated time stamps, but not last accessed.
                    if (xModified == null || !DateTime.TryParseExact(xModified.InnerText, JFS_DATE_FORMAT, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out var lastModified))
                        lastModified = new DateTime();
                    return new FileEntry(name, size, lastModified, lastModified);
                }
            }
            return null;
        }

        /// <summary>
        /// Retrieves info for a single file (used to determine file size for chunking)
        /// </summary>
        /// <param name="remotename">The remote file name</param>
        /// <param name="cancelToken">The cancellation token</param>
        /// <returns>The file entry</returns>
        public async Task<IFileEntry?> Info(string remotename, CancellationToken cancelToken)
        {
            var doc = new System.Xml.XmlDocument();
            try
            {
                // Send request and load XML response.
                var req = CreateRequest(WebRequestMethods.Http.Get, remotename, "", false);
                var areq = new Utility.AsyncHttpRequest(req);
                await Utility.Utility.WithTimeout(m_timeouts.ShortTimeout, cancelToken, _ =>
                {
                    using (var rs = areq.GetResponseStream())
                        doc.Load(rs);
                }).ConfigureAwait(false);
            }
            catch (WebException wex)
            {
                if (wex.Response is HttpWebResponse response && response.StatusCode == HttpStatusCode.NotFound)
                    throw new FileMissingException(wex);
                throw;
            }
            // Handle XML response. Since we in the constructor demand a folder below the mount point we know the root
            // element must be a "folder", else it could also have been a "mountPoint" (which has a very similar structure).
            // We must check for "deleted" attribute, because files/folders which has it is deleted (attribute contains the timestamp of deletion)
            // so we treat them as non-existent here.
            var xFile = doc.DocumentElement;
            if (xFile?.Attributes["deleted"] != null)
                throw new FileMissingException(string.Format("{0}: {1}", LC.L("The requested file does not exist"), remotename));

            return ToFileEntry(xFile);
        }

        public async Task PutAsync(string remotename, string filename, CancellationToken cancelToken)
        {
            using (var fs = File.OpenRead(filename))
                await PutAsync(remotename, fs, cancelToken).ConfigureAwait(false);
        }

        public async Task GetAsync(string remotename, string filename, CancellationToken cancelToken)
        {
            using (var fs = File.Create(filename))
                await GetAsync(remotename, fs, cancelToken).ConfigureAwait(false);
        }

        public Task DeleteAsync(string remotename, CancellationToken cancelToken)
        {
            var req = CreateRequest(WebRequestMethods.Http.Post, remotename, "rm=true", false); // rm=true means permanent delete, dl=true would be move to trash.
            var areq = new Utility.AsyncHttpRequest(req);

            return Utility.Utility.WithTimeout(m_timeouts.ShortTimeout, cancelToken, _ =>
            {
                using (var resp = (HttpWebResponse)areq.GetResponse())
                { }
            });
        }

        public IList<ICommandLineArgument> SupportedCommands => [
            .. AuthIdOptionsHelper.GetOptions(TOKEN_URL),
            new CommandLineArgument(JFS_DEVICE_OPTION, CommandLineArgument.ArgumentType.String, Strings.Jottacloud.DescriptionDeviceShort, Strings.Jottacloud.DescriptionDeviceLong(JFS_MOUNT_POINT_OPTION)),
            new CommandLineArgument(JFS_MOUNT_POINT_OPTION, CommandLineArgument.ArgumentType.String, Strings.Jottacloud.DescriptionMountPointShort, Strings.Jottacloud.DescriptionMountPointLong(JFS_DEVICE_OPTION)),
            new CommandLineArgument(JFS_THREADS, CommandLineArgument.ArgumentType.Integer, Strings.Jottacloud.ThreadsShort, Strings.Jottacloud.ThreadsLong, JFS_DEFAULT_THREADS),
            new CommandLineArgument(JFS_CHUNKSIZE, CommandLineArgument.ArgumentType.Size, Strings.Jottacloud.ChunksizeShort, Strings.Jottacloud.ChunksizeLong, JFS_DEFAULT_CHUNKSIZE),
            .. TimeoutOptionsHelper.GetOptions(),
        ];

        public string Description => Strings.Jottacloud.Description;

        public Task TestAsync(CancellationToken cancelToken)
            => this.TestListAsync(cancelToken);

        public async Task CreateFolderAsync(CancellationToken cancelToken)
        {
            // When using custom (backup) device we must create the device first (if not already exists).
            if (!m_device_builtin)
            {
                var req = CreateRequest(WebRequestMethods.Http.Post, m_url_device, "type=WORKSTATION"); // Hard-coding device type. Must be one of "WORKSTATION", "LAPTOP", "IMAC", "MACBOOK", "IPAD", "ANDROID", "IPHONE" or "WINDOWS_PHONE".
                var areq = new AsyncHttpRequest(req);
                await Utility.Utility.WithTimeout(m_timeouts.ShortTimeout, cancelToken, _ =>
                {
                    using (var resp = (HttpWebResponse)areq.GetResponse())
                    { }
                }).ConfigureAwait(false);
            }
            // Create the folder path, and if using custom mount point it will be created as well in the same operation.
            {
                var req = CreateRequest(WebRequestMethods.Http.Post, "", "mkDir=true", false);
                var areq = new AsyncHttpRequest(req);
                await Utility.Utility.WithTimeout(m_timeouts.ShortTimeout, cancelToken, _ =>
                {
                    using (var resp = (HttpWebResponse)areq.GetResponse())
                    { }
                }).ConfigureAwait(false);
            }

        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
        }

        #endregion

        private HttpWebRequest CreateRequest(string method, string url, string queryparams)
        {
            return m_oauth.CreateRequest(url + (string.IsNullOrEmpty(queryparams) || queryparams.Trim().Length == 0 ? "" : "?" + queryparams), method);
        }

        private HttpWebRequest CreateRequest(string method, string remotename, string queryparams, bool upload)
        {
            var url = (upload ? m_url_upload : m_url) + Library.Utility.Uri.UrlEncode(remotename).Replace("+", "%20");
            return CreateRequest(method, url, queryparams);
        }

        public Task<string[]> GetDNSNamesAsync(CancellationToken cancelToken) => Task.FromResult(new string[] {
            new System.Uri(JFS_ROOT).Host,
            new System.Uri(JFS_ROOT_UPLOAD).Host
        });

        public async Task GetAsync(string remotename, Stream stream, CancellationToken cancelToken)
        {
            if (m_threads > 1)
            {
                using (var timeoutStream = stream.ObserveWriteTimeout(m_timeouts.ReadWriteTimeout, false))
                    await ParallelGetAsync(remotename, stream, cancelToken).ConfigureAwait(false);
                return;
            }
            // Downloading from Jottacloud: Will only succeed if the file has a completed revision,
            // and if there are multiple versions of the file we will only get the latest completed version,
            // ignoring any incomplete or corrupt versions.
            var req = CreateRequest(WebRequestMethods.Http.Get, remotename, "mode=bin", false);
            var areq = new AsyncHttpRequest(req);
            using (var s = await Utility.Utility.WithTimeout(m_timeouts.ShortTimeout, cancelToken, _ => areq.GetResponseStream()).ConfigureAwait(false))
            using (var t = s.ObserveReadTimeout(m_timeouts.ReadWriteTimeout))
                await Utility.Utility.CopyStreamAsync(t, stream, true, cancelToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Fetches the file in chunks (parallelized)
        /// </summary>
        public async Task ParallelGetAsync(string remotename, Stream stream, CancellationToken cancelToken)
        {
            var info = await Info(remotename, cancelToken);
            if (info == null)
                throw new FileMissingException(remotename);
            var size = info.Size;

            var chunks = new Queue<Tuple<long, long>>(); // Tuple => Position (from), Position (to)

            long position = 0;

            while (position < size)
            {
                var length = Math.Min(m_chunksize, size - position);
                chunks.Enqueue(new Tuple<long, long>(position, position + length));
                position += length;
            }

            var tasks = new Queue<Task<byte[]>>();

            while (tasks.Count > 0 || chunks.Count > 0)
            {
                cancelToken.ThrowIfCancellationRequested();
                while (chunks.Count > 0 && tasks.Count < m_threads)
                {
                    var item = chunks.Dequeue();
                    tasks.Enqueue(Task.Run(() =>
                    {
                        var req = CreateRequest(WebRequestMethods.Http.Get, remotename, "mode=bin", false);
                        req.AddRange(item.Item1, item.Item2 - 1);
                        var areq = new AsyncHttpRequest(req);
                        using (var s = areq.GetResponseStream())
                        using (var reader = new BinaryReader(s))
                        {
                            var length = item.Item2 - item.Item1;
                            return reader.ReadBytes((int)length);
                        }
                    }));
                }
                var buffer = await tasks.Dequeue().ConfigureAwait(false);
                await stream.WriteAsync(buffer, 0, buffer.Length, cancelToken).ConfigureAwait(false);
            }
        }

        public async Task PutAsync(string remotename, Stream stream, CancellationToken cancelToken)
        {
            // Some challenges with uploading to Jottacloud:
            // - Jottacloud supports use of a custom header where we can tell the server the MD5 hash of the file
            //   we are uploading, and then it will verify the content of our request against it. But the HTTP
            //   status code we get back indicates success even if there is a mismatch, so we must dig into the
            //   XML response to see if we were able to correctly upload the new content or not. Another issue 
            //   is that if the stream is not seek-able we have a challenge pre-calculating MD5 hash on it before
            //   writing it out on the HTTP request stream. And even if the stream is seek-able it may be throttled.
            //   One way to avoid using the throttled stream for calculating the MD5 is to try to get the
            //   underlying stream from the "m_basestream" field, with fall-back to a temporary file.
            // - We can instead chose to upload the data without setting the MD5 hash header. The server will
            //   calculate the MD5 on its side and return it in the response back to use. We can then compare it
            //   with the MD5 hash of the stream (using a MD5CalculatingStream), and if there is a mismatch we can
            //   request the server to remove the file again and throw an exception. But there is a requirement that
            //   we specify the file size in a custom header. And if the stream is not seek-able we are not able
            //   to use stream.Length, so we are back at square one.

            (stream, var md5Hash, var tmp) = await Utility.Utility.CalculateThrottledStreamHash(stream, "MD5", cancelToken).ConfigureAwait(false);
            using var _ = tmp;

            // Create request, with query parameter, and a few custom headers.
            // NB: If we wanted to we could send the same POST request as below but without the file contents
            // and with "cphash=[md5Hash]" as the only query parameter. Then we will get an HTTP 200 (OK) response
            // if an identical file already exists, and we can skip uploading the new file. We will get
            // HTTP 404 (Not Found) if file does not exists or it exists with a different hash, in which
            // case we must send a new request to upload the new content.
            var fileSize = stream.Length;
            var req = CreateRequest(WebRequestMethods.Http.Post, remotename, "umode=nomultipart", true);
            req.Headers.Add("JMd5", md5Hash); // Not required, but it will make the server verify the content and mark the file as corrupt if there is a mismatch.
            req.Headers.Add("JSize", fileSize.ToString()); // Required, and used to mark file as incomplete if we upload something  be the total size of the original file!
                                                           // File time stamp headers: Since we are working with a stream here we do not know the local file's timestamps,
                                                           // and then we can just omit the JCreated and JModified and let the server automatically set the current time.
                                                           //req.Headers.Add("JCreated", timeCreated);
                                                           //req.Headers.Add("JModified", timeModified);
            req.ContentType = "application/octet-stream";
            req.ContentLength = fileSize;

            // Write post data request
            var areq = new AsyncHttpRequest(req);
            using (var rs = areq.GetRequestStream())
            using (var ts = rs.ObserveWriteTimeout(m_timeouts.ReadWriteTimeout))
                await Utility.Utility.CopyStreamAsync(stream, rs, true, cancelToken).ConfigureAwait(false);
            // Send request, and check response
            using (var resp = (HttpWebResponse)areq.GetResponse())
            {
                if (resp.StatusCode != HttpStatusCode.Created)
                    throw new WebException(Strings.Jottacloud.FileUploadError, null, WebExceptionStatus.ProtocolError, resp);

                // Request seems to be successful, but we must verify the response XML content to be sure that the file
                // was correctly uploaded: The server will verify the JSize header and mark the file as incomplete if
                // there was mismatch, and it will verify the JMd5 header and mark the file as corrupt if there was a hash
                // mismatch. The returned XML contains a file element, and if upload was error free it contains a single
                // child element "currentRevision", which has a "state" child element with the string "COMPLETED".
                // If there was a problem we should have a "latestRevision" child element, and this will have state with
                // value "INCOMPLETE" or "CORRUPT". If the file was new or had no previous complete versions the latestRevision
                // will be the only child, but if not there may also be a "currentRevision" representing the previous
                // complete version - and then we need to detect the case where our upload failed but there was an existing
                // complete version!
                using (var rs = areq.GetResponseStream())
                {
                    var doc = new System.Xml.XmlDocument();
                    try { doc.Load(rs); }
                    catch (System.Xml.XmlException)
                    {
                        throw new WebException(Strings.Jottacloud.FileUploadError, WebExceptionStatus.ProtocolError);
                    }
                    bool uploadCompletedSuccessfully = false;
                    var xFile = doc["file"];
                    if (xFile != null)
                    {
                        var xRevState = xFile.SelectSingleNode("latestRevision");
                        if (xRevState == null)
                        {
                            xRevState = xFile.SelectSingleNode("currentRevision/state");
                            if (xRevState != null)
                                uploadCompletedSuccessfully = xRevState.InnerText == "COMPLETED"; // Success: There is no "latestRevision", only a "currentRevision" (and it specifies the file is complete, but I think it always will).
                        }
                    }
                    if (!uploadCompletedSuccessfully) // Report error (and we just let the incomplete/corrupt file revision stay on the server..)
                        throw new WebException(Strings.Jottacloud.FileUploadError, WebExceptionStatus.ProtocolError);
                }
            }
        }
    }
}
