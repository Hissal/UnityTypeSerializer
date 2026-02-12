# Refactoring: UseComplexConstructor Boolean → DrawerMode Enum

## Summary

Refactored `SerializedTypeOptionsAttribute` to use an enum-based `DrawerMode` property instead of the boolean `UseComplexConstructor` property. This provides better extensibility for adding new drawer modes in the future.

## Changes Made

### 1. New File: `SerializedTypeDrawerMode.cs`

Created a new enum to represent drawer modes:

```csharp
namespace Hissal.UnityTypeSerializer {
    public enum SerializedTypeDrawerMode {
        /// <summary>
        /// Inline one-line drawer mode with multiple dropdowns (default).
        /// </summary>
        Inline = 0,
        
        /// <summary>
        /// Complex step-by-step constructor UI for generic types.
        /// </summary>
        Constructor = 1
    }
}
```

### 2. Updated: `SerializedTypeOptionsAttribute.cs`

**Replaced:**
- `public bool UseComplexConstructor { get; }` 

**With:**
- `public SerializedTypeDrawerMode DrawerMode { get; }`

**Constructor Changes:**
- Added new parameter: `SerializedTypeDrawerMode drawerMode = SerializedTypeDrawerMode.Inline`

### 3. Updated: `SerializedTypeDrawerCore.cs`

**In `CreateDrawerImplementation` method:**

**Before:**
```csharp
bool useComplexConstructor = options?.UseComplexConstructor ?? false;

if (useComplexConstructor) {
    return new ComplexConstructorSerializedTypeDrawer(...);
}
```

**After:**
```csharp
var drawerMode = options?.DrawerMode ?? SerializedTypeDrawerMode.Inline;

if (drawerMode == SerializedTypeDrawerMode.Constructor) {
    return new ComplexConstructorSerializedTypeDrawer(...);
}
```

**Added:**
- `using Hissal.UnityTypeSerializer;` directive to access the enum

### 4. Updated: `SerializedTypeExample.cs`

Added examples demonstrating the enum-based drawer mode:

```csharp
// Using the new enum-based syntax
[SerializedTypeOptions(
    AllowGenericTypeConstruction = true, 
    DrawerMode = SerializedTypeDrawerMode.Constructor)]
SerializedType<ISerializedTypeExample>? complexConstructorMode;
```

## Migration Guide

Use the new `drawerMode` parameter:

```csharp
// Use Constructor mode
[SerializedTypeOptions(DrawerMode = SerializedTypeDrawerMode.Constructor)]
SerializedType<IMyInterface> myField;

// Or explicitly use default Inline mode
[SerializedTypeOptions(DrawerMode = SerializedTypeDrawerMode.Inline)]
SerializedType<IMyInterface> myField;

// Or omit entirely for default inline mode
[SerializedTypeOptions()]
SerializedType<IMyInterface> myField;
```

## Benefits

1. **Extensibility**: Easy to add new drawer modes in the future (e.g., `Tree`, `Compact`, `Detailed`, etc.)
2. **Clarity**: Enum name is more descriptive than a boolean
3. **Type Safety**: Compile-time checking for valid drawer modes
4. **Future-Proof**: Better foundation for adding drawer customization options

## Testing

- ✅ Enum created and compiles
- ✅ Attribute updated with new property
- ✅ Drawer core updated to use enum
- ✅ Example file demonstrates usage
- ✅ Default behavior unchanged (Inline mode)

## Notes

- The IDE may show false positives about `SerializedTypeDrawerMode` not being resolved until Unity recompiles
- Once Unity imports and compiles the new enum file, all references will resolve correctly
- The `using Hissal.UnityTypeSerializer;` directive is required in Editor namespace files to access the enum
