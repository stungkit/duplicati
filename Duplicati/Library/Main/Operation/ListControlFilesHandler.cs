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
using Duplicati.Library.Main.Database;
using Duplicati.Library.Utility;
using System.Threading;
using System.Threading.Tasks;

namespace Duplicati.Library.Main.Operation
{
    internal class ListControlFilesHandler
    {
        private readonly Options m_options;
        private readonly ListResults m_result;

        public ListControlFilesHandler(Options options, ListResults result)
        {
            m_options = options;
            m_result = result;
        }

        public async Task RunAsync(IBackendManager backendManager, IEnumerable<string> filterstrings, IFilter compositefilter)
        {
            var cancellationToken = CancellationToken.None;
            using var tmpdb = new TempFile();
            await using var db = await LocalDatabase.CreateLocalDatabaseAsync(System.IO.File.Exists(m_options.Dbpath) ? m_options.Dbpath : (string)tmpdb, "ListControlFiles", true, null, m_result.TaskControl.ProgressToken).ConfigureAwait(false);
            var filter = JoinedFilterExpression.Join(new Library.Utility.FilterExpression(filterstrings), compositefilter);

            try
            {
                var filteredList = ListFilesHandler.ParseAndFilterFilesets(await backendManager.ListAsync(cancellationToken).ConfigureAwait(false), m_options);
                if (filteredList.Count == 0)
                    throw new Exception("No filesets found on remote target");

                Exception lastEx = new Exception("No suitable files found on remote target");

                foreach (var fileversion in filteredList)
                    try
                    {
                        if (!await m_result.TaskControl.ProgressRendevouz().ConfigureAwait(false))
                            return;

                        var file = fileversion.Value.File;
                        var entry = await db
                            .GetRemoteVolume(file.Name, m_result.TaskControl.ProgressToken)
                            .ConfigureAwait(false);

                        var files = new List<Library.Interface.IListResultFile>();
                        using (var tmpfile = await backendManager.GetAsync(file.Name, entry.Hash, entry.Size < 0 ? file.Size : entry.Size, cancellationToken).ConfigureAwait(false))
                        using (var tmp = new Volumes.FilesetVolumeReader(RestoreHandler.GetCompressionModule(file.Name), tmpfile, m_options))
                            foreach (var cf in tmp.ControlFiles)
                                if (Library.Utility.FilterExpression.Matches(filter, cf.Key))
                                    files.Add(new ListResultFile(cf.Key, null));

                        m_result.SetResult(new Library.Interface.IListResultFileset[] { new ListResultFileset(fileversion.Key, BackupType.PARTIAL_BACKUP, fileversion.Value.Time, -1, -1) }, files);
                        lastEx = null;
                        break;
                    }
                    catch (Exception ex) when (!ex.IsAbortException())
                    {
                        lastEx = ex;
                    }

                if (lastEx != null)
                    throw lastEx;
            }
            finally
            {
                await backendManager.WaitForEmptyAsync(db, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}

