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
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[SerializedType] Rebuilt SerializedType usage manifest.");
            }
            else {
                Debug.Log("[SerializedType] SerializedType usage manifest is already up to date.");
            }
        }

        internal static bool RebuildManifest() {
            var manifest = GetOrCreateManifestAsset();
            var usageEntries = CollectUsageEntries();
            SortUsageEntries(usageEntries);

            var hasEntryChanges = !AreEntriesEqual(manifest.Entries, usageEntries);
            var generatedAtUtc = hasEntryChanges
                ? DateTime.UtcNow.ToString("O")
                : manifest.GeneratedAtUtc;

            if (hasEntryChanges) {
                manifest.SetData(generatedAtUtc, usageEntries);
                EditorUtility.SetDirty(manifest);
            }

            var hasXmlChanges = WriteAnalyzerManifestXml(usageEntries, generatedAtUtc);
            return hasEntryChanges || hasXmlChanges;
        }

        static SerializedTypeUsageManifest GetOrCreateManifestAsset() {
            var manifestAssetPath = SerializedTypeUsageManifestPaths.ManifestAssetPath;
            var manifest = AssetDatabase.LoadAssetAtPath<SerializedTypeUsageManifest>(manifestAssetPath);
            if (manifest != null)
                return manifest;

            var folderPath = Path.GetDirectoryName(manifestAssetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folderPath)) {
                throw new InvalidOperationException("Unable to resolve manifest asset folder path.");
            }

            if (!AssetDatabase.IsValidFolder(folderPath)) {
                EnsureFolderHierarchy(folderPath);
            }

            manifest = ScriptableObject.CreateInstance<SerializedTypeUsageManifest>();
            AssetDatabase.CreateAsset(manifest, manifestAssetPath);
            return manifest;
        }

        static void EnsureFolderHierarchy(string folderPath) {
            var parts = folderPath.Split('/');
            var current = parts[0];
            for (var i = 1; i < parts.Length; i++) {
                var next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
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

        static bool AreEntriesEqual(IReadOnlyList<SerializedTypeUsageEntry> left, IReadOnlyList<SerializedTypeUsageEntry> right) {
            if (ReferenceEquals(left, right))
                return true;

            if (left.Count != right.Count)
                return false;

            for (var i = 0; i < left.Count; i++) {
                var a = left[i];
                var b = right[i];

                if (!AreEntryFieldsEqual(a, b))
                    return false;
            }

            return true;
        }

        static bool AreEntryFieldsEqual(SerializedTypeUsageEntry a, SerializedTypeUsageEntry b) {
            return string.Equals(a.DeclaringAssembly, b.DeclaringAssembly, StringComparison.Ordinal) &&
                   string.Equals(a.DeclaringType, b.DeclaringType, StringComparison.Ordinal) &&
                   string.Equals(a.FieldName, b.FieldName, StringComparison.Ordinal) &&
                   string.Equals(a.BaseConstraint, b.BaseConstraint, StringComparison.Ordinal) &&
                   string.Equals(a.BaseConstraintMetadataName, b.BaseConstraintMetadataName, StringComparison.Ordinal) &&
                   a.AllowOpenGenerics == b.AllowOpenGenerics &&
                   a.AllowedTypeKinds == b.AllowedTypeKinds &&
                   ArrayEquals(a.InheritsOrImplementsAll, b.InheritsOrImplementsAll) &&
                   ArrayEquals(a.InheritsOrImplementsAny, b.InheritsOrImplementsAny) &&
                   ArrayEquals(a.InheritsOrImplementsAllMetadataNames, b.InheritsOrImplementsAllMetadataNames) &&
                   ArrayEquals(a.InheritsOrImplementsAnyMetadataNames, b.InheritsOrImplementsAnyMetadataNames) &&
                   string.Equals(a.CustomTypeFilter, b.CustomTypeFilter, StringComparison.Ordinal);
        }

        static bool ArrayEquals(string[] left, string[] right) {
            if (ReferenceEquals(left, right))
                return true;

            if (left == null || right == null)
                return false;

            if (left.Length != right.Length)
                return false;

            for (var i = 0; i < left.Length; i++) {
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                    return false;
            }

            return true;
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

        static bool WriteAnalyzerManifestXml(IReadOnlyList<SerializedTypeUsageEntry> entries, string generatedAtUtc) {
            var manifestXmlPath = SerializedTypeUsageManifestPaths.ManifestXmlPath;
            var folderPath = Path.GetDirectoryName(manifestXmlPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(folderPath)) {
                throw new InvalidOperationException("Unable to resolve manifest xml folder path.");
            }

            if (!AssetDatabase.IsValidFolder(folderPath)) {
                EnsureFolderHierarchy(folderPath);
            }

            var document = new XDocument(
                new XElement("SerializedTypeUsageManifest",
                    new XAttribute("generatedAtUtc", generatedAtUtc),
                    entries.Select(entry => new XElement(
                        "Entry",
                        new XAttribute("baseConstraint", entry.BaseConstraintMetadataName ?? string.Empty),
                        new XAttribute("allowOpenGenerics", entry.AllowOpenGenerics),
                        new XAttribute("allowedTypeKinds", entry.AllowedTypeKinds),
                        new XAttribute("inheritsAll", string.Join(";", entry.InheritsOrImplementsAllMetadataNames ?? Array.Empty<string>())),
                        new XAttribute("inheritsAny", string.Join(";", entry.InheritsOrImplementsAnyMetadataNames ?? Array.Empty<string>())),
                        new XAttribute("customTypeFilter", entry.CustomTypeFilter ?? string.Empty)))));

            if (File.Exists(manifestXmlPath)) {
                try {
                    var existingDocument = XDocument.Load(manifestXmlPath);
                    if (XNode.DeepEquals(existingDocument, document))
                        return false;
                }
                catch {
                    // Ignore parse/read failures and overwrite below.
                }
            }

            document.Save(manifestXmlPath);
            return true;
        }
    }
}
