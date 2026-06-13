using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Company.MultiPlatformAddressablesBuilder.Editor
{
    public static class MultiPlatformAddressablesBuilderCli
    {
        public static void BuildFromDefaultConfig()
        {
            var args = Environment.GetCommandLineArgs();
            var configPath = GetArg(args, "-mpabConfig", MultiPlatformAddressablesBuildConfig.DefaultAssetPath);
            var platforms = GetArg(args, "-mpabPlatforms", string.Empty);
            var scopeValue = GetArg(args, "-mpabScope", MpabResourceScope.CommonAndPlatform.ToString());

            var config = AssetDatabase.LoadAssetAtPath<MultiPlatformAddressablesBuildConfig>(configPath);
            if (config == null)
            {
                Debug.LogError($"[MPAB] Config not found: {configPath}");
                EditorApplication.Exit(1);
                return;
            }

            var request = MultiPlatformAddressablesBuildRequest.FromConfig(config);

            if (!string.IsNullOrWhiteSpace(platforms))
                request.PlatformIds = new List<string>(platforms.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

            if (Enum.TryParse(scopeValue, true, out MpabResourceScope parsedScope))
                request.ResourceScope = parsedScope;

            var result = MultiPlatformAddressablesBuildController.RunBuild(request);
            if (!result.Succeeded)
            {
                Debug.LogError($"[MPAB] Build failed: {result.ErrorMessage}");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"[MPAB] Build succeeded. Report: {result.ReportPath}");
            EditorApplication.Exit(0);
        }

        private static string GetArg(string[] args, string key, string defaultValue)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
                    return args[i + 1];
            }

            return defaultValue;
        }
    }
}
