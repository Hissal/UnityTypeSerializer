using UnityEditor;

namespace Hissal.UnityTypeSerializer.Editor {
    internal static class SerializedTypeEditorPreferences {
        const string AutomaticUsageManifestRebuildEnabledKey =
            "Hissal.UnityTypeSerializer.AutomaticUsageManifestRebuildEnabled";

        public static bool AutomaticUsageManifestRebuildEnabled {
            get => EditorPrefs.GetBool(AutomaticUsageManifestRebuildEnabledKey, true);
            set => EditorPrefs.SetBool(AutomaticUsageManifestRebuildEnabledKey, value);
        }
    }
}
