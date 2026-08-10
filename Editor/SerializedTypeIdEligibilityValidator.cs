#if ODIN_VALIDATOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector.Editor.Validation;

[assembly: RegisterValidator(typeof(Hissal.UnityTypeSerializer.Editor.SerializedTypeIdEligibilityValidator))]

namespace Hissal.UnityTypeSerializer.Editor {
    internal sealed class SerializedTypeIdEligibilityValidator : GlobalValidator {
        static HashSet<Type>? s_typesWithSerializedTypeId;

        const int TYPE_KIND_CLASS = 1 << 0;
        const int TYPE_KIND_STRUCT = 1 << 1;
        const int TYPE_KIND_ABSTRACT = 1 << 2;
        const int TYPE_KIND_INTERFACE = 1 << 3;
        const int TYPE_KIND_STATIC = 1 << 4;
        const int TYPE_KIND_ENUM = 1 << 5;
        const int TYPE_KIND_DELEGATE = 1 << 6;
        const int TYPE_KIND_PRIMITIVE = 1 << 7;
        const int TYPE_KIND_OBJECT = TYPE_KIND_CLASS | TYPE_KIND_STRUCT;
        const int TYPE_KIND_ALL = TYPE_KIND_CLASS | TYPE_KIND_STRUCT | TYPE_KIND_ABSTRACT | TYPE_KIND_INTERFACE | TYPE_KIND_STATIC | TYPE_KIND_ENUM | TYPE_KIND_DELEGATE | TYPE_KIND_PRIMITIVE;

        public override IEnumerable RunValidation(ValidationResult result) {
            var allTypes = SerializedTypeEditorTypeCache.GetLoadableDomainTypes();
            var typeByMetadataName = BuildMetadataTypeLookup(allTypes);
            var constraints = BuildConstraints(typeByMetadataName);
            if (constraints.Count == 0)
                yield break;

            var typesWithSerializedTypeId = GetTypesWithSerializedTypeId();

            foreach (var type in allTypes) {
                if (typesWithSerializedTypeId.Contains(type))
                    continue;

                var reasons = new SortedSet<string>(StringComparer.Ordinal);
                foreach (var constraint in constraints) {
                    if (MatchesConstraint(type, constraint, out var reason)) {
                        reasons.Add(reason);
                    }
                }

                if (reasons.Count == 0)
                    continue;

                var reasonText = string.Join("; ", reasons.Take(3));
                result.AddWarning($"Type '{GetTypeDisplayName(type)}' matches SerializedType constraints ({reasonText}) and should likely have [SerializedTypeId]");
            }

            yield break;
        }

        static HashSet<Type> GetTypesWithSerializedTypeId() {
            return s_typesWithSerializedTypeId ??= new HashSet<Type>(
                SerializedTypeEditorTypeCache.GetTypesWithAttribute<SerializedTypeIdAttribute>());
        }

        static List<ManifestConstraint> BuildConstraints(IReadOnlyDictionary<string, Type> typeByMetadataName) {
            var constraints = new List<ManifestConstraint>();
            var entries = SerializedTypeUsageManifestDatabase.GetEntries();
            for (var i = 0; i < entries.Count; i++) {
                var entry = entries[i];
                if (!string.IsNullOrWhiteSpace(entry.CustomTypeFilter))
                    continue;

                if (!TryResolveMetadataType(typeByMetadataName, entry.BaseConstraintMetadataName, out var baseConstraint))
                    continue;

                var explicitTypeList = ResolveConstraintTypes(typeByMetadataName, entry.ExplicitTypeListMetadataNames);
                if (explicitTypeList.HasRequestedTypes && explicitTypeList.ResolvedTypes.Length == 0)
                    continue;

                var excludedTypes = ResolveConstraintTypes(typeByMetadataName, entry.ExcludedTypesMetadataNames);

                var inheritsAll = ResolveConstraintTypes(typeByMetadataName, entry.InheritsOrImplementsAllMetadataNames);
                if (inheritsAll.HasUnresolvedTypes)
                    continue;

                var inheritsAny = ResolveConstraintTypes(typeByMetadataName, entry.InheritsOrImplementsAnyMetadataNames);
                if (inheritsAny.HasRequestedTypes && inheritsAny.ResolvedTypes.Length == 0)
                    continue;

                var inheritsNone = ResolveConstraintTypes(typeByMetadataName, entry.InheritsOrImplementsNoneMetadataNames);

                if (baseConstraint == typeof(object) &&
                    explicitTypeList.ResolvedTypes.Length == 0 &&
                    !excludedTypes.HasRequestedTypes &&
                    inheritsAll.ResolvedTypes.Length == 0 &&
                    inheritsAny.ResolvedTypes.Length == 0 &&
                    !inheritsNone.HasRequestedTypes) {
                    continue;
                }

                constraints.Add(new ManifestConstraint(
                    baseConstraint,
                    entry.AllowedTypeKinds,
                    entry.AllowGenericTypeConstruction,
                    entry.AllowOpenGenerics,
                    explicitTypeList.ResolvedTypes,
                    excludedTypes.ResolvedTypes,
                    inheritsAll.ResolvedTypes,
                    inheritsAny.ResolvedTypes,
                    inheritsNone.ResolvedTypes,
                    BuildManifestConstraintReason(
                        entry,
                        baseConstraint,
                        explicitTypeList.ResolvedTypes,
                        excludedTypes.ResolvedTypes,
                        inheritsAll.ResolvedTypes,
                        inheritsAny.ResolvedTypes,
                        inheritsNone.ResolvedTypes)));
            }

            return constraints;
        }

        static IReadOnlyDictionary<string, Type> BuildMetadataTypeLookup(IEnumerable<Type> types) {
            var dictionary = new Dictionary<string, Type>(StringComparer.Ordinal);

            foreach (var type in types) {
                var metadataName = type.FullName;
                if (string.IsNullOrWhiteSpace(metadataName))
                    continue;

                dictionary.TryAdd(metadataName, type);
            }

            return dictionary;
        }

        static ResolvedConstraintTypeList ResolveConstraintTypes(IReadOnlyDictionary<string, Type> typeByMetadataName, IReadOnlyList<string> metadataNames) {
            if (metadataNames == null || metadataNames.Count == 0)
                return new ResolvedConstraintTypeList(Array.Empty<Type>(), false, false);

            var resolvedTypes = new List<Type>(metadataNames.Count);
            var hasRequestedTypes = false;
            var hasUnresolvedTypes = false;
            for (var i = 0; i < metadataNames.Count; i++) {
                if (string.IsNullOrWhiteSpace(metadataNames[i]))
                    continue;

                hasRequestedTypes = true;
                if (TryResolveMetadataType(typeByMetadataName, metadataNames[i], out var resolvedType)) {
                    resolvedTypes.Add(resolvedType);
                }
                else {
                    hasUnresolvedTypes = true;
                }
            }

            return new ResolvedConstraintTypeList(resolvedTypes.ToArray(), hasRequestedTypes, hasUnresolvedTypes);
        }

        static bool TryResolveMetadataType(IReadOnlyDictionary<string, Type> typeByMetadataName, string metadataName, out Type resolvedType) {
            if (string.IsNullOrWhiteSpace(metadataName)) {
                resolvedType = default;
                return false;
            }

            return typeByMetadataName.TryGetValue(metadataName.Trim(), out resolvedType);
        }

        static bool MatchesConstraint(Type candidateType, ManifestConstraint constraint, out string reason) {
            reason = string.Empty;

            if (!IsAssignableTo(candidateType, constraint.BaseConstraint))
                return false;

            if (constraint.ExplicitTypeList.Length > 0 &&
                constraint.ExplicitTypeList.All(explicitType => !MatchesDirectType(candidateType, explicitType)))
                return false;

            if (constraint.ExcludedTypes.Any(excludedType => MatchesDirectType(candidateType, excludedType)))
                return false;

            if (!PassesAllowedKind(candidateType, constraint.AllowedTypeKinds))
                return false;

            if (ShouldRejectOpenGenericCandidate(candidateType, constraint))
                return false;

            if (constraint.InheritsOrImplementsAll.Any(required => !IsAssignableTo(candidateType, required)))
                return false;

            if (constraint.InheritsOrImplementsAny.Length > 0 &&
                constraint.InheritsOrImplementsAny.All(required => !IsAssignableTo(candidateType, required)))
                return false;

            if (constraint.InheritsOrImplementsNone.Any(excludedBase => IsAssignableTo(candidateType, excludedBase)))
                return false;

            reason = constraint.Reason;
            return true;
        }

        static bool ShouldRejectOpenGenericCandidate(Type candidateType, ManifestConstraint constraint) {
            if (constraint.AllowOpenGenerics || constraint.AllowGenericTypeConstruction)
                return false;

            return candidateType.ContainsGenericParameters;
        }

        static bool IsAssignableTo(Type sourceType, Type targetType) {
            if (sourceType == targetType)
                return true;

            for (var current = sourceType; current != null; current = current.BaseType) {
                if (IsTypeMatch(current, targetType))
                    return true;
            }

            foreach (var interfaceType in sourceType.GetInterfaces()) {
                if (IsTypeMatch(interfaceType, targetType))
                    return true;
            }

            return false;
        }

        static bool MatchesDirectType(Type candidateType, Type configuredType) {
            if (candidateType == configuredType)
                return true;

            var candidateDefinition = candidateType.IsGenericType ? candidateType.GetGenericTypeDefinition() : candidateType;
            var configuredDefinition = configuredType.IsGenericType ? configuredType.GetGenericTypeDefinition() : configuredType;
            return candidateDefinition == configuredType || candidateType == configuredDefinition || candidateDefinition == configuredDefinition;
        }

        static bool IsTypeMatch(Type left, Type right) {
            if (left == right)
                return true;

            var leftDefinition = left.IsGenericType ? left.GetGenericTypeDefinition() : left;
            var rightDefinition = right.IsGenericType ? right.GetGenericTypeDefinition() : right;

            return leftDefinition == right || left == rightDefinition || leftDefinition == rightDefinition;
        }

        static bool PassesAllowedKind(Type type, int allowedTypeKinds) {
            if ((allowedTypeKinds & TYPE_KIND_ALL) == TYPE_KIND_ALL)
                return true;

            var isInterface = type.IsInterface;
            var isStaticClass = type.IsClass && type.IsAbstract && type.IsSealed;
            var isAbstractClass = type.IsClass && type.IsAbstract && !type.IsSealed;
            var isEnum = type.IsEnum;
            var isDelegate = typeof(MulticastDelegate).IsAssignableFrom(type) && type != typeof(MulticastDelegate) && type != typeof(Delegate);
            var isPrimitive = type.IsPrimitive || type == typeof(decimal);
            var isStruct = type.IsValueType && !isPrimitive && !isEnum;
            var isClass = type.IsClass && !isStaticClass && !isAbstractClass;

            if (isClass && (allowedTypeKinds & TYPE_KIND_CLASS) != 0)
                return true;
            if (isStruct && (allowedTypeKinds & TYPE_KIND_STRUCT) != 0)
                return true;
            if (isAbstractClass && (allowedTypeKinds & TYPE_KIND_ABSTRACT) != 0)
                return true;
            if (isInterface && (allowedTypeKinds & TYPE_KIND_INTERFACE) != 0)
                return true;
            if (isStaticClass && (allowedTypeKinds & TYPE_KIND_STATIC) != 0)
                return true;
            if (isEnum && (allowedTypeKinds & TYPE_KIND_ENUM) != 0)
                return true;
            if (isDelegate && (allowedTypeKinds & TYPE_KIND_DELEGATE) != 0)
                return true;
            if (isPrimitive && (allowedTypeKinds & TYPE_KIND_PRIMITIVE) != 0)
                return true;

            return (isClass || isStruct) && (allowedTypeKinds & TYPE_KIND_OBJECT) == TYPE_KIND_OBJECT;
        }

        static string BuildManifestConstraintReason(
            SerializedTypeUsageEntry entry,
            Type baseConstraint,
            Type[] explicitTypeList,
            Type[] excludedTypes,
            Type[] inheritsOrImplementsAll,
            Type[] inheritsOrImplementsAny,
            Type[] inheritsOrImplementsNone) {

            var sourceDescription = !string.IsNullOrWhiteSpace(entry.DeclaringType) && !string.IsNullOrWhiteSpace(entry.FieldName)
                ? $"manifest field '{entry.DeclaringType}.{entry.FieldName}'"
                : "manifest entry";

            var parts = new List<string> {
                sourceDescription,
                $"base '{GetTypeDisplayName(baseConstraint)}'",
                $"AllowedTypeKinds={FormatAllowedTypeKinds(entry.AllowedTypeKinds)}",
            };

            if (explicitTypeList.Length > 0)
                parts.Add($"ExplicitTypeList=[{FormatTypeList(explicitTypeList)}]");

            if (excludedTypes.Length > 0)
                parts.Add($"ExcludedTypes=[{FormatTypeList(excludedTypes)}]");

            if (inheritsOrImplementsAll.Length > 0)
                parts.Add($"InheritsOrImplementsAll=[{FormatTypeList(inheritsOrImplementsAll)}]");

            if (inheritsOrImplementsAny.Length > 0)
                parts.Add($"InheritsOrImplementsAny=[{FormatTypeList(inheritsOrImplementsAny)}]");

            if (inheritsOrImplementsNone.Length > 0)
                parts.Add($"InheritsOrImplementsNone=[{FormatTypeList(inheritsOrImplementsNone)}]");

            if (entry.AllowGenericTypeConstruction)
                parts.Add("AllowGenericTypeConstruction=true");

            if (entry.AllowOpenGenerics)
                parts.Add("AllowOpenGenerics=true");

            return string.Join(", ", parts);
        }

        static string FormatTypeList(Type[] types) {
            return string.Join(", ", types.Select(GetTypeDisplayName));
        }

        static string FormatAllowedTypeKinds(int allowedTypeKinds) {
            if (allowedTypeKinds == 0)
                return "None";

            if ((allowedTypeKinds & TYPE_KIND_ALL) == TYPE_KIND_ALL && (allowedTypeKinds & ~TYPE_KIND_ALL) == 0)
                return "All";

            if (allowedTypeKinds == TYPE_KIND_OBJECT)
                return "Object";

            var names = new List<string>();
            AddKindName(names, allowedTypeKinds, TYPE_KIND_CLASS, "Class");
            AddKindName(names, allowedTypeKinds, TYPE_KIND_STRUCT, "Struct");
            AddKindName(names, allowedTypeKinds, TYPE_KIND_ABSTRACT, "Abstract");
            AddKindName(names, allowedTypeKinds, TYPE_KIND_INTERFACE, "Interface");
            AddKindName(names, allowedTypeKinds, TYPE_KIND_STATIC, "Static");
            AddKindName(names, allowedTypeKinds, TYPE_KIND_ENUM, "Enum");
            AddKindName(names, allowedTypeKinds, TYPE_KIND_DELEGATE, "Delegate");
            AddKindName(names, allowedTypeKinds, TYPE_KIND_PRIMITIVE, "Primitive");

            var unknownFlags = allowedTypeKinds & ~TYPE_KIND_ALL;
            if (unknownFlags != 0)
                names.Add(unknownFlags.ToString());

            return names.Count == 0
                ? allowedTypeKinds.ToString()
                : string.Join("|", names);
        }

        static void AddKindName(List<string> names, int allowedTypeKinds, int flag, string name) {
            if ((allowedTypeKinds & flag) != 0)
                names.Add(name);
        }

        static string GetTypeDisplayName(Type type) {
            return type.FullName ?? type.Name;
        }

        readonly struct ResolvedConstraintTypeList {
            public ResolvedConstraintTypeList(Type[] resolvedTypes, bool hasRequestedTypes, bool hasUnresolvedTypes) {
                ResolvedTypes = resolvedTypes;
                HasRequestedTypes = hasRequestedTypes;
                HasUnresolvedTypes = hasUnresolvedTypes;
            }

            public Type[] ResolvedTypes { get; }
            public bool HasRequestedTypes { get; }
            public bool HasUnresolvedTypes { get; }
        }

        readonly struct ManifestConstraint {
            public ManifestConstraint(
                Type baseConstraint,
                int allowedTypeKinds,
                bool allowGenericTypeConstruction,
                bool allowOpenGenerics,
                Type[] explicitTypeList,
                Type[] excludedTypes,
                Type[] inheritsOrImplementsAll,
                Type[] inheritsOrImplementsAny,
                Type[] inheritsOrImplementsNone,
                string reason) {
                BaseConstraint = baseConstraint;
                AllowedTypeKinds = allowedTypeKinds;
                AllowGenericTypeConstruction = allowGenericTypeConstruction;
                AllowOpenGenerics = allowOpenGenerics;
                ExplicitTypeList = explicitTypeList;
                ExcludedTypes = excludedTypes;
                InheritsOrImplementsAll = inheritsOrImplementsAll;
                InheritsOrImplementsAny = inheritsOrImplementsAny;
                InheritsOrImplementsNone = inheritsOrImplementsNone;
                Reason = reason;
            }

            public Type BaseConstraint { get; }
            public int AllowedTypeKinds { get; }
            public bool AllowGenericTypeConstruction { get; }
            public bool AllowOpenGenerics { get; }
            public Type[] ExplicitTypeList { get; }
            public Type[] ExcludedTypes { get; }
            public Type[] InheritsOrImplementsAll { get; }
            public Type[] InheritsOrImplementsAny { get; }
            public Type[] InheritsOrImplementsNone { get; }
            public string Reason { get; }
        }
    }
}
#endif
