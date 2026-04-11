using System;
using UnityEditor;

namespace Hissal.UnityTypeSerializer.Editor {
    internal static class SerializedTypeUsageManifestPaths {
        const string MANIFEST_ASSET_FILE_NAME = "SerializedTypeUsageManifest.asset";
        const string MANIFEST_XML_FILE_NAME = "SerializedTypeUsageManifest.xml";

        static string? s_generatedFolderPath;

        public static string ManifestAssetPath => BuildPath(MANIFEST_ASSET_FILE_NAME);
        public static string ManifestXmlPath => BuildPath(MANIFEST_XML_FILE_NAME);

        static string BuildPath(string fileName) {
            return GeneratedFolderPath + "/" + fileName;
        }

        static string GeneratedFolderPath => s_generatedFolderPath ??= ResolveGeneratedFolderPath();

        static string ResolveGeneratedFolderPath() {
            var scriptFolderPath = FindScriptFolderPath("SerializedTypeUsageManifestBuilder");
            if (string.IsNullOrEmpty(scriptFolderPath))
                scriptFolderPath = FindScriptFolderPath("SerializedTypeUsageManifestDatabase");

            if (string.IsNullOrEmpty(scriptFolderPath))
                return "Assets/Editor/Generated";

            return scriptFolderPath + "/Generated";
        }

        static string? FindScriptFolderPath(string scriptName) {
            var scriptGuids = AssetDatabase.FindAssets(scriptName + " t:Script");
            for (var i = 0; i < scriptGuids.Length; i++) {
                var scriptPath = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(scriptGuids[i]));
                if (string.IsNullOrEmpty(scriptPath))
                    continue;

                var expectedSuffix = "/" + scriptName + ".cs";
                if (!scriptPath.EndsWith(expectedSuffix, StringComparison.Ordinal))
                    continue;

                var folderPath = System.IO.Path.GetDirectoryName(scriptPath)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(folderPath))
                    return folderPath;
            }

            return null;
        }

        static string NormalizeAssetPath(string path) {
            return path.Replace('\\', '/');
        }
    }
}


