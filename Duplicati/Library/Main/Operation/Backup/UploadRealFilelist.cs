﻿//  Copyright (C) 2015, The Duplicati Team
//  http://www.duplicati.com, info@duplicati.com
//
//  This library is free software; you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as
//  published by the Free Software Foundation; either version 2.1 of the
//  License, or (at your option) any later version.
//
//  This library is distributed in the hope that it will be useful, but
//  WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
//  Lesser General Public License for more details.
//
//  You should have received a copy of the GNU Lesser General Public
//  License along with this library; if not, write to the Free Software
//  Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA 02111-1307 USA
using System;
using System.Threading.Tasks;
using CoCoL;
using Duplicati.Library.Main.Volumes;
using System.IO;

namespace Duplicati.Library.Main.Operation.Backup
{
    internal static class UploadRealFilelist
    {
        public static Task Run(BackupResults result, BackupDatabase db, Options options, FilesetVolumeWriter filesetvolume, long filesetid)
        {
            return AutomationExtensions.RunTask(new
            {
                Output = Channels.BackendRequest.ForWrite,
                LogChannel = Common.Channels.LogChannel.ForWrite
            },

            async self =>
            {
                var log = new Common.LogWrapper(self.LogChannel);

                // Update the reported source and backend changes
                using(new Logging.Timer("UpdateChangeStatistics"))
                    await db.UpdateChangeStatisticsAsync(result);

                var changeCount = 
                    result.AddedFiles + result.ModifiedFiles + result.DeletedFiles +
                    result.AddedFolders + result.ModifiedFolders + result.DeletedFolders +
                    result.AddedSymlinks + result.ModifiedSymlinks + result.DeletedSymlinks;

                //Changes in the filelist triggers a filelist upload
                if (options.UploadUnchangedBackups || changeCount > 0)
                {
                    using(new Logging.Timer("Uploading a new fileset"))
                    {
                        if (!string.IsNullOrEmpty(options.ControlFiles))
                            foreach(var p in options.ControlFiles.Split(new char[] { System.IO.Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries))
                                filesetvolume.AddControlFile(p, options.GetCompressionHintFromFilename(p));

                        await db.WriteFilesetAsync(filesetvolume, filesetid);
                        filesetvolume.Close();

                        if (options.Dryrun)
                            await log.WriteDryRunAsync("Would upload fileset volume: {0}, size: {1}", filesetvolume.RemoteFilename, Library.Utility.Utility.FormatSizeString(new FileInfo(filesetvolume.LocalFilename).Length));
                        else
                        {
                            await db.UpdateRemoteVolumeAsync(filesetvolume.RemoteFilename, RemoteVolumeState.Uploading, -1, null);

                            await db.CommitTransactionAsync("CommitUpdateRemoteVolume");

                            await self.Output.WriteAsync(new FilesetUploadRequest(filesetvolume));
                        }
                    }
                }
                else
                {
                    await log.WriteVerboseAsync("removing temp files, as no data needs to be uploaded");
                    await db.RemoveRemoteVolumeAsync(filesetvolume.RemoteFilename);
                }

                await db.CommitTransactionAsync("CommitUpdateRemoteVolume");
            });
        }
    }
}

