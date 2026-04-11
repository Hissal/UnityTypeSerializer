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

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().OrderBy(a => a.FullName, StringComparer.Ordinal)) {
                foreach (var type in GetLoadableTypes(assembly)) {
                    if (type == null)
                        continue;

                    const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                    foreach (var field in type.GetFields(flags)) {
                        if (!TryCreateUsageEntry(type, field, out var entry))
                            continue;

                        entries.Add(entry);
                    }
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

        static bool TryCreateUsageEntry(Type declaringType, FieldInfo field, out SerializedTypeUsageEntry entry) {
            entry = new SerializedTypeUsageEntry();

            var fieldType = field.FieldType;
            if (!fieldType.IsGenericType || fieldType.GetGenericTypeDefinition() != typeof(SerializedType<>))
                return false;

            var baseConstraint = fieldType.GetGenericArguments()[0];
            var options = field.GetCustomAttribute<SerializedTypeOptionsAttribute>(true);

            entry = new SerializedTypeUsageEntry {
                DeclaringAssembly = declaringType.Assembly.GetName().Name ?? string.Empty,
                DeclaringType = declaringType.FullName ?? declaringType.Name,
                FieldName = field.Name,
                BaseConstraint = baseConstraint.AssemblyQualifiedName ?? baseConstraint.FullName ?? baseConstraint.Name,
                BaseConstraintMetadataName = GetMetadataTypeName(baseConstraint),
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
                        new XAttribute("allowOpenGenerics", entry.AllowOpenGenerics),
                        new XAttribute("allowedTypeKinds", entry.AllowedTypeKinds),
                        new XAttribute("inheritsAll", string.Join(";", entry.InheritsOrImplementsAllMetadataNames)),
                        new XAttribute("inheritsAny", string.Join(";", entry.InheritsOrImplementsAnyMetadataNames)),
                        new XAttribute("customTypeFilter", entry.CustomTypeFilter)))));
        }
    }
}
