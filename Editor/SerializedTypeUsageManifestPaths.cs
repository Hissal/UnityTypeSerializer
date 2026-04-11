using System.IO;
using UnityEngine;

namespace Hissal.UnityTypeSerializer.Editor {
    internal static class SerializedTypeUsageManifestPaths {
        const string GENERATED_ROOT_RELATIVE_PATH = "Library/Hissal/UnityTypeSerializer";
        const string MANIFEST_XML_FILE_NAME = "SerializedTypeUsageManifest.xml";

        static string? s_projectRootPath;
        static string? s_generatedRootAbsolutePath;

        public static string GeneratedRootRelativePath => GENERATED_ROOT_RELATIVE_PATH;
        public static string GeneratedRootAbsolutePath => s_generatedRootAbsolutePath ??= Path.Combine(ProjectRootPath, GENERATED_ROOT_RELATIVE_PATH);
        public static string ManifestXmlDiskPath => Path.Combine(GeneratedRootAbsolutePath, MANIFEST_XML_FILE_NAME);

        static string ProjectRootPath => s_projectRootPath ??= Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }
}


