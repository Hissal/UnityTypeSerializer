using System;
using System.Diagnostics.CodeAnalysis;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Hissal.UnityTypeSerializer {
    /// <summary>
    /// Represents a serializable reference to a type that derives from a specified base class or interface.
    /// By default, only concrete, non-generic types are included. Abstract types, interfaces, and generic type definitions are excluded.
    /// This behavior can be customized via the <see cref="TypeRefOptionsAttribute"/>.
    /// </summary>
    /// <typeparam name="TBase">
    /// The base class or interface that the referenced type must derive from or implement.
    /// </typeparam>
    [Serializable, InlineProperty]
    public sealed class TypeRef<TBase> where TBase : class {
        /// <summary>
        /// Stores the assembly-qualified name of the referenced type.
        /// </summary>
        [SerializeField, HideInInspector] 
        string aqn = string.Empty;

        /// <summary>
        /// Indicates whether a valid type is currently referenced.
        /// </summary>
        /// <remarks>
        /// This property uses the <see cref="Type"/> property to determine if a type is set.
        /// </remarks>
        [MemberNotNullWhen(true, nameof(Type))]
        public bool HasType => Type != null;
        
        /// <summary>
        /// Gets the referenced type based on the stored assembly-qualified name.
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