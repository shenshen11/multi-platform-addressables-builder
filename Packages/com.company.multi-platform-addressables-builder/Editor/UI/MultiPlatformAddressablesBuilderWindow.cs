using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Company.MultiPlatformAddressablesBuilder.Editor
{
    public sealed class MultiPlatformAddressablesBuilderWindow : EditorWindow
    {
        private MultiPlatformAddressablesBuildConfig config;
        private readonly Dictionary<string, bool> selectedPlatforms = new Dictionary<string, bool>();
        private MpabResourceScope scope = MpabResourceScope.CommonAndPlatform;
        private bool cleanBeforeBuild;
        private bool restorePlatform = true;
        private bool restoreProfile = true;
        private bool restoreGroups = true;
        private Vector2 scroll;
        private string lastReportPath;
        private string validationText;

        [MenuItem("Tools/Multi Platform Addressables Builder")]
        public static void Open()
        {
            GetWindow<MultiPlatformAddressablesBuilderWindow>("MP Addressables");
        }

        private void OnEnable()
        {
            config = AssetDatabase.LoadAssetAtPath<MultiPlatformAddressablesBuildConfig>(MultiPlatformAddressablesBuildConfig.DefaultAssetPath);
            SyncFromConfig();
        }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                var newConfig = (MultiPlatformAddressablesBuildConfig)EditorGUILayout.ObjectField(config, typeof(MultiPlatformAddressablesBuildConfig), false);
                if (newConfig != config)
                {
                    config = newConfig;
                    SyncFromConfig();
                }

                if (GUILayout.Button("Create Default Config", GUILayout.Width(170)))
                {
                    config = MultiPlatformAddressablesBuildConfig.CreateDefaultAsset();
                    SyncFromConfig();
                }
            }

            EditorGUILayout.Space(8);
            DrawPlatformSelection();
            EditorGUILayout.Space(8);
            DrawOptions();
            EditorGUILayout.Space(8);
            DrawActions();
            DrawValidation();

            EditorGUILayout.EndScrollView();
        }

        private void DrawPlatformSelection()
        {
            EditorGUILayout.LabelField("Build Platforms", EditorStyles.boldLabel);
            if (config == null)
            {
                EditorGUILayout.HelpBox("Assign or create a config asset.", MessageType.Info);
                return;
            }

            foreach (var platform in config.Platforms)
            {
                if (platform == null)
                    continue;

                if (!selectedPlatforms.ContainsKey(platform.PlatformId))
                    selectedPlatforms[platform.PlatformId] = platform.BuildByDefault;

                selectedPlatforms[platform.PlatformId] = EditorGUILayout.ToggleLeft(platform.PlatformId, selectedPlatforms[platform.PlatformId]);
            }
        }

        private void DrawOptions()
        {
            EditorGUILayout.LabelField("Build Options", EditorStyles.boldLabel);
            scope = (MpabResourceScope)EditorGUILayout.EnumPopup("Resource Scope", scope);
            cleanBeforeBuild = EditorGUILayout.Toggle("Clean Before Build", cleanBeforeBuild);
            restorePlatform = EditorGUILayout.Toggle("Restore Platform", restorePlatform);
            restoreProfile = EditorGUILayout.Toggle("Restore Profile", restoreProfile);
            restoreGroups = EditorGUILayout.Toggle("Restore Group States", restoreGroups);
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(config == null))
                {
                    if (GUILayout.Button("Validate"))
                        Validate();

                    if (GUILayout.Button("Build Selected"))
                        BuildSelected();
                }

                if (GUILayout.Button("Open Output"))
                    OpenOutput();

                using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(lastReportPath) || !File.Exists(lastReportPath)))
                {
                    if (GUILayout.Button("Open Latest Report"))
                        EditorUtility.RevealInFinder(lastReportPath);
                }
            }
        }

        private void DrawValidation()
        {
            if (string.IsNullOrEmpty(validationText))
                return;

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Validation / Result", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(validationText, validationText.Contains("[Error]") ? MessageType.Error : MessageType.Info);
        }

        private void Validate()
        {
            var request = CreateRequest();
            var result = MultiPlatformAddressablesBuildController.Validate(request);
            validationText = result.ToDisplayString();
            Repaint();
        }

        private void BuildSelected()
        {
            var request = CreateRequest();
            var result = MultiPlatformAddressablesBuildController.RunBuild(request);
            lastReportPath = result.ReportPath;
            validationText = result.Succeeded
                ? $"Build succeeded.\nReport: {result.ReportPath}"
                : $"Build failed.\n{result.ErrorMessage}\nReport: {result.ReportPath}";
            Repaint();
        }

        private MultiPlatformAddressablesBuildRequest CreateRequest()
        {
            var request = MultiPlatformAddressablesBuildRequest.FromConfig(config);
            request.PlatformIds.Clear();

            if (config != null)
            {
                foreach (var platform in config.Platforms)
                {
                    if (platform != null &&
                        selectedPlatforms.TryGetValue(platform.PlatformId, out var selected) &&
                        selected)
                        request.PlatformIds.Add(platform.PlatformId);
                }
            }

            request.ResourceScope = scope;
            request.CleanBeforeBuild = cleanBeforeBuild;
            request.RestoreOriginalPlatformAfterBuild = restorePlatform;
            request.RestoreOriginalProfileAfterBuild = restoreProfile;
            request.RestoreOriginalGroupStatesAfterBuild = restoreGroups;
            return request;
        }

        private void OpenOutput()
        {
            var output = MpabPathUtility.ToAbsolutePath("BuildOutput");
            Directory.CreateDirectory(output);
            EditorUtility.RevealInFinder(output);
        }

        private void SyncFromConfig()
        {
            selectedPlatforms.Clear();

            if (config == null)
                return;

            scope = config.Defaults.ResourceScope;
            cleanBeforeBuild = config.Defaults.CleanBeforeBuild;
            restorePlatform = config.Defaults.RestoreOriginalPlatformAfterBuild;
            restoreProfile = config.Defaults.RestoreOriginalProfileAfterBuild;
            restoreGroups = config.Defaults.RestoreOriginalGroupStatesAfterBuild;

            foreach (var platform in config.Platforms)
            {
                if (platform != null)
                    selectedPlatforms[platform.PlatformId] = platform.BuildByDefault;
            }
        }
    }
}
