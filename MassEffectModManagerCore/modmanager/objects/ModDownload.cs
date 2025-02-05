﻿using LegendaryExplorerCore.Helpers;
using ME3TweaksCore.Helpers;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.me3tweaks.services;
using ME3TweaksModManager.modmanager.nexusmodsintegration;
using ME3TweaksModManager.ui;
using Pathoschild.FluentNexus.Models;
using PropertyChanged;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Markup;
using M3OnlineContent = ME3TweaksModManager.modmanager.me3tweaks.services.M3OnlineContent;
using MemoryAnalyzer = ME3TweaksModManager.modmanager.memoryanalyzer.MemoryAnalyzer;

namespace ME3TweaksModManager.modmanager.objects
{
    /// <summary>
    /// Class for information about a mod that is being downloaded
    /// </summary>
    [AddINotifyPropertyChangedInterface]
    public class ModDownload
    {
        /// <summary>
        /// The maximum size of an archive that can be downloaded and loaded from memory.
        /// </summary>
        private static readonly long DOWNLOAD_TO_MEMORY_SIZE_CAP = 100 * FileSize.MebiByte;
        public string NXMLink { get; set; }
        public List<ModFileDownloadLink> DownloadLinks { get; } = new List<ModFileDownloadLink>();
        public ModFile ModFile { get; private set; }
        public NexusProtocolLink ProtocolLink { get; private set; }
        /// <summary>
        /// If this mod has been initialized
        /// </summary>
        public bool Initialized { get; private set; }
        public long ProgressValue { get; private set; }
        public long ProgressMaximum { get; private set; }
        public bool ProgressIndeterminate { get; private set; } = true;
        public string DownloadStatus { get; private set; }
        /// <summary>
        /// If this mod has been downloaded
        /// </summary>
        public bool Downloaded { get; set; }
        /// <summary>
        /// The downloaded stream data
        /// </summary>
        public Stream DownloadedStream { get; private set; }
        /// <summary>
        /// Invoked when the mod has initialized
        /// </summary>
        public event EventHandler<EventArgs> OnInitialized;
        /// <summary>
        /// Invoked when a mod download has completed
        /// </summary>
        public event EventHandler<DataEventArgs> OnModDownloaded;
        /// <summary>
        /// Invoked when a mod download has an error
        /// </summary>
        public event EventHandler<string> OnModDownloadError;

        public ModDownload(string nxmlink)
        {
            NXMLink = nxmlink;
        }

        /// <summary>
        /// Begins downloading of the mod to disk or memory.
        /// </summary>
        /// <param name="cancellationToken">Token to indicate the download has been canceled.</param>
        public void StartDownload(CancellationToken cancellationToken)
        {
            Task.Run(() =>
            {
                if (ProgressMaximum < DOWNLOAD_TO_MEMORY_SIZE_CAP && Settings.ModDownloadCacheFolder == null) // Mod Manager 8.0.1: If cache is set, always download to disk    
                {
                    DownloadedStream = new MemoryStream();
                    MemoryAnalyzer.AddTrackedMemoryItem(@"NXM Download MemoryStream", new WeakReference(DownloadedStream));
                }
                else
                {
                    DownloadedStream = new FileStream(Path.Combine(M3Filesystem.GetModDownloadCacheDirectory(), ModFile.FileName), FileMode.Create);
                    MemoryAnalyzer.AddTrackedMemoryItem(@"NXM Download FileStream", new WeakReference(DownloadedStream));
                }

                var downloadUri = DownloadLinks[0].Uri;
                var downloadResult = M3OnlineContent.DownloadToStream(downloadUri.ToString(), OnDownloadProgress, null, true, DownloadedStream, cancellationToken);
                if (downloadResult.errorMessage != null)
                {
                    DownloadedStream?.Dispose();
                    if (cancellationToken.IsCancellationRequested)
                    {
                        // Aborted download.
                    }
                    else
                    {
                        M3Log.Error($@"Download failed: {downloadResult.errorMessage}");
                        OnModDownloadError?.Invoke(this, downloadResult.errorMessage);
                    }
                    // Download didn't work!
                    TelemetryInterposer.TrackEvent(@"NXM Download", new Dictionary<string, string>()
                    {
                        {@"Domain", ProtocolLink?.Domain},
                        {@"File", ModFile?.Name},
                        {@"Result", $@"Failed, {downloadResult.errorMessage}"},
                    });
                }
                else
                {
                    // Verify
                    ProgressIndeterminate = true;
                    DownloadStatus = M3L.GetString(M3L.string_verifyingDownload);
                    try
                    {
                        // Todo: If verification fails, should we let user try to continue anyways?

                        var hash = M3Utilities.CalculateMD5(DownloadedStream);
                        var matchingHashedFiles = NexusModsUtilities.MD5Search(ProtocolLink.Domain, hash);
                        if (matchingHashedFiles.All(x => x.Mod.ModID != ProtocolLink.ModId))
                        {
                            // Our hash does not match
                            M3Log.Error(@"Download failed: File does not appear to match file on NexusMods");
                            OnModDownloadError?.Invoke(this, M3L.GetString(M3L.string_fileDidNotVerifyDownloadMayBeCorrupt));
                            TelemetryInterposer.TrackEvent(@"NXM Download", new Dictionary<string, string>()
                            {
                                {@"Domain", ProtocolLink?.Domain},
                                {@"File", ModFile?.Name},
                                {@"Result", @"Corrupt download"},
                            });

                            return;
                        }
                        M3Log.Information(@"File verified OK, nexus MD5 search returned correct result (NexusMods: Please make MD5 available as part of download API! This is a ridiculous way of verifying files)");
                    }
                    catch (Exception ex)
                    {
                        M3Log.Warning($@"An error occurred while attempting to verify the file: {ex.Message}. Skipping verification for this download.");
                    }

                    TelemetryInterposer.TrackEvent(@"NXM Download", new Dictionary<string, string>()
                    {
                        {@"Domain", ProtocolLink?.Domain},
                        {@"File", ModFile?.Name},
                        {@"Result", @"Success"},
                    });
                }
                Downloaded = true;
                OnModDownloaded?.Invoke(this, new DataEventArgs(DownloadedStream));
            });
        }

        private void OnDownloadProgress(long done, long total)
        {
            ProgressValue = done;
            ProgressMaximum = total;
            ProgressIndeterminate = false;
            DownloadStatus = $@"{FileSize.FormatSize(ProgressValue)}/{FileSize.FormatSize(ProgressMaximum)}";
        }

        /// <summary>
        /// Loads the information about this nxmlink into this object. Subscribe to OnInitialized() to know when it has initialized and is ready for download to begin.
        /// THIS IS A BLOCKING CALL DO NOT RUN ON THE UI
        /// </summary>
        public void Initialize()
        {
            M3Log.Information($@"Initializing {NXMLink}");
            Task.Run(() =>
            {
                try
                {
                    DownloadLinks.Clear();

                    ProtocolLink = NexusProtocolLink.Parse(NXMLink);
                    if (ProtocolLink == null) return; // Parse failed.

                    if (!NexusModsUtilities.AllSupportedNexusDomains.Contains(ProtocolLink?.Domain))
                    {
                        M3Log.Error($@"Cannot download file from unsupported domain: {ProtocolLink?.Domain}. Open your preferred mod manager from that game first");
                        Initialized = true;
                        ProgressIndeterminate = false;
                        OnModDownloadError?.Invoke(this,
                            M3L.GetString(M3L.string_interp_dialog_modNotForThisModManager, ProtocolLink.Domain));
                        return;
                    }

                    // Mod Manager 8: Blacklisting files
                    if (BlacklistingService.IsNXMBlacklisted(ProtocolLink))
                    {
                        M3Log.Error($@"File is blacklisted by ME3Tweaks: {ProtocolLink?.Domain} file {ProtocolLink.FileId}");
                        Initialized = true;
                        ProgressIndeterminate = false;
                        OnModDownloadError?.Invoke(this, M3L.GetString(M3L.string_description_blacklistedMod));
                        return;
                    }

                    ModFile = NexusModsUtilities.GetClient().ModFiles.GetModFile(ProtocolLink.Domain, ProtocolLink.ModId, ProtocolLink.FileId).Result;
                    if (ModFile != null)
                    {
                        if (ModFile.SizeInBytes != null && ModFile.SizeInBytes.Value > DOWNLOAD_TO_MEMORY_SIZE_CAP && M3Utilities.GetDiskFreeSpaceEx(M3Filesystem.GetModDownloadCacheDirectory(), out var free, out var total, out var totalFree))
                        {
                            // Check free disk space.
                            if (totalFree < ModFile.SizeInBytes * 1.2) // 20% buffer.
                            //if (totalFree < ModFile.SizeInBytes * 100000.0) // Debug code
                            {
                                M3Log.Error($@"There is not enough free space on {Path.GetPathRoot(M3Filesystem.GetModDownloadCacheDirectory())} to download {ModFile.FileName}. We need {FileSize.FormatSize(ModFile.SizeInBytes.Value)} but only {FileSize.FormatSize(totalFree)} is available.");
                                Initialized = true;
                                ProgressIndeterminate = false;
                                OnModDownloadError?.Invoke(this, M3L.GetString(M3L.string_interp_notEnoughFreeSpaceForDownload, Path.GetPathRoot(M3Filesystem.GetModDownloadCacheDirectory()), ModFile.FileName, FileSize.FormatSize(ModFile.SizeInBytes.Value), FileSize.FormatSize(totalFree)));
                                return;
                            }
                        }


                        if (ModFile.Category != FileCategory.Deleted)
                        {
                            if (ProtocolLink.Key != null)
                            {
                                // Website click


                                if (ProtocolLink.Domain is @"masseffect" or @"masseffect2" && !IsDownloadWhitelisted(ProtocolLink.Domain, ModFile))
                                {
                                    // Check to see file has moddesc.ini the listing
                                    var fileListing = NexusModsUtilities.GetFileListing(ModFile);

                                    // 02/27/2022: We check for TPISService SizeInBytes. It's not 100% accurate since we don't have an MD5 to check against. But it's pretty likely it's supported.
                                    if (fileListing == null || !HasModdescIni(fileListing) && ModFile.SizeInBytes != null && TPIService.GetImportingInfosBySize(ModFile.SizeInBytes.Value).Count == 0)
                                    {
                                        M3Log.Error($@"This file is not whitelisted for download and does not contain a moddesc.ini file, this is not a mod manager mod: {ModFile.FileName}");
                                        Initialized = true;
                                        ProgressIndeterminate = false;
                                        OnModDownloadError?.Invoke(this, M3L.GetString(M3L.string_interp_nexusModNotCompatible, ModFile.Name));
                                        return;
                                    }
                                }


                                // download with manager was clicked.

                                // Check if parameters are correct!
                                DownloadLinks.AddRange(NexusModsUtilities.GetDownloadLinkForFile(ProtocolLink).Result);
                            }
                            else
                            {
                                // premium? no parameters were supplied...
                                if (!NexusModsUtilities.UserInfo.IsPremium)
                                {
                                    M3Log.Error(
                                        $@"Cannot download {ModFile.FileName}: User is not premium, but this link is not generated from NexusMods");
                                    Initialized = true;
                                    ProgressIndeterminate = false;
                                    OnModDownloadError?.Invoke(this,
                                        M3L.GetString(M3L.string_dialog_mustBePremiumUserToDownload));
                                    return;
                                }

                                DownloadLinks.AddRange(NexusModsUtilities.GetDownloadLinkForFile(ProtocolLink)
                                    ?.Result);
                            }

                            ProgressMaximum = ModFile.SizeInBytes ?? ModFile.SizeInKilobytes * 1024L; // SizeKb is the original version. They added SizeInBytes at my request
                            Initialized = true;
                            M3Log.Information($@"ModDownload has initialized: {ModFile.FileName}");
                            OnInitialized?.Invoke(this, null);
                        }
                        else
                        {
                            M3Log.Error($@"Cannot download {ModFile.FileName}: File deleted from NexusMods");
                            Initialized = true;
                            ProgressIndeterminate = false;
                            OnModDownloadError?.Invoke(this,
                                M3L.GetString(M3L.string_dialog_cannotDownloadDeletedFile));
                        }
                    }
                }
                catch (Exception e)
                {
                    M3Log.Error($@"Error downloading {ModFile?.FileName}: {e.Message}");
                    Initialized = true;
                    ProgressIndeterminate = false;
                    OnModDownloadError?.Invoke(this, M3L.GetString(M3L.string_interp_errorDownloadingModX, e.Message));
                }
            });
        }

        private static readonly int[] WhitelistedME1FileIDs = new[]
        {
            116, // skip intro movies
            117, // skip to main menu
            120, // controller skip intro movies
            121, // controll skip to main menu
            245, // ME1 Controller 1.2.2
            326, // MAKO MOD
            327, // Mako Mod v2
            328, // Mako mod v3
            569, // mass effect ultrawide
        };

        private static readonly int[] WhitelistedME2FileIDs = new[]
        {
            3, // cheat console
            44, // faster load screens animated
            338, // Controller Mod 1.7.2
            365, // no minigames 2.0.2
        };

        private static readonly int[] WhitelistedME3FileIDs = new[]
        {
            0,
        };

        private bool IsDownloadWhitelisted(string domain, ModFile modFile)
        {
            switch (domain)
            {
                case @"masseffect":
                    return WhitelistedME1FileIDs.Contains(modFile.FileID);
                case @"massseffect2":
                    return WhitelistedME2FileIDs.Contains(modFile.FileID);
                case @"masseffect3":
                    return WhitelistedME3FileIDs.Contains(modFile.FileID);
            }

            return false;
        }

        /// <summary>
        /// Determines if the file listing has any moddesc.ini files in it.
        /// </summary>
        /// <param name="fileListing"></param>
        /// <returns></returns>
        private bool HasModdescIni(ContentPreview fileListing)
        {
            foreach (var e in fileListing.Children)
            {
                if (HasModdescIniRecursive(e))
                    return true;
            }

            return false;
        }

        private bool HasModdescIniRecursive(ContentPreviewEntry entry)
        {
            // Directory
            if (entry.Type == ContentPreviewEntryType.Directory)
            {
                foreach (var e in entry.Children)
                {
                    if (HasModdescIniRecursive(e))
                        return true;
                }

                return false;
            }

            // File
            return entry.Name == @"moddesc.ini";
        }

    }
}
