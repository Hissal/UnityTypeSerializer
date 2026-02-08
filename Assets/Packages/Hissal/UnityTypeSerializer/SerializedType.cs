using System;
using System.Diagnostics.CodeAnalysis;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Hissal.UnityTypeSerializer {
    /// <summary>
    /// Represents a fully serializable, inspector-constructible representation of a type that derives from a specified base class or interface.
    /// By default, only concrete, non-generic types are included. Abstract types, interfaces, and generic type definitions are excluded.
    /// This behavior can be customized via the <see cref="SerializedTypeOptionsAttribute"/>.
    /// </summary>
    /// <typeparam name="TBase">
    /// The base class or interface that the serialized type must derive from or implement.
    /// </typeparam>
    [Serializable, InlineProperty]
    public sealed class SerializedType<TBase> where TBase : class {
        /// <summary>
        /// Stores the assembly-qualified name of the serialized type.
        /// </summary>
        [SerializeField, HideInInspector] 
        string aqn = string.Empty;

        /// <summary>
        /// Indicates whether a valid type is currently set.
        /// </summary>
        /// <remarks>
        /// This property uses the <see cref="Type"/> property to determine if a type is set.
        /// </remarks>
        [MemberNotNullWhen(true, nameof(Type))]
        public bool HasType => Type != null;
        
        /// <summary>
        /// Gets the serialized type based on the stored assembly-qualified name.
        /// </summary>
        /// <remarks>
        /// Returns <c>null</c> if the assembly-qualified name is empty or invalid.
        /// </remarks>
        public Type? Type {
            get => string.IsNullOrEmpty(aqn) ? null : Type.GetType(aqn);
            internal set => aqn = value?.AssemblyQualifiedName ?? string.Empty;
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
    [Serializable, InlineProperty]
    public sealed class SerializedType {
        /// <summary>
        /// Stores the assembly-qualified name of the serialized type.
        /// </summary>
        [SerializeField, HideInInspector] 
        string aqn = string.Empty;

        /// <summary>
        /// Indicates whether a valid type is currently set.
        /// </summary>
        /// <remarks>
        /// This property uses the <see cref="Type"/> property to determine if a type is set.
        /// </remarks>
        [MemberNotNullWhen(true, nameof(Type))]
        public bool HasType => Type != null;
        
        /// <summary>
        /// Gets the serialized type based on the stored assembly-qualified name.
        /// </summary>
        /// <remarks>
        /// Returns <c>null</c> if the assembly-qualified name is empty or invalid.
        /// </remarks>
        public Type? Type {
            get => string.IsNullOrEmpty(aqn) ? null : Type.GetType(aqn);
            internal set => aqn = value?.AssemblyQualifiedName ?? string.Empty;
        }
    }
}