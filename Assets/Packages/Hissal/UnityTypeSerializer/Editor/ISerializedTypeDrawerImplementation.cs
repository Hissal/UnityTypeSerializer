using System;
using System.Collections.Generic;
using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace Hissal.UnityTypeSerializer.Editor {
    /// <summary>
    /// Interface for SerializedType drawer implementation strategies.
    /// Allows different drawing modes (inline, complex constructor) to be implemented separately.
    /// </summary>
    internal interface ISerializedTypeDrawerImplementation {
        /// <summary>
        /// Draws the SerializedType property with the appropriate UI.
        /// </summary>
        /// <param name="label">The label to display for the property</param>
        void DrawPropertyLayout(GUIContent label);
    }
    
    /// <summary>
    /// Base class containing shared utilities for SerializedType drawer implementations.
    /// </summary>
    internal abstract class SerializedTypeDrawerBase<TBase> where TBase : class {
        protected readonly InspectorProperty Property;
        protected readonly PropertyValueEntry<SerializedType<TBase>> ValueEntry;
        protected readonly SerializedTypeOptionsAttribute? Options;
        protected readonly List<Type> AvailableTypes;
        
        protected SerializedTypeDrawerBase(
            InspectorProperty property,
            PropertyValueEntry<SerializedType<TBase>> valueEntry,
            SerializedTypeOptionsAttribute? options,
            List<Type> availableTypes) {
            Property = property;
            ValueEntry = valueEntry;
            Options = options;
            AvailableTypes = availableTypes;
        }
        
        /// <summary>
        /// Gets a display name for a type, including generic parameters if applicable.
        /// </summary>
        protected static string GetTypeName(Type type) {
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
        
        /// <summary>
        /// Validates if the current type satisfies the attribute options.
        /// </summary>
        /// <param name="type">The type to validate</param>
        /// <param name="errorMessage">Output error message if invalid</param>
        /// <returns>True if valid, false otherwise</returns>
        protected bool ValidateType(Type? type, out string? errorMessage) {
            errorMessage = null;
            
            if (type == null)
                return true; // No type selected is valid
            
            bool allowGenericTypeConstruction = Options?.AllowGenericTypeConstruction ?? false;
            bool allowOpenGenerics = Options?.AllowOpenGenerics ?? false;
            
            // Check if type is an open generic
            if (type.IsGenericTypeDefinition) {
                if (!allowOpenGenerics) {
                    errorMessage = $"Open generic types are not allowed for this field.\nType '{GetTypeName(type)}' must be fully constructed.";
                    return false;
                }
                return true;
            }
            
            // Check if type contains generic parameters (partially constructed)
            if (type.ContainsGenericParameters) {
                if (!allowOpenGenerics) {
                    errorMessage = $"Types with unresolved generic parameters are not allowed for this field.\nType '{GetTypeName(type)}' contains generic parameters.";
                    return false;
                }
                return true;
            }
            
            // Concrete type is always valid
            return true;
        }
    }
}
