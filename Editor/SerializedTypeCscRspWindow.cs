using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hissal.UnityTypeSerializer.Editor {
    internal sealed class SerializedTypeCscRspWindow : EditorWindow {
        [SerializeField]
        bool hasLoadedDraft;

        [SerializeField]
        bool automaticUpdatesEnabled;

        [SerializeField]
        List<string> rootFolders = new();

        [SerializeField]
        List<string> excludedFolders = new();

        [SerializeField]
        bool hasUnsavedChanges;

        [SerializeField]
        string lastResultSummary = string.Empty;

        [SerializeField]
        bool lastResultHasFailures;

        Vector2 scrollPosition;

        [MenuItem("Tools/SerializedType/csc.rsp Setup")]
        static void OpenWindow() {
            var window = GetWindow<SerializedTypeCscRspWindow>();
            window.titleContent = new GUIContent("SerializedType csc.rsp");
            window.minSize = new Vector2(520f, 420f);
            window.Show();
        }

        void OnEnable() {
            titleContent = new GUIContent("SerializedType csc.rsp");
            minSize = new Vector2(520f, 420f);

            if (!hasLoadedDraft)
                LoadDraftFromSettings();
        }

        void OnGUI() {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            EditorGUILayout.LabelField("Usage Manifest Assembly Integration", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Registers the persistent usage manifest as a Roslyn additional file for each configured assembly. " +
                "The manifest survives Library deletion and is passed directly to the analyzer. Existing unrelated " +
                "csc.rsp content is preserved.",
                MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Compiler directive", EditorStyles.miniBoldLabel);
            EditorGUILayout.SelectableLabel(
                SerializedTypeCscRspUpdater.AdditionalFileDirective,
                EditorStyles.textField,
                GUILayout.Height(EditorGUIUtility.singleLineHeight));

            EditorGUILayout.Space();
            DrawFolderList("Root Folders", rootFolders, "Add Root Folder");

            EditorGUILayout.Space();
            DrawFolderList("Excluded Folders", excludedFolders, "Add Excluded Folder");
            EditorGUILayout.HelpBox(
                "An excluded folder wins over a root folder for itself and all descendants. Changing or removing an " +
                "exclusion does not remove directives that were added previously.",
                MessageType.None);

            EditorGUILayout.Space();
            var updatedAutomaticUpdatesEnabled = EditorGUILayout.ToggleLeft(
                new GUIContent(
                    "Automatically maintain csc.rsp files",
                    "Reconcile on editor startup and when asmdef or csc.rsp assets are imported, moved, or deleted."),
                automaticUpdatesEnabled);
            if (updatedAutomaticUpdatesEnabled != automaticUpdatesEnabled) {
                automaticUpdatesEnabled = updatedAutomaticUpdatesEnabled;
                hasUnsavedChanges = true;
            }

            if (hasUnsavedChanges)
                EditorGUILayout.HelpBox("The configuration has unapplied changes.", MessageType.Warning);

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Configuration"))
                ApplyConfiguration(reconcileIfAutomatic: true);

            if (GUILayout.Button("Run Now") && ApplyConfiguration(reconcileIfAutomatic: false))
                ReconcileAndShowResult(logUnchanged: true);
            EditorGUILayout.EndHorizontal();

            if (!string.IsNullOrEmpty(lastResultSummary)) {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(
                    lastResultSummary,
                    lastResultHasFailures ? MessageType.Error : MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        void DrawFolderList(string label, List<string> paths, string addButtonLabel) {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

            for (var i = 0; i < paths.Count; i++) {
                EditorGUILayout.BeginHorizontal();
                var currentPath = paths[i];
                var currentFolder = string.IsNullOrWhiteSpace(currentPath)
                    ? null
                    : AssetDatabase.LoadAssetAtPath<DefaultAsset>(currentPath);
                var selectedFolder = (DefaultAsset)EditorGUILayout.ObjectField(
                    currentFolder,
                    typeof(DefaultAsset),
                    false);

                if (selectedFolder != currentFolder) {
                    paths[i] = selectedFolder == null
                        ? string.Empty
                        : AssetDatabase.GetAssetPath(selectedFolder);
                    hasUnsavedChanges = true;
                }

                if (GUILayout.Button("Remove", GUILayout.Width(70f))) {
                    paths.RemoveAt(i);
                    hasUnsavedChanges = true;
                    EditorGUILayout.EndHorizontal();
                    break;
                }

                EditorGUILayout.EndHorizontal();
            }

            if (GUILayout.Button(addButtonLabel)) {
                paths.Add(string.Empty);
                hasUnsavedChanges = true;
            }
        }

        bool ApplyConfiguration(bool reconcileIfAutomatic) {
            if (!TryValidateFolders(rootFolders, "root", out var validatedRoots, out var rootError)) {
                ShowValidationError(rootError);
                return false;
            }

            if (!TryValidateFolders(
                    excludedFolders,
                    "excluded",
                    out var validatedExclusions,
                    out var exclusionError)) {
                ShowValidationError(exclusionError);
                return false;
            }

            SerializedTypeCscRspSettings.instance.SetConfiguration(
                automaticUpdatesEnabled,
                validatedRoots,
                validatedExclusions);

            rootFolders = validatedRoots;
            excludedFolders = validatedExclusions;
            hasUnsavedChanges = false;

            if (reconcileIfAutomatic && automaticUpdatesEnabled)
                ReconcileAndShowResult(logUnchanged: true);
            else if (reconcileIfAutomatic) {
                lastResultSummary = "Configuration applied. Automatic updates are disabled.";
                lastResultHasFailures = false;
            }

            return true;
        }

        void ReconcileAndShowResult(bool logUnchanged) {
            SerializedTypeCscRspAssetPostprocessor.MarkSessionReconciled();
            var result = SerializedTypeCscRspUpdater.ReconcileConfiguredAssemblies();
            SerializedTypeCscRspUpdater.LogResult("Updated csc.rsp files", result, logUnchanged);

            lastResultSummary = result.Summary;
            if (result.Failures.Count > 0)
                lastResultSummary += " See the Console for failure details.";
            lastResultHasFailures = result.FailedCount > 0;
        }

        void LoadDraftFromSettings() {
            var settings = SerializedTypeCscRspSettings.instance;
            automaticUpdatesEnabled = settings.AutomaticUpdatesEnabled;
            rootFolders = settings.RootFolders.ToList();
            excludedFolders = settings.ExcludedFolders.ToList();
            hasLoadedDraft = true;
            hasUnsavedChanges = false;
        }

        static bool TryValidateFolders(
            IReadOnlyList<string> paths,
            string folderKind,
            out List<string> validatedPaths,
            out string error) {

            validatedPaths = new List<string>();
            var seenPaths = new HashSet<string>(System.StringComparer.Ordinal);

            foreach (var path in paths) {
                var normalizedPath = SerializedTypeCscRspUpdater.NormalizeAssetPath(path);
                if (string.IsNullOrWhiteSpace(normalizedPath)) {
                    error = $"Select a folder for every {folderKind} folder entry or remove the empty entry.";
                    return false;
                }

                if (!SerializedTypeCscRspUpdater.IsAssetsPath(normalizedPath)) {
                    error = $"The {folderKind} folder '{normalizedPath}' is outside Assets.";
                    return false;
                }

                if (!AssetDatabase.IsValidFolder(normalizedPath)) {
                    error = $"The {folderKind} folder '{normalizedPath}' does not exist.";
                    return false;
                }

                if (seenPaths.Add(normalizedPath))
                    validatedPaths.Add(normalizedPath);
            }

            error = string.Empty;
            return true;
        }

        void ShowValidationError(string error) {
            lastResultSummary = error;
            lastResultHasFailures = true;
            EditorUtility.DisplayDialog("Invalid csc.rsp configuration", error, "OK");
        }
    }
}
