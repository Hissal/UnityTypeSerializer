using System;
using Sirenix.OdinInspector.Editor;

namespace Hissal.UnityTypeSerializer.Editor {
    /// <summary>
    /// Abstraction for reading and writing the selected type in a SerializedType property.
    /// Hides the Odin PropertyValueEntry generic differences between SerializedType and SerializedType{TBase}.
    /// </summary>
    internal interface ISerializedTypeValueAccessor {
        /// <summary>
        /// Gets the currently selected type, or null if none is set.
        /// </summary>
        Type? GetSelectedType();

        /// <summary>
        /// Gets the raw serialized assembly-qualified name.
        /// Used to detect unresolved type references (invalid serialized values).
        /// </summary>
        string GetSerializedAqn();

        /// <summary>
        /// Gets the raw serialized type id.
        /// Used to detect and repair id/AQN sync issues.
        /// </summary>
        string GetSerializedTypeId();
        
        /// <summary>
        /// Sets the selected type, creating or replacing the backing instance as needed.
        /// </summary>
        void SetSelectedType(Type? type);
        
        /// <summary>
        /// Applies pending changes to the serialized property.
        /// </summary>
        void ApplyChanges();

        /// <summary>
        /// Sets raw serialized values directly.
        /// </summary>
        void SetSerializedValues(string serializedTypeId, string serializedAqn);

        /// <summary>
        /// Returns all id/AQN mismatches found in the serialized type tree.
        /// </summary>
        SerializedTypeTreeMismatch[] GetTypeTreeMismatches();

        /// <summary>
        /// Applies a fix for a single mismatch node by path.
        /// </summary>
        bool TryApplyTypeTreeMismatchFix(string path, SerializedTypeTreeFixMode mode, out string impactMessage);
        
        /// <summary>
        /// Gets the base type constraint for filtering available types.
        /// For SerializedType{TBase} this is typeof(TBase); for non-generic SerializedType this is typeof(object).
        /// </summary>
        Type BaseConstraint { get; }
    }
    
    /// <summary>
    /// Value accessor for non-generic <see cref="SerializedType"/> properties.
    /// </summary>
    internal sealed class SerializedTypeValueAccessor : ISerializedTypeValueAccessor {
        readonly PropertyValueEntry<SerializedType> valueEntry;
        
        public SerializedTypeValueAccessor(PropertyValueEntry<SerializedType> valueEntry) {
            this.valueEntry = valueEntry;
        }
        
        public Type BaseConstraint => typeof(object);
        
        public Type? GetSelectedType() => valueEntry.SmartValue?.Type;

        public string GetSerializedAqn() => valueEntry.SmartValue?.SerializedAqn ?? string.Empty;

        public string GetSerializedTypeId() => valueEntry.SmartValue?.SerializedTypeId ?? string.Empty;
        
        public void SetSelectedType(Type? type) {
            var value = valueEntry.SmartValue;
            if (value == null) {
                value = new SerializedType();
                valueEntry.SmartValue = value;
            }
            value.Type = type;
        }
        
        public void ApplyChanges() => valueEntry.ApplyChanges();

        public void SetSerializedValues(string serializedTypeId, string serializedAqn) {
            var value = valueEntry.SmartValue;
            if (value == null) {
                value = new SerializedType();
                valueEntry.SmartValue = value;
            }

            value.SetSerializedValues(serializedTypeId, serializedAqn);
        }

        public SerializedTypeTreeMismatch[] GetTypeTreeMismatches() {
            return valueEntry.SmartValue?.GetTypeTreeMismatches() ?? Array.Empty<SerializedTypeTreeMismatch>();
        }

        public bool TryApplyTypeTreeMismatchFix(string path, SerializedTypeTreeFixMode mode, out string impactMessage) {
            impactMessage = string.Empty;
            var value = valueEntry.SmartValue;
            if (value == null)
                return false;

            return value.TryApplyTypeTreeMismatchFix(path, mode, out impactMessage);
        }
    }
    
    /// <summary>
    /// Value accessor for generic <see cref="SerializedType{TBase}"/> properties.
    /// </summary>
    internal sealed class SerializedTypeValueAccessor<TBase> : ISerializedTypeValueAccessor where TBase : class {
        readonly PropertyValueEntry<SerializedType<TBase>> valueEntry;
        
        public SerializedTypeValueAccessor(PropertyValueEntry<SerializedType<TBase>> valueEntry) {
            this.valueEntry = valueEntry;
        }
        
        public Type BaseConstraint => typeof(TBase);
        
        public Type? GetSelectedType() => valueEntry.SmartValue?.Type;

        public string GetSerializedAqn() => valueEntry.SmartValue?.SerializedAqn ?? string.Empty;

        public string GetSerializedTypeId() => valueEntry.SmartValue?.SerializedTypeId ?? string.Empty;
        
        public void SetSelectedType(Type? type) {
            var value = valueEntry.SmartValue;
            if (value == null) {
                value = new SerializedType<TBase>();
                valueEntry.SmartValue = value;
            }
            value.Type = type;
        }
        
        public void ApplyChanges() => valueEntry.ApplyChanges();

        public void SetSerializedValues(string serializedTypeId, string serializedAqn) {
            var value = valueEntry.SmartValue;
            if (value == null) {
                value = new SerializedType<TBase>();
                valueEntry.SmartValue = value;
            }

            value.SetSerializedValues(serializedTypeId, serializedAqn);
        }

        public SerializedTypeTreeMismatch[] GetTypeTreeMismatches() {
            return valueEntry.SmartValue?.GetTypeTreeMismatches() ?? Array.Empty<SerializedTypeTreeMismatch>();
        }

        public bool TryApplyTypeTreeMismatchFix(string path, SerializedTypeTreeFixMode mode, out string impactMessage) {
            impactMessage = string.Empty;
            var value = valueEntry.SmartValue;
            if (value == null)
                return false;

            return value.TryApplyTypeTreeMismatchFix(path, mode, out impactMessage);
        }
    }
}
