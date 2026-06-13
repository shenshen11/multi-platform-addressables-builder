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
    }
}
