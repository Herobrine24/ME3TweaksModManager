﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ME3TweaksCoreWPF;
using ME3TweaksCoreWPF.Targets;
using ME3TweaksModManager.modmanager.diagnostics;
using ME3TweaksModManager.modmanager.helpers;
using ME3TweaksModManager.modmanager.localizations;
using ME3TweaksModManager.modmanager.objects.mod;
using ME3TweaksModManager.modmanager.objects.mod.editor;
using ME3TweaksModManager.modmanager.objects.mod.merge;
using PropertyChanged;

namespace ME3TweaksModManager.modmanager.objects.alternates
{
    [DebuggerDisplay(@"AlternateFile | {Condition} {Operation}, ConditionalDLC: {ConditionalDLC}, ModFile: {ModFile}, AltFile: {AltFile}")]
    [AddINotifyPropertyChangedInterface]
    public sealed class AlternateFile : AlternateOption
    {
        public enum AltFileOperation
        {
            INVALID_OPERATION,
            OP_SUBSTITUTE,
            OP_NOINSTALL,
            OP_INSTALL,
            OP_APPLY_MULTILISTFILES,
            OP_NOINSTALL_MULTILISTFILES,
            OP_APPLY_MERGEMODS,
            OP_NOTHING //Used for alt groups
        }

        public enum AltFileCondition
        {
            /// <summary>
            /// If condition is not set. This is always an invalid condition.
            /// </summary>
            INVALID_CONDITION,
            COND_MANUAL,
            COND_ALWAYS,
            COND_DLC_PRESENT,
            COND_DLC_NOT_PRESENT
        }

        public AltFileCondition Condition { get; set; }
        public AltFileOperation Operation { get; set; }

        public override bool IsManual => Condition == AltFileCondition.COND_MANUAL;
        public override bool IsAlways => Condition == AltFileCondition.COND_ALWAYS;

        //public override bool UIRequired => !IsManual && IsSelected && !IsAlways;
        //public override bool UINotApplicable => !IsManual && !IsSelected && !IsAlways;

        /// <summary>
        /// Alternate file to use, if the operation uses an alternate file
        /// </summary>
        public string AltFile { get; private set; }

        /// <summary>
        /// List of loaded of MergeMods to use, if the operation deals with MergeMod files.
        /// </summary>
        public IMergeMod[] MergeMods { get; private set; }

        /// <summary>
        /// In-game relative path that will be operated on according to the specified operation
        /// </summary>
        public string ModFile { get; private set; }

        /// <summary>
        /// In-game relative path that will be targeted as the root. It's like ModFile but more descriptive for multilist implementations.
        /// </summary>
        public string MultiListTargetPath { get; }

        internal bool HasRelativeFile()
        {
            if (Operation == AltFileOperation.INVALID_OPERATION) return false;
            if (Operation == AltFileOperation.OP_NOINSTALL) return false;
            if (Operation == AltFileOperation.OP_NOINSTALL_MULTILISTFILES) return false;
            if (Operation == AltFileOperation.OP_APPLY_MULTILISTFILES) return true;
            if (Operation == AltFileOperation.OP_APPLY_MERGEMODS) return true;
            return AltFile != null;
        }

        //public override bool UIIsSelectable
        //{
        //    // Logic:
        //    // It's not always chosen
        //    // It's not required
        //    // It's not not applicable
        //    // OR ^|v
        //    // It's manual
        //    // This logic should probably be updated to ensure DLCRequirements...
        //    get => !IsAlways && !UIRequired && !UINotApplicable || IsManual;
        //    set { } //you can't set these for altfiles
        //}

        /// <summary>
        /// ONLY FOR USE IN MODDESC.INI EDITOR
        /// Creates a new, blank Alternate DLC object
        /// </summary>
        /// <param name="alternateName"></param>
        public AlternateFile(string alternateName, AltFileCondition condition, AltFileOperation operation)
        {
            FriendlyName = alternateName;
            Condition = condition;
            Operation = operation;
            BuildParameterMap(null);
        }

        public AlternateFile(string alternateFileText, ModJob associatedJob, Mod modForValidating)
        {
            var properties = StringStructParser.GetCommaSplitValues(alternateFileText);
            if (properties.TryGetValue(@"FriendlyName", out string friendlyName))
            {
                FriendlyName = friendlyName;
            }
            if (modForValidating.ModDescTargetVersion >= 6 && string.IsNullOrWhiteSpace(FriendlyName))
            {
                //Cannot be null.
                M3Log.Error(@"Alternate File does not specify FriendlyName. Mods targeting moddesc >= 6.0 cannot have empty FriendlyName");
                ValidAlternate = false;
                LoadFailedReason = M3L.GetString(M3L.string_validation_altfile_oneAltDlcMissingFriendlyNameCmm6);
                return;
            }

            if (properties.TryGetValue(@"Condition", out string altCond))
            {
                if (!Enum.TryParse<AltFileCondition>(altCond, out var cond) || cond == AltFileCondition.INVALID_CONDITION)
                {
                    M3Log.Error($@"Alternate File specifies unknown/unsupported condition: {altCond}"); //do not localize
                    ValidAlternate = false;
                    LoadFailedReason = $@"{M3L.GetString(M3L.string_validation_altfile_unknownCondition)} {altCond}";
                    return;
                }

                Condition = cond;
            }

            if (properties.TryGetValue(@"ConditionalDLC", out string conditionalDlc))
            {
                var conditionalList = StringStructParser.GetSemicolonSplitList(conditionalDlc);
                foreach (var dlc in conditionalList)
                {
                    if (Enum.TryParse(dlc, out ModJob.JobHeader header) && ModJob.GetHeadersToDLCNamesMap(modForValidating.Game).TryGetValue(header, out var foldername))
                    {
                        ConditionalDLC.Add(foldername);
                        continue;
                    }
                    if (!dlc.StartsWith(@"DLC_"))
                    {
                        M3Log.Error(@"An item in Alternate Files's ConditionalDLC doesn't start with DLC_");
                        LoadFailedReason = M3L.GetString(M3L.string_validation_altfile_conditionalDLCInvalidValue, FriendlyName);
                        return;
                    }
                    else
                    {
                        ConditionalDLC.Add(dlc);
                    }
                }
            }

            if (properties.TryGetValue(@"ModOperation", out var modOp))
            {
                if (!Enum.TryParse<AltFileOperation>(modOp, out var op) || op == AltFileOperation.INVALID_OPERATION)
                {
                    M3Log.Error(@"Alternate File specifies unknown/unsupported operation: " +
                              modOp);
                    ValidAlternate = false;
                    LoadFailedReason = $@"{M3L.GetString(M3L.string_validation_altfile_unknownOperation)} {modOp}";
                    return;
                }

                Operation = op;
            }
            else
            {
                M3Log.Error($@"Alternate File does not specify ModOperation, which is required for all Alternate Files: {FriendlyName}");
                ValidAlternate = false;
                LoadFailedReason = $@"Alternate File does not specify ModOperation, which is required for all Alternate Files: {FriendlyName}";
                return;
            }

            if (properties.TryGetValue(@"Description", out string description))
            {
                Description = description;
            }


            if (modForValidating.ModDescTargetVersion >= 6 && string.IsNullOrWhiteSpace(Description))
            {
                //Cannot be null.
                M3Log.Error($@"Alternate File {FriendlyName} with mod targeting moddesc >= 6.0 cannot have empty Description or missing Description");
                ValidAlternate = false;
                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altfile_cmmver6RequiresDescription, FriendlyName);
                return;
            }

            if (Operation != AltFileOperation.OP_NOTHING)
            {
                if (Operation == AltFileOperation.OP_APPLY_MULTILISTFILES)
                {
                    #region MULTILIST
                    if (associatedJob.Header == ModJob.JobHeader.CUSTOMDLC)
                    {
                        //This cannot be used on custom dlc
                        M3Log.Error($@"Alternate File ({FriendlyName}) specifies operation OP_APPLY_MULTILISTFILES on the CUSTOM DLC task - this operation is not supported on this header. Use the altdlc version instead, see the moddesc.ini documentation.");
                        ValidAlternate = false;
                        LoadFailedReason = $@"Alternate File ({FriendlyName}) specifies operation OP_APPLY_MULTILISTFILES on the CUSTOM DLC task - this operation is not supported on this header. Use the altdlc version instead, see the moddesc.ini documentation.";
                        return;
                    }
                    // ModDesc 8.0 change: Require MultiListRootPath not be an empty string.
                    // This checks because EGM LE did not set it so this would break loading that mod on future builds
                    if (properties.TryGetValue(@"MultiListRootPath", out var rootpath) && (modForValidating.ModDescTargetVersion < 8.0 || !string.IsNullOrWhiteSpace(rootpath)))
                    {
                        MultiListRootPath = rootpath.TrimStart('\\', '/').Replace('/', '\\');
                    }
                    else
                    {
                        M3Log.Error($@"Alternate File ({FriendlyName}) specifies operation OP_APPLY_MULTILISTFILES but does not specify the required item MultiListRootPath.");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_interp_altfile_multilistMissingMultiListRootPath, FriendlyName);
                        return;
                    }

                    if (properties.TryGetValue(@"MultiListTargetPath", out var targetpath))
                    {
                        MultiListTargetPath = targetpath.TrimStart('\\', '/').Replace('/', '\\');
                    }
                    else
                    {
                        M3Log.Error($@"Alternate File ({FriendlyName}) specifies operation OP_APPLY_MULTILISTFILES but does not specify the required item MultiListTargetPath.");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_interp_altfile_multilistMissingMultiListTargetPath, FriendlyName);
                        return;
                    }

                    if (properties.TryGetValue(@"MultiListId", out string multilistidstr) && int.TryParse(multilistidstr, out var multilistid))
                    {
                        if (associatedJob.MultiLists.TryGetValue(multilistid, out var ml))
                        {
                            MultiListId = multilistid;
                            MultiListSourceFiles = ml.Select(x => x.TrimStart('\\', '/')).ToArray();
                        }
                        else
                        {
                            M3Log.Error($@"Alternate File ({FriendlyName}) Multilist ID does not exist as part of the task: multilist" + multilistid);
                            ValidAlternate = false;
                            var id = @"multilist" + multilistid;
                            LoadFailedReason = M3L.GetString(M3L.string_interp_altfile_multilistMissingFileInMultiList, FriendlyName, associatedJob.Header) + $@" multilist{multilistid}";
                            return;
                        }
                    }
                    else
                    {
                        M3Log.Error($@"Alternate File ({FriendlyName}) specifies operation OP_APPLY_MULTILISTFILES but does not specify the MultiListId attribute, or it could not be parsed to an integer.");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_interp_altfile_multilistIdNotIntegerOrMissing, FriendlyName);
                        return;
                    }

                    // ModDesc 8.0: Allow flattening output of multilist output.
                    // Backported to 7.0 125 build for EGM - it must work on 125 7.0 and above.
                    if ((modForValidating.ModDescTargetVersion >= 7.0 && modForValidating.MinimumSupportedBuild >= 125) 
                        || modForValidating.ModDescTargetVersion >= 8.0)
                    {
                        if (properties.TryGetValue(@"FlattenMultiListOutput", out var multiListFlattentStr) && !string.IsNullOrWhiteSpace(multiListFlattentStr))
                        {
                            if (bool.TryParse(multiListFlattentStr, out var multiListFlatten))
                            {
                                FlattenMultilistOutput = multiListFlatten;
                            }
                            else
                            {
                                M3Log.Error($@"Alternate File ({FriendlyName}) specifies 'FlattenMultiListOutput' descriptor, but the value is not 'true' or 'false': {multiListFlattentStr}");
                                ValidAlternate = false;
                                LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altfile_flattentNotTrueOrFalse, FriendlyName, multiListFlattentStr);
                                return;
                            }
                        }
                    }

                    #endregion
                }
                else if (Operation == AltFileOperation.OP_NOINSTALL_MULTILISTFILES)
                {
                    #region NOINSTALL MULTILIST
                    if (modForValidating.ModDescTargetVersion < 6.1)
                    {
                        M3Log.Error($@"Alternate File ({FriendlyName}) specifies operation OP_NOINSTALL_MULTILISTFILES, but this feature is only supported on moddesc version 6.1 or higher.");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_interp_altfile_opnoinstallmultilistfiles_requires_moddesc61, FriendlyName);
                        return;
                    }

                    if (properties.TryGetValue(@"MultiListTargetPath", out var rootpath))
                    {
                        MultiListTargetPath = rootpath.TrimStart('\\', '/').Replace('/', '\\');
                    }
                    else
                    {
                        M3Log.Error($@"Alternate File ({FriendlyName}) specifies operation OP_NOINSTALL_MULTILISTFILES but does not specify the required item MultiListRootPath.");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_interp_altfile_multilistNIMissingMultiListTargetPath, FriendlyName);
                        return;
                    }

                    if (properties.TryGetValue(@"MultiListTargetPath", out var targetpath))
                    {
                        MultiListTargetPath = targetpath.TrimStart('\\', '/').Replace('/', '\\');
                    }

                    if (properties.TryGetValue(@"MultiListId", out string multilistidstr) && int.TryParse(multilistidstr, out var multilistid))
                    {
                        if (associatedJob.MultiLists.TryGetValue(multilistid, out var ml))
                        {
                            MultiListId = multilistid;
                            MultiListSourceFiles = ml;
                        }
                        else
                        {
                            M3Log.Error($@"Alternate File ({FriendlyName}) Multilist ID does not exist as part of the task: multilist" + multilistid);
                            ValidAlternate = false;
                            LoadFailedReason = M3L.GetString(M3L.string_interp_altfile_multilistMissingFileInMultiList, FriendlyName, associatedJob.Header) + $@" multilist{multilistid}";
                            return;
                        }
                    }
                    else
                    {
                        M3Log.Error($@"Alternate File ({FriendlyName}) specifies operation OP_NOINSTALL_MULTILISTFILES but does not specify the MultiListId attribute, or it could not be parsed to an integer.");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_interp_altfile_multilistIdNINotIntegerOrMissing, FriendlyName);
                        return;
                    }
                    // There's no way to verify files not being installed cause they can change at runtime
                    // Just have to trust developer on it
                    #endregion
                }
                else if (Operation == AltFileOperation.OP_APPLY_MERGEMODS)
                {
                    #region MERGE MOD
                    if (modForValidating.ModDescTargetVersion < 7.0)
                    {
                        M3Log.Error($@"Alternate File operation OP_APPLY_MERGEMOD can only be used when targeting cmmver >= 7.0");
                        ValidAlternate = false;
                        LoadFailedReason = $@"Alternate File operation OP_APPLY_MERGEMOD can only be used when targeting cmmver >= 7.0";
                        return;
                    }
                    if (associatedJob.Header != ModJob.JobHeader.BASEGAME)
                    {
                        M3Log.Error($@"Alternate File {FriendlyName} attempts to use OP_APPLY_MERGEMODS operation on a non-BASEGAME header, which currently is not allowed");
                        ValidAlternate = false;
                        LoadFailedReason = $@"Alternate File {FriendlyName} attempts to use OP_APPLY_MERGEMODS operation on a non-BASEGAME header, which currently is not allowed";
                        return;
                    }
                    if (properties.TryGetValue(@"MergeFiles", out string mf))
                    {
                        var mergeFiles = StringStructParser.GetSemicolonSplitList(mf).Select(x => x.TrimStart('\\', '/')).ToArray();

                        // Verify files exist
                        var merges = new List<IMergeMod>();
                        foreach (var mFile in mergeFiles)
                        {
                            if (mFile.Contains(@".."))
                            {
                                // Security issue
                                M3Log.Error($@"Alternate File {FriendlyName} has merge filename with a .. in it, which is not allowed: {mFile}");
                                ValidAlternate = false;
                                LoadFailedReason = $@"Alternate File {FriendlyName} has merge filename with a .. in it, which is not allowed: {mFile}";
                                return;
                            }

                            var mergeFilePath = FilesystemInterposer.PathCombine(modForValidating.IsInArchive, modForValidating.ModPath, Mod.MergeModFolderName, mFile);
                            var mergeFileExists = FilesystemInterposer.FileExists(mergeFilePath, modForValidating.Archive);
                            if (!mergeFileExists)
                            {
                                M3Log.Error($@"Alternate File merge file (item in MergeFiles) does not exist: {mFile}");
                                ValidAlternate = false;
                                LoadFailedReason = $@"Alternate File merge file (item in MergeFiles) does not exist: {mFile}";
                                return;
                            }

                            var mm = modForValidating.LoadMergeMod(mergeFilePath);
                            if (mm == null)
                            {
                                // MM failed to load
                                M3Log.Error($@"Alternate File merge file {mFile} failed to load: {modForValidating.LoadFailedReason}");
                                ValidAlternate = false;
                                LoadFailedReason = $@"Alternate File merge file {mFile} failed to load: {modForValidating.LoadFailedReason}";
                                return;
                            }
                            merges.Add(mm);
                        }

                        MergeMods = merges.ToArray();
                    }
                    else
                    {
                        M3Log.Error($@"Alternate File merge filenames (MergeFiles) required but not specified. This value is required for Alternate Files using the OP_APPLY_MERGEMODS operation.");
                        ValidAlternate = false;
                        LoadFailedReason = $@"Alternate File merge filenames (MergeFiles) required but not specified. This value is required for Alternate Files using the OP_APPLY_MERGEMODS operation.";
                        return;
                    }
                    #endregion
                }
                else
                {
                    #region SUBSTITUTE, INSTALL, NOINSTALL
                    if (properties.TryGetValue(@"ModFile", out string modfile))
                    {
                        ModFile = modfile.TrimStart('\\', '/').Replace('/', '\\');
                    }
                    else
                    {
                        M3Log.Error($@"Alternate file in-mod target (ModFile) required but not specified. This value is required for all Alternate files except when using . Friendlyname: {FriendlyName}");
                        ValidAlternate = false;
                        LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altfile_noModFileDeclared, FriendlyName);
                        return;
                    }

                    if (associatedJob.Header == ModJob.JobHeader.CUSTOMDLC)
                    {
                        //Verify target folder is reachable by the mod
                        var modFilePath = FilesystemInterposer.PathCombine(modForValidating.IsInArchive, modForValidating.ModPath, ModFile);
                        var pathSplit = ModFile.Split('\\');
                        if (pathSplit.Length > 0)
                        {
                            var dlcName = pathSplit[0];
                            var jobKey = associatedJob.CustomDLCFolderMapping.FirstOrDefault(x => x.Value.Equals(dlcName, StringComparison.InvariantCultureIgnoreCase));
                            if (jobKey.Key != null)
                            {
                                //todo: Find DLC target to make sure this rule can actually be applied. Somewhat difficult logic here
                            }
                            else
                            {
                                M3Log.Error($@"Alternate file {FriendlyName} in-mod target (ModFile) does not appear to target a DLC target this mod will (always) install: {ModFile}");
                                ValidAlternate = false;
                                LoadFailedReason = M3L.GetString(M3L.string_interp_altfile_installToNotGuaranteedCustomDLC, FriendlyName, ModFile);
                                return;
                            }
                        }
                    }
                    else
                    {
                        // Non DLC 
                        if (!associatedJob.FilesToInstall.TryGetValue(ModFile, out var sourceFile))
                        {
                            M3Log.Error($@"Alternate file {FriendlyName} in-mod target (ModFile) specified but does not exist in job: {ModFile}");
                            ValidAlternate = false;
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altfile_couldNotFindModFile, FriendlyName, ModFile);
                            return;
                        }
                    }

                    //these both are the same these days i guess, I honestly can't remember which one I wanted to use
                    if (properties.TryGetValue(@"AltFile", out string altfile))
                    {
                        AltFile = altfile.TrimStart('\\', '/');
                    }
                    else if (AltFile == null && properties.TryGetValue(@"ModAltFile", out string maltfile))
                    {
                        AltFile = maltfile.TrimStart('\\', '/'); ;
                    }


                    //workaround for 4.5
                    if (modForValidating.ModDescTargetVersion == 4.5)
                    {
                        // BACKWARDS COMPATIBLILITY ONLY: ModDesc 4.5 used SubstituteFile but was removed from support in 5.0
                        // In 5.0 and above this became AltFile.
                        properties.TryGetValue(@"SubstituteFile", out var substituteFile);

                        if (Operation == AltFileOperation.OP_SUBSTITUTE && substituteFile != null)
                        {
                            AltFile = substituteFile; // not trimming start to avoid logic change
                        }
                    }

                    if (!string.IsNullOrEmpty(AltFile))
                    {
                        AltFile = AltFile.Replace('/', '\\'); //Standardize paths
                    }

                    //This needs reworked from java's hack implementation
                    //Need to identify mods using substitution features

                    if (Operation == AltFileOperation.OP_INSTALL || Operation == AltFileOperation.OP_SUBSTITUTE)
                    {
                        //Validate file
                        var altPath = FilesystemInterposer.PathCombine(modForValidating.IsInArchive, modForValidating.ModPath, AltFile);
                        var altFileSourceExists = FilesystemInterposer.FileExists(altPath, modForValidating.Archive);
                        if (!altFileSourceExists)
                        {
                            M3Log.Error(@"Alternate file source (AltFile) does not exist: " + AltFile);
                            ValidAlternate = false;
                            LoadFailedReason = M3L.GetString(M3L.string_interp_validation_altfile_specifiedAltFileDoesntExist, Operation.ToString(), AltFile);
                            return;
                        }

                        //Ensure it is not part of  DLC directory itself.
                        var modFile = FilesystemInterposer.PathCombine(modForValidating.IsInArchive, modForValidating.ModPath, ModFile);
                    }
                    #endregion
                }
            }

            if (!ReadSharedOptions(modForValidating, properties))
            {
                return; // Failed in super call
            }

            if (Condition == AltFileCondition.COND_MANUAL && properties.TryGetValue(@"CheckedByDefault", out string checkedByDefault) && bool.TryParse(checkedByDefault, out bool cbd))
            {
                CheckedByDefault = cbd;
            }

            M3Log.Information($@"Alternate file loaded and validated: {FriendlyName}", Settings.LogModStartup);
            ValidAlternate = true;
        }



        public void SetupInitialSelection(GameTargetWPF target, Mod mod)
        {
            UIIsSelected = CheckedByDefault; //Reset
            if (IsAlways)
            {
                UIIsSelected = true;
                return;
            }
            if (IsManual)
            {
                UIIsSelectable = true; // Manual options can be chosen by user. A follow up pass will lock it out if it's not eligible.
                UIIsSelected = CheckedByDefault;
                return;
            }
            var installedDLC = target.GetInstalledDLC();
            switch (Condition)
            {
                case AltFileCondition.COND_DLC_NOT_PRESENT:
                    UIIsSelected = !ConditionalDLC.All(i => installedDLC.Contains(i, StringComparer.CurrentCultureIgnoreCase));
                    break;
                case AltFileCondition.COND_DLC_PRESENT:
                    UIIsSelected = ConditionalDLC.Any(i => installedDLC.Contains(i, StringComparer.CurrentCultureIgnoreCase));
                    break;
                    //The following conditions don't exist right now
                    //case AltFileCondition.COND_ALL_DLC_NOT_PRESENT:
                    //    IsSelected = !ConditionalDLC.Any(i => installedDLC.Contains(i, StringComparer.CurrentCultureIgnoreCase));
                    //    break;
                    //case AltFileCondition.COND_ALL_DLC_PRESENT:
                    //    IsSelected = ConditionalDLC.All(i => installedDLC.Contains(i, StringComparer.CurrentCultureIgnoreCase));
                    //    break;
            }
        }

        /// <summary>
        /// List of all keys in the altdlc struct that are publicly parsable
        /// </summary>
        public override void BuildParameterMap(Mod mod)
        {
            var conditions = Enum.GetValues<AltFileCondition>().Where(x => x != AltFileCondition.INVALID_CONDITION).Select(x => x.ToString());
            var operations = Enum.GetValues<AltFileOperation>().Where(x => x != AltFileOperation.INVALID_OPERATION).Select(x => x.ToString());


            var parameterDictionary = new Dictionary<string, object>()
            {
                { @"Condition", new MDParameter(@"string", @"Condition", Condition.ToString(), conditions, AltFileCondition.COND_MANUAL.ToString())},
                { @"ConditionalDLC", ConditionalDLC},
                { @"ModOperation", new MDParameter(@"string", @"ModOperation", Operation.ToString(), operations, AltFileOperation.OP_NOTHING.ToString())},
                { @"AltFile", AltFile},
                { @"ModFile", ModFile},
                { @"MergeFiles", MergeMods != null  ? string.Join(';',MergeMods.Select(x=>x.MergeModFilename)) : ""},
                { @"MultiListId", MultiListId > 0 ? MultiListId.ToString() : null},
                { @"MultiListRootPath", MultiListRootPath},
                { @"MultiListTargetPath", MultiListTargetPath},
                { @"FlattenMultiListOutput", new MDParameter(@"FlattenMultiListOutput", FlattenMultilistOutput, false)},
            };

            BuildSharedParameterMap(parameterDictionary);
            ParameterMap.ReplaceAll(MDParameter.MapIntoParameterMap(parameterDictionary));
        }
    }
}
    