namespace Company.MultiPlatformAddressablesBuilder.Editor
{
    public interface IMpabPlatformSwitchHandler
    {
        bool SwitchPlatform(MpabPlatformConfig platform, out string error);
    }
}
