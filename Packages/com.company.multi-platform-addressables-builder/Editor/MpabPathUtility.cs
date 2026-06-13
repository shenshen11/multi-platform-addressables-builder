using System.IO;
using UnityEngine;

namespace Company.MultiPlatformAddressablesBuilder.Editor
{
    public static class MpabPathUtility
    {
        public static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        public static string ToAbsolutePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return ProjectRoot;

            if (Path.IsPathRooted(path))
                return path;

            return Path.GetFullPath(Path.Combine(ProjectRoot, path));
        }
    }
}
