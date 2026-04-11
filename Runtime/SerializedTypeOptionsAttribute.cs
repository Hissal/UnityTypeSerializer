using System;

namespace Hissal.UnityTypeSerializer {
    /// <summary>
    /// Configures filtering options for a <see cref="SerializedType{TBase}"/> field.
    /// Controls which types appear in the dropdown and how generic types are handled.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class SerializedTypeOptionsAttribute : Attribute {
        /// <summary>
        /// Gets or initializes the drawer mode to use for displaying and editing the SerializedType field.
        /// Default is <see cref="SerializedTypeDrawerMode.Inline"/>.
        /// </summary>
        public SerializedTypeDrawerMode DrawerMode { get; init; } = SerializedTypeDrawerMode.Inline;
        
        /// <summary>
        /// Gets or initializes whether generic type construction UI should be enabled.
        /// When true, allows constructing closed generic types from open generic definitions (e.g., <c>List&lt;&gt;</c> → <c>List&lt;int&gt;</c>).
        /// If <see cref="AllowOpenGenerics"/> is false, construction is mandatory before assignment.
        /// If <see cref="AllowOpenGenerics"/> is true, a "Construct" button appears to optionally construct the type.
        /// Default is false.
        /// </summary>
        public bool AllowGenericTypeConstruction { get; init; }

        /// <summary>
        /// Gets or initializes whether self-nesting of types is allowed (e.g., <c>TypeA&lt;TypeA&lt;TypeA&gt;&gt;</c>).
        /// When false, prevents selecting the same generic type definition for its own type arguments.
        /// Default is false.
        /// </summary>
        public bool AllowSelfNesting { get; init; }

        /// <summary>
        /// Gets or initializes whether open generic type definitions can be left as the final result (e.g., <c>MyType&lt;T&gt;</c>).
        /// When true, open generics can be immediately assigned without construction.
        /// When false (and <see cref="AllowGenericTypeConstruction"/> is true), construction is required.
        /// Default is false.
        /// </summary>
        public bool AllowOpenGenerics { get; init; }

        /// <summary>
        /// Controls which kind of types are shown in the dropdown.
        /// Default is <see cref="SerializedTypeKind.Object"/> only.
        /// Use bitwise flags to allow multiple kinds (e.g., <c>Object | Abstract</c>).
        /// </summary>
        public SerializedTypeKind AllowedTypeKinds { get; init; } = SerializedTypeKind.Object;

        /// <summary>
        /// Gets or initializes the name of a static or instance member (method, property, or field) that returns
        /// a <see cref="SerializedTypeFilter"/> or <see cref="System.Collections.Generic.IEnumerable{Type}"/>
        /// for controlling which types appear in the dropdown.
        /// Supports <c>"MemberName"</c> (on the declaring/context type) or <c>"TypeName.MemberName"</c> (explicit type).
        /// </summary>
        /// <seealso cref="SerializedTypeFilter"/>
        public string CustomTypeFilter { get; init; } = string.Empty;

        /// <summary>
        /// Candidate type must satisfy ALL of these types (AND bucket).
        /// </summary>
        public Type[]? InheritsOrImplementsAll { get; init; }

        /// <summary>
        /// Candidate type must satisfy AT LEAST ONE of these types (OR bucket).
        /// </summary>
        public Type[]? InheritsOrImplementsAny { get; init; }

        /// <summary>
        /// Gets or initializes the name of a parameterless instance method to invoke
        /// after the selected type value changes.
        /// </summary>
        public string OnTypeChanged { get; init; } = string.Empty;
    }
}
