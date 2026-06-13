using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Company.MultiPlatformAddressablesBuilder.Editor
{
    public sealed class MpabBuildReportWriter
    {
        public string Write(MpabBuildReport report, string reportDirectory)
        {
            var absoluteDirectory = MpabPathUtility.ToAbsolutePath(reportDirectory);
            Directory.CreateDirectory(absoluteDirectory);

            var fileName = $"mpab_report_{report.SessionId}.json";
            var path = Path.Combine(absoluteDirectory, fileName);
            File.WriteAllText(path, JsonUtility.ToJson(report, true));
            AssetDatabase.Refresh();
            return path;
        }
    }
}
