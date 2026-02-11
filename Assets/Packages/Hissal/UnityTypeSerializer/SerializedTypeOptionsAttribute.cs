using System;

namespace Hissal.UnityTypeSerializer {
    /// <summary>
    /// Configures filtering options for a <see cref="SerializedType{TBase}"/> field.
    /// Controls which types appear in the dropdown and how generic types are handled.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class SerializedTypeOptionsAttribute : Attribute {
        /// <summary>
        /// Gets the drawer mode to use for displaying and editing the SerializedType field.
        /// Default is <see cref="SerializedTypeDrawerMode.Inline"/>.
        /// </summary>
        public SerializedTypeDrawerMode DrawerMode { get; }
        
        /// <summary>
        /// Gets whether generic type construction UI should be enabled.
        /// When true, allows constructing closed generic types from open generic definitions (e.g., <c>List&lt;&gt;</c> → <c>List&lt;int&gt;</c>).
        /// If <see cref="AllowOpenGenerics"/> is false, construction is mandatory before assignment.
        /// If <see cref="AllowOpenGenerics"/> is true, a "Construct" button appears to optionally construct the type.
        /// Default is false.
        /// </summary>
        public bool AllowGenericTypeConstruction { get; }

        /// <summary>
        /// Gets whether self-nesting of types is allowed (e.g., <c>TypeA&lt;TypeA&lt;TypeA&gt;&gt;</c>).
        /// When false, prevents selecting the same generic type definition for its own type arguments.
        /// Default is false.
        /// </summary>
        public bool AllowSelfNesting { get; }

        /// <summary>
        /// Gets whether open generic type definitions can be left as the final result (e.g., <c>MyType&lt;T&gt;</c>).
        /// When true, open generics can be immediately assigned without construction.
        /// When false (and <see cref="AllowGenericTypeConstruction"/> is true), construction is required.
        /// Default is false.
        /// </summary>
        public bool AllowOpenGenerics { get; }

        /// <summary>
        /// Controls which kind of types are shown in the dropdown (concrete, abstract, interfaces).
        /// Default is <see cref="SerializedTypeKind.Concrete"/> only (abstract + interfaces excluded).
        /// Use bitwise flags to allow multiple kinds (e.g., <c>Concrete | Abstract</c>).
        /// </summary>
        public SerializedTypeKind AllowedTypeKinds { get; init; } = SerializedTypeKind.Concrete;

        /// <summary>
        /// Gets or sets the name of a static or instance member (method, property, or field) that returns
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
        /// Initializes a new instance of the <see cref="SerializedTypeOptionsAttribute"/> class.
        /// </summary>
        /// <param name="allowGenericTypeConstruction">
        /// If true, enables the UI for constructing closed generic types from open definitions (e.g., <c>List&lt;&gt;</c> → <c>List&lt;int&gt;</c>).
        /// Combined with <paramref name="allowOpenGenerics"/>, controls whether construction is mandatory or optional.
        /// Default is false.
        /// </param>
        /// <param name="allowSelfNesting">
        /// If true, allows types to nest themselves (e.g., <c>Wrapper&lt;Wrapper&lt;int&gt;&gt;</c>).
        /// Default is false.
        /// </param>
        /// <param name="allowOpenGenerics">
        /// If true, allows leaving generic type parameters unresolved (e.g., <c>MyType&lt;T&gt;</c>).
        /// When combined with <paramref name="allowGenericTypeConstruction"/> being true, shows an optional "Construct" button.
        /// Default is false.
        /// </param>
        /// <param name="drawerMode">
        /// The drawer mode to use for displaying the SerializedType field.
        /// Default is <see cref="SerializedTypeDrawerMode.Inline"/>.
        /// </param>
        public SerializedTypeOptionsAttribute(
            bool allowGenericTypeConstruction = false,
            bool allowSelfNesting = false,
            bool allowOpenGenerics = false,
            SerializedTypeDrawerMode drawerMode = SerializedTypeDrawerMode.Inline) {
            AllowGenericTypeConstruction = allowGenericTypeConstruction;
            AllowSelfNesting = allowSelfNesting;
            AllowOpenGenerics = allowOpenGenerics;
            DrawerMode = drawerMode;
        }
    }
}
