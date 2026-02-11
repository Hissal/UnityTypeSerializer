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
        /// Uses the accessor's base constraint and the options' include/exclude filters.
        /// </summary>
        public static List<Type> RefreshAvailableTypes(
            Type baseConstraint,
            SerializedTypeOptionsAttribute? options,
            InspectorProperty property) {
            
            bool allowGenericTypeConstruction = options?.AllowGenericTypeConstruction ?? false;
            bool allowOpenGenerics = options?.AllowOpenGenerics ?? false;
            
            // If either option is true, we need to show generic type definitions in the dropdown
            bool includeGenericTypeDefinitions = allowGenericTypeConstruction || allowOpenGenerics;
            
            // Resolve custom filter types if any
            var customFilterTypes = GetFilteredTypes(
                options?.IncludeTypes,
                options?.IncludeTypesResolver,
                property
            )?.ToList();
            
            // Resolve excluded types if any
            var excludedTypes = GetFilteredTypes(
                options?.ExcludeTypes,
                options?.ExcludeTypesResolver,
                property
            )?.ToHashSet();
            
            IEnumerable<Type> typesToFilter = (customFilterTypes != null && customFilterTypes.Any())
                ? customFilterTypes
                : TypeCache.GetTypesDerivedFrom(baseConstraint);
            
            return typesToFilter
                .Where(t => {
                    // Always exclude abstract types and interfaces
                    if (t.IsAbstract || t.IsInterface)
                        return false;

                    // Apply exclusion filter
                    if (excludedTypes != null && excludedTypes.Contains(t))
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
            const BindingFlags staticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
            const BindingFlags instanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            
            // Determine which binding flags to use
            var flags = instance != null ? instanceFlags : staticFlags;
            
            // Try property first
            var prop = targetType.GetProperty(memberName, flags);
            if (prop != null && prop.CanRead) {
                var value = prop.GetValue(instance);
                if (value is IEnumerable<Type> enumerable)
                    return enumerable;
            }
            
            // Try method
            var method = targetType.GetMethod(memberName, flags, null, Type.EmptyTypes, null);
            if (method != null) {
                var value = method.Invoke(instance, null);
                if (value is IEnumerable<Type> enumerable)
                    return enumerable;
            }
            
            // Try field
            var field = targetType.GetField(memberName, flags);
            if (field != null) {
                var value = field.GetValue(instance);
                if (value is IEnumerable<Type> enumerable)
                    return enumerable;
            }
            
            return null;
        }
    }
}
