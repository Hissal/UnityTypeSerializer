# SerializedType Features Summary

## Overview
The `SerializedType<TBase>` system provides a powerful way to select and construct types at edit-time in Unity, with full support for generic type construction including infinite nesting depth.

## Core Features

### 1. **Basic Type Selection**
- Select any concrete type that implements the base interface
- Example: `SerializedType<IDamageEffect>` shows all concrete damage types

### 2. **Generic Type Construction**
- Enable with `[SerializedTypeOptions(includeGenericTypeDefinitions: true)]`
- Select open generic types (e.g., `Container<T>`)
- Fill in type arguments interactively
- Supports nested generics at any depth (e.g., `Wrapper<Container<ElementalDamage<FireElement>>>`)

### 3. **Constraint Validation**
- Automatically filters types based on generic parameter constraints
- Supports:
  - Interface constraints (e.g., `where T : IElement`)
  - Class constraints (e.g., `where T : class`)
  - Struct constraints (e.g., `where T : struct`)
  - new() constraints (blocks open generics)
  - Multiple constraints per parameter

### 4. **Self-Nesting Control**
- **Default (false)**: Prevents `Type<Type<Type<...>>>` structures
- **Enabled**: `[SerializedTypeOptions(allowSelfNesting: true)]`
  - Allows recursive type structures like `MegaWrapper<MegaWrapper<int>>`

### 5. **Open Generic Support**
- **Default (false)**: All type arguments must be concrete
- **Enabled**: `[SerializedTypeOptions(allowOpenGenerics: true)]`
  - Allows partially constructed generics like `MyType<T>`

### 6. **Type Filtering**

#### Unified Filtering via `CustomTypeFilter` String Resolver
```csharp
// Reference a method returning IEnumerable<Type>
[SerializedTypeOptions(CustomTypeFilter = nameof(GetIncludedTypes))]

// Reference a method returning SerializedTypeFilter (for include + exclude)
[SerializedTypeOptions(CustomTypeFilter = nameof(GetFilter))]

// Reference a static method on another type
[SerializedTypeOptions(CustomTypeFilter = "TypeName.MethodName")]
```

**Note**: Inclusion filters take precedence over normal base type filtering.

### 7. **Type Resolver Methods**
- Methods, properties, or fields that return `IEnumerable<Type>` or `SerializedTypeFilter`
- Format: `"TypeName.MemberName"` (static) or just `"MemberName"` (on declaring type, may be instance or static)
- Example:
```csharp
static IEnumerable<Type> GetIncludedTypes() =>
    new[] { typeof(FireDamage), typeof(IceDamage) };

static SerializedTypeFilter GetFilter() =>
    SerializedTypeFilter.Include(new[] { typeof(FireDamage) })
        .WithExclude(new[] { typeof(IceDamage) });

[SerializedTypeOptions(CustomTypeFilter = nameof(GetIncludedTypes))]
SerializedType<IDamageEffect> myField;
```

## UI Features

### Display Names
- Generic types show human-readable names
- Example: `Container<ElementalDamage<FireElement>>` instead of `Container\`1`
- Nested generics are fully expanded

### Nested Construction UI
- Hierarchical display with indentation for clarity
- Arrow indicators (↳) show nesting depth
- "Apply" and "Cancel" buttons for each nesting level
- Construct button (▶ Construct) for open generic arguments

### Debug Logging
- Use the "Log All Type Infos" button in the example script
- Shows selected types with full generic arguments
- Reports nesting depth

## Test Cases

The `SerializedTypeExample.cs` file includes comprehensive test cases:

1. **Basic Examples**
   - Concrete types only
   - Simple generic construction
   - Multiple generic parameters

2. **Nested Generics**
   - 1-level nesting
   - 2+ levels nesting
   - Complex multi-parameter nesting

3. **Advanced Constraints**
   - new() constraint testing
   - Multiple constraint combinations

4. **Real-World Patterns**
   - Repository pattern
   - Strategy pattern

5. **Extreme Nesting**
   - 4+ levels deep
   - Massive nested structures with `MegaWrapper5<T1, T2, T3, T4, T5>`

6. **Filtering Options**
   - Self-nesting enabled/disabled
   - Exclusion by array
   - Exclusion by resolver
   - Inclusion by array
   - Inclusion by resolver
   - Combined options

## Implementation Details

### Recursive Construction
- Uses path-based state management for infinite nesting depth
- Each nesting level is represented as a list of indices: `[0, 2, 1]`
- No hard-coded depth limit

### Performance
- Types are cached per field
- Constraint validation happens once during filtering
- UI updates are efficient with minimal allocations

### Serialization
- Only the final constructed type is serialized
- Construction state is editor-only
- Survives Unity reloads and recompilation

## Common Use Cases

### 1. Plugin/Mod System
```csharp
[SerializedTypeOptions(includeGenericTypeDefinitions: true)]
SerializedType<IPlugin> pluginType;
```

### 2. Damage System
```csharp
[SerializedTypeOptions(
    allowGenericTypeConstruction: true,
    CustomTypeFilter = nameof(GetDamageFilter))]
SerializedType<IDamageEffect> damageType;

static SerializedTypeFilter GetDamageFilter() =>
    SerializedTypeFilter.Exclude(new[] { typeof(DeprecatedDamage) });
```

### 3. Generic Factory
```csharp
[SerializedTypeOptions(
    allowGenericTypeConstruction: true,
    allowSelfNesting: false
)]
SerializedType<IFactory> factoryType;
```

### 4. Data Container Selection
```csharp
[SerializedTypeOptions(
    allowGenericTypeConstruction: true,
    CustomTypeFilter = "GetContainerTypes")]
SerializedType<IDataContainer> containerType;
```

## Limitations

1. **Abstract Types**: Excluded by default (cannot be instantiated)
2. **Interfaces**: Excluded by default (cannot be instantiated)
3. **new() Constraint**: Open generics are excluded when this constraint is present
4. **Constructed Generics**: Not shown in dropdown (must be built via construction UI)

## Future Enhancements

Potential additions:
- Visual tree view for complex nested structures
- Copy/paste type structures
- Type templates/presets
- Runtime type construction API
- Generic argument naming hints
