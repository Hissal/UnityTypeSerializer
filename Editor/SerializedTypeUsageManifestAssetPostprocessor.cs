using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Hissal.UnityTypeSerializer.Editor {
    internal sealed class SerializedTypeUsageManifestAssetPostprocessor : AssetPostprocessor {
        static readonly HashSet<string> RelevantExtensions = new(StringComparer.OrdinalIgnoreCase) {
            ".asmdef",
            ".asmref",
            ".cs",
            ".dll",
        };

        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths,
            bool didDomainReload) {

            if (ContainsRelevantAsset(importedAssets, includeExtensionlessPaths: false) ||
                ContainsRelevantAsset(deletedAssets, includeExtensionlessPaths: true) ||
                ContainsRelevantAsset(movedAssets, includeExtensionlessPaths: false) ||
                ContainsRelevantAsset(movedFromAssetPaths, includeExtensionlessPaths: true)) {
                SerializedTypeUsageManifestBuilder.MarkAutomaticRebuildPending();
            }
        }

        internal static bool IsRelevantAssetPath(string assetPath, bool includeExtensionlessPaths) {
            var extension = Path.GetExtension(assetPath);
            if (string.IsNullOrEmpty(extension))
                return includeExtensionlessPaths;

            return RelevantExtensions.Contains(extension);
        }

        static bool ContainsRelevantAsset(
            IEnumerable<string> assetPaths,
            bool includeExtensionlessPaths) {

            return assetPaths.Any(path => IsRelevantAssetPath(path, includeExtensionlessPaths));
        }
    }
}
