using System.Collections.Generic;
using UnityEditor;

namespace Hissal.UnityTypeSerializer.Editor {
    internal static class SerializedTypeUsageManifestDatabase {
        public static IReadOnlyList<SerializedTypeUsageEntry> GetEntries() {
            var manifest = AssetDatabase.LoadAssetAtPath<SerializedTypeUsageManifest>(SerializedTypeUsageManifestPaths.ManifestAssetPath);
            return manifest?.Entries ?? System.Array.Empty<SerializedTypeUsageEntry>();
        }
    }
}

