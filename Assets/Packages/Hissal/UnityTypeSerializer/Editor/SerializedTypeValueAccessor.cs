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
        /// Sets the selected type, creating or replacing the backing instance as needed.
        /// </summary>
        void SetSelectedType(Type? type);
        
        /// <summary>
        /// Applies pending changes to the serialized property.
        /// </summary>
        void ApplyChanges();
        
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
        
        public void SetSelectedType(Type? type) {
            var value = valueEntry.SmartValue;
            if (value == null) {
                value = new SerializedType();
                valueEntry.SmartValue = value;
            }
            value.Type = type;
        }
        
        public void ApplyChanges() => valueEntry.ApplyChanges();
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
        
        public void SetSelectedType(Type? type) {
            valueEntry.SmartValue = new SerializedType<TBase> { Type = type };
        }
        
        public void ApplyChanges() => valueEntry.ApplyChanges();
    }
}
