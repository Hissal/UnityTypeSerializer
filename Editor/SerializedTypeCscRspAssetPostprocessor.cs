using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace Hissal.UnityTypeSerializer.Editor {
    internal sealed class SerializedTypeCscRspAssetPostprocessor : AssetPostprocessor {
        const string SessionReconciledKey =
            "Hissal.UnityTypeSerializer.CscRspAutomationReconciledThisSession";

        static readonly HashSet<string> PendingDirectories = new(StringComparer.Ordinal);

        static bool isReconcileQueued;
        static bool shouldRunFullReconcile;

        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths,
            bool didDomainReload) {

            var settings = SerializedTypeCscRspSettings.instance;
            if (!settings.AutomaticUpdatesEnabled)
                return;

            if (didDomainReload && !SessionState.GetBool(SessionReconciledKey, false)) {
                MarkSessionReconciled();
                shouldRunFullReconcile = true;
            }

            CollectRelevantDirectories(importedAssets);
            CollectRelevantDirectories(deletedAssets);
            CollectRelevantDirectories(movedAssets);
            CollectRelevantDirectories(movedFromAssetPaths);

            if (movedAssets.Any(path =>
                    AssetDatabase.IsValidFolder(path) &&
                    SerializedTypeCscRspUpdater.IsPathIncluded(
                        path,
                        settings.RootFolders,
                        settings.ExcludedFolders))) {
                shouldRunFullReconcile = true;
            }

            QueueReconcileIfNeeded();
        }

        internal static void MarkSessionReconciled() {
            SessionState.SetBool(SessionReconciledKey, true);
        }

        internal static bool IsRelevantAssetPath(string assetPath) {
            var normalizedPath = SerializedTypeCscRspUpdater.NormalizeAssetPath(assetPath);
            return normalizedPath.EndsWith(".asmdef", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(
                       Path.GetFileName(normalizedPath),
                       SerializedTypeCscRspUpdater.ResponseFileName,
                       StringComparison.OrdinalIgnoreCase);
        }

        static void CollectRelevantDirectories(IEnumerable<string> assetPaths) {
            foreach (var assetPath in assetPaths) {
                if (!IsRelevantAssetPath(assetPath))
                    continue;

                var directoryPath = Path.GetDirectoryName(assetPath);
                if (string.IsNullOrWhiteSpace(directoryPath))
                    continue;

                PendingDirectories.Add(SerializedTypeCscRspUpdater.NormalizeAssetPath(directoryPath));
            }
        }

        static void QueueReconcileIfNeeded() {
            if ((!shouldRunFullReconcile && PendingDirectories.Count == 0) || isReconcileQueued)
                return;

            isReconcileQueued = true;
            EditorApplication.delayCall += ReconcilePendingChanges;
        }

        static void ReconcilePendingChanges() {
            isReconcileQueued = false;

            if (!SerializedTypeCscRspSettings.instance.AutomaticUpdatesEnabled) {
                PendingDirectories.Clear();
                shouldRunFullReconcile = false;
                return;
            }

            var runFullReconcile = shouldRunFullReconcile;
            shouldRunFullReconcile = false;
            var pendingDirectories = PendingDirectories.ToArray();
            PendingDirectories.Clear();

            var result = runFullReconcile
                ? SerializedTypeCscRspUpdater.ReconcileConfiguredAssemblies()
                : SerializedTypeCscRspUpdater.ReconcileDirectories(pendingDirectories);
            SerializedTypeCscRspUpdater.LogResult("Automatically updated csc.rsp files", result, logUnchanged: false);
        }
    }
}
