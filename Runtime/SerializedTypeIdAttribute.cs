using System;

namespace Hissal.UnityTypeSerializer {
    /// <summary>
    /// Assigns a stable identifier to a type so SerializedType can survive AQN/name changes.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Class |
        AttributeTargets.Struct |
        AttributeTargets.Interface |
        AttributeTargets.Enum |
        AttributeTargets.Delegate,
        AllowMultiple = false,
        Inherited = false)]
    public sealed class SerializedTypeIdAttribute : Attribute {
        /// <summary>
        /// Stable identifier for the annotated type.
        /// </summary>
        public string Id { get; }

        public SerializedTypeIdAttribute(string id) {
            Id = id ?? string.Empty;
        }
    }
}

