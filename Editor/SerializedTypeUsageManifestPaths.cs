using System.IO;
using UnityEngine;

namespace Hissal.UnityTypeSerializer.Editor {
    internal static class SerializedTypeUsageManifestPaths {
        const string GENERATED_ROOT_RELATIVE_PATH = "Library/Hissal/UnityTypeSerializer";
        const string ANALYZER_INPUT_ROOT_RELATIVE_PATH = "ProjectSettings/Hissal/UnityTypeSerializer";
        const string MANIFEST_XML_FILE_NAME = "SerializedTypeUsageManifest.xml";

        static string? s_projectRootPath;
        static string? s_generatedRootAbsolutePath;
        static string? s_analyzerInputRootAbsolutePath;

        public static string GeneratedRootRelativePath => GENERATED_ROOT_RELATIVE_PATH;
        public static string ManifestXmlRelativePath => $"{GENERATED_ROOT_RELATIVE_PATH}/{MANIFEST_XML_FILE_NAME}";
        public static string AnalyzerManifestXmlRelativePath =>
            $"{ANALYZER_INPUT_ROOT_RELATIVE_PATH}/{MANIFEST_XML_FILE_NAME}";
        public static string GeneratedRootAbsolutePath =>
            s_generatedRootAbsolutePath ??= Path.Combine(ProjectRootPath, GENERATED_ROOT_RELATIVE_PATH);
        public static string AnalyzerInputRootAbsolutePath =>
            s_analyzerInputRootAbsolutePath ??= Path.Combine(ProjectRootPath, ANALYZER_INPUT_ROOT_RELATIVE_PATH);
        public static string ManifestXmlDiskPath => Path.Combine(GeneratedRootAbsolutePath, MANIFEST_XML_FILE_NAME);
        public static string AnalyzerManifestXmlDiskPath => Path.Combine(AnalyzerInputRootAbsolutePath, MANIFEST_XML_FILE_NAME);

        public static string ProjectRootPath => s_projectRootPath ??= Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    }
}


