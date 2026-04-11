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
        /// Allow non-static, non-abstract class types.
        /// </summary>
        Class = 1 << 0,

        /// <summary>
        /// Allow non-primitive, non-enum struct types.
        /// </summary>
        Struct = 1 << 1,

        /// <summary>
        /// Allow abstract class types (excluding static classes).
        /// </summary>
        Abstract = 1 << 2,

        /// <summary>
        /// Allow interface types.
        /// </summary>
        Interface = 1 << 3,

        /// <summary>
        /// Allow static class types.
        /// </summary>
        Static = 1 << 4,

        /// <summary>
        /// Allow enum types.
        /// </summary>
        Enum = 1 << 5,

        /// <summary>
        /// Allow delegate types (excluding <see cref="Delegate"/> and <see cref="MulticastDelegate"/>).
        /// </summary>
        Delegate = 1 << 6,

        /// <summary>
        /// Allow primitive CLR types.
        /// </summary>
        Primitive = 1 << 7,

        /// <summary>
        /// Allow object-like types (<see cref="Class"/> and <see cref="Struct"/>).
        /// </summary>
        Object = Class | Struct,

        /// <summary>
        /// Allow all type kinds.
        /// </summary>
        All = Class | Struct | Abstract | Interface | Static | Enum | Delegate | Primitive,

        /// <summary>
        /// Backward-compatible alias for <see cref="Object"/>.
        /// </summary>
        [Obsolete("Use Object instead.")]
        Concrete = Object,
    }
}
