using System;
using UnityEditor;

namespace Company.MultiPlatformAddressablesBuilder.Editor
{
    public sealed class MpabPlatformSwitcher
    {
        public bool SwitchPlatform(MpabPlatformConfig platform, out string error)
        {
            error = string.Empty;

            if (platform.SwitchMode == MpabPlatformSwitchMode.CurrentEditor)
            {
                MpabLogger.Info($"Using current editor platform for {platform.PlatformId}.");
                return true;
            }

            if (platform.SwitchMode == MpabPlatformSwitchMode.CustomHandler)
                return SwitchWithCustomHandler(platform, out error);

            if (!MpabBuildTargetResolver.TryResolve(platform, out var group, out var target, out error))
                return false;

            if (EditorUserBuildSettings.activeBuildTarget == target)
            {
                MpabLogger.Info($"BuildTarget already active: {target}.");
                return true;
            }

            MpabLogger.Info($"Switching BuildTarget: {EditorUserBuildSettings.activeBuildTarget} -> {target}.");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target))
            {
                error = $"Unity failed to switch active build target to {target} ({group}).";
                return false;
            }

            return true;
        }

        private static bool SwitchWithCustomHandler(MpabPlatformConfig platform, out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(platform.CustomSwitchHandlerTypeName))
            {
                error = $"Platform {platform.PlatformId} uses CustomHandler but no type name is configured.";
                return false;
            }

            var type = Type.GetType(platform.CustomSwitchHandlerTypeName);
            if (type == null)
            {
                error = $"Custom switch handler type not found: {platform.CustomSwitchHandlerTypeName}.";
                return false;
            }

            if (!typeof(IMpabPlatformSwitchHandler).IsAssignableFrom(type))
            {
                error = $"Custom switch handler must implement {nameof(IMpabPlatformSwitchHandler)}: {platform.CustomSwitchHandlerTypeName}.";
                return false;
            }

            var handler = (IMpabPlatformSwitchHandler)Activator.CreateInstance(type);
            return handler.SwitchPlatform(platform, out error);
        }
    }
}
