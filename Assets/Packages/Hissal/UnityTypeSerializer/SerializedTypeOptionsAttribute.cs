using System;

namespace Hissal.UnityTypeSerializer {
    /// <summary>
    /// Configures filtering options for a <see cref="SerializedType{TBase}"/> field.
    /// Controls which types appear in the dropdown and how generic types are handled.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class SerializedTypeOptionsAttribute : Attribute {
        /// <summary>
        /// Gets whether to use the complex step-by-step constructor UI for generic types.
        /// When false (default), uses the inline one-line drawer mode with multiple dropdowns.
        /// When true, uses the complex constructor UI with nested expandable sections.
        /// Default is false.
        /// </summary>
        public bool UseComplexConstructor { get; }
        
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
        /// Gets the types to exclude from the dropdown. These types will never appear in the selection list.
        /// </summary>
        public Type[]? ExcludeTypes { get; init; }

        /// <summary>
        /// Gets the name of a method or property that returns an IEnumerable&lt;Type&gt; to exclude.
        /// The member should be static and parameterless. Can be in format "TypeName.MemberName" or just "MemberName" for current class.
        /// </summary>
        public string? ExcludeTypesResolver { get; init; }

        /// <summary>
        /// Gets the custom filter types. When set, only these types (and their derived types if base type constraints apply) will appear.
        /// Takes precedence over normal type filtering based on TBase constraint.
        /// </summary>
        public Type[]? IncludeTypes { get; init; }

        /// <summary>
        /// Gets the name of a method or property that returns an IEnumerable&lt;Type&gt; for custom filtering.
        /// The member should be static and parameterless. Can be in format "TypeName.MemberName" or just "MemberName" for current class.
        /// When set, only these types will appear in the dropdown.
        /// </summary>
        public string? IncludeTypesResolver { get; init; }

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
        /// <param name="useComplexConstructor">
        /// If true, uses the complex step-by-step constructor UI for generic types.
        /// If false (default), uses the inline one-line drawer mode with multiple dropdowns.
        /// Default is false.
        /// </param>
        public SerializedTypeOptionsAttribute(
            bool allowGenericTypeConstruction = false,
            bool allowSelfNesting = false,
            bool allowOpenGenerics = false,
            bool useComplexConstructor = false) {
            AllowGenericTypeConstruction = allowGenericTypeConstruction;
            AllowSelfNesting = allowSelfNesting;
            AllowOpenGenerics = allowOpenGenerics;
            UseComplexConstructor = useComplexConstructor;
        }
    }
}
