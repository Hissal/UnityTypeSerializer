using System;
using System.Collections.Generic;

namespace Hissal.UnityTypeSerializer {
    /// <summary>
    /// Unified type filter for <see cref="SerializedTypeOptionsAttribute"/>.
    /// Combines include and exclude filtering (both explicit types and resolver member names) into a single abstraction.
    /// </summary>
    /// <param name="IncludeTypes">
    /// Types to include in the dropdown. When set, only these types (and types satisfying base constraints) will appear.
    /// </param>
    /// <param name="IncludeResolver">
    /// Name of a static, parameterless member returning <see cref="IEnumerable{Type}"/> for inclusion filtering.
    /// Supports <c>"MemberName"</c> (on the declaring/context type) or <c>"TypeName.MemberName"</c> (explicit type + member).
    /// </param>
    /// <param name="ExcludeTypes">
    /// Types to exclude from the dropdown. These types will never appear in the selection list.
    /// </param>
    /// <param name="ExcludeResolver">
    /// Name of a static, parameterless member returning <see cref="IEnumerable{Type}"/> for exclusion filtering.
    /// Supports <c>"MemberName"</c> (on the declaring/context type) or <c>"TypeName.MemberName"</c> (explicit type + member).
    /// </param>
    public readonly record struct SerializedTypeFilter(
        Type[]? IncludeTypes = null,
        string IncludeResolver = "",
        Type[]? ExcludeTypes = null,
        string ExcludeResolver = "") {

        /// <summary>Creates a filter that includes only the specified types.</summary>
        public static SerializedTypeFilter Include(Type[] types) => new(types, string.Empty, null, string.Empty);

        /// <summary>Creates a filter that includes types resolved from the specified member name.</summary>
        public static SerializedTypeFilter Include(string memberName) => new(null, memberName, null, string.Empty);

        /// <summary>Creates a filter that excludes the specified types.</summary>
        public static SerializedTypeFilter Exclude(Type[] types) => new(null, string.Empty, types, string.Empty);

        /// <summary>Creates a filter that excludes types resolved from the specified member name.</summary>
        public static SerializedTypeFilter Exclude(string memberName) => new(null, string.Empty, null, memberName);

        /// <summary>Returns a new filter with the specified include types added.</summary>
        public SerializedTypeFilter WithInclude(Type[] types) => this with { IncludeTypes = types };

        /// <summary>Returns a new filter with the specified include resolver member name.</summary>
        public SerializedTypeFilter WithInclude(string memberName) => this with { IncludeResolver = memberName };

        /// <summary>Returns a new filter with the specified exclude types added.</summary>
        public SerializedTypeFilter WithExclude(Type[] types) => this with { ExcludeTypes = types };

        /// <summary>Returns a new filter with the specified exclude resolver member name.</summary>
        public SerializedTypeFilter WithExclude(string memberName) => this with { ExcludeResolver = memberName };

        /// <summary>Explicit conversion from a type array to an include filter.</summary>
        public static explicit operator SerializedTypeFilter(Type[] types) => Include(types);

        /// <summary>Explicit conversion from a member name string to an include filter.</summary>
        public static explicit operator SerializedTypeFilter(string memberName) => Include(memberName);
    }
}
