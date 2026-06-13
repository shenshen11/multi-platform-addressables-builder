using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;

namespace Company.MultiPlatformAddressablesBuilder.Editor
{
    public sealed class MpabBuildValidator
    {
        private readonly MpabAddressablesEditorAdapter addressables = new MpabAddressablesEditorAdapter();
        private readonly MpabAddressablesGroupRuleEvaluator ruleEvaluator = new MpabAddressablesGroupRuleEvaluator();

        public MpabValidationResult Validate(MultiPlatformAddressablesBuildRequest request)
        {
            var result = new MpabValidationResult();

            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                result.Error("Build is blocked while the editor is in Play Mode or is changing Play Mode.");

            if (request == null)
            {
                result.Error("Build request is null.");
                return result;
            }

            if (request.Config == null)
            {
                result.Error("Build config is missing.");
                return result;
            }

            if (addressables.Settings == null)
            {
                result.Error("Addressables settings asset was not found.");
                return result;
            }

            if (request.PlatformIds == null || request.PlatformIds.Count == 0)
                result.Error("No platform selected.");

            ValidatePlatforms(request, result);
            ValidateGroups(request.Config, result);
            ValidateOutputIsolation(request, result);

            if (!result.HasErrors)
                result.Info("Validation completed without blocking errors.");

            return result;
        }

        private void ValidatePlatforms(MultiPlatformAddressablesBuildRequest request, MpabValidationResult result)
        {
            foreach (var platformId in request.PlatformIds)
            {
                var platform = request.Config.FindPlatform(platformId);
                if (platform == null)
                {
                    result.Error($"Selected platform '{platformId}' does not exist in config.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(platform.AddressablesProfileName))
                {
                    result.Error($"Platform '{platform.PlatformId}' has no Addressables profile configured.");
                }
                else
                {
                    var profileId = addressables.Settings.profileSettings.GetProfileId(platform.AddressablesProfileName);
                    if (string.IsNullOrEmpty(profileId))
                        result.Error($"Addressables profile '{platform.AddressablesProfileName}' for platform '{platform.PlatformId}' does not exist.");
                }

                if (platform.SwitchMode == MpabPlatformSwitchMode.UnityBuildTarget &&
                    !MpabBuildTargetResolver.TryResolve(platform, out _, out _, out var error))
                    result.Error($"Platform '{platform.PlatformId}' cannot resolve Unity build target: {error}");

                if (platform.SwitchMode == MpabPlatformSwitchMode.CurrentEditor)
                    result.Warning($"Platform '{platform.PlatformId}' uses CurrentEditor switch mode. Addressables will build for the currently active Unity BuildTarget.");

                if (platform.SwitchMode == MpabPlatformSwitchMode.CustomHandler)
                {
                    if (string.IsNullOrWhiteSpace(platform.CustomSwitchHandlerTypeName))
                    {
                        result.Error($"Platform '{platform.PlatformId}' uses CustomHandler but no handler type is configured.");
                    }
                    else if (Type.GetType(platform.CustomSwitchHandlerTypeName) == null)
                    {
                        result.Error($"Platform '{platform.PlatformId}' custom handler type was not found: {platform.CustomSwitchHandlerTypeName}");
                    }
                }

                if (string.IsNullOrWhiteSpace(platform.OutputPath))
                    result.Error($"Platform '{platform.PlatformId}' has no output path configured.");
            }
        }

        private void ValidateGroups(MultiPlatformAddressablesBuildConfig config, MpabValidationResult result)
        {
            var matchedRules = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var group in addressables.Settings.groups)
            {
                if (group == null || MpabAddressablesEditorAdapter.GetBundledSchema(group) == null)
                    continue;

                var rule = ruleEvaluator.FindRule(config.GroupRules, group.Name);
                if (rule == null)
                {
                    // A group with no matching rule is the real risk: it will be included in all
                    // platforms when ResourceScope = AllIncludedByPlatform, and silently skipped
                    // otherwise. Both outcomes are likely unintentional.
                    result.Warning($"Addressables group '{group.Name}' is not matched by any group rule. " +
                        $"Add it to a rule's ExplicitGroupNames or update the wildcard pattern.");
                    continue;
                }

                matchedRules.Add(rule.Name);
            }

            foreach (var rule in config.GroupRules)
            {
                if (rule == null || rule.Kind == MpabGroupRuleKind.Ignored)
                    continue;

                if (!matchedRules.Contains(rule.Name))
                {
                    // A rule that matched nothing is not necessarily wrong (e.g. QNX_* rule in an
                    // Android-only project), so this is Info rather than Warning.
                    result.Info($"Group rule '{rule.Name}' (pattern: '{rule.GroupNamePattern}') did not match any bundled Addressables group.");
                }
            }
        }

        private static void ValidateOutputIsolation(MultiPlatformAddressablesBuildRequest request, MpabValidationResult result)
        {
            var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var platformId in request.PlatformIds)
            {
                var platform = request.Config.FindPlatform(platformId);
                if (platform == null || string.IsNullOrWhiteSpace(platform.OutputPath))
                    continue;

                var fullPath = Path.GetFullPath(MpabPathUtility.ToAbsolutePath(platform.OutputPath)).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                foreach (var existing in paths)
                {
                    if (string.Equals(fullPath, existing.Value, StringComparison.OrdinalIgnoreCase))
                        result.Error($"Platforms '{existing.Key}' and '{platform.PlatformId}' share the same output path: {fullPath}");
                }

                paths[platform.PlatformId] = fullPath;
            }
        }
    }
}
