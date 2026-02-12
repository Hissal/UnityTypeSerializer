# Unity Type Serializer

> **⚠️ Important:** This library requires [Odin Inspector](https://odininspector.com/) for editor integration and functionality.

A Unity package providing serializable type references with support for generic type construction and inspector integration. Unity Type Serializer allows you to store and manipulate type references in the Unity Inspector with full support for constructing complex generic types, filtering based on inheritance, and runtime type resolution.

## Overview

Unity Type Serializer solves the problem of storing and selecting types in the Unity Inspector. While Unity can serialize many things, it cannot natively serialize `System.Type` references in a way that's easy to work with in the Inspector. This package provides:

- **Type-safe serialization**: Store references to types with compile-time base class/interface constraints
- **Inspector-based type selection**: Choose types from dropdown menus in the Unity Inspector
- **Generic type construction**: Build complex generic types (e.g., `Repository<PlayerData>`, `Strategy<ModifiedCalculation<CriticalModifier>>`) directly in the Inspector
- **Flexible filtering**: Control which types appear in dropdowns using attributes, custom filters, and inheritance rules
- **Two drawer modes**: Inline mode for simple selection and Constructor mode for complex nested generics

## Features

- **Serializable Type References**: Store and serialize references to types that derive from a specified base class or interface
- **Generic Type Construction**: Build closed generic types from open generic definitions in the Unity Inspector
- **Flexible Configuration**: Control type filtering, generic handling, and validation through attributes
- **Custom Type Filters**: Use methods, properties, or fields to dynamically filter available types
- **Type Kind Filtering**: Choose between concrete classes, abstract classes, and interfaces
- **Inspector Integration**: Seamless Unity Editor integration with Odin Inspector support
- **Self-Nesting Control**: Prevent or allow recursive generic type definitions
- **Two Drawer Modes**: Inline (compact, single-line) or Constructor (step-by-step, nested UI)

## Installation

### Prerequisites

- Unity 6000.3 or later
- [Odin Inspector](https://odininspector.com/) (required for editor integration)

### Installation Steps

1. Ensure Odin Inspector is installed in your Unity project
2. Open Unity Package Manager (Window → Package Manager)
3. Click the `+` button in the top-left corner
4. Select "Add package from git URL..."
5. Enter the following URL:
   ```
   https://github.com/Hissal/UnityTypeSerializer.git
   ```
6. Click "Add" and Unity will automatically install the package

## Core Types

### `SerializedType<TBase>`

The main type for storing serializable type references with a base class or interface constraint.

```csharp
public class MyComponent : MonoBehaviour {
    // Only types implementing IDamageEffect can be selected
    [SerializeField]
    SerializedType<IDamageEffect> damageType;
    
    void Start() {
        if (damageType.HasType) {
            Type selectedType = damageType.Type;
            // Use selectedType for reflection, instantiation, etc.
        }
    }
}
```

### `SerializedType` (Non-Generic)

A non-generic version that accepts any type. Equivalent to `SerializedType<object>`.

```csharp
[SerializeField]
SerializedType anyType;  // Can select any type
```

### `SerializedTypeOptionsAttribute`

Configures filtering and behavior for `SerializedType` fields.

**Properties:**
- `AllowGenericTypeConstruction`: Enable generic type construction UI
- `AllowOpenGenerics`: Allow open generic definitions (e.g., `List<>`) as final result
- `AllowSelfNesting`: Allow recursive nesting (e.g., `Wrapper<Wrapper<int>>`)
- `DrawerMode`: Choose between `Inline` (default) or `Constructor` drawer mode
- `AllowedTypeKinds`: Control which type kinds appear (Concrete, Abstract, Interface)
- `CustomTypeFilter`: Name of a member that returns a type filter
- `InheritsOrImplementsAll`: Types must inherit/implement ALL of these types
- `InheritsOrImplementsAny`: Types must inherit/implement AT LEAST ONE of these types

### `SerializedTypeDrawerMode`

Enum defining drawer display modes:
- `Inline` (default): Compact single-line with multiple dropdowns
- `Constructor`: Step-by-step nested UI for complex generic construction

## Usage Examples

### Basic Usage

**Simple Type Selection:**
```csharp
public interface IWeapon { }
public class Sword : IWeapon { }
public class Bow : IWeapon { }

public class WeaponSystem : MonoBehaviour {
    [SerializeField]
    SerializedType<IWeapon> weaponType;
    
    void EquipWeapon() {
        if (weaponType.HasType) {
            // Create instance of selected weapon type
            IWeapon weapon = (IWeapon)Activator.CreateInstance(weaponType.Type);
        }
    }
}
```

### Generic Type Construction

**Enable Generic Construction:**
```csharp
public interface IRepository { }
public class Repository<TData> : IRepository where TData : class { }

public class DataManager : MonoBehaviour {
    [SerializeField]
    [SerializedTypeOptions(AllowGenericTypeConstruction = true)]
    SerializedType<IRepository> repositoryType;
    
    // In Inspector: Can select Repository<> and construct it as Repository<PlayerData>
}
```

**Open Generics Allowed:**
```csharp
[SerializeField]
[SerializedTypeOptions(AllowOpenGenerics = true)]
SerializedType<IRepository> openGenericType;

// Allows selecting Repository<> without constructing it
// Useful when you want the open generic definition itself
```

**Optional Construction:**
```csharp
[SerializeField]
[SerializedTypeOptions(
    AllowGenericTypeConstruction = true, 
    AllowOpenGenerics = true)]
SerializedType<IRepository> optionalConstruction;

// Selecting Repository<> assigns it immediately
// A "▶ Construct" button appears to optionally construct it
```

### Type Kind Filtering

**Concrete Types Only (Default):**
```csharp
[SerializeField]
SerializedType<IWeapon> concreteOnly;
// Shows only concrete implementations (Sword, Bow)
// Excludes abstract classes and interfaces
```

**Include Abstract Classes:**
```csharp
[SerializeField]
[SerializedTypeOptions(AllowedTypeKinds = SerializedTypeKind.Concrete | SerializedTypeKind.Abstract)]
SerializedType<IWeapon> concreteAndAbstract;
// Shows concrete classes and abstract classes
```

**Interfaces Only:**
```csharp
[SerializeField]
[SerializedTypeOptions(AllowedTypeKinds = SerializedTypeKind.Interface)]
SerializedType<object> interfacesOnly;
// Shows only interface types
```

**All Types:**
```csharp
[SerializeField]
[SerializedTypeOptions(AllowedTypeKinds = 
    SerializedTypeKind.Concrete | 
    SerializedTypeKind.Abstract | 
    SerializedTypeKind.Interface)]
SerializedType<IWeapon> allTypes;
// Shows everything: concrete, abstract, and interfaces
```

### Custom Type Filtering

**Using Instance Method:**
```csharp
public class CustomFilterExample : MonoBehaviour {
    [SerializeField]
    [SerializedTypeOptions(CustomTypeFilter = nameof(GetAllowedTypes))]
    SerializedType<IWeapon> filteredWeapon;
    
    IEnumerable<Type> GetAllowedTypes() {
        return new Type[] { 
            typeof(Sword), 
            typeof(Bow) 
        };
    }
}
```

**Using Static Method:**
```csharp
[SerializeField]
[SerializedTypeOptions(CustomTypeFilter = "WeaponFilters.GetMeleeWeapons")]
SerializedType<IWeapon> meleeWeapon;

public static class WeaponFilters {
    public static IEnumerable<Type> GetMeleeWeapons() {
        return new Type[] { typeof(Sword), typeof(Axe) };
    }
}
```

### Self-Nesting Control

**Prevent Self-Nesting (Default):**
```csharp
[SerializeField]
[SerializedTypeOptions(AllowGenericTypeConstruction = true)]
SerializedType<IWrapper> noSelfNesting;
// Prevents: Wrapper<Wrapper<Wrapper<...>>>
```

**Allow Self-Nesting:**
```csharp
[SerializeField]
[SerializedTypeOptions(
    AllowGenericTypeConstruction = true, 
    AllowSelfNesting = true)]
SerializedType<IWrapper> selfNestingAllowed;
// Allows: Wrapper<Wrapper<int>>
```

### Drawer Modes

**Inline Mode (Default):**
```csharp
[SerializeField]
[SerializedTypeOptions(AllowGenericTypeConstruction = true)]
SerializedType<IRepository> inlineMode;
// Compact single-line with multiple dropdowns
// Best for most use cases
```

**Constructor Mode:**
```csharp
[SerializeField]
[SerializedTypeOptions(
    AllowGenericTypeConstruction = true,
    DrawerMode = SerializedTypeDrawerMode.Constructor)]
SerializedType<IRepository> constructorMode;
// Step-by-step nested UI
// Better for deeply nested generic types
```

### Real-World Patterns

**Damage System:**
```csharp
public interface IDamageEffect { }
public interface IElement { }

public class ElementalDamage<TElement> : IDamageEffect 
    where TElement : IElement { }

public class DamageCombo<TDamage1, TDamage2> : IDamageEffect 
    where TDamage1 : IDamageEffect 
    where TDamage2 : IDamageEffect { }

public class DamageSystem : MonoBehaviour {
    [SerializeField]
    [SerializedTypeOptions(AllowGenericTypeConstruction = true)]
    SerializedType<IDamageEffect> damageEffect;
    
    // Can construct: ElementalDamage<FireElement>
    // Or: DamageCombo<FireDamage, IceDamage>
}
```

**Repository Pattern:**
```csharp
public interface IRepository { }
public interface IData { }

public class Repository<TData> : IRepository 
    where TData : class, IData { }

public class CachedRepository<TData> : IRepository 
    where TData : class, IData { }

public class DataSystem : MonoBehaviour {
    [SerializeField]
    [SerializedTypeOptions(AllowGenericTypeConstruction = true)]
    SerializedType<IRepository> repository;
    
    // Can construct: Repository<PlayerData>
    // Or: CachedRepository<EnemyData>
}
```

**Strategy Pattern:**
```csharp
public interface IStrategy { }
public interface ICalculation { }
public interface IModifier { }

public class Strategy<TCalculation> : IStrategy 
    where TCalculation : ICalculation { }

public class ModifiedCalculation<TModifier> : ICalculation 
    where TModifier : IModifier { }

public class StrategySystem : MonoBehaviour {
    [SerializeField]
    [SerializedTypeOptions(AllowGenericTypeConstruction = true)]
    SerializedType<IStrategy> strategy;
    
    // Can construct: Strategy<DamageCalculation>
    // Or deeply nested: Strategy<ModifiedCalculation<CriticalModifier>>
}
```

## API Reference

### `SerializedType<TBase>`

| Member | Type | Description |
|--------|------|-------------|
| `HasType` | `bool` | Returns true if a valid type is set |
| `Type` | `Type?` | Gets the stored type, or null if none is set |

### `SerializedType`

| Member | Type | Description |
|--------|------|-------------|
| `HasType` | `bool` | Returns true if a valid type is set |
| `Type` | `Type?` | Gets the stored type, or null if none is set |

### `SerializedTypeOptionsAttribute`

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DrawerMode` | `SerializedTypeDrawerMode` | `Inline` | Drawer display mode |
| `AllowGenericTypeConstruction` | `bool` | `false` | Enable generic type construction UI |
| `AllowSelfNesting` | `bool` | `false` | Allow recursive type nesting |
| `AllowOpenGenerics` | `bool` | `false` | Allow open generic definitions |
| `AllowedTypeKinds` | `SerializedTypeKind` | `Concrete` | Types to show in dropdown |
| `CustomTypeFilter` | `string` | `""` | Member name returning type filter |
| `InheritsOrImplementsAll` | `Type[]?` | `null` | Types must inherit/implement ALL |
| `InheritsOrImplementsAny` | `Type[]?` | `null` | Types must inherit/implement ANY |

### `SerializedTypeKind` (Flags Enum)

| Value | Description |
|-------|-------------|
| `Concrete` | Non-abstract, non-interface classes |
| `Abstract` | Abstract classes |
| `Interface` | Interface types |

Use bitwise OR to combine: `Concrete | Abstract | Interface`

## Best Practices

1. **Choose the Right Drawer Mode**: Use Inline mode for most cases. Switch to Constructor mode only for deeply nested generics that are hard to read inline.

2. **Filter Appropriately**: Use `AllowedTypeKinds` and `CustomTypeFilter` to limit choices to relevant types. This improves usability and prevents errors.

3. **Consider Self-Nesting**: Keep `AllowSelfNesting` false unless you specifically need recursive structures.

4. **Open Generics**: Only enable `AllowOpenGenerics` when you need the generic definition itself, not a constructed type.

5. **Type Safety**: Prefer `SerializedType<TBase>` over `SerializedType` for better compile-time type safety.

6. **Runtime Usage**: Always check `HasType` before accessing `Type` to avoid null reference issues.

## Examples

For comprehensive examples and test cases, see:
- `SerializedTypeExample.cs` in the `Examples` folder

This example file demonstrates:
- All drawer modes and configurations
- Generic type construction scenarios
- Custom filtering patterns
- Real-world usage patterns (damage systems, repositories, strategies)
- Generic constraint handling

## Requirements

- Unity 6000.3 or later
- [Odin Inspector](https://odininspector.com/) (required for editor integration)

## License

MIT License - See LICENSE file for details.

