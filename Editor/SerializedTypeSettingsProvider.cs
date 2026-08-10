using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Hissal.UnityTypeSerializer.Editor {
    internal static class SerializedTypeSettingsProvider {
        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider() {
            return new SettingsProvider("Preferences/Unity Type Serializer", SettingsScope.User) {
                label = "Unity Type Serializer",
                guiHandler = DrawPreferences,
                keywords = new HashSet<string> {
                    "Serialized Type",
                    "Usage Manifest",
                    "Automatic Rebuild",
                },
            };
        }

        static void DrawPreferences(string _) {
            EditorGUILayout.LabelField("Usage Manifest", EditorStyles.boldLabel);

            var currentAutomaticRebuildEnabled =
                SerializedTypeEditorPreferences.AutomaticUsageManifestRebuildEnabled;
            var automaticRebuildEnabled = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "Automatically Rebuild Usage Manifest",
                    "Rebuild the usage manifest after Unity reloads scripts."),
                currentAutomaticRebuildEnabled);

            if (automaticRebuildEnabled != currentAutomaticRebuildEnabled) {
                SerializedTypeEditorPreferences.AutomaticUsageManifestRebuildEnabled = automaticRebuildEnabled;
            }

            EditorGUILayout.HelpBox(
                "When automatic rebuilding is disabled, analyzer diagnostics may use stale usage manifest data until " +
                "the manifest is rebuilt manually.",
                MessageType.Info);

            EditorGUILayout.Space();
            if (GUILayout.Button("Rebuild Usage Manifest Now"))
                SerializedTypeUsageManifestBuilder.RebuildManifestAndLogResult();
        }
    }
}
