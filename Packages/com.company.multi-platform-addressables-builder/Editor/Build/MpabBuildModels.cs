using System;
using System.Collections.Generic;

namespace Company.MultiPlatformAddressablesBuilder.Editor
{
    [Serializable]
    public sealed class MpabGroupState
    {
        public string GroupGuid;
        public string GroupName;
        public bool IncludeInBuild;
    }

    [Serializable]
    public sealed class MpabGroupBuildDecision
    {
        public string GroupGuid;
        public string GroupName;
        public string RuleName;
        public bool IncludeInBuild;
        public bool IsMatched;
    }

    [Serializable]
    public sealed class MpabEntryRelocation
    {
        public string EntryGuid;
        public string AssetPath;
        public string OriginalGroupGuid;
        public string OriginalGroupName;
    }

    [Serializable]
    public sealed class MpabPlatformBuildReport
    {
        public string PlatformId;
        public string DisplayName;
        public string BuildTargetName;
        public string BuildTargetGroupName;
        public string AddressablesProfileName;
        public string OutputPath;
        public string ContentStatePath;
        public string Status;
        public string ErrorMessage;
        public double DurationSeconds;
        public List<string> IncludedGroups = new List<string>();
        public List<string> ExcludedGroups = new List<string>();
        public List<string> UnmatchedGroups = new List<string>();
        public List<string> LabelFilteredEntries = new List<string>();
    }

    [Serializable]
    public sealed class MpabBuildReport
    {
        public string SessionId;
        public string BuildTimeUtc;
        public string UnityVersion;
        public string AddressablesVersion;
        public List<string> RequestedPlatforms = new List<string>();
        public string ResourceScope;
        public string Status;
        public string ErrorMessage;
        // Whether the restore step (profile, group states, platform) completed without errors.
        // false here means the Addressables .asset files may be in a modified state and may
        // require manual git checkout to recover.
        public bool RestoreSucceeded;
        public List<MpabPlatformBuildReport> Platforms = new List<MpabPlatformBuildReport>();
    }

    public sealed class MpabBuildResult
    {
        public bool Succeeded;
        public string ErrorMessage;
        public string ReportPath;
        public MpabBuildReport Report;
    }
}
