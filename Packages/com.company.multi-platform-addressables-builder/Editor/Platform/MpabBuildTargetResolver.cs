using System;
using UnityEditor;

namespace Company.MultiPlatformAddressablesBuilder.Editor
{
    public static class MpabBuildTargetResolver
    {
        public static bool TryResolve(MpabPlatformConfig platform, out BuildTargetGroup group, out BuildTarget target, out string error)
        {
            group = BuildTargetGroup.Unknown;
            target = BuildTarget.NoTarget;
            error = string.Empty;

            if (platform == null)
            {
                error = "Platform config is null.";
                return false;
            }

            if (!Enum.TryParse(platform.BuildTargetName, true, out target) || target == BuildTarget.NoTarget)
            {
                error = $"BuildTarget '{platform.BuildTargetName}' is not a built-in Unity target.";
                return false;
            }

            group = BuildPipeline.GetBuildTargetGroup(target);
            return true;
        }
    }
}
