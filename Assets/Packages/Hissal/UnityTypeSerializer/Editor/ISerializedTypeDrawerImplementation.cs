using System;
using System.Collections.Generic;
using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace Hissal.UnityTypeSerializer.Editor {
    /// <summary>
    /// Shared utilities for SerializedType drawer implementations.
    /// </summary>
    internal static class SerializedTypeDrawerUtilities {
        /// <summary>
        /// Gets a display name for a type, including generic parameters if applicable.
        /// </summary>
        public static string GetTypeName(Type type) {
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
    /// Uses <see cref="ISerializedTypeValueAccessor"/> to abstract over the generic vs non-generic value entry.
    /// </summary>
    internal abstract class SerializedTypeDrawerBase {
        protected readonly InspectorProperty Property;
        protected readonly ISerializedTypeValueAccessor Accessor;
        protected readonly SerializedTypeOptionsAttribute? Options;
        protected readonly List<Type> AvailableTypes;
        
        protected SerializedTypeDrawerBase(
            InspectorProperty property,
            ISerializedTypeValueAccessor accessor,
            SerializedTypeOptionsAttribute? options,
            List<Type> availableTypes) {
            Property = property;
            Accessor = accessor;
            Options = options;
            AvailableTypes = availableTypes;
        }
        
        /// <summary>
        /// Gets a display name for a type, including generic parameters if applicable.
        /// </summary>
        protected static string GetTypeName(Type type) {
            return SerializedTypeDrawerUtilities.GetTypeName(type);
        }

        /// <summary>
        /// Gets the display name for the current selection, distinguishing empty from unresolved values.
        /// </summary>
        protected string GetSelectionDisplayName(Type? type) {
            return SerializedTypeDrawerCore.GetSelectionDisplayName(type, Accessor.GetSerializedAqn());
        }
        
        /// <summary>
        /// Validates if the current type satisfies the attribute options.
        /// </summary>
        /// <param name="type">The type to validate</param>
        /// <param name="errorMessage">Output error message if invalid</param>
        /// <returns>True if valid, false otherwise</returns>
        protected bool ValidateType(Type? type, out string? errorMessage) {
            return SerializedTypeDrawerCore.TryValidateSelectedType(
                type,
                Accessor.GetSerializedAqn(),
                Accessor.BaseConstraint,
                Options,
                Property,
                out errorMessage);
        }

        protected void ApplySelectedType(Type? newType) {
            var previousType = Accessor.GetSelectedType();
            Accessor.SetSelectedType(newType);
            Accessor.ApplyChanges();
            SerializedTypeDrawerCore.InvokeOnTypeChangedCallback(Options, Property, previousType, newType);
        }
    }
}
