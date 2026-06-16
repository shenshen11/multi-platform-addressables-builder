using UnityEditor;

namespace Company.MultiPlatformAddressablesBuilder.Editor
{
    public static class MultiPlatformAddressablesBuildController
    {
        public static MpabValidationResult Validate(MultiPlatformAddressablesBuildRequest request)
        {
            return new MpabBuildValidator().Validate(request);
        }

        public static MpabBuildResult RunBuild(MultiPlatformAddressablesBuildRequest request)
        {
            var validation = Validate(request);
            if (validation.HasErrors)
            {
                var error = validation.ToDisplayString();
                MpabLogger.Error(error);
                return new MpabBuildResult
                {
                    Succeeded = false,
                    ErrorMessage = error
                };
            }

            return new MpabBuildOrchestrator().Run(request);
        }

    }
}
