using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Company.MultiPlatformAddressablesBuilder.Editor
{
    [CreateAssetMenu(
        fileName = "MultiPlatformAddressablesBuildConfig",
        menuName = "Build/Multi Platform Addressables Build Config")]
    public sealed class MultiPlatformAddressablesBuildConfig : ScriptableObject
    {
        public const string DefaultAssetPath = "Assets/Build/MultiPlatformAddressablesBuildConfig.asset";

        [SerializeField] private List<MpabPlatformConfig> platforms = new List<MpabPlatformConfig>();
        [SerializeField] private List<MpabGroupRule> groupRules = new List<MpabGroupRule>();
        [SerializeField] private MpabBuildDefaults defaults = new MpabBuildDefaults();

        public List<MpabPlatformConfig> Platforms => platforms;
        public List<MpabGroupRule> GroupRules => groupRules;
        public MpabBuildDefaults Defaults => defaults;

        public MpabPlatformConfig FindPlatform(string platformId)
        {
            return platforms.Find(p => string.Equals(p.PlatformId, platformId, StringComparison.OrdinalIgnoreCase));
        }

        public void ResetToDefaults()
        {
            platforms = new List<MpabPlatformConfig>
            {
                new MpabPlatformConfig
                {
                    PlatformId = "Android",
                    BuildByDefault = true,
                    SwitchMode = MpabPlatformSwitchMode.UnityBuildTarget,
                    BuildTargetName = "Android",
                    AddressablesProfileName = "Default",
                },
                new MpabPlatformConfig
                {
                    PlatformId = "QNX",
                    BuildByDefault = false,
                    SwitchMode = MpabPlatformSwitchMode.CustomHandler,
                    BuildTargetName = "QNX",
                    AddressablesProfileName = "Default",
                }
            };

            groupRules = new List<MpabGroupRule>
            {
                new MpabGroupRule
                {
                    Name = "Common",
                    GroupNamePattern = "Common*",
                    Kind = MpabGroupRuleKind.Common,
                    PlatformIds = new List<string> { "Android", "QNX" }
                },
                new MpabGroupRule
                {
                    Name = "Android",
                    GroupNamePattern = "Android*",
                    Kind = MpabGroupRuleKind.Platform,
                    PlatformIds = new List<string> { "Android" }
                },
                new MpabGroupRule
                {
                    Name = "QNX",
                    GroupNamePattern = "QNX*",
                    Kind = MpabGroupRuleKind.Platform,
                    PlatformIds = new List<string> { "QNX" }
                },
                new MpabGroupRule
                {
                    Name = "Built In Data",
                    GroupNamePattern = "Built In Data",
                    Kind = MpabGroupRuleKind.Ignored,
                    PlatformIds = new List<string>()
                }
            };

            defaults = new MpabBuildDefaults();
        }

        public static MultiPlatformAddressablesBuildConfig CreateDefaultAsset()
        {
            var existing = AssetDatabase.LoadAssetAtPath<MultiPlatformAddressablesBuildConfig>(DefaultAssetPath);
            if (existing != null)
                return existing;

            const string folder = "Assets/Build";
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets", "Build");

            var asset = CreateInstance<MultiPlatformAddressablesBuildConfig>();
            asset.ResetToDefaults();
            AssetDatabase.CreateAsset(asset, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = asset;
            return asset;
        }

        private void OnValidate()
        {
            platforms ??= new List<MpabPlatformConfig>();
            groupRules ??= new List<MpabGroupRule>();
            defaults ??= new MpabBuildDefaults();
        }
    }

    [Serializable]
    public sealed class MpabPlatformConfig
    {
        public string PlatformId = "Android";
        public bool BuildByDefault = true;
        public MpabPlatformSwitchMode SwitchMode = MpabPlatformSwitchMode.UnityBuildTarget;
        public string BuildTargetName = "Android";
        public string AddressablesProfileName = "Default";
        public string CustomSwitchHandlerTypeName = string.Empty;
        // Labels to include during label-filtered builds. Only entries carrying at least one
        // of these labels will be built when a group rule has EnableLabelFilter = true.
        // Leave empty to disable label filtering for this platform entirely.
        public List<string> IncludedLabels = new List<string>();
    }

    [Serializable]
    public sealed class MpabGroupRule
    {
        public string Name = "Common";
        public string GroupNamePattern = "Common*";
        public MpabGroupRuleKind Kind = MpabGroupRuleKind.Common;
        public List<string> PlatformIds = new List<string>();
        // Groups listed here are matched by exact name before the wildcard pattern is tried.
        // Use this to classify groups whose names don't follow the naming convention
        // (e.g. third-party plugin groups, legacy groups).
        public List<string> ExplicitGroupNames = new List<string>();
        // When true, entries within this group are filtered by the platform's IncludedLabels
        // before building. Entries without a matching label are temporarily moved out.
        // Requires the platform to have IncludedLabels configured.
        public bool EnableLabelFilter = false;
    }

    [Serializable]
    public sealed class MpabBuildDefaults
    {
        public MpabResourceScope ResourceScope = MpabResourceScope.CommonAndPlatform;
        public bool CleanBeforeBuild;
        public bool RestoreOriginalPlatformAfterBuild = true;
        public bool RestoreOriginalProfileAfterBuild = true;
        public bool RestoreOriginalGroupStatesAfterBuild = true;
        public string ReportDirectory = "BuildOutput/MultiPlatformAddressablesBuilder/Reports";
    }
}
