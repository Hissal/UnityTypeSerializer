using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace Hissal.UnityTypeSerializer.Editor {
    [InitializeOnLoad]
    internal static class SerializedTypeUsageManifestBuilder {
        static SerializedTypeUsageManifestBuilder() {
            EditorApplication.delayCall += () => RebuildManifest();
        }

        [MenuItem("Tools/SerializedType/Rebuild Usage Manifest")]
        static void RebuildManifestMenu() {
            if (RebuildManifest()) {
                Debug.Log("[SerializedType] Rebuilt SerializedType usage manifest.");
            }
            else {
                Debug.Log("[SerializedType] SerializedType usage manifest is already up to date.");
            }
        }

        internal static bool RebuildManifest() {
            var usageEntries = CollectUsageEntries();
            SortUsageEntries(usageEntries);
            return WriteAnalyzerManifestXml(usageEntries);
        }

        static List<SerializedTypeUsageEntry> CollectUsageEntries() {
            var entries = new List<SerializedTypeUsageEntry>();
            var allTypes = SerializedTypeEditorTypeCache.GetRuntimeDependentTypes();
            var genericConstraintIndex = new GenericConstraintIndex(allTypes);

            foreach (var type in allTypes) {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                foreach (var field in type.GetFields(flags)) {
                    AddUsageEntriesForField(genericConstraintIndex, type, field, entries);
                }
            }

            DeduplicateAnalyzerEquivalentEntries(entries);
            return entries;
        }

        static void SortUsageEntries(List<SerializedTypeUsageEntry> entries) {
            entries.Sort(CompareEntriesForSort);
        }

        static int CompareEntriesForSort(SerializedTypeUsageEntry a, SerializedTypeUsageEntry b) {
            var compare = string.CompareOrdinal(a.DeclaringAssembly, b.DeclaringAssembly);
            if (compare != 0)
                return compare;

            compare = string.CompareOrdinal(a.DeclaringType, b.DeclaringType);
            if (compare != 0)
                return compare;

            compare = string.CompareOrdinal(a.FieldName, b.FieldName);
            if (compare != 0)
                return compare;

            compare = string.CompareOrdinal(a.BaseConstraintMetadataName, b.BaseConstraintMetadataName);
            if (compare != 0)
                return compare;

            compare = a.AllowGenericTypeConstruction.CompareTo(b.AllowGenericTypeConstruction);
            if (compare != 0)
                return compare;

            compare = a.AllowOpenGenerics.CompareTo(b.AllowOpenGenerics);
            if (compare != 0)
                return compare;

            compare = a.AllowedTypeKinds.CompareTo(b.AllowedTypeKinds);
            if (compare != 0)
                return compare;

            compare = string.CompareOrdinal(a.CustomTypeFilter, b.CustomTypeFilter);
            if (compare != 0)
                return compare;

            compare = CompareSequence(a.InheritsOrImplementsAllMetadataNames, b.InheritsOrImplementsAllMetadataNames);
            if (compare != 0)
                return compare;

            return CompareSequence(a.InheritsOrImplementsAnyMetadataNames, b.InheritsOrImplementsAnyMetadataNames);
        }

        static int CompareSequence(IReadOnlyList<string> left, IReadOnlyList<string> right) {
            var count = Math.Min(left.Count, right.Count);
            for (var i = 0; i < count; i++) {
                var compare = string.CompareOrdinal(left[i], right[i]);
                if (compare != 0)
                    return compare;
            }

            return left.Count.CompareTo(right.Count);
        }

        static void AddUsageEntriesForField(
            GenericConstraintIndex genericConstraintIndex,
            Type declaringType,
            FieldInfo field,
            List<SerializedTypeUsageEntry> entries) {
            var fieldType = field.FieldType;

            if (fieldType == typeof(SerializedType)) {
                var options = field.GetCustomAttribute<SerializedTypeOptionsAttribute>(true);
                if (!HasMeaningfulNonGenericOptions(options))
                    return;

                if (TryCreateUsageEntry(declaringType, field, typeof(object), options, out var nonGenericEntry))
                    entries.Add(nonGenericEntry);
                return;
            }

            if (!fieldType.IsGenericType || fieldType.GetGenericTypeDefinition() != typeof(SerializedType<>))
                return;

            var options2 = field.GetCustomAttribute<SerializedTypeOptionsAttribute>(true);
            var baseConstraint = fieldType.GetGenericArguments()[0];
            if (!baseConstraint.ContainsGenericParameters) {
                if (TryCreateUsageEntry(declaringType, field, baseConstraint, options2, out var directEntry))
                    entries.Add(directEntry);
                return;
            }

            foreach (var (resolvedDeclaringType, resolvedConstraint) in genericConstraintIndex.Resolve(declaringType, baseConstraint)) {
                if (TryCreateUsageEntry(resolvedDeclaringType, field, resolvedConstraint, options2, out var resolvedEntry))
                    entries.Add(resolvedEntry);
            }
        }

        static bool TryCreateUsageEntry(
            Type declaringType,
            FieldInfo field,
            Type baseConstraint,
            SerializedTypeOptionsAttribute? options,
            out SerializedTypeUsageEntry entry) {
            entry = new SerializedTypeUsageEntry {
                DeclaringAssembly = declaringType.Assembly.GetName().Name ?? string.Empty,
                DeclaringType = declaringType.FullName ?? declaringType.Name,
                FieldName = field.Name,
                BaseConstraint = baseConstraint.AssemblyQualifiedName ?? baseConstraint.FullName ?? baseConstraint.Name,
                BaseConstraintMetadataName = GetMetadataTypeName(baseConstraint),
                AllowGenericTypeConstruction = options?.AllowGenericTypeConstruction ?? false,
                AllowOpenGenerics = options?.AllowOpenGenerics ?? false,
                AllowedTypeKinds = (int)(options?.AllowedTypeKinds ?? SerializedTypeKind.Object),
                InheritsOrImplementsAll = (options?.InheritsOrImplementsAll ?? Array.Empty<Type>())
                    .Where(t => t != null)
                    .Select(GetTypeKey)
                    .ToArray(),
                InheritsOrImplementsAny = (options?.InheritsOrImplementsAny ?? Array.Empty<Type>())
                    .Where(t => t != null)
                    .Select(GetTypeKey)
                    .ToArray(),
                InheritsOrImplementsAllMetadataNames = (options?.InheritsOrImplementsAll ?? Array.Empty<Type>())
                    .Where(t => t != null)
                    .Select(GetMetadataTypeName)
                    .ToArray(),
                InheritsOrImplementsAnyMetadataNames = (options?.InheritsOrImplementsAny ?? Array.Empty<Type>())
                    .Where(t => t != null)
                    .Select(GetMetadataTypeName)
                    .ToArray(),
                CustomTypeFilter = options?.CustomTypeFilter ?? string.Empty,
            };

            return true;
        }

        static bool HasMeaningfulNonGenericOptions(SerializedTypeOptionsAttribute? options) {
            if (options?.InheritsOrImplementsAll?.Any(t => t != null) == true)
                return true;

            if (options?.InheritsOrImplementsAny?.Any(t => t != null) == true)
                return true;

            return false;
        }

        static Type? ResolveGenericType(Type type, IReadOnlyDictionary<Type, Type> map) {
            if (type.IsGenericParameter) {
                return map.TryGetValue(type, out var resolvedParameter)
                    ? resolvedParameter
                    : null;
            }

            if (!type.IsGenericType)
                return type;

            var genericArguments = type.GetGenericArguments();
            var resolvedArguments = new Type[genericArguments.Length];
            for (var i = 0; i < genericArguments.Length; i++) {
                var resolvedArgument = ResolveGenericType(genericArguments[i], map);
                if (resolvedArgument == null)
                    return null;

                resolvedArguments[i] = resolvedArgument;
            }

            var genericDefinition = type.IsGenericTypeDefinition
                ? type
                : type.GetGenericTypeDefinition();
            try {
                return genericDefinition.MakeGenericType(resolvedArguments);
            }
            catch {
                return null;
            }
        }

        static string GetTypeKey(Type type) {
            return type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
        }

        static string GetMetadataTypeName(Type type) {
            if (type.IsGenericType && !type.IsGenericTypeDefinition)
                type = type.GetGenericTypeDefinition();

            return type.FullName ?? type.Name;
        }

        static bool WriteAnalyzerManifestXml(IReadOnlyList<SerializedTypeUsageEntry> entries) {
            var manifestXmlPath = SerializedTypeUsageManifestPaths.ManifestXmlDiskPath;
            var folderPath = Path.GetDirectoryName(manifestXmlPath);
            if (string.IsNullOrEmpty(folderPath))
                throw new InvalidOperationException("Unable to resolve manifest xml folder path.");

            Directory.CreateDirectory(folderPath);

            if (File.Exists(manifestXmlPath)) {
                try {
                    var existingDocument = XDocument.Load(manifestXmlPath);
                    var existingGeneratedAtUtc = (string?)existingDocument.Root?.Attribute("generatedAtUtc") ?? string.Empty;
                    var unchangedCandidate = BuildManifestDocument(entries, existingGeneratedAtUtc);
                    if (XNode.DeepEquals(existingDocument, unchangedCandidate))
                        return false;
                }
                catch {
                    // Ignore parse/read failures and overwrite below.
                }
            }

            var generatedAtUtc = DateTime.UtcNow.ToString("O");
            var updatedDocument = BuildManifestDocument(entries, generatedAtUtc);
            updatedDocument.Save(manifestXmlPath);
            return true;
        }

        static XDocument BuildManifestDocument(IReadOnlyList<SerializedTypeUsageEntry> entries, string generatedAtUtc) {
            return new XDocument(
                new XElement("SerializedTypeUsageManifest",
                    new XAttribute("generatedAtUtc", generatedAtUtc),
                    entries.Select(entry => new XElement(
                        "Entry",
                        new XAttribute("baseConstraint", entry.BaseConstraintMetadataName),
                        new XAttribute("allowGenericTypeConstruction", entry.AllowGenericTypeConstruction),
                        new XAttribute("allowOpenGenerics", entry.AllowOpenGenerics),
                        new XAttribute("allowedTypeKinds", entry.AllowedTypeKinds),
                        new XAttribute("inheritsAll", string.Join(";", entry.InheritsOrImplementsAllMetadataNames)),
                        new XAttribute("inheritsAny", string.Join(";", entry.InheritsOrImplementsAnyMetadataNames)),
                        new XAttribute("customTypeFilter", entry.CustomTypeFilter)))));
        }

        static void DeduplicateAnalyzerEquivalentEntries(List<SerializedTypeUsageEntry> entries) {
            var seenEntries = new HashSet<SerializedTypeUsageEntry>(AnalyzerEquivalentEntryComparer.Instance);
            var deduplicatedEntries = new List<SerializedTypeUsageEntry>(entries.Count);

            foreach (var entry in entries) {
                if (seenEntries.Add(entry))
                    deduplicatedEntries.Add(entry);
            }

            entries.Clear();
            entries.AddRange(deduplicatedEntries);
        }

        sealed class GenericConstraintIndex {
            readonly Dictionary<Type, List<GenericConstraintCandidate>> candidatesByGenericDefinition = new();

            public GenericConstraintIndex(IReadOnlyList<Type> allTypes) {
                foreach (var candidateType in allTypes) {
                    if (candidateType.ContainsGenericParameters)
                        continue;

                    AddCandidateType(candidateType);
                }
            }

            public IEnumerable<(Type DeclaringType, Type BaseConstraint)> Resolve(Type genericDeclaringType, Type baseConstraint) {
                if (!genericDeclaringType.ContainsGenericParameters)
                    yield break;

                var genericDefinition = genericDeclaringType.IsGenericType
                    ? genericDeclaringType.GetGenericTypeDefinition()
                    : genericDeclaringType;

                if (!candidatesByGenericDefinition.TryGetValue(genericDefinition, out var candidates))
                    yield break;

                foreach (var candidate in candidates) {
                    var resolvedConstraint = ResolveGenericType(baseConstraint, candidate.GenericParameterMap);
                    if (resolvedConstraint == null || resolvedConstraint.ContainsGenericParameters)
                        continue;

                    yield return (candidate.CandidateType, resolvedConstraint);
                }
            }

            void AddCandidateType(Type candidateType) {
                for (var current = candidateType; current != null; current = current.BaseType) {
                    if (!current.IsGenericType)
                        continue;

                    var genericDefinition = current.GetGenericTypeDefinition();
                    var genericParameterMap = CreateGenericParameterMap(genericDefinition, current);
                    if (genericParameterMap.Count == 0)
                        continue;

                    if (!candidatesByGenericDefinition.TryGetValue(genericDefinition, out var candidates)) {
                        candidates = new List<GenericConstraintCandidate>();
                        candidatesByGenericDefinition[genericDefinition] = candidates;
                    }

                    candidates.Add(new GenericConstraintCandidate(candidateType, genericParameterMap));
                }
            }

            static Dictionary<Type, Type> CreateGenericParameterMap(Type genericDefinition, Type constructedType) {
                var map = new Dictionary<Type, Type>();
                var definitionArguments = genericDefinition.GetGenericArguments();
                var actualArguments = constructedType.GetGenericArguments();

                for (var i = 0; i < Math.Min(definitionArguments.Length, actualArguments.Length); i++) {
                    map[definitionArguments[i]] = actualArguments[i];
                }

                return map;
            }
        }

        readonly struct GenericConstraintCandidate {
            public GenericConstraintCandidate(Type candidateType, IReadOnlyDictionary<Type, Type> genericParameterMap) {
                CandidateType = candidateType;
                GenericParameterMap = genericParameterMap;
            }

            public Type CandidateType { get; }
            public IReadOnlyDictionary<Type, Type> GenericParameterMap { get; }
        }

        sealed class AnalyzerEquivalentEntryComparer : IEqualityComparer<SerializedTypeUsageEntry> {
            public static readonly AnalyzerEquivalentEntryComparer Instance = new();

            public bool Equals(SerializedTypeUsageEntry? x, SerializedTypeUsageEntry? y) {
                if (ReferenceEquals(x, y))
                    return true;

                if (x == null || y == null)
                    return false;

                return string.Equals(x.BaseConstraintMetadataName, y.BaseConstraintMetadataName, StringComparison.Ordinal)
                       && x.AllowGenericTypeConstruction == y.AllowGenericTypeConstruction
                       && x.AllowOpenGenerics == y.AllowOpenGenerics
                       && x.AllowedTypeKinds == y.AllowedTypeKinds
                       && string.Equals(x.CustomTypeFilter, y.CustomTypeFilter, StringComparison.Ordinal)
                       && x.InheritsOrImplementsAllMetadataNames.SequenceEqual(y.InheritsOrImplementsAllMetadataNames)
                       && x.InheritsOrImplementsAnyMetadataNames.SequenceEqual(y.InheritsOrImplementsAnyMetadataNames);
            }

            public int GetHashCode(SerializedTypeUsageEntry obj) {
                unchecked {
                    var hash = StringComparer.Ordinal.GetHashCode(obj.BaseConstraintMetadataName);
                    hash = (hash * 397) ^ obj.AllowGenericTypeConstruction.GetHashCode();
                    hash = (hash * 397) ^ obj.AllowOpenGenerics.GetHashCode();
                    hash = (hash * 397) ^ obj.AllowedTypeKinds;
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(obj.CustomTypeFilter);
                    hash = AddSequenceHash(hash, obj.InheritsOrImplementsAllMetadataNames);
                    hash = AddSequenceHash(hash, obj.InheritsOrImplementsAnyMetadataNames);
                    return hash;
                }
            }

            static int AddSequenceHash(int hash, IReadOnlyList<string> values) {
                unchecked {
                    foreach (var value in values) {
                        hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(value);
                    }

                    return hash;
                }
            }
        }
    }
}
