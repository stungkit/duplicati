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
using COSXML;
using COSXML.Auth;
using COSXML.Model.Bucket;
using COSXML.Model.Object;
using COSXML.Model.Tag;
using COSXML.Utils;
using Duplicati.Library.Common.IO;
using Duplicati.Library.Interface;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Duplicati.Library.Backend.TencentCOS
{
    /// <summary>
    /// Tencent COS
    /// en: https://intl.cloud.tencent.com/document/product/436
    /// zh: https://cloud.tencent.com/document/product/436
    /// </summary>
    public class COS : IBackend, IStreamingBackend, IRenameEnabledBackend
    {
        private static readonly string LOGTAG = Logging.Log.LogTagFromType<COS>();

        private const string COS_APP_ID = "cos-app-id";
        private const string COS_REGION = "cos-region";
        private const string COS_SECRET_ID = "cos-secret-id";
        private const string COS_SECRET_KEY = "cos-secret-key";
        private const string COS_BUCKET = "cos-bucket";
        private const string COS_STORAGE_CLASS = "cos-storage-class";

        /// <summary>
        /// Set default HTTPS request
        /// </summary>
        private const bool COS_USE_SSL = true;
        /// <summary>
        /// Connection timeout time, in milliseconds, default 45000ms
        /// </summary>
        private const int CONNECTION_TIMEOUT_MS = 1000 * 60 * 60;
        /// <summary>
        /// Read and write timeout time, in milliseconds, default 45000ms
        /// </summary>
        private const int READ_WRITE_TIMEOUT_MS = 1000 * 60 * 60;
        /// <summary>
        /// The valid time of each request signature, unit: second
        /// </summary>
        private const int KEY_DURATION_SECOND = 60 * 60;

        private readonly CosOptions _cosOptions;

        CosXml cosXml;

        /// <summary>
        /// Tencent Cloud Object Storage Configuration
        /// </summary>
        public class CosOptions
        {
            /// <summary>
            /// Tencent Cloud Account APPID
            /// </summary>
            public string Appid { get; set; }
            /// <summary>
            /// Cloud API Secret ID
            /// </summary>
            public string SecretId { get; set; }
            /// <summary>
            /// Cloud API Secret Key
            /// </summary>
            public string SecretKey { get; set; }
            /// <summary>
            /// Bucket region ap-guangzhou ap-hongkong
            /// en: https://intl.cloud.tencent.com/document/product/436/6224
            /// zh: https://cloud.tencent.com/document/product/436/6224
            /// </summary>
            public string Region { get; set; }
            /// <summary>
            /// Bucket, format: BucketName-APPID
            /// </summary>
            public string Bucket { get; set; }
            /// <summary>
            /// A path or subfolder in a bucket
            /// </summary>
            public string Path { get; set; }
            /// <summary>
            /// Storage class of the object
            /// en: https://intl.cloud.tencent.com/document/product/436/30925
            /// zh: https://cloud.tencent.com/document/product/436/33417
            /// </summary>
            public string StorageClass { get; set; }
        }

        public COS() { }

        public COS(string url, Dictionary<string, string> options)
        {
            _cosOptions = new CosOptions();

            var uri = new Utility.Uri(url?.Trim());
            var prefix = uri.HostAndPath?.Trim()?.Trim('/')?.Trim('\\');

            if (!string.IsNullOrEmpty(prefix))
            {
                _cosOptions.Path = prefix + "/";
            }

            if (options.ContainsKey(COS_APP_ID))
            {
                _cosOptions.Appid = options[COS_APP_ID];
            }

            if (options.ContainsKey(COS_REGION))
            {
                _cosOptions.Region = options[COS_REGION];
            }

            if (options.ContainsKey(COS_SECRET_ID))
            {
                _cosOptions.SecretId = options[COS_SECRET_ID];
            }

            if (options.ContainsKey(COS_SECRET_KEY))
            {
                _cosOptions.SecretKey = options[COS_SECRET_KEY];
            }

            if (options.ContainsKey(COS_BUCKET))
            {
                _cosOptions.Bucket = options[COS_BUCKET];
            }

            if (options.ContainsKey(COS_STORAGE_CLASS))
            {
                _cosOptions.StorageClass = options[COS_STORAGE_CLASS];
            }
        }

        CosXml GetCosXml()
        {
            string appid = _cosOptions.Appid;
            string region = _cosOptions.Region;
            string secretId = _cosOptions.SecretId;
            string secretKey = _cosOptions.SecretKey;

            CosXmlConfig config = new CosXmlConfig.Builder()
              .SetConnectionTimeoutMs(CONNECTION_TIMEOUT_MS)
              .SetReadWriteTimeoutMs(READ_WRITE_TIMEOUT_MS)
              .IsHttps(COS_USE_SSL)
              .SetAppid(appid)
              .SetRegion(region)
              .Build();

            // Initialization QCloudCredentialProvider, COS SDK provides three ways: the permanent temporary keys custom
            QCloudCredentialProvider cosCredentialProvider = new DefaultQCloudCredentialProvider(secretId, secretKey, KEY_DURATION_SECOND);

            return new CosXmlServer(config, cosCredentialProvider);
        }

        /// <inheritdoc />
        public async IAsyncEnumerable<IFileEntry> ListAsync([EnumeratorCancellation] CancellationToken cancelToken)
        {
            bool isTruncated = true;
            string filename = null;

            while (isTruncated)
            {
                cosXml = GetCosXml();
                var bucket = _cosOptions.Bucket;
                var prefix = _cosOptions.Path;

                var request = new GetBucketRequest(bucket);
                request.SetSign(TimeUtils.GetCurrentTime(TimeUnit.SECONDS), 600);

                if (!string.IsNullOrEmpty(filename))
                    request.SetMarker(filename);

                if (!string.IsNullOrEmpty(prefix))
                    request.SetPrefix(prefix);

                var tcs = new TaskCompletionSource<GetBucketResult>();
                cancelToken.Register(() => tcs.TrySetCanceled());
                cosXml.GetBucket(request, (result) => tcs.SetResult(result as GetBucketResult), (clientEx, serverEx) => tcs.SetException((Exception)clientEx ?? serverEx));
                var result = await tcs.Task.ConfigureAwait(false);

                var info = result.listBucket;

                isTruncated = result.listBucket.isTruncated;
                filename = result.listBucket.nextMarker;

                foreach (var item in info.contentsList)
                {
                    var last = DateTime.Parse(item.lastModified);

                    var fileName = item.key;
                    if (!string.IsNullOrWhiteSpace(prefix))
                    {
                        fileName = fileName.Substring(prefix.Length);

                        if (fileName.StartsWith("/", StringComparison.Ordinal))
                        {
                            fileName = fileName.Trim('/');
                        }
                    }

                    yield return new FileEntry(fileName, item.size, last, last);
                }
            }
        }

        public async Task PutAsync(string remotename, string filename, CancellationToken cancelToken)
        {
            using (FileStream fs = File.Open(filename, FileMode.Open, FileAccess.Read, FileShare.Read))
                await PutAsync(remotename, fs, cancelToken).ConfigureAwait(false);
        }

        public async Task GetAsync(string remotename, string filename, CancellationToken cancelToken)
        {
            using (var fs = File.Open(filename, FileMode.Create, FileAccess.Write, FileShare.None))
                await GetAsync(remotename, fs, cancelToken).ConfigureAwait(false);
        }

        public Task DeleteAsync(string remotename, CancellationToken cancelToken)
        {
            try
            {
                cosXml = GetCosXml();
                string bucket = _cosOptions.Bucket;
                string key = GetFullKey(remotename);

                var request = new DeleteObjectRequest(bucket, key);
                cancelToken.Register(() => request.Cancel());

                request.SetSign(TimeUtils.GetCurrentTime(TimeUnit.SECONDS), 600);

                var result = cosXml.DeleteObject(request);
            }
            catch (COSXML.CosException.CosClientException clientEx)
            {
                Logging.Log.WriteErrorMessage(LOGTAG, "Delete", clientEx, "Delete failed: {0}", remotename);
                throw;
            }
            catch (COSXML.CosException.CosServerException serverEx)
            {
                Logging.Log.WriteErrorMessage(LOGTAG, "Delete", serverEx, "Delete failed: {0}, {1}", remotename, serverEx.GetInfo());
                throw;
            }

            return Task.CompletedTask;
        }

        public Task TestAsync(CancellationToken cancelToken)
        {
            var json = JsonConvert.SerializeObject(_cosOptions);
            try
            {
                cosXml = GetCosXml();
                string bucket = _cosOptions.Bucket;
                var request = new HeadBucketRequest(bucket);
                request.SetSign(TimeUtils.GetCurrentTime(TimeUnit.SECONDS), 600);
                cancelToken.Register(() => request.Cancel());
                var result = cosXml.HeadBucket(request);

                Logging.Log.WriteInformationMessage(LOGTAG, "Test", "Request complete {0}: {1}, {2}", result.httpCode, json, result.GetResultInfo());
            }
            catch (COSXML.CosException.CosClientException clientEx)
            {
                Logging.Log.WriteErrorMessage(LOGTAG, "Test", clientEx, "Request failed: {0}", json);
                throw;
            }
            catch (COSXML.CosException.CosServerException serverEx)
            {
                Logging.Log.WriteErrorMessage(LOGTAG, "Test", serverEx, "Request failed: {0}, {1}", json, serverEx.GetInfo());
                throw;
            }

            return Task.CompletedTask;
        }

        public Task CreateFolderAsync(CancellationToken cancelToken)
        {
            // No need to create folders
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (cosXml != null)
            {
                cosXml = null;
            }
        }

        public Task PutAsync(string remotename, Stream stream, CancellationToken cancelToken)
        {
            try
            {
                cosXml = GetCosXml();
                string bucket = _cosOptions.Bucket;
                string key = GetFullKey(remotename);

                byte[] buffer = new byte[stream.Length];
                if (Utility.Utility.ForceStreamRead(stream, buffer, buffer.Length) != stream.Length)
                {
                    throw new Exception("Bad file read");
                }

                PutObjectRequest request = new PutObjectRequest(bucket, key, buffer);

                request.SetSign(TimeUtils.GetCurrentTime(TimeUnit.SECONDS), KEY_DURATION_SECOND);
                request.SetRequestHeader("Content-Type", "application/octet-stream");
                if (!string.IsNullOrEmpty(_cosOptions.StorageClass))
                {
                    request.SetRequestHeader("x-" + COS_STORAGE_CLASS, _cosOptions.StorageClass);
                }
                cancelToken.Register(() => request.Cancel());

                var result = cosXml.PutObject(request);
            }
            catch (COSXML.CosException.CosClientException clientEx)
            {
                Logging.Log.WriteErrorMessage(LOGTAG, "PutAsync", clientEx, "Put failed: {0}", remotename);
                throw;
            }
            catch (COSXML.CosException.CosServerException serverEx)
            {
                Logging.Log.WriteErrorMessage(LOGTAG, "PutAsync", serverEx, "Put failed: {0}, {1}", remotename, serverEx.GetInfo());
                throw;
            }

            return Task.CompletedTask;
        }

        public async Task GetAsync(string remotename, Stream stream, CancellationToken cancelToken)
        {
            try
            {
                cosXml = GetCosXml();
                string bucket = _cosOptions.Bucket;
                string key = GetFullKey(remotename);

                var request = new GetObjectBytesRequest(bucket, key);

                request.SetSign(TimeUtils.GetCurrentTime(TimeUnit.SECONDS), KEY_DURATION_SECOND);
                cancelToken.Register(() => request.Cancel());

                var result = cosXml.GetObject(request);

                var bytes = result.content;

                using (var ms = new MemoryStream(bytes))
                    await Utility.Utility.CopyStreamAsync(ms, stream, cancelToken).ConfigureAwait(false);
            }
            catch (COSXML.CosException.CosClientException clientEx)
            {
                Logging.Log.WriteErrorMessage(LOGTAG, "Get", clientEx, "Get failed: {0}", remotename);
                throw;
            }
            catch (COSXML.CosException.CosServerException serverEx)
            {
                Logging.Log.WriteErrorMessage(LOGTAG, "Get", serverEx, "Get failed: {0}, {1}", remotename, serverEx.GetInfo());
                throw;
            }
        }

        public async Task RenameAsync(string oldname, string newname, CancellationToken cancelToken)
        {
            try
            {
                cosXml = GetCosXml();
                var sourceAppid = _cosOptions.Appid;
                var sourceBucket = _cosOptions.Bucket;
                var sourceRegion = _cosOptions.Region;
                var sourceKey = GetFullKey(oldname);

                var copySource = new CopySourceStruct(sourceAppid, sourceBucket, sourceRegion, sourceKey);

                var bucket = _cosOptions.Bucket;
                var key = GetFullKey(newname);
                var request = new CopyObjectRequest(bucket, key);

                request.SetSign(TimeUtils.GetCurrentTime(TimeUnit.SECONDS), KEY_DURATION_SECOND);
                request.SetCopySource(copySource);
                request.SetCopyMetaDataDirective(COSXML.Common.CosMetaDataDirective.COPY);
                cancelToken.Register(() => request.Cancel());

                var result = cosXml.CopyObject(request);

                //Console.WriteLine(result.GetResultInfo());

                await DeleteAsync(oldname, cancelToken).ConfigureAwait(false);
            }
            catch (COSXML.CosException.CosClientException clientEx)
            {
                Logging.Log.WriteErrorMessage(LOGTAG, "Rename", clientEx, "Rename failed: {0} to {1}", oldname, newname);
                throw;
            }
            catch (COSXML.CosException.CosServerException serverEx)
            {
                Logging.Log.WriteErrorMessage(LOGTAG, "Rename", serverEx, "Rename failed: {0} to {1}, {2}", oldname, newname, serverEx.GetInfo());
                throw;
            }
        }

        public string DisplayName => Strings.COSBackend.DisplayName;

        public string Description => Strings.COSBackend.Description;

        public string ProtocolKey => "cos";

        public IList<ICommandLineArgument> SupportedCommands
        {
            get
            {
                return new List<ICommandLineArgument>(new ICommandLineArgument[] {
                    new CommandLineArgument(COS_APP_ID, CommandLineArgument.ArgumentType.String, Strings.COSBackend.COSAccountDescriptionShort, Strings.COSBackend.COSAccountDescriptionLong),
                    new CommandLineArgument(COS_REGION, CommandLineArgument.ArgumentType.String, Strings.COSBackend.COSLocationDescriptionShort, Strings.COSBackend.COSLocationDescriptionLong),
                    new CommandLineArgument(COS_SECRET_ID, CommandLineArgument.ArgumentType.String, Strings.COSBackend.COSAPISecretIdDescriptionShort, Strings.COSBackend.COSAPISecretIdDescriptionLong),
                    new CommandLineArgument(COS_SECRET_KEY, CommandLineArgument.ArgumentType.Password, Strings.COSBackend.COSAPISecretKeyDescriptionShort, Strings.COSBackend.COSAPISecretKeyDescriptionLong),
                    new CommandLineArgument(COS_BUCKET, CommandLineArgument.ArgumentType.String, Strings.COSBackend.COSBucketDescriptionShort, Strings.COSBackend.COSBucketDescriptionLong),
                    new CommandLineArgument(COS_STORAGE_CLASS, CommandLineArgument.ArgumentType.String, Strings.COSBackend.COSStorageClassDescriptionShort, Strings.COSBackend.COSStorageClassDescriptionLong)
                });
            }
        }

        public Task<string[]> GetDNSNamesAsync(CancellationToken cancelToken) => Task.FromResult(Array.Empty<string>());

        private string GetFullKey(string name)
        {
            if (string.IsNullOrWhiteSpace(_cosOptions?.Path))
            {
                return name;
            }
            return _cosOptions.Path + name;
        }
    }
}
