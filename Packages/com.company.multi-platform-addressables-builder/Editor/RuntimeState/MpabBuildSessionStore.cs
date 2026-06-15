using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Company.MultiPlatformAddressablesBuilder.Editor
{
    [Serializable]
    public sealed class MpabBuildSessionState
    {
        public string SessionId;
        public string CurrentPlatformId;
        public string OriginalBuildTargetName;
        public string OriginalBuildTargetGroupName;
        public string OriginalAddressablesProfileId;
        public string OriginalAddressablesProfileName;
        public string ErrorMessage;
        public MpabSessionStep Step;
        public List<string> PendingPlatforms = new List<string>();
        public List<string> CompletedPlatforms = new List<string>();
        public List<MpabGroupState> OriginalGroupStates = new List<MpabGroupState>();
        // Entries temporarily relocated to the exclusion group by label filtering.
        // Populated before each platform build, cleared after per-platform restore.
        // Non-empty here after a crash means restore is needed on next editor launch.
        public List<MpabEntryRelocation> RelocatedEntries = new List<MpabEntryRelocation>();
    }

    public sealed class MpabBuildSessionStore
    {
        private const string SessionRelativePath = "Library/MultiPlatformAddressablesBuilder/build_session.json";

        public static string SessionPath => Path.Combine(MpabPathUtility.ProjectRoot, SessionRelativePath);

        public void Save(MpabBuildSessionState state)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SessionPath));
            File.WriteAllText(SessionPath, JsonUtility.ToJson(state, true));
        }

        public MpabBuildSessionState Load()
        {
            if (!File.Exists(SessionPath))
                return null;

            return JsonUtility.FromJson<MpabBuildSessionState>(File.ReadAllText(SessionPath));
        }

        public void Clear()
        {
            if (File.Exists(SessionPath))
                File.Delete(SessionPath);
        }

        public static MpabBuildSessionState CreateNew(
            string sessionId,
            IEnumerable<string> pendingPlatforms,
            MpabAddressablesEditorAdapter addressables)
        {
            var activeGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);

            return new MpabBuildSessionState
            {
                SessionId = sessionId,
                Step = MpabSessionStep.Prepare,
                OriginalBuildTargetName = EditorUserBuildSettings.activeBuildTarget.ToString(),
                OriginalBuildTargetGroupName = activeGroup.ToString(),
                OriginalAddressablesProfileId = addressables.ActiveProfileId,
                OriginalAddressablesProfileName = addressables.ActiveProfileName,
                PendingPlatforms = new List<string>(pendingPlatforms),
                OriginalGroupStates = addressables.CaptureGroupStates()
            };
        }
    }
}
