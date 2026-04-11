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
            var allTypes = AppDomain.CurrentDomain.GetAssemblies()
                .OrderBy(a => a.FullName, StringComparer.Ordinal)
                .SelectMany(GetLoadableTypes)
                .Where(t => t != null)
                .Cast<Type>()
                .ToArray();

            foreach (var type in allTypes) {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                foreach (var field in type.GetFields(flags)) {
                    AddUsageEntriesForField(allTypes, type, field, entries);
                }
            }

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

            return CompareSequence(a.InheritsOrImplementsAllMetadataNames, b.InheritsOrImplementsAllMetadataNames);
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
            IReadOnlyList<Type> allTypes,
            Type declaringType,
            FieldInfo field,
            List<SerializedTypeUsageEntry> entries) {
            var fieldType = field.FieldType;
            var options = field.GetCustomAttribute<SerializedTypeOptionsAttribute>(true);

            if (fieldType == typeof(SerializedType)) {
                if (!HasMeaningfulNonGenericOptions(options))
                    return;

                if (TryCreateUsageEntry(declaringType, field, typeof(object), options, out var nonGenericEntry))
                    entries.Add(nonGenericEntry);
                return;
            }

            if (!fieldType.IsGenericType || fieldType.GetGenericTypeDefinition() != typeof(SerializedType<>))
                return;

            var baseConstraint = fieldType.GetGenericArguments()[0];
            if (!baseConstraint.ContainsGenericParameters) {
                if (TryCreateUsageEntry(declaringType, field, baseConstraint, options, out var directEntry))
                    entries.Add(directEntry);
                return;
            }

            foreach (var (resolvedDeclaringType, resolvedConstraint) in ResolveConcreteGenericConstraints(allTypes, declaringType, baseConstraint)) {
                if (TryCreateUsageEntry(resolvedDeclaringType, field, resolvedConstraint, options, out var resolvedEntry))
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

        static IEnumerable<(Type DeclaringType, Type BaseConstraint)> ResolveConcreteGenericConstraints(
            IReadOnlyList<Type> allTypes,
            Type genericDeclaringType,
            Type baseConstraint) {
            if (!genericDeclaringType.ContainsGenericParameters)
                yield break;

            foreach (var candidate in allTypes) {
                if (candidate.ContainsGenericParameters)
                    continue;

                if (!TryCreateGenericParameterMap(candidate, genericDeclaringType, out var map))
                    continue;

                var resolvedConstraint = ResolveGenericType(baseConstraint, map);
                if (resolvedConstraint == null || resolvedConstraint.ContainsGenericParameters)
                    continue;

                yield return (candidate, resolvedConstraint);
            }
        }

        static bool TryCreateGenericParameterMap(
            Type candidateType,
            Type genericDeclaringType,
            out Dictionary<Type, Type> map) {
            map = new Dictionary<Type, Type>();

            var declaringGenericDefinition = genericDeclaringType.IsGenericType
                ? genericDeclaringType.GetGenericTypeDefinition()
                : genericDeclaringType;

            for (var current = candidateType; current != null; current = current.BaseType) {
                if (!current.IsGenericType)
                    continue;

                var currentDefinition = current.GetGenericTypeDefinition();
                if (currentDefinition != declaringGenericDefinition)
                    continue;

                var definitionArguments = currentDefinition.GetGenericArguments();
                var actualArguments = current.GetGenericArguments();
                for (var i = 0; i < Math.Min(definitionArguments.Length, actualArguments.Length); i++) {
                    map[definitionArguments[i]] = actualArguments[i];
                }

                return true;
            }

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

        static IEnumerable<Type?> GetLoadableTypes(Assembly assembly) {
            try {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e) {
                return e.Types;
            }
            catch {
                return Array.Empty<Type>();
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
    }
}
