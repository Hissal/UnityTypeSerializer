using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace Hissal.UnityTypeSerializer {
    internal enum SerializedTypeTreeFixMode {
        MatchTypeId,
        MatchAqn
    }
    
    internal readonly struct SerializedTypeTreeMismatch {
        public string Path { get; init; }
        public string WarningMessage { get; init; }
        public string MatchTypeIdPreview { get; init; }
        public string MatchTypeIdImpact { get; init; }
        public bool CanMatchTypeId { get; init; }
        public string MatchAqnPreview { get; init; }
        public string MatchAqnImpact { get; init; }
        public bool CanMatchAqn { get; init; }
    }

    [Serializable]
    internal sealed class SerializedTypeTreeNode {
        [SerializeField] string typeId = string.Empty;
        [SerializeField] string aqn = string.Empty;
        [SerializeReference] SerializedTypeTreeNode[]? genericArguments;

        public string TypeId {
            get => typeId;
            set => typeId = value;
        }

        public string Aqn {
            get => aqn;
            set => aqn = value;
        }

        public SerializedTypeTreeNode[] GenericArguments {
            get => genericArguments ?? Array.Empty<SerializedTypeTreeNode>();
            set => genericArguments = value;
        }
    }

    internal static class SerializedTypeTreeUtility {
        public static SerializedTypeTreeMismatch[] GetMismatches(SerializedTypeTreeNode? root) {
            if (root == null)
                return Array.Empty<SerializedTypeTreeMismatch>();

            var result = new System.Collections.Generic.List<SerializedTypeTreeMismatch>();
            CollectMismatchesRecursive(root, root, "root", result);
            return result.ToArray();
        }

        public static bool TryApplyMismatchFix(
            SerializedTypeTreeNode? root,
            string path,
            SerializedTypeTreeFixMode mode,
            out string impactMessage) {

            impactMessage = string.Empty;
            if (root == null)
                return false;

            if (!TryGetNodeByPath(root, path, out var node))
                return false;

            Type? resolvedFromId = null;
            if (!string.IsNullOrEmpty(node.TypeId)) {
                SerializedTypeIdRegistry.TryResolveType(node.TypeId, out resolvedFromId);
            }

            var resolvedFromAqn = !string.IsNullOrEmpty(node.Aqn) ? Type.GetType(node.Aqn) : null;

            Type? targetType;
            switch (mode) {
                case SerializedTypeTreeFixMode.MatchTypeId:
                    targetType = resolvedFromId ?? resolvedFromAqn;
                    break;
                case SerializedTypeTreeFixMode.MatchAqn:
                    targetType = resolvedFromAqn;
                    break;
                default:
                    targetType = null;
                    break;
            }

            if (targetType == null)
                return false;

            node.Aqn = targetType.AssemblyQualifiedName ?? string.Empty;
            node.TypeId = TryGetTypeIdForTypeOrGenericDefinition(targetType, out var matchedTypeId)
                ? matchedTypeId
                : string.Empty;

            var currentChildren = node.GenericArguments;
            int requiredChildren = GetRequiredGenericArgumentCount(targetType);
            impactMessage = BuildGenericImpactMessage(currentChildren.Length, requiredChildren);

            if (requiredChildren != currentChildren.Length) {
                var resized = new SerializedTypeTreeNode[requiredChildren];
                int copyCount = Math.Min(requiredChildren, currentChildren.Length);
                for (int i = 0; i < copyCount; i++) {
                    resized[i] = currentChildren[i];
                }

                for (int i = copyCount; i < requiredChildren; i++) {
                    resized[i] = new SerializedTypeTreeNode();
                }

                node.GenericArguments = resized;
            }

            return true;
        }

        public static SerializedTypeTreeNode? BuildTypeTree(Type? type) {
            if (type == null)
                return null;

            SerializedTypeIdRegistry.TryGetTypeId(type, out var serializedTypeId);
            var node = new SerializedTypeTreeNode {
                TypeId = serializedTypeId,
                Aqn = type.AssemblyQualifiedName ?? string.Empty
            };

            if (type.IsGenericType && !type.IsGenericTypeDefinition) {
                var args = type.GetGenericArguments();
                var children = new SerializedTypeTreeNode[args.Length];
                for (int i = 0; i < args.Length; i++) {
                    var childNode = BuildTypeTree(args[i]);
                    if (childNode == null)
                        return node;
                    children[i] = childNode;
                }

                node.GenericArguments = children;
            }

            return node;
        }

        public static bool TryResolveTypeTree(SerializedTypeTreeNode? node, out Type? type) {
            type = null;
            if (node == null)
                return false;

            if (!TryResolveNodeType(node, out var resolvedRoot) || resolvedRoot == null)
                return false;

            var children = node.GenericArguments;
            if (children.Length == 0) {
                type = resolvedRoot;
                return true;
            }

            var genericDefinition = resolvedRoot.IsGenericTypeDefinition
                ? resolvedRoot
                : resolvedRoot.GetGenericTypeDefinition();

            var genericParameters = genericDefinition.GetGenericArguments();
            if (genericParameters.Length != children.Length)
                return false;

            var resolvedArgs = new Type[children.Length];
            for (int i = 0; i < children.Length; i++) {
                if (!TryResolveTypeTree(children[i], out var resolvedArg) || resolvedArg == null)
                    return false;

                resolvedArgs[i] = resolvedArg;
            }

            try {
                type = genericDefinition.MakeGenericType(resolvedArgs);
                return true;
            }
            catch {
                return false;
            }
        }

        static bool TryResolveNodeType(SerializedTypeTreeNode node, out Type? type) {
            if (!string.IsNullOrEmpty(node.TypeId)
                && SerializedTypeIdRegistry.TryResolveType(node.TypeId, out var typeFromId)
                && typeFromId != null) {
                type = typeFromId;
                return true;
            }

            if (!string.IsNullOrEmpty(node.Aqn)) {
                type = Type.GetType(node.Aqn);
                return type != null;
            }

            type = null;
            return false;
        }

        static void CollectMismatchesRecursive(
            SerializedTypeTreeNode root,
            SerializedTypeTreeNode node,
            string path,
            System.Collections.Generic.List<SerializedTypeTreeMismatch> result) {

            Type? resolvedFromId = null;
            if (!string.IsNullOrEmpty(node.TypeId)) {
                SerializedTypeIdRegistry.TryResolveType(node.TypeId, out resolvedFromId);
            }

            var resolvedFromAqn = !string.IsNullOrEmpty(node.Aqn) ? Type.GetType(node.Aqn) : null;

            bool mismatch = false;
            if (resolvedFromId != null && resolvedFromAqn != null) {
                mismatch = !IsSameTypeRoot(resolvedFromId, resolvedFromAqn);
            }
            else if (string.IsNullOrEmpty(node.TypeId) && resolvedFromAqn != null && TryGetTypeIdForTypeOrGenericDefinition(resolvedFromAqn, out _)) {
                mismatch = true;
            }
            else if (!string.IsNullOrEmpty(node.TypeId) && resolvedFromId == null) {
                mismatch = true;
            }

            if (mismatch) {
                var matchTypeIdTarget = resolvedFromId ?? resolvedFromAqn;
                var matchAqnTarget = resolvedFromAqn;

                int currentChildCount = node.GenericArguments.Length;

                result.Add(new SerializedTypeTreeMismatch {
                    Path = path,
                    WarningMessage = BuildNodeWarning(path, resolvedFromId, resolvedFromAqn, node.TypeId, node.Aqn),
                    MatchTypeIdPreview = matchTypeIdTarget != null
                        ? BuildTreePreviewAfterFix(root, path, SerializedTypeTreeFixMode.MatchTypeId)
                        : "Unavailable",
                    MatchTypeIdImpact = matchTypeIdTarget != null
                        ? BuildGenericImpactMessage(currentChildCount, GetRequiredGenericArgumentCount(matchTypeIdTarget))
                        : "Cannot apply: TypeId and AQN cannot be resolved.",
                    CanMatchTypeId = matchTypeIdTarget != null,
                    MatchAqnPreview = matchAqnTarget != null
                        ? BuildTreePreviewAfterFix(root, path, SerializedTypeTreeFixMode.MatchAqn)
                        : "Unavailable",
                    MatchAqnImpact = matchAqnTarget != null
                        ? BuildGenericImpactMessage(currentChildCount, GetRequiredGenericArgumentCount(matchAqnTarget))
                        : "Cannot apply: AQN cannot be resolved.",
                    CanMatchAqn = matchAqnTarget != null
                });
            }

            var children = node.GenericArguments;
            for (int i = 0; i < children.Length; i++) {
                CollectMismatchesRecursive(root, children[i], $"{path}/{i}", result);
            }
        }

        static string BuildTreePreviewAfterFix(SerializedTypeTreeNode root, string path, SerializedTypeTreeFixMode mode) {
            var clone = CloneTree(root);
            if (!TryApplyMismatchFix(clone, path, mode, out _))
                return "Unavailable";

            return BuildTreeDisplay(clone);
        }

        static SerializedTypeTreeNode CloneTree(SerializedTypeTreeNode source) {
            var children = source.GenericArguments;
            var clonedChildren = new SerializedTypeTreeNode[children.Length];
            for (int i = 0; i < children.Length; i++) {
                clonedChildren[i] = CloneTree(children[i]);
            }

            return new SerializedTypeTreeNode {
                TypeId = source.TypeId,
                Aqn = source.Aqn,
                GenericArguments = clonedChildren
            };
        }

        static string BuildTreeDisplay(SerializedTypeTreeNode node) {
            string baseName;
            if (TryResolveNodeType(node, out var resolvedType) && resolvedType != null) {
                baseName = GetBaseTypeName(resolvedType);
            }
            else {
                baseName = GetFallbackNodeName(node);
            }

            var children = node.GenericArguments;
            if (children.Length == 0)
                return baseName;

            var childDisplays = new string[children.Length];
            for (int i = 0; i < children.Length; i++) {
                childDisplays[i] = BuildTreeDisplay(children[i]);
            }

            return $"{baseName}<{string.Join(", ", childDisplays)}>";
        }

        static string GetBaseTypeName(Type type) {
            var target = type.IsGenericType && !type.IsGenericTypeDefinition
                ? type.GetGenericTypeDefinition()
                : type;

            return target.IsGenericType
                ? target.Name.Split('`')[0]
                : target.Name;
        }

        static string GetFallbackNodeName(SerializedTypeTreeNode node) {
            if (!string.IsNullOrEmpty(node.Aqn)) {
                var typeName = node.Aqn.Split(',')[0].Trim();
                if (!string.IsNullOrEmpty(typeName)) {
                    var lastDotIndex = typeName.LastIndexOf('.');
                    return lastDotIndex >= 0 ? typeName.Substring(lastDotIndex + 1) : typeName;
                }
            }

            if (!string.IsNullOrEmpty(node.TypeId))
                return $"id:{node.TypeId}";

            return "?";
        }

        static bool TryGetNodeByPath(SerializedTypeTreeNode root, string path, out SerializedTypeTreeNode node) {
            node = root;
            if (string.IsNullOrEmpty(path) || path == "root")
                return true;

            var parts = path.Split('/');
            int start = parts.Length > 0 && parts[0] == "root" ? 1 : 0;

            for (int i = start; i < parts.Length; i++) {
                if (!int.TryParse(parts[i], out var childIndex))
                    return false;

                var children = node.GenericArguments;
                if (childIndex < 0 || childIndex >= children.Length)
                    return false;

                node = children[childIndex];
            }

            return true;
        }

        static bool TryGetTypeIdForTypeOrGenericDefinition(Type type, out string serializedTypeId) {
            if (SerializedTypeIdRegistry.TryGetTypeId(type, out serializedTypeId))
                return true;

            if (type.IsGenericType && !type.IsGenericTypeDefinition) {
                return SerializedTypeIdRegistry.TryGetTypeId(type.GetGenericTypeDefinition(), out serializedTypeId);
            }

            serializedTypeId = string.Empty;
            return false;
        }

        static bool IsSameTypeRoot(Type left, Type right) {
            var leftRoot = left.IsGenericType && !left.IsGenericTypeDefinition ? left.GetGenericTypeDefinition() : left;
            var rightRoot = right.IsGenericType && !right.IsGenericTypeDefinition ? right.GetGenericTypeDefinition() : right;
            return leftRoot == rightRoot;
        }

        static int GetRequiredGenericArgumentCount(Type type) {
            if (type.IsGenericTypeDefinition)
                return type.GetGenericArguments().Length;

            if (type.IsGenericType)
                return type.GetGenericArguments().Length;

            return 0;
        }

        static string GetTypeDisplayName(Type type) {
            if (!type.IsGenericType)
                return type.Name;

            var genericArgs = type.GetGenericArguments();
            var baseName = type.Name.Split('`')[0];
            var argNames = string.Join(", ", Array.ConvertAll(genericArgs, GetTypeDisplayName));
            return $"{baseName}<{argNames}>";
        }

        static string BuildNodeWarning(
            string path,
            Type? typeFromId,
            Type? typeFromAqn,
            string serializedTypeId,
            string serializedAqn) {

            if (typeFromId != null && typeFromAqn != null && !IsSameTypeRoot(typeFromId, typeFromAqn)) {
                return $"[{path}] TypeId resolves to '{GetTypeDisplayName(typeFromId)}' but AQN resolves to '{GetTypeDisplayName(typeFromAqn)}'.";
            }

            if (string.IsNullOrEmpty(serializedTypeId) && typeFromAqn != null && TryGetTypeIdForTypeOrGenericDefinition(typeFromAqn, out _)) {
                return $"[{path}] AQN resolves to '{GetTypeDisplayName(typeFromAqn)}' but TypeId is missing.";
            }

            if (!string.IsNullOrEmpty(serializedTypeId) && typeFromId == null) {
                return $"[{path}] TypeId '{serializedTypeId}' is invalid or no longer registered.";
            }

            if (!string.IsNullOrEmpty(serializedAqn) && typeFromAqn == null) {
                return $"[{path}] AQN reference is invalid and cannot be resolved.";
            }

            return $"[{path}] Type node values are unsynced.";
        }

        static string BuildGenericImpactMessage(int currentCount, int requiredCount) {
            if (requiredCount == currentCount)
                return "Generic argument shape is unchanged.";

            if (requiredCount < currentCount) {
                int removed = currentCount - requiredCount;
                return $"This will remove {removed} generic argument node(s) from this branch.";
            }

            int added = requiredCount - currentCount;
            return $"This will add {added} unresolved generic argument slot(s); complete them before final resolution.";
        }

        public static bool TryResolveLegacy(string serializedTypeId, string serializedAqn, out Type? type) {
            if (!string.IsNullOrEmpty(serializedTypeId)
                && SerializedTypeIdRegistry.TryResolveType(serializedTypeId, out var resolvedFromId)
                && resolvedFromId != null) {
                type = resolvedFromId;
                return true;
            }

            if (!string.IsNullOrEmpty(serializedAqn)) {
                type = Type.GetType(serializedAqn);
                return type != null;
            }

            type = null;
            return false;
        }
    }
    
    /// <summary>
    /// Represents a fully serializable, inspector-constructible representation of a type that derives from a specified base class or interface.
    /// By default, only concrete, non-generic types are included. Abstract types, interfaces, and generic type definitions are excluded.
    /// This behavior can be customized via the <see cref="SerializedTypeOptionsAttribute"/>.
    /// </summary>
    /// <typeparam name="TBase">
    /// The base class or interface that the serialized type must derive from or implement.
    /// </typeparam>
    [Serializable]
    public sealed class SerializedType<TBase> where TBase : class {
        /// <summary>
        /// Stores the assembly-qualified name of the serialized type.
        /// </summary>
        [SerializeField, HideInInspector] 
        string aqn = string.Empty;

        /// <summary>
        /// Stores the stable serialized type identifier.
        /// </summary>
        [SerializeField, HideInInspector]
        string typeId = string.Empty;

        /// <summary>
        /// Stores the full serialized type tree (root + nested generic arguments).
        /// </summary>
        [SerializeField, HideInInspector]
        SerializedTypeTreeNode? typeTree;

        /// <summary>
        /// Gets the raw serialized assembly-qualified name.
        /// Exposed for editor-time diagnostics and validation.
        /// </summary>
        internal string SerializedAqn => aqn;

        /// <summary>
        /// Gets the raw serialized type identifier.
        /// Exposed for editor-time diagnostics and validation.
        /// </summary>
        internal string SerializedTypeId => typeId;

        /// <summary>
        /// Cached type instance to avoid repeated Type.GetType calls.
        /// </summary>
        [NonSerialized]
        Type? cachedType;

        /// <summary>
        /// Indicates whether a valid type is currently set.
        /// </summary>
        /// <remarks>
        /// This property is obsolete. Use <see cref="IsValid"/> instead.
        /// </remarks>
        [Obsolete("Use IsValid instead.")]
        [MemberNotNullWhen(true, nameof(Type))]
        public bool HasType => Type != null;
        
        /// <summary>
        /// Indicates whether a valid type is currently set.
        /// </summary>
        /// <remarks>
        /// This property uses the <see cref="Type"/> property to determine if a type is set.
        /// </remarks>
        [MemberNotNullWhen(true, nameof(Type))]
        public bool IsValid => Type != null;
        
        /// <summary>
        /// Gets the serialized type based on the stored assembly-qualified name.
        /// </summary>
        /// <remarks>
        /// Returns <c>null</c> if the assembly-qualified name is empty or invalid.
        /// The type is looked up each time and cached.
        /// </remarks>
        public Type? Type {
            get {
                if (cachedType != null) return cachedType;
                if (SerializedTypeTreeUtility.TryResolveTypeTree(typeTree, out var resolvedFromTree)
                    && resolvedFromTree != null) {
                    cachedType = resolvedFromTree;
                    return cachedType;
                }

                if (!string.IsNullOrEmpty(typeId)
                    && SerializedTypeIdRegistry.TryResolveType(typeId, out var resolvedFromId)
                    && resolvedFromId != null) {
                    cachedType = resolvedFromId;
                    return cachedType;
                }

                if (string.IsNullOrEmpty(aqn)) return null;

                cachedType = Type.GetType(aqn);

                return cachedType;
            }
            internal set {
                cachedType = null;
                aqn = value?.AssemblyQualifiedName ?? string.Empty;
                typeId = SerializedTypeIdRegistry.TryGetTypeId(value, out var resolvedTypeId)
                    ? resolvedTypeId
                    : string.Empty;
                typeTree = SerializedTypeTreeUtility.BuildTypeTree(value);
            }
        }

        internal void SetSerializedValues(string serializedTypeId, string serializedAqn) {
            cachedType = null;
            typeId = serializedTypeId;
            aqn = serializedAqn;
            typeTree = SerializedTypeTreeUtility.TryResolveLegacy(serializedTypeId, serializedAqn, out var resolvedType)
                ? SerializedTypeTreeUtility.BuildTypeTree(resolvedType)
                : null;
        }

        internal SerializedTypeTreeMismatch[] GetTypeTreeMismatches() {
            return SerializedTypeTreeUtility.GetMismatches(typeTree);
        }

        internal bool TryApplyTypeTreeMismatchFix(string path, SerializedTypeTreeFixMode mode, out string impactMessage) {
            if (!SerializedTypeTreeUtility.TryApplyMismatchFix(typeTree, path, mode, out impactMessage))
                return false;

            cachedType = null;
            if (SerializedTypeTreeUtility.TryResolveTypeTree(typeTree, out var resolvedType) && resolvedType != null) {
                aqn = resolvedType.AssemblyQualifiedName ?? string.Empty;
                typeId = SerializedTypeIdRegistry.TryGetTypeId(resolvedType, out var resolvedTypeId)
                    ? resolvedTypeId
                    : string.Empty;
            }
            else {
                aqn = typeTree?.Aqn ?? aqn;
                typeId = typeTree?.TypeId ?? typeId;
            }

            return true;
        }
    }
    
    /// <summary>
    /// Non-generic version of <see cref="SerializedType{TBase}"/> that accepts any type.
    /// This is a convenience type that behaves identically to <see cref="SerializedType{TBase}"/> where TBase is <see cref="object"/>.
    /// Use <see cref="SerializedTypeOptionsAttribute"/> to control which types are selectable via include/exclude filters.
    /// </summary>
    /// <remarks>
    /// This type provides the same serialization format and runtime API as <see cref="SerializedType{TBase}"/>,
    /// but without generic type constraints. All types in the project are selectable unless restricted
    /// by <see cref="SerializedTypeOptionsAttribute"/> filtering options.
    /// </remarks>
    /// <seealso cref="SerializedType{TBase}"/>
    /// <seealso cref="SerializedTypeOptionsAttribute"/>
    [Serializable]
    public sealed class SerializedType {
        /// <summary>
        /// Stores the assembly-qualified name of the serialized type.
        /// </summary>
        [SerializeField, HideInInspector] 
        string aqn = string.Empty;

        /// <summary>
        /// Stores the stable serialized type identifier.
        /// </summary>
        [SerializeField, HideInInspector]
        string typeId = string.Empty;

        /// <summary>
        /// Stores the full serialized type tree (root + nested generic arguments).
        /// </summary>
        [SerializeField, HideInInspector]
        SerializedTypeTreeNode? typeTree;

        /// <summary>
        /// Gets the raw serialized assembly-qualified name.
        /// Exposed for editor-time diagnostics and validation.
        /// </summary>
        internal string SerializedAqn => aqn;

        /// <summary>
        /// Gets the raw serialized type identifier.
        /// Exposed for editor-time diagnostics and validation.
        /// </summary>
        internal string SerializedTypeId => typeId;

        /// <summary>
        /// Cached type instance to avoid repeated Type.GetType calls.
        /// </summary>
        [NonSerialized]
        Type? cachedType;

        /// <summary>
        /// Indicates whether a valid type is currently set.
        /// </summary>
        /// <remarks>
        /// This property is obsolete. Use <see cref="IsValid"/> instead.
        /// </remarks>
        [Obsolete("Use IsValid instead.")]
        [MemberNotNullWhen(true, nameof(Type))]
        public bool HasType => Type != null;
        
        /// <summary>
        /// Indicates whether a valid type is currently set.
        /// </summary>
        /// <remarks>
        /// This property uses the <see cref="Type"/> property to determine if a type is set.
        /// </remarks>
        [MemberNotNullWhen(true, nameof(Type))]
        public bool IsValid => Type != null;
        
        /// <summary>
        /// Gets the serialized type based on the stored assembly-qualified name.
        /// </summary>
        /// <remarks>
        /// Returns <c>null</c> if the assembly-qualified name is empty or invalid.
        /// The type is looked up each time and cached.
        /// </remarks>
        public Type? Type {
            get {
                if (cachedType != null) return cachedType;
                if (SerializedTypeTreeUtility.TryResolveTypeTree(typeTree, out var resolvedFromTree)
                    && resolvedFromTree != null) {
                    cachedType = resolvedFromTree;
                    return cachedType;
                }

                if (!string.IsNullOrEmpty(typeId)
                    && SerializedTypeIdRegistry.TryResolveType(typeId, out var resolvedFromId)
                    && resolvedFromId != null) {
                    cachedType = resolvedFromId;
                    return cachedType;
                }

                if (string.IsNullOrEmpty(aqn)) return null;

                cachedType = Type.GetType(aqn);

                return cachedType;
            }
            internal set {
                cachedType = null;
                aqn = value?.AssemblyQualifiedName ?? string.Empty;
                typeId = SerializedTypeIdRegistry.TryGetTypeId(value, out var resolvedTypeId)
                    ? resolvedTypeId
                    : string.Empty;
                typeTree = SerializedTypeTreeUtility.BuildTypeTree(value);
            }
        }

        internal void SetSerializedValues(string serializedTypeId, string serializedAqn) {
            cachedType = null;
            typeId = serializedTypeId;
            aqn = serializedAqn;
            typeTree = SerializedTypeTreeUtility.TryResolveLegacy(serializedTypeId, serializedAqn, out var resolvedType)
                ? SerializedTypeTreeUtility.BuildTypeTree(resolvedType)
                : null;
        }

        internal SerializedTypeTreeMismatch[] GetTypeTreeMismatches() {
            return SerializedTypeTreeUtility.GetMismatches(typeTree);
        }

        internal bool TryApplyTypeTreeMismatchFix(string path, SerializedTypeTreeFixMode mode, out string impactMessage) {
            if (!SerializedTypeTreeUtility.TryApplyMismatchFix(typeTree, path, mode, out impactMessage))
                return false;

            cachedType = null;
            if (SerializedTypeTreeUtility.TryResolveTypeTree(typeTree, out var resolvedType) && resolvedType != null) {
                aqn = resolvedType.AssemblyQualifiedName ?? string.Empty;
                typeId = SerializedTypeIdRegistry.TryGetTypeId(resolvedType, out var resolvedTypeId)
                    ? resolvedTypeId
                    : string.Empty;
            }
            else {
                aqn = typeTree?.Aqn ?? aqn;
                typeId = typeTree?.TypeId ?? typeId;
            }

            return true;
        }
    }
}