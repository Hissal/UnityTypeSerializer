using System.Collections.Generic;
using System.Xml.Linq;

namespace Hissal.UnityTypeSerializer.Editor {
    internal static class SerializedTypeUsageManifestDatabase {
        public static string GetGeneratedAtUtc() {
            var document = LoadDocument();
            return (string?)document?.Root?.Attribute("generatedAtUtc") ?? string.Empty;
        }

        public static IReadOnlyList<SerializedTypeUsageEntry> GetEntries() {
            var document = LoadDocument();
            var root = document?.Root;
            if (root is null)
                return System.Array.Empty<SerializedTypeUsageEntry>();

            var entries = new List<SerializedTypeUsageEntry>();
            foreach (var element in root.Elements("Entry")) {
                var entry = new SerializedTypeUsageEntry {
                    DeclaringType = (string?)element.Attribute("declaringType") ?? string.Empty,
                    FieldName = (string?)element.Attribute("fieldName") ?? string.Empty,
                    BaseConstraintMetadataName = (string?)element.Attribute("baseConstraint") ?? string.Empty,
                    AllowGenericTypeConstruction = bool.TryParse((string?)element.Attribute("allowGenericTypeConstruction"), out var allowGenericTypeConstruction) && allowGenericTypeConstruction,
                    AllowOpenGenerics = bool.TryParse((string?)element.Attribute("allowOpenGenerics"), out var allowOpenGenerics) && allowOpenGenerics,
                    AllowedTypeKinds = int.TryParse((string?)element.Attribute("allowedTypeKinds"), out var allowedTypeKinds) ? allowedTypeKinds : 0,
                    ExplicitTypeListMetadataNames = SplitConstraintList((string?)element.Attribute("explicitTypes")),
                    ExcludedTypesMetadataNames = SplitConstraintList((string?)element.Attribute("excludedTypes")),
                    InheritsOrImplementsAllMetadataNames = SplitConstraintList((string?)element.Attribute("inheritsAll")),
                    InheritsOrImplementsAnyMetadataNames = SplitConstraintList((string?)element.Attribute("inheritsAny")),
                    InheritsOrImplementsNoneMetadataNames = SplitConstraintList((string?)element.Attribute("inheritsNone")),
                    CustomTypeFilter = (string?)element.Attribute("customTypeFilter") ?? string.Empty,
                };
                entries.Add(entry);
            }

            return entries;
        }

        static XDocument? LoadDocument() {
            var path = SerializedTypeUsageManifestPaths.ManifestXmlDiskPath;
            if (!System.IO.File.Exists(path))
                return null;

            try {
                return XDocument.Load(path);
            }
            catch {
                return null;
            }
        }

        static string[] SplitConstraintList(string? value) {
            if (string.IsNullOrWhiteSpace(value))
                return System.Array.Empty<string>();

            return value!.Split(new[] { ';' }, System.StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
