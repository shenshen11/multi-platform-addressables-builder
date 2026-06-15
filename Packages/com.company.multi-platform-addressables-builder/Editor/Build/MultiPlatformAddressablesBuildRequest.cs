using System;
using System.Collections.Generic;

namespace Company.MultiPlatformAddressablesBuilder.Editor
{
    [Serializable]
    public sealed class MultiPlatformAddressablesBuildRequest
    {
        public MultiPlatformAddressablesBuildConfig Config;
        public List<string> PlatformIds = new List<string>();
        public MpabResourceScope ResourceScope = MpabResourceScope.CommonAndPlatform;
        public bool CleanBeforeBuild;
        public bool RestoreOriginalPlatformAfterBuild = true;
        public bool RestoreOriginalProfileAfterBuild = true;
        public bool RestoreOriginalGroupStatesAfterBuild = true;

        public static MultiPlatformAddressablesBuildRequest FromConfig(MultiPlatformAddressablesBuildConfig config)
        {
            var request = new MultiPlatformAddressablesBuildRequest
            {
                Config = config
            };

            if (config == null)
                return request;

            request.ResourceScope = config.Defaults.ResourceScope;
            request.CleanBeforeBuild = config.Defaults.CleanBeforeBuild;
            request.RestoreOriginalPlatformAfterBuild = config.Defaults.RestoreOriginalPlatformAfterBuild;
            request.RestoreOriginalProfileAfterBuild = config.Defaults.RestoreOriginalProfileAfterBuild;
            request.RestoreOriginalGroupStatesAfterBuild = config.Defaults.RestoreOriginalGroupStatesAfterBuild;

            foreach (var platform in config.Platforms)
            {
                if (platform != null && platform.BuildByDefault && !string.IsNullOrWhiteSpace(platform.PlatformId))
                    request.PlatformIds.Add(platform.PlatformId);
            }

            return request;
        }
    }
}
