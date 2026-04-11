using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hissal.UnityTypeSerializer.Editor {
    [Serializable]
    public sealed class SerializedTypeUsageEntry {
        public string DeclaringAssembly = string.Empty;
        public string DeclaringType = string.Empty;
        public string FieldName = string.Empty;
        public string BaseConstraint = string.Empty;
        public string BaseConstraintMetadataName = string.Empty;
        public bool AllowOpenGenerics;
        public int AllowedTypeKinds;
        public string[] InheritsOrImplementsAll = Array.Empty<string>();
        public string[] InheritsOrImplementsAny = Array.Empty<string>();
        public string[] InheritsOrImplementsAllMetadataNames = Array.Empty<string>();
        public string[] InheritsOrImplementsAnyMetadataNames = Array.Empty<string>();
        public string CustomTypeFilter = string.Empty;
    }

    /// <summary>
    /// Persisted cross-assembly index of SerializedType field usages discovered in the loaded AppDomain.
    /// </summary>
    public sealed class SerializedTypeUsageManifest : ScriptableObject {
        [SerializeField] string generatedAtUtc = string.Empty;
        [SerializeField] List<SerializedTypeUsageEntry> entries = new();

        public string GeneratedAtUtc => generatedAtUtc;
        public IReadOnlyList<SerializedTypeUsageEntry> Entries => entries;

        internal void SetData(string utcTimestamp, List<SerializedTypeUsageEntry> usageEntries) {
            generatedAtUtc = utcTimestamp;
            entries = usageEntries;
        }
    }
}

