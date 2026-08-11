using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Hissal.UnityTypeSerializer.Editor {
    [FilePath("ProjectSettings/UnityTypeSerializerCscRspSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    internal sealed class SerializedTypeCscRspSettings : ScriptableSingleton<SerializedTypeCscRspSettings> {
        [SerializeField]
        bool automaticUpdatesEnabled;

        [SerializeField]
        List<string> rootFolders = new() { "Assets" };

        [SerializeField]
        List<string> excludedFolders = new();

        public bool AutomaticUpdatesEnabled => automaticUpdatesEnabled;
        public IReadOnlyList<string> RootFolders => rootFolders;
        public IReadOnlyList<string> ExcludedFolders => excludedFolders;

        public void SetConfiguration(
            bool enableAutomaticUpdates,
            IEnumerable<string> roots,
            IEnumerable<string> exclusions) {

            automaticUpdatesEnabled = enableAutomaticUpdates;
            rootFolders = roots.ToList();
            excludedFolders = exclusions.ToList();
            Save(true);
        }
    }
}
