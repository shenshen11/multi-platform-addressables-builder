using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;

namespace Company.MultiPlatformAddressablesBuilder.Editor
{
    public sealed class MpabAddressablesEditorAdapter
    {
        private const string ExcludedGroupName = "__MPAB_LabelExcluded__";

        public AddressableAssetSettings Settings => AddressableAssetSettingsDefaultObject.Settings;

        public string ActiveProfileId => Settings != null ? Settings.activeProfileId : string.Empty;

        public string ActiveProfileName
        {
            get
            {
                if (Settings == null)
                    return string.Empty;

                return Settings.profileSettings.GetProfileName(Settings.activeProfileId);
            }
        }

        public bool TrySetActiveProfile(string profileName, out string error)
        {
            error = string.Empty;

            if (Settings == null)
            {
                error = "Addressables settings asset was not found.";
                return false;
            }

            var profileId = Settings.profileSettings.GetProfileId(profileName);
            if (string.IsNullOrEmpty(profileId))
            {
                error = $"Addressables profile '{profileName}' was not found.";
                return false;
            }

            Settings.activeProfileId = profileId;
            EditorUtility.SetDirty(Settings);
            MpabLogger.Info($"Active Addressables profile: {profileName}.");
            return true;
        }

        public List<MpabGroupState> CaptureGroupStates()
        {
            var states = new List<MpabGroupState>();

            if (Settings == null)
                return states;

            foreach (var group in Settings.groups)
            {
                var schema = GetBundledSchema(group);
                if (group == null || schema == null)
                    continue;

                states.Add(new MpabGroupState
                {
                    GroupGuid = group.Guid,
                    GroupName = group.Name,
                    IncludeInBuild = schema.IncludeInBuild
                });
            }

            return states;
        }

        public void ApplyGroupStates(IEnumerable<MpabGroupBuildDecision> decisions)
        {
            if (Settings == null)
                return;

            foreach (var decision in decisions)
            {
                var group = FindGroup(decision.GroupGuid, decision.GroupName);
                var schema = GetBundledSchema(group);
                if (group == null || schema == null)
                    continue;

                if (schema.IncludeInBuild == decision.IncludeInBuild)
                    continue;

                schema.IncludeInBuild = decision.IncludeInBuild;
                EditorUtility.SetDirty(schema);
                EditorUtility.SetDirty(group);
                MpabLogger.Info($"{group.Name}: IncludeInBuild={decision.IncludeInBuild}.");
            }
        }

        public void RestoreGroupStates(IEnumerable<MpabGroupState> states)
        {
            if (Settings == null)
                return;

            foreach (var state in states)
            {
                var group = FindGroup(state.GroupGuid, state.GroupName);
                var schema = GetBundledSchema(group);
                if (group == null || schema == null)
                    continue;

                if (schema.IncludeInBuild == state.IncludeInBuild)
                    continue;

                schema.IncludeInBuild = state.IncludeInBuild;
                EditorUtility.SetDirty(schema);
                EditorUtility.SetDirty(group);
            }
        }

        public void SaveModifiedAddressablesAssets()
        {
            if (Settings != null)
                EditorUtility.SetDirty(Settings);

            AssetDatabase.SaveAssets();
        }

        public AddressablesPlayerBuildResult BuildPlayerContent(bool cleanBeforeBuild)
        {
            if (cleanBeforeBuild)
            {
                MpabLogger.Info("Cleaning Addressables player content.");
                AddressableAssetSettings.CleanPlayerContent();
            }

            MpabLogger.Info("Building Addressables player content.");
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            return result;
        }

        public AddressableAssetGroup FindGroup(string groupGuid, string groupName)
        {
            if (Settings == null)
                return null;

            foreach (var group in Settings.groups)
            {
                if (group == null)
                    continue;

                if (!string.IsNullOrEmpty(groupGuid) && string.Equals(group.Guid, groupGuid, StringComparison.OrdinalIgnoreCase))
                    return group;

                if (!string.IsNullOrEmpty(groupName) && string.Equals(group.Name, groupName, StringComparison.OrdinalIgnoreCase))
                    return group;
            }

            return null;
        }

        public static BundledAssetGroupSchema GetBundledSchema(AddressableAssetGroup group)
        {
            return group != null ? group.GetSchema<BundledAssetGroupSchema>() : null;
        }

        public List<MpabEntryRelocation> RelocateEntriesByLabel(
            IEnumerable<MpabGroupBuildDecision> decisions,
            MultiPlatformAddressablesBuildConfig config,
            MpabPlatformConfig platform)
        {
            var relocations = new List<MpabEntryRelocation>();

            if (Settings == null || platform.IncludedLabels == null || platform.IncludedLabels.Count == 0)
                return relocations;

            AddressableAssetGroup excludedGroup = null;

            foreach (var decision in decisions)
            {
                if (!decision.IncludeInBuild || string.IsNullOrEmpty(decision.RuleName))
                    continue;

                var rule = config.GroupRules.Find(r => r != null &&
                    string.Equals(r.Name, decision.RuleName, StringComparison.OrdinalIgnoreCase));
                if (rule == null || !rule.EnableLabelFilter)
                    continue;

                var group = FindGroup(decision.GroupGuid, decision.GroupName);
                if (group == null)
                    continue;

                var entries = new List<AddressableAssetEntry>();
                group.GatherAllAssets(entries, false, false, false);

                foreach (var entry in entries)
                {
                    if (entry == null)
                        continue;

                    var hasMatchingLabel = false;
                    foreach (var label in platform.IncludedLabels)
                    {
                        if (entry.labels.Contains(label))
                        {
                            hasMatchingLabel = true;
                            break;
                        }
                    }

                    if (hasMatchingLabel)
                        continue;

                    if (excludedGroup == null)
                        excludedGroup = GetOrCreateExcludedGroup();

                    relocations.Add(new MpabEntryRelocation
                    {
                        EntryGuid = entry.guid,
                        AssetPath = entry.AssetPath,
                        OriginalGroupGuid = group.Guid,
                        OriginalGroupName = group.Name
                    });

                    Settings.MoveEntry(entry, excludedGroup, false, false);
                    MpabLogger.Info($"Label filter: '{entry.AssetPath}' → excluded (no match in platform '{platform.PlatformId}' labels).");
                }
            }

            if (excludedGroup != null)
            {
                EditorUtility.SetDirty(excludedGroup);
                EditorUtility.SetDirty(Settings);
            }

            return relocations;
        }

        public void RestoreRelocatedEntries(List<MpabEntryRelocation> relocations)
        {
            if (relocations == null || relocations.Count == 0 || Settings == null)
                return;

            foreach (var relocation in relocations)
            {
                var entry = Settings.FindAssetEntry(relocation.EntryGuid);
                if (entry == null)
                {
                    MpabLogger.Warning($"Label filter restore: entry not found for '{relocation.AssetPath}'.");
                    continue;
                }

                var targetGroup = FindGroup(relocation.OriginalGroupGuid, relocation.OriginalGroupName);
                if (targetGroup == null)
                {
                    MpabLogger.Warning($"Label filter restore: group '{relocation.OriginalGroupName}' not found for '{relocation.AssetPath}'.");
                    continue;
                }

                Settings.MoveEntry(entry, targetGroup, false, false);
                EditorUtility.SetDirty(targetGroup);
            }

            var excludedGroup = Settings.FindGroup(ExcludedGroupName);
            if (excludedGroup != null)
            {
                var remaining = new List<AddressableAssetEntry>();
                excludedGroup.GatherAllAssets(remaining, false, false, false);
                if (remaining.Count == 0)
                    Settings.RemoveGroup(excludedGroup);
            }

            EditorUtility.SetDirty(Settings);
        }

        private AddressableAssetGroup GetOrCreateExcludedGroup()
        {
            var existing = Settings.FindGroup(ExcludedGroupName);
            if (existing != null)
                return existing;

            var group = Settings.CreateGroup(ExcludedGroupName, false, false, false, null,
                typeof(BundledAssetGroupSchema));
            var schema = group.GetSchema<BundledAssetGroupSchema>();
            schema.IncludeInBuild = false;
            EditorUtility.SetDirty(schema);
            MpabLogger.Info($"Created temporary label-exclusion group '{ExcludedGroupName}'.");
            return group;
        }
    }
}
