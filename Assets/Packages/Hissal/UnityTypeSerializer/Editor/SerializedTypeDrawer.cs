using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Hissal.UnityTypeSerializer.Editor {
    /// <summary>
    /// Custom Odin drawer for SerializedType that properly handles the SerializedTypeOptionsAttribute.
    /// Delegates to either the inline drawer or complex constructor drawer based on options.
    /// </summary>
    public sealed class SerializedTypeDrawer<TBase> : OdinValueDrawer<SerializedType<TBase>> where TBase : class {
        SerializedTypeOptionsAttribute? options;
        List<Type>? availableTypes;
        bool initialized;
        ISerializedTypeDrawerImplementation? drawerImplementation;

        protected override void Initialize() {
            base.Initialize();
            
            // Get the attribute from the property
            options = Property.GetAttribute<SerializedTypeOptionsAttribute>();
            
            // Build the available types list
            RefreshAvailableTypes();
            
            // Create the appropriate drawer implementation
            bool useComplexConstructor = options?.UseComplexConstructor ?? false;
            
            // Cast IPropertyValueEntry to PropertyValueEntry
            var propertyValueEntry = (PropertyValueEntry<SerializedType<TBase>>)ValueEntry;
            
            if (useComplexConstructor) {
                drawerImplementation = new ComplexConstructorSerializedTypeDrawer<TBase>(
                    Property,
                    propertyValueEntry,
                    options,
                    availableTypes!
                );
            } else {
                drawerImplementation = new InlineSerializedTypeDrawer<TBase>(
                    Property,
                    propertyValueEntry,
                    options,
                    availableTypes!
                );
            }
            
            initialized = true;
        }

        void RefreshAvailableTypes() {
            bool allowGenericTypeConstruction = options?.AllowGenericTypeConstruction ?? false;
            bool allowOpenGenerics = options?.AllowOpenGenerics ?? false;
            
            // If either option is true, we need to show generic type definitions in the dropdown
            bool includeGenericTypeDefinitions = allowGenericTypeConstruction || allowOpenGenerics;
            
            // Resolve custom filter types if any
            var customFilterTypes = GetFilteredTypes(
                options?.IncludeTypes,
                options?.IncludeTypesResolver
            )?.ToList();
            
            // Resolve excluded types if any
            var excludedTypes = GetFilteredTypes(
                options?.ExcludeTypes,
                options?.ExcludeTypesResolver
            )?.ToHashSet();
            
            IEnumerable<Type> typesToFilter;
            
            // If custom filter is specified, use it as base
            if (customFilterTypes != null && customFilterTypes.Any()) {
                typesToFilter = customFilterTypes;
            }
            else {
                // Otherwise, get all types derived from TBase
                typesToFilter = TypeCache.GetTypesDerivedFrom<TBase>();
            }
            
            availableTypes = typesToFilter
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
                .OrderBy(t => GetTypeName(t))
                .ToList();
        }
        
        /// <summary>
        /// Resolves types from either an array or a member resolver string.
        /// </summary>
        IEnumerable<Type>? GetFilteredTypes(Type[]? typeArray, string? resolverMemberName) {
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
                var resolvedTypes = ResolveTypesFromMember(resolverMemberName);
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
        /// Resolves IEnumerable&lt;Type&gt; from a member name (method or property).
        /// Format: "TypeName.MemberName" or "MemberName" (searches in declaring type).
        /// </summary>
        IEnumerable<Type>? ResolveTypesFromMember(string memberName) {
            try {
                Type? targetType = null;
                string? targetMemberName = null;
                
                // Parse format: "TypeName.MemberName" or "MemberName"
                var parts = memberName.Split('.');
                if (parts.Length == 1) {
                    // Just member name - search in declaring type
                    targetType = Property.ParentType;
                    targetMemberName = parts[0];
                }
                else if (parts.Length == 2) {
                    // "TypeName.MemberName" format
                    var typeName = parts[0];
                    targetMemberName = parts[1];
                    
                    // Try to find the type
                    targetType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => {
                            try { return a.GetTypes(); }
                            catch { return Array.Empty<Type>(); }
                        })
                        .FirstOrDefault(t => t.Name == typeName || t.FullName == typeName);
                }
                
                if (targetType == null || targetMemberName == null)
                    return null;
                
                // Try to find and invoke the member
                const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                
                // Try property first
                var property = targetType.GetProperty(targetMemberName, flags);
                if (property != null && property.CanRead) {
                    var value = property.GetValue(null);
                    if (value is IEnumerable<Type> enumerable)
                        return enumerable;
                }
                
                // Try method
                var method = targetType.GetMethod(targetMemberName, flags, null, Type.EmptyTypes, null);
                if (method != null) {
                    var value = method.Invoke(null, null);
                    if (value is IEnumerable<Type> enumerable)
                        return enumerable;
                }
                
                return null;
            }
            catch (Exception ex) {
                Debug.LogError($"Error resolving types from member '{memberName}': {ex.Message}");
                return null;
            }
        }

        protected override void DrawPropertyLayout(GUIContent label) {
            if (!initialized) {
                Initialize();
            }

            // Delegate to the appropriate implementation
            drawerImplementation?.DrawPropertyLayout(label);
        }
        
        static string GetTypeName(Type type) {
            if (!type.IsGenericType)
                return type.Name;

            // For generic types, show something like "List<T>" or "Dictionary<TKey, TValue>"
            // For constructed generics, show "List<int>" or "Dictionary<string, int>"
            var genericArgs = type.GetGenericArguments();
            var baseName = type.Name.Split('`')[0];
            
            // Recursively format type arguments (handles nested generics like Container<ElementFire<int>>)
            var argNames = string.Join(", ", Array.ConvertAll(genericArgs, t => {
                if (t.IsGenericType) {
                    return GetTypeName(t); // Recursive call for nested generics
                }
                return t.Name;
            }));
            
            return $"{baseName}<{argNames}>";
        }
    }
}
