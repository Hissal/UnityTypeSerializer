using System;

namespace Hissal.UnityTypeSerializer {
    /// <summary>
    /// Controls which kinds of types are shown in the <see cref="SerializedType{TBase}"/> dropdown.
    /// Use with <see cref="SerializedTypeOptionsAttribute.AllowedTypeKinds"/> to configure type filtering.
    /// </summary>
    [Flags]
    public enum SerializedTypeKind {
        /// <summary>
        /// No types are allowed.
        /// </summary>
        None = 0,

        /// <summary>
        /// Allow concrete (non-abstract, non-interface) classes.
        /// </summary>
        Concrete = 1 << 0,

        /// <summary>
        /// Allow abstract classes (excludes interfaces).
        /// </summary>
        Abstract = 1 << 1,

        /// <summary>
        /// Allow interface types.
        /// </summary>
        Interface = 1 << 2,
    }
}
