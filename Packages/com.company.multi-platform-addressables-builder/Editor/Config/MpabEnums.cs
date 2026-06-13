namespace Company.MultiPlatformAddressablesBuilder.Editor
{
    public enum MpabResourceScope
    {
        CommonOnly,
        PlatformOnly,
        CommonAndPlatform,
        AllIncludedByPlatform
    }

    public enum MpabGroupRuleKind
    {
        Common,
        Platform,
        Ignored
    }

    public enum MpabPlatformSwitchMode
    {
        UnityBuildTarget,
        CurrentEditor,
        CustomHandler
    }

    public enum MpabLogSeverity
    {
        Info,
        Warning,
        Error
    }

    public enum MpabSessionStep
    {
        Idle,
        Prepare,
        ValidatePreconditions,
        SwitchPlatform,
        WaitForCompilation,
        CheckCompilation,
        ApplyAddressablesConfig,
        SaveModifiedConfig,
        BuildAddressables,
        CollectResult,
        NextPlatform,
        Restore,
        SaveRestoredConfig,
        GenerateReport,
        Done,
        Failed
    }
}
