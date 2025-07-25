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
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Duplicati.Library.Interface;
using Duplicati.Library.Main.Database;
using Duplicati.Library.Main.Operation.Common;
using System.Threading.Tasks;
using Duplicati.Library.Utility;
using System.Data;
using System.Threading;

namespace Duplicati.Library.Main.Volumes
{
    public class FilesetVolumeWriter : VolumeWriterBase
    {
        private readonly Library.Utility.TempFile m_tempFile;
        private readonly Stream m_tempStream;
        private StreamWriter m_streamwriter;
        private readonly JsonWriter m_writer;
        private long m_filecount;
        private long m_foldercount;

        public override RemoteVolumeType FileType { get { return RemoteVolumeType.Files; } }

        public FilesetVolumeWriter(Options options, DateTime timestamp)
            : base(options, timestamp)
        {
            m_tempFile = new Library.Utility.TempFile();
            m_tempStream = File.Open(m_tempFile, FileMode.Create, FileAccess.ReadWrite);
            m_streamwriter = new StreamWriter(m_tempStream, ENCODING);
            m_writer = new JsonTextWriter(m_streamwriter);
            m_writer.WriteStartArray();
        }

        private void WriteMetaProperties(string metahash, long metasize, string metablockhash, IEnumerable<string> metablocklisthashes)
        {
            m_writer.WritePropertyName("metahash");
            m_writer.WriteValue(metahash);
            m_writer.WritePropertyName("metasize");
            m_writer.WriteValue(metasize);

            if (metablocklisthashes != null)
            {
                // Slightly awkward, but we avoid writing if there are no entries.
                using (var en = metablocklisthashes.GetEnumerator())
                {
                    if (en.MoveNext() && !string.IsNullOrEmpty(en.Current))
                    {
                        m_writer.WritePropertyName("metablocklists");
                        m_writer.WriteStartArray();
                        m_writer.WriteValue(en.Current);
                        while (en.MoveNext())
                            m_writer.WriteValue(en.Current);
                        m_writer.WriteEndArray();
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(metablockhash))
            {
                m_writer.WritePropertyName("metablockhash");
                m_writer.WriteValue(metablockhash);
            }
        }

        public async Task AddFile(string name, string filehash, long size, DateTime lastmodified, string metahash, long metasize, string metablockhash, string blockhash, long blocksize, IAsyncEnumerable<string> blocklisthashes, IEnumerable<string> metablocklisthashes)
        {
            await AddFileEntry(FilelistEntryType.File, name, filehash, size, lastmodified, metahash, metasize, metablockhash, blockhash, blocksize, blocklisthashes, metablocklisthashes)
                .ConfigureAwait(false);
        }

        public async Task AddAlternateStream(string name, string filehash, long size, DateTime lastmodified, string metahash, string metablockhash, long metasize, string blockhash, long blocksize, IAsyncEnumerable<string> blocklisthashes, IEnumerable<string> metablocklisthashes)
        {
            await AddFileEntry(FilelistEntryType.AlternateStream, name, filehash, size, lastmodified, metahash, metasize, metablockhash, blockhash, blocksize, blocklisthashes, metablocklisthashes)
                .ConfigureAwait(false);
        }

        private async Task AddFileEntry(FilelistEntryType type, string name, string filehash, long size, DateTime lastmodified, string metahash, long metasize, string metablockhash, string blockhash, long blocksize, IAsyncEnumerable<string> blocklisthashes, IEnumerable<string> metablocklisthashes)
        {
            m_filecount++;
            m_writer.WriteStartObject();
            m_writer.WritePropertyName("type");
            m_writer.WriteValue(type.ToString());
            m_writer.WritePropertyName("path");
            m_writer.WriteValue(name);
            m_writer.WritePropertyName("hash");
            m_writer.WriteValue(filehash);
            m_writer.WritePropertyName("size");
            m_writer.WriteValue(size);
            m_writer.WritePropertyName("time");
            m_writer.WriteValue(Library.Utility.Utility.SerializeDateTime(lastmodified));
            if (metahash != null)
                WriteMetaProperties(metahash, metasize, metablockhash, metablocklisthashes);

            if (blocklisthashes != null)
            {
                //Slightly awkward, but we avoid writing if there are no entries
                await using (var en = blocklisthashes.GetAsyncEnumerator())
                {
                    if (await en.MoveNextAsync().ConfigureAwait(false) && !string.IsNullOrEmpty(en.Current))
                    {
                        m_writer.WritePropertyName("blocklists");
                        m_writer.WriteStartArray();
                        m_writer.WriteValue(en.Current);
                        while (await en.MoveNextAsync().ConfigureAwait(false))
                            m_writer.WriteValue(en.Current);
                        m_writer.WriteEndArray();
                    }
                }
            }
            else if (!string.IsNullOrWhiteSpace(blockhash))
            {
                m_writer.WritePropertyName("blockhash");
                m_writer.WriteValue(blockhash);
                m_writer.WritePropertyName("blocksize");
                m_writer.WriteValue(blocksize);
            }

            m_writer.WriteEndObject();
        }

        public void AddDirectory(string name, string metahash, long metasize, string metablockhash, IEnumerable<string> metablocklisthashes)
        {
            AddMetaEntry(FilelistEntryType.Folder, name, metahash, metasize, metablockhash, metablocklisthashes);
        }

        public void AddMetaEntry(FilelistEntryType type, string name, string metahash, long metasize, string metablockhash, IEnumerable<string> metablocklisthashes)
        {
            m_foldercount++;
            m_writer.WriteStartObject();
            m_writer.WritePropertyName("type");
            m_writer.WriteValue(type.ToString());
            m_writer.WritePropertyName("path");
            m_writer.WriteValue(name);
            if (metahash != null)
                WriteMetaProperties(metahash, metasize, metablockhash, metablocklisthashes);

            m_writer.WriteEndObject();
        }

        public override void Close()
        {
            if (m_streamwriter != null)
            {
                this.AddFilelistFile();
            }

            if (m_tempStream != null)
            {
                m_tempStream.Dispose();
            }

            if (m_tempFile != null)
            {
                m_tempFile.Dispose();
            }

            base.Close();
        }

        private void AddFilelistFile()
        {
            m_writer.WriteEndArray();
            m_writer.Flush();
            m_streamwriter.Flush();

            try
            {
                using (Stream sr = m_compression.CreateFile(FILELIST, CompressionHint.Compressible, DateTime.UtcNow))
                {
                    m_tempStream.Seek(0, SeekOrigin.Begin);
                    m_tempStream.CopyTo(sr);
                    sr.Flush();
                }
            }
            finally
            {
                m_writer.Close();
                m_streamwriter.Dispose();
                m_streamwriter = null;
            }
        }

        public void AddControlFile(string localfile, CompressionHint hint, string filename = null)
        {
            filename = filename ?? System.IO.Path.GetFileName(localfile);
            using (var t = m_compression.CreateFile(CONTROL_FILES_FOLDER + filename, hint, DateTime.UtcNow))
            using (var s = System.IO.File.OpenRead(localfile))
                Library.Utility.Utility.CopyStream(s, t);
        }

        public override void Dispose()
        {
            this.Close();
            base.Dispose();
        }

        public long FileCount { get { return m_filecount; } }
        public long FolderCount { get { return m_foldercount; } }

        public void AddSymlink(string name, string metahash, long metasize, string metablockhash, IEnumerable<string> metablocklisthashes)
        {
            AddMetaEntry(FilelistEntryType.Symlink, name, metahash, metasize, metablockhash, metablocklisthashes);
        }

        public void CreateFilesetFile(bool isFullBackup)
        {
            using (var sr = new StreamWriter(this.m_compression.CreateFile(FILESET_FILENAME, CompressionHint.Compressible, DateTime.UtcNow), ENCODING))
            {
                sr.Write(FilesetData.GetFilesetInstance(isFullBackup));
            }
        }

        /// <summary>
        /// Probes for an unused filename, using the current time as a starting point
        /// </summary>
        /// <param name="database">The database to check for clashes</param>
        /// <param name="options">The options to use for the filename</param>
        /// <param name="start">The starting time to probe from</param>
        /// <param name="cancellationToken">The cancellation token to use for the operation</param>
        /// <param name="increment">The time to increment by each probe</param>
        /// <param name="maxTries">The maximum number of tries to probe</param>
        /// <returns>The first unused filename</returns>
        internal static Task<DateTime> ProbeUnusedFilenameName(DatabaseCommon database, Options options, DateTime start, CancellationToken cancellationToken, TimeSpan increment = default, int maxTries = 60)
            => ProbeUnusedFilenameName((name, CancellationToken) => database.GetRemoteVolumeIDAsync(name, cancellationToken), options, start, cancellationToken, increment, maxTries);

        /// <summary>
        /// Probes for an unused filename, using the current time as a starting point.
        /// </summary>
        /// <param name="database">The database to check for clashes.</param>
        /// <param name="options">The options to use for the filename.</param>
        /// <param name="start">The starting time to probe from.</param>
        /// <param name="increment">The time to increment by each probe.</param>
        /// <param name="maxTries">The maximum number of tries to probe.</param>
        /// <returns>A task that when awaited returns the first unused filename.</returns>
        internal static async Task<DateTime> ProbeUnusedFilenameName(LocalDatabase database, Options options, DateTime start, CancellationToken cancellationToken, TimeSpan increment = default, int maxTries = 60)
            => await ProbeUnusedFilenameName(database.GetRemoteVolumeID, options, start, cancellationToken, increment, maxTries)
                .ConfigureAwait(false);

        /// <summary>
        /// Probes for an unused filename, using the current time as a starting point
        /// </summary>
        /// <param name="ProbeForId">The function to use to probe for the ID</param>
        /// <param name="options">The options to use for the filename</param>
        /// <param name="start">The starting time to probe from</param>
        /// <param name="increment">The time to increment by each probe</param>
        /// <param name="maxTries">The maximum number of tries to probe</param>
        /// <returns>The first unused filename</returns>
        private static async Task<DateTime> ProbeUnusedFilenameName(Func<string, CancellationToken, Task<long>> ProbeForId, Options options, DateTime start, CancellationToken cancellationToken, TimeSpan increment = default, int maxTries = 60)
        {
            if (increment == default)
                increment = TimeSpan.FromSeconds(1);

            var s = 1;
            var time = start;

            while (s < maxTries)
            {
                var id = await ProbeForId(GenerateFilename(RemoteVolumeType.Files, options, null, time), cancellationToken).ConfigureAwait(false);
                if (id < 0)
                    return time;

                time += increment;
            }

            throw new UserInformationException($"Failed to find an unused filename, tried {increment} * {maxTries} from {start}", "FailedToFindUnusedFilename");

        }
    }
}
