using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Hissal.UnityTypeSerializer.Editor {
    /// <summary>
    /// Shared core logic for SerializedType drawers.
    /// Provides type list generation, member resolution, and drawer creation
    /// shared between generic and non-generic SerializedType Odin drawers.
    /// </summary>
    internal static class SerializedTypeDrawerCore {
        /// <summary>
        /// Creates the appropriate drawer implementation based on options.
        /// </summary>
        public static ISerializedTypeDrawerImplementation CreateDrawerImplementation(
            InspectorProperty property,
            ISerializedTypeValueAccessor accessor,
            SerializedTypeOptionsAttribute? options,
            List<Type> availableTypes) {
            
            bool useComplexConstructor = options?.UseComplexConstructor ?? false;
            
            if (useComplexConstructor) {
                return new ComplexConstructorSerializedTypeDrawer(
                    property,
                    accessor,
                    options,
                    availableTypes
                );
            }
            
            return new InlineSerializedTypeDrawer(
                property,
                accessor,
                options,
                availableTypes
            );
        }
        
        /// <summary>
        /// Builds the available types list for a SerializedType property.
        /// Uses the accessor's base constraint and the options' <see cref="SerializedTypeFilter"/> for filtering.
        /// </summary>
        public static List<Type> RefreshAvailableTypes(
            Type baseConstraint,
            SerializedTypeOptionsAttribute? options,
            InspectorProperty property) {
            
            bool allowGenericTypeConstruction = options?.AllowGenericTypeConstruction ?? false;
            bool allowOpenGenerics = options?.AllowOpenGenerics ?? false;
            
            // If either option is true, we need to show generic type definitions in the dropdown
            bool includeGenericTypeDefinitions = allowGenericTypeConstruction || allowOpenGenerics;
            
            // Resolve custom filter from string-based resolver
            var filter = ResolveSerializedTypeFilter(options?.CustomTypeFilter, property);
            var includedTypes = filter.HasValue
                ? GetFilteredTypes(filter.Value.IncludeTypes, filter.Value.IncludeResolver, property)?.ToHashSet()
                : null;
            
            // Resolve excluded types from unified filter
            var excludedTypes = filter.HasValue
                ? GetFilteredTypes(filter.Value.ExcludeTypes, filter.Value.ExcludeResolver, property)?.ToHashSet()
                : null;
            
            IEnumerable<Type> typesToFilter = (includedTypes != null && includedTypes.Count > 0)
                ? includedTypes.Where(t => baseConstraint.IsAssignableFrom(t))
                : TypeCache.GetTypesDerivedFrom(baseConstraint);
            typesToFilter = typesToFilter.Where(t => PassesInheritanceConstraints(t, options));
            
            if (excludedTypes != null && excludedTypes.Count > 0) {
                typesToFilter = typesToFilter.Where(t => !excludedTypes.Contains(t));
            }
            
            // Get allowed type kinds from options (default to Concrete only)
            var allowedKinds = options?.AllowedTypeKinds ?? SerializedTypeKind.Concrete;

            return typesToFilter
                .Where(t => {
                    // Check type kind filtering
                    bool isInterface = t.IsInterface;
                    bool isAbstractClass = t.IsAbstract && !isInterface;
                    bool isConcreteClass = !t.IsAbstract && !isInterface;

                    bool passesTypeKindFilter =
                        (isConcreteClass && allowedKinds.HasFlag(SerializedTypeKind.Concrete)) ||
                        (isAbstractClass && allowedKinds.HasFlag(SerializedTypeKind.Abstract)) ||
                        (isInterface && allowedKinds.HasFlag(SerializedTypeKind.Interface));

                    if (!passesTypeKindFilter)
                        return false;

                    // Check generic type definition (e.g., List<>)
                    if (t.IsGenericTypeDefinition)
                        return includeGenericTypeDefinitions;

                    // Regular non-generic types are always included
                    return true;
                })
                .OrderBy(t => SerializedTypeDrawerUtilities.GetTypeName(t))
                .ToList();
        }

        static bool PassesInheritanceConstraints(Type candidateType, SerializedTypeOptionsAttribute? options) {
            if (options == null)
                return true;
            
            var inheritsAll = options.InheritsOrImplementsAll;
            var inheritsAny = options.InheritsOrImplementsAny;
            bool hasAll = inheritsAll != null && inheritsAll.Length > 0;
            bool hasAny = inheritsAny != null && inheritsAny.Length > 0;
            
            if (!hasAll && !hasAny)
                return true;
            
            if (hasAll && !inheritsAll!.All(constraint => constraint.IsAssignableFrom(candidateType)))
                return false;
            
            if (hasAny && !inheritsAny!.Any(constraint => constraint.IsAssignableFrom(candidateType)))
                return false;
            
            return true;
        }
        
        /// <summary>
        /// Resolves a string-based member name to a <see cref="SerializedTypeFilter"/>.
        /// The member may return either a <see cref="SerializedTypeFilter"/> or an <see cref="IEnumerable{Type}"/>.
        /// Supports <c>"MemberName"</c> (on the declaring/context type) or <c>"TypeName.MemberName"</c> (explicit type).
        /// </summary>
        /// <param name="resolverName">Name of a static or instance member (method, property, or field).</param>
        /// <param name="property">The inspector property used to resolve the declaring type and instance.</param>
        /// <returns>The resolved <see cref="SerializedTypeFilter"/>, or null if resolution fails or <paramref name="resolverName"/> is empty.</returns>
        internal static SerializedTypeFilter? ResolveSerializedTypeFilter(
            string? resolverName,
            InspectorProperty property) {
            
            if (string.IsNullOrEmpty(resolverName))
                return null;
            
            try {
                Type? targetType = null;
                string? targetMemberName = null;
                object? instance = null;
                
                // Parse format: "TypeName.MemberName" or "MemberName"
                var parts = resolverName!.Split('.');
                if (parts.Length == 1) {
                    // Just member name - search in declaring type (may be instance or static)
                    targetType = GetDeclaringType(property);
                    targetMemberName = parts[0];
                    instance = GetDeclaringInstance(property);
                }
                else if (parts.Length == 2) {
                    // "TypeName.MemberName" format (must be static)
                    var typeName = parts[0];
                    targetMemberName = parts[1];
                    
                    targetType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => {
                            try { return a.GetTypes(); }
                            catch { return Array.Empty<Type>(); }
                        })
                        .FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
                    
                    instance = null;
                }
                
                if (targetType == null || targetMemberName == null) {
                    Debug.LogError($"[SerializedType] Could not resolve target type for filter member '{resolverName}'");
                    return null;
                }
                
                var value = TryGetMemberValueRaw(targetType, targetMemberName, instance);
                if (value == null) {
                    var memberType = instance != null ? "instance" : "static";
                    Debug.LogError($"[SerializedType] Could not find {memberType} member '{targetMemberName}' in type '{targetType.Name}' returning SerializedTypeFilter or IEnumerable<Type>");
                    return null;
                }
                
                // Evaluate return type
                if (value is SerializedTypeFilter filter)
                    return filter;
                
                if (value is IEnumerable<Type> types)
                    return SerializedTypeFilter.Include(types.ToArray());
                
                Debug.LogError($"[SerializedType] Member '{resolverName}' returned unsupported type '{value.GetType().Name}'. Expected SerializedTypeFilter or IEnumerable<Type>.");
                return null;
            }
            catch (Exception ex) {
                Debug.LogError($"[SerializedType] Error resolving filter from member '{resolverName}': {ex}");
                return null;
            }
        }
        
        /// <summary>
        /// Resolves types from either an array or a member resolver string.
        /// </summary>
        public static IEnumerable<Type>? GetFilteredTypes(
            Type[]? typeArray,
            string? resolverMemberName,
            InspectorProperty property) {
            
            var result = new HashSet<Type>();
            
            // Add types from array
            if (typeArray != null && typeArray.Length > 0) {
                foreach (var type in typeArray) {
                    if (type != null)
                        result.Add(type);
                }
            }
            
            // Add types from resolver
            if (!string.IsNullOrWhiteSpace(resolverMemberName)) {
                var resolvedTypes = ResolveTypesFromMember(resolverMemberName!, property);
                if (resolvedTypes != null) {
                    foreach (var type in resolvedTypes) {
                        if (type != null)
                            result.Add(type);
                    }
                }
            }
            
            return result.Count > 0 ? result : null;
        }
        
        /// <summary>
        /// Resolves IEnumerable&lt;Type&gt; from a member name (method, property, or field).
        /// Format: "TypeName.MemberName" or "MemberName" (searches in declaring type).
        /// </summary>
        public static IEnumerable<Type>? ResolveTypesFromMember(
            string memberName,
            InspectorProperty property) {
            
            try {
                Type? targetType = null;
                string? targetMemberName = null;
                object? instance = null;
                
                // Parse format: "TypeName.MemberName" or "MemberName"
                var parts = memberName.Split('.');
                if (parts.Length == 1) {
                    // Just member name - search in declaring type (instance member)
                    targetType = GetDeclaringType(property);
                    targetMemberName = parts[0];
                    instance = GetDeclaringInstance(property);
                }
                else if (parts.Length == 2) {
                    // "TypeName.MemberName" format (static member)
                    var typeName = parts[0];
                    targetMemberName = parts[1];
                    
                    // Try to find the type
                    targetType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => {
                            try { return a.GetTypes(); }
                            catch { return Array.Empty<Type>(); }
                        })
                        .FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
                    
                    // No instance for static members
                    instance = null;
                }
                
                if (targetType == null || targetMemberName == null) {
                    Debug.LogWarning($"Could not resolve target type for member '{memberName}'");
                    return null;
                }
                
                // Try to resolve the member value
                var result = TryGetMemberValue(targetType, targetMemberName, instance);
                if (result == null) {
                    var memberType = instance != null ? "instance" : "static";
                    Debug.LogWarning($"Could not find {memberType} member '{targetMemberName}' in type '{targetType.Name}' that returns IEnumerable<Type>");
                }
                
                return result;
            }
            catch (Exception ex) {
                Debug.LogError($"Error resolving types from member '{memberName}': {ex}");
                return null;
            }
        }
        
        /// <summary>
        /// Gets the type that declares the field (the parent object containing the SerializedType field).
        /// </summary>
        static Type? GetDeclaringType(InspectorProperty property) {
            // Try to get the parent value's type (the class containing the field)
            var parent = property.Parent;
            if (parent?.ValueEntry != null) {
                var parentValue = parent.ValueEntry.WeakSmartValue;
                if (parentValue != null) {
                    return parentValue.GetType();
                }
            }
            
            // Fallback: try to get from property info
            var memberInfo = property.Info.GetMemberInfo();
            if (memberInfo is not null) {
                return memberInfo.DeclaringType;
            }
            
            return null;
        }
        
        /// <summary>
        /// Gets the parent instance that contains the SerializedType field.
        /// </summary>
        static object? GetDeclaringInstance(InspectorProperty property) {
            var parent = property.Parent;
            if (parent?.ValueEntry != null) {
                return parent.ValueEntry.WeakSmartValue;
            }
            
            return null;
        }
        
        /// <summary>
        /// Tries to get a value from a member (property, method, or field).
        /// Supports both static and instance members.
        /// </summary>
        static IEnumerable<Type>? TryGetMemberValue(Type targetType, string memberName, object? instance = null) {
            var raw = TryGetMemberValueRaw(targetType, memberName, instance);
            return raw as IEnumerable<Type>;
        }
        
        /// <summary>
        /// Tries to get the raw value from a member (property, method, or field).
        /// Supports both static and instance members. Returns the raw object for type evaluation by callers.
        /// When an instance is provided, searches both instance and static members on the declaring type.
        /// </summary>
        static object? TryGetMemberValueRaw(Type targetType, string memberName, object? instance = null) {
            const BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            const BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            
            // When instance is provided, search both instance and static members
            // When instance is null, only search static members
            var flagSets = instance != null
                ? new[] { instanceFlags, staticFlags }
                : new[] { staticFlags };
            
            foreach (var flags in flagSets) {
                var isStatic = (flags & BindingFlags.Static) != 0;
                var invokeInstance = isStatic ? null : instance;
                
                // Try property
                var prop = targetType.GetProperty(memberName, flags);
                if (prop != null && prop.CanRead) {
                    var value = prop.GetValue(invokeInstance);
                    if (value is SerializedTypeFilter or IEnumerable<Type>)
                        return value;
                }
                
                // Try method
                var method = targetType.GetMethod(memberName, flags, null, Type.EmptyTypes, null);
                if (method != null) {
                    var value = method.Invoke(invokeInstance, null);
                    if (value is SerializedTypeFilter or IEnumerable<Type>)
                        return value;
                }
                
                // Try field
                var field = targetType.GetField(memberName, flags);
                if (field != null) {
                    var value = field.GetValue(invokeInstance);
                    if (value is SerializedTypeFilter or IEnumerable<Type>)
                        return value;
                }
            }
            
            return null;
        }
    }
}
