using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor.AddressableAssets.Settings;

namespace Company.MultiPlatformAddressablesBuilder.Editor
{
    public sealed class MpabAddressablesGroupRuleEvaluator
    {
        public List<MpabGroupBuildDecision> Evaluate(
            MultiPlatformAddressablesBuildConfig config,
            string platformId,
            MpabResourceScope scope,
            IEnumerable<AddressableAssetGroup> groups)
        {
            var decisions = new List<MpabGroupBuildDecision>();

            foreach (var group in groups)
            {
                if (group == null || MpabAddressablesEditorAdapter.GetBundledSchema(group) == null)
                    continue;

                var rule = FindRule(config.GroupRules, group.Name);
                if (rule == null)
                {
                    decisions.Add(new MpabGroupBuildDecision
                    {
                        GroupGuid = group.Guid,
                        GroupName = group.Name,
                        RuleName = string.Empty,
                        IncludeInBuild = scope == MpabResourceScope.AllIncludedByPlatform,
                        IsMatched = false
                    });
                    continue;
                }

                var include = ShouldInclude(rule, platformId, scope);
                decisions.Add(new MpabGroupBuildDecision
                {
                    GroupGuid = group.Guid,
                    GroupName = group.Name,
                    RuleName = rule.Name,
                    IncludeInBuild = include,
                    IsMatched = true
                });
            }

            return decisions;
        }

        public MpabGroupRule FindRule(IEnumerable<MpabGroupRule> rules, string groupName)
        {
            // Pass 1: explicit name list takes priority over wildcard pattern.
            // This allows groups that don't follow naming conventions to be classified correctly.
            foreach (var rule in rules)
            {
                if (rule == null)
                    continue;

                if (rule.ExplicitGroupNames != null && rule.ExplicitGroupNames.Count > 0)
                {
                    foreach (var explicitName in rule.ExplicitGroupNames)
                    {
                        if (string.Equals(explicitName, groupName, StringComparison.OrdinalIgnoreCase))
                            return rule;
                    }
                }
            }

            // Pass 2: wildcard pattern matching as the default inference rule.
            foreach (var rule in rules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.GroupNamePattern))
                    continue;

                if (WildcardMatches(rule.GroupNamePattern, groupName))
                    return rule;
            }

            return null;
        }

        private static bool ShouldInclude(MpabGroupRule rule, string platformId, MpabResourceScope scope)
        {
            if (rule.Kind == MpabGroupRuleKind.Ignored)
                return false;

            var appliesToPlatform = AppliesToPlatform(rule, platformId);
            if (!appliesToPlatform)
                return false;

            if (scope == MpabResourceScope.CommonOnly)
                return rule.Kind == MpabGroupRuleKind.Common;

            if (scope == MpabResourceScope.PlatformOnly)
                return rule.Kind == MpabGroupRuleKind.Platform;

            return rule.Kind == MpabGroupRuleKind.Common || rule.Kind == MpabGroupRuleKind.Platform;
        }

        private static bool AppliesToPlatform(MpabGroupRule rule, string platformId)
        {
            if (rule.PlatformIds == null || rule.PlatformIds.Count == 0)
                return true;

            return rule.PlatformIds.Exists(p => string.Equals(p, platformId, StringComparison.OrdinalIgnoreCase));
        }

        private static bool WildcardMatches(string pattern, string value)
        {
            var escaped = Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".");
            return Regex.IsMatch(value ?? string.Empty, "^" + escaped + "$", RegexOptions.IgnoreCase);
        }
    }
}
