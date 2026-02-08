using System;
using System.Collections.Generic;
using Sirenix.OdinInspector.Editor;
using UnityEngine;

namespace Hissal.UnityTypeSerializer.Editor {
    /// <summary>
    /// Custom Odin drawer for SerializedType{TBase} that properly handles the SerializedTypeOptionsAttribute.
    /// Thin wrapper that delegates to shared implementation via <see cref="SerializedTypeDrawerCore"/>.
    /// </summary>
    public sealed class SerializedTypeDrawer<TBase> : OdinValueDrawer<SerializedType<TBase>> where TBase : class {
        ISerializedTypeDrawerImplementation? drawerImplementation;
        bool initialized;

        protected override void Initialize() {
            base.Initialize();
            
            var options = Property.GetAttribute<SerializedTypeOptionsAttribute>();
            var accessor = new SerializedTypeValueAccessor<TBase>(
                (PropertyValueEntry<SerializedType<TBase>>)ValueEntry
            );
            var availableTypes = SerializedTypeDrawerCore.RefreshAvailableTypes(
                accessor.BaseConstraint, options, Property
            );
            
            drawerImplementation = SerializedTypeDrawerCore.CreateDrawerImplementation(
                Property, accessor, options, availableTypes
            );
            initialized = true;
        }

        protected override void DrawPropertyLayout(GUIContent label) {
            if (!initialized) {
                Initialize();
            }

            drawerImplementation?.DrawPropertyLayout(label);
        }
    }
    
    /// <summary>
    /// Custom Odin drawer for non-generic SerializedType that accepts any type.
    /// Thin wrapper that delegates to shared implementation via <see cref="SerializedTypeDrawerCore"/>.
    /// </summary>
    public sealed class SerializedTypeDrawer : OdinValueDrawer<SerializedType> {
        ISerializedTypeDrawerImplementation? drawerImplementation;
        bool initialized;

        protected override void Initialize() {
            base.Initialize();
            
            var options = Property.GetAttribute<SerializedTypeOptionsAttribute>();
            var accessor = new SerializedTypeValueAccessor(
                (PropertyValueEntry<SerializedType>)ValueEntry
            );
            var availableTypes = SerializedTypeDrawerCore.RefreshAvailableTypes(
                accessor.BaseConstraint, options, Property
            );
            
            drawerImplementation = SerializedTypeDrawerCore.CreateDrawerImplementation(
                Property, accessor, options, availableTypes
            );
            initialized = true;
        }

        protected override void DrawPropertyLayout(GUIContent label) {
            if (!initialized) {
                Initialize();
            }

            drawerImplementation?.DrawPropertyLayout(label);
        }
    }
}
