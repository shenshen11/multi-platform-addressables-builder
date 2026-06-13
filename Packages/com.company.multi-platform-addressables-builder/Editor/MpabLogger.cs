using UnityEngine;

namespace Company.MultiPlatformAddressablesBuilder.Editor
{
    public static class MpabLogger
    {
        public static void Info(string message)
        {
            Debug.Log("[MPAB] " + message);
        }

        public static void Warning(string message)
        {
            Debug.LogWarning("[MPAB] " + message);
        }

        public static void Error(string message)
        {
            Debug.LogError("[MPAB] " + message);
        }
    }
}
