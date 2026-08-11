using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hissal.UnityTypeSerializer.Editor {
    internal static class SerializedTypeCscRspUpdater {
        internal const string ResponseFileName = "csc.rsp";

        static readonly Encoding StrictUtf8Encoding = new UTF8Encoding(false, true);

        public static string AdditionalFileDirective =>
            $"-additionalfile:\"{SerializedTypeUsageManifestPaths.ManifestXmlRelativePath}\"";

        public static SerializedTypeCscRspUpdateResult ReconcileConfiguredAssemblies() {
            var settings = SerializedTypeCscRspSettings.instance;
            var result = new SerializedTypeCscRspUpdateResult();
            var targetAssetPaths = FindTargetResponseFileAssetPaths(
                settings.RootFolders,
                settings.ExcludedFolders,
                result);

            return ReconcileTargetAssetPaths(targetAssetPaths, result);
        }

        public static SerializedTypeCscRspUpdateResult ReconcileDirectories(IEnumerable<string> assetDirectoryPaths) {
            var settings = SerializedTypeCscRspSettings.instance;
            var result = new SerializedTypeCscRspUpdateResult();
            var assemblyDefinitionPaths = new List<string>();

            foreach (var directoryPath in assetDirectoryPaths
                         .Select(NormalizeAssetPath)
                         .Where(path => IsAssetsPath(path))
                         .Distinct(StringComparer.Ordinal)
                         .OrderBy(path => path, StringComparer.Ordinal)) {

                if (!IsPathIncluded(directoryPath, settings.RootFolders, settings.ExcludedFolders))
                    continue;

                var diskDirectoryPath = GetDiskPath(directoryPath);
                if (!Directory.Exists(diskDirectoryPath))
                    continue;

                try {
                    assemblyDefinitionPaths.AddRange(
                        Directory.EnumerateFiles(diskDirectoryPath, "*.asmdef", SearchOption.TopDirectoryOnly)
                            .Select(path => $"{directoryPath}/{Path.GetFileName(path)}"));
                }
                catch (Exception exception) {
                    result.AddFailure($"{directoryPath}: {exception.Message}");
                }
            }

            var targetAssetPaths = GetTargetResponseFileAssetPaths(
                assemblyDefinitionPaths,
                settings.RootFolders,
                settings.ExcludedFolders);
            return ReconcileTargetAssetPaths(targetAssetPaths, result);
        }

        internal static IReadOnlyList<string> GetTargetResponseFileAssetPaths(
            IEnumerable<string> assemblyDefinitionAssetPaths,
            IReadOnlyList<string> rootFolders,
            IReadOnlyList<string> excludedFolders) {

            return assemblyDefinitionAssetPaths
                .Select(NormalizeAssetPath)
                .Where(path => path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                .Where(path => IsPathIncluded(path, rootFolders, excludedFolders))
                .Select(Path.GetDirectoryName)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => NormalizeAssetPath(path!))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => $"{path}/{ResponseFileName}")
                .ToArray();
        }

        internal static bool IsPathIncluded(
            string assetPath,
            IReadOnlyList<string> rootFolders,
            IReadOnlyList<string> excludedFolders) {

            assetPath = NormalizeAssetPath(assetPath);
            if (!IsAssetsPath(assetPath))
                return false;

            var isIncluded = rootFolders
                .Select(NormalizeAssetPath)
                .Where(IsAssetsPath)
                .Any(root => IsPathInsideFolder(assetPath, root));
            if (!isIncluded)
                return false;

            return !excludedFolders
                .Select(NormalizeAssetPath)
                .Where(IsAssetsPath)
                .Any(exclusion => IsPathInsideFolder(assetPath, exclusion));
        }

        internal static string NormalizeAssetPath(string path) {
            return path.Trim().Replace('\\', '/').TrimEnd('/');
        }

        internal static bool IsAssetsPath(string path) {
            path = NormalizeAssetPath(path);
            return path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal);
        }

        internal static SerializedTypeCscRspUpdateResult UpdateResponseFiles(
            IEnumerable<string> responseFileDiskPaths) {

            var result = new SerializedTypeCscRspUpdateResult();
            foreach (var responseFilePath in responseFileDiskPaths.Distinct(StringComparer.Ordinal)) {
                try {
                    switch (EnsureResponseFileDirective(responseFilePath)) {
                        case SerializedTypeCscRspFileUpdateStatus.Created:
                            result.AddCreated();
                            break;
                        case SerializedTypeCscRspFileUpdateStatus.Updated:
                            result.AddUpdated();
                            break;
                        case SerializedTypeCscRspFileUpdateStatus.Unchanged:
                            result.AddUnchanged();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception exception) {
                    result.AddFailure($"{responseFilePath}: {exception.Message}");
                }
            }

            return result;
        }

        internal static SerializedTypeCscRspFileUpdateStatus EnsureResponseFileDirective(string responseFilePath) {
            if (!File.Exists(responseFilePath)) {
                File.WriteAllText(
                    responseFilePath,
                    AdditionalFileDirective + Environment.NewLine,
                    new UTF8Encoding(false));
                return SerializedTypeCscRspFileUpdateStatus.Created;
            }

            var fileInfo = new FileInfo(responseFilePath);
            if (fileInfo.IsReadOnly)
                throw new UnauthorizedAccessException("The response file is read-only.");

            var existingBytes = File.ReadAllBytes(responseFilePath);
            var (encoding, preambleLength) = DetectEncoding(existingBytes);
            var existingText = encoding.GetString(
                existingBytes,
                preambleLength,
                existingBytes.Length - preambleLength);

            if (ContainsEquivalentDirective(existingText))
                return SerializedTypeCscRspFileUpdateStatus.Unchanged;

            var newline = DetectNewline(existingText);
            var appendText = existingText.Length == 0 || EndsWithNewline(existingText)
                ? AdditionalFileDirective + newline
                : newline + AdditionalFileDirective + newline;
            var appendedBytes = encoding.GetBytes(appendText);

            using (var stream = new FileStream(responseFilePath, FileMode.Append, FileAccess.Write, FileShare.Read)) {
                stream.Write(appendedBytes, 0, appendedBytes.Length);
            }

            return SerializedTypeCscRspFileUpdateStatus.Updated;
        }

        internal static bool ContainsEquivalentDirective(string responseFileContents) {
            using var reader = new StringReader(responseFileContents);
            while (reader.ReadLine() is { } line) {
                var trimmedLine = line.Trim();
                var separatorIndex = trimmedLine.IndexOf(':');
                if (separatorIndex < 0)
                    continue;

                var option = trimmedLine.Substring(0, separatorIndex).Trim();
                if (option.Length < 2 || (option[0] != '-' && option[0] != '/'))
                    continue;

                if (!string.Equals(option.Substring(1), "additionalfile", StringComparison.OrdinalIgnoreCase))
                    continue;

                var value = trimmedLine.Substring(separatorIndex + 1).Trim();
                if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                    value = value.Substring(1, value.Length - 2).Trim();

                value = value.Replace('\\', '/');
                if (string.Equals(
                        value,
                        SerializedTypeUsageManifestPaths.ManifestXmlRelativePath,
                        StringComparison.Ordinal)) {
                    return true;
                }
            }

            return false;
        }

        public static void LogResult(string operation, SerializedTypeCscRspUpdateResult result, bool logUnchanged) {
            if (logUnchanged || result.HasChanges)
                Debug.Log($"[SerializedType] {operation}. {result.Summary}");

            foreach (var failure in result.Failures)
                Debug.LogError($"[SerializedType] csc.rsp update failed: {failure}");
        }

        static IReadOnlyList<string> FindTargetResponseFileAssetPaths(
            IReadOnlyList<string> rootFolders,
            IReadOnlyList<string> excludedFolders,
            SerializedTypeCscRspUpdateResult result) {

            var validRoots = new List<string>();
            foreach (var rootFolder in rootFolders
                         .Select(NormalizeAssetPath)
                         .Distinct(StringComparer.Ordinal)) {

                if (!IsAssetsPath(rootFolder) || !AssetDatabase.IsValidFolder(rootFolder)) {
                    result.AddSkipped();
                    continue;
                }

                validRoots.Add(rootFolder);
            }

            if (validRoots.Count == 0)
                return Array.Empty<string>();

            var assemblyDefinitionPaths = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rootFolder in validRoots) {
                foreach (var guid in AssetDatabase.FindAssets("t:AssemblyDefinitionAsset", new[] { rootFolder })) {
                    var path = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                    if (path.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase))
                        assemblyDefinitionPaths.Add(path);
                }
            }

            return GetTargetResponseFileAssetPaths(
                assemblyDefinitionPaths,
                validRoots,
                excludedFolders);
        }

        static SerializedTypeCscRspUpdateResult ReconcileTargetAssetPaths(
            IReadOnlyList<string> targetAssetPaths,
            SerializedTypeCscRspUpdateResult result) {

            if (targetAssetPaths.Count == 0)
                return result;

            if (!EnsureManifestExists(result))
                return result;

            foreach (var targetAssetPath in targetAssetPaths) {
                var responseFilePath = GetDiskPath(targetAssetPath);
                try {
                    switch (EnsureResponseFileDirective(responseFilePath)) {
                        case SerializedTypeCscRspFileUpdateStatus.Created:
                            result.AddCreated();
                            break;
                        case SerializedTypeCscRspFileUpdateStatus.Updated:
                            result.AddUpdated();
                            break;
                        case SerializedTypeCscRspFileUpdateStatus.Unchanged:
                            result.AddUnchanged();
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception exception) {
                    result.AddFailure($"{targetAssetPath}: {exception.Message}");
                }
            }

            if (result.HasChanges)
                AssetDatabase.Refresh();

            return result;
        }

        static bool EnsureManifestExists(SerializedTypeCscRspUpdateResult result) {
            if (File.Exists(SerializedTypeUsageManifestPaths.ManifestXmlDiskPath))
                return true;

            try {
                SerializedTypeUsageManifestBuilder.RebuildManifest();
            }
            catch (Exception exception) {
                result.AddFailure($"Unable to build the usage manifest: {exception.Message}");
                return false;
            }

            if (File.Exists(SerializedTypeUsageManifestPaths.ManifestXmlDiskPath))
                return true;

            result.AddFailure("The usage manifest builder completed without creating the manifest file.");
            return false;
        }

        static bool IsPathInsideFolder(string assetPath, string folderPath) {
            return assetPath == folderPath || assetPath.StartsWith(folderPath + "/", StringComparison.Ordinal);
        }

        static string GetDiskPath(string assetPath) {
            return Path.GetFullPath(Path.Combine(
                SerializedTypeUsageManifestPaths.ProjectRootPath,
                assetPath.Replace('/', Path.DirectorySeparatorChar)));
        }

        static (Encoding Encoding, int PreambleLength) DetectEncoding(byte[] bytes) {
            if (StartsWith(bytes, new byte[] { 0x00, 0x00, 0xFE, 0xFF }))
                return (new UTF32Encoding(true, true, true), 4);

            if (StartsWith(bytes, new byte[] { 0xFF, 0xFE, 0x00, 0x00 }))
                return (new UTF32Encoding(false, true, true), 4);

            if (StartsWith(bytes, new byte[] { 0xEF, 0xBB, 0xBF }))
                return (new UTF8Encoding(true, true), 3);

            if (StartsWith(bytes, new byte[] { 0xFE, 0xFF }))
                return (new UnicodeEncoding(true, true, true), 2);

            if (StartsWith(bytes, new byte[] { 0xFF, 0xFE }))
                return (new UnicodeEncoding(false, true, true), 2);

            StrictUtf8Encoding.GetString(bytes);
            return (StrictUtf8Encoding, 0);
        }

        static bool StartsWith(IReadOnlyList<byte> bytes, IReadOnlyList<byte> prefix) {
            if (bytes.Count < prefix.Count)
                return false;

            for (var i = 0; i < prefix.Count; i++) {
                if (bytes[i] != prefix[i])
                    return false;
            }

            return true;
        }

        static string DetectNewline(string text) {
            for (var i = 0; i < text.Length; i++) {
                if (text[i] == '\r')
                    return i + 1 < text.Length && text[i + 1] == '\n' ? "\r\n" : "\r";

                if (text[i] == '\n')
                    return "\n";
            }

            return Environment.NewLine;
        }

        static bool EndsWithNewline(string text) {
            return text.EndsWith("\r", StringComparison.Ordinal) ||
                   text.EndsWith("\n", StringComparison.Ordinal);
        }
    }
}
