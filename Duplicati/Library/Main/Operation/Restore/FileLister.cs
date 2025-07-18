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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CoCoL;
using Duplicati.Library.Main.Database;

namespace Duplicati.Library.Main.Operation.Restore
{

    /// <summary>
    /// Process that holds the files that this particular restore operation needs to restore.
    /// </summary>
    internal class FileLister
    {
        /// <summary>
        /// The log tag for this class.
        /// </summary>
        private static readonly string LOGTAG = Logging.Log.LogTagFromType<FileLister>();

        /// <summary>
        /// Runs the file lister process that lists the files that need to be restored
        /// and sends them to the <see cref="FileProcessor"/>.
        /// </summary>
        /// <param name="channels">The named channels for the restore operation.</param>
        /// <param name="db">The restore database, which is queried for the file list.</param>
        /// <param name="options">The restore options</param>
        /// <param name="result">The restore results</param>
        public static Task Run(Channels channels, LocalRestoreDatabase db, Options options, RestoreResults result)
        {
            return AutomationExtensions.RunTask(
            new
            {
                Output = channels.FilesToRestore.AsWrite()
            },
            async self =>
            {
                Stopwatch sw_get_files = options.InternalProfiling ? new() : null;
                Stopwatch sw_write_file = options.InternalProfiling ? new() : null;
                Stopwatch sw_get_folders = options.InternalProfiling ? new() : null;
                Stopwatch sw_write_folder = options.InternalProfiling ? new() : null;

                bool threw_exception = false;

                try
                {
                    sw_get_files?.Start();
                    // The enumerables are cast to arrays to force the query to be executed and release the database lock.
                    var files = await db
                        .GetFilesAndSymlinksToRestore(result.TaskControl.ProgressToken)
                        .ToArrayAsync()
                        .ConfigureAwait(false);

                    result.OperationProgressUpdater.UpdatePhase(OperationPhase.Restore_DownloadingRemoteFiles);
                    sw_get_files?.Stop();

                    sw_write_file?.Start();
                    foreach (var file in files)
                        await self.Output.WriteAsync(file).ConfigureAwait(false);
                    sw_write_file?.Stop();

                    if (!options.SkipMetadata)
                    {
                        sw_get_folders?.Start();
                        // The enumerables are cast to arrays to force the query to be executed and release the database lock.
                        var folders = await db
                            .GetFolderMetadataToRestore(result.TaskControl.ProgressToken)
                            .ToArrayAsync()
                            .ConfigureAwait(false);
                        sw_get_folders?.Stop();

                        sw_write_folder?.Start();
                        foreach (var folder in folders)
                            await self.Output.WriteAsync(folder).ConfigureAwait(false);
                        sw_write_folder?.Stop();
                    }
                }
                catch (Exception ex)
                {
                    Logging.Log.WriteErrorMessage(LOGTAG, "FileListerError", ex, "Error during file listing");
                    threw_exception = true;
                    throw;
                }
                finally
                {
                    if (!threw_exception)
                        Logging.Log.WriteVerboseMessage(LOGTAG, "RetiredProcess", null, "File lister retired");

                    if (options.InternalProfiling)
                    {
                        Logging.Log.WriteProfilingMessage(LOGTAG, "InternalTimings", $"Get files: {sw_get_files.ElapsedMilliseconds}ms, Write files: {sw_write_file.ElapsedMilliseconds}ms, Get folders: {sw_get_folders.ElapsedMilliseconds}ms, Write folders: {sw_write_folder.ElapsedMilliseconds}ms");
                    }
                }
            });
        }
    }

}