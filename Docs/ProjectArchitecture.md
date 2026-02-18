# Project Architecture

**Last Updated:** February 12, 2026

This document describes the architecture of the UnityTypeSerializer project. It covers key architectural concepts, design patterns, and component relationships to help maintainers and AI agents understand and work with the codebase effectively.

---

## Overview

UnityTypeSerializer provides a robust system for serializing and selecting .NET types in the Unity Inspector. The architecture follows a layered design where:

1. **Runtime types** (`SerializedType`, `SerializedType<TBase>`) handle serialization
2. **Editor drawers** provide Inspector UI for type selection
3. **Shared core logic** centralizes common functionality
4. **Value accessors** abstract differences between generic and non-generic types

---

## Core Components

### 1. SerializedType - The Central Serialization Class

`SerializedType` is the foundation of the project. It provides two variants:

#### Generic Version: `SerializedType<TBase>`
- **Purpose**: Serialize types constrained to a base class or interface
- **Location**: `Runtime/SerializedType.cs`
- **Key Features**:
  - Generic constraint ensures type safety: `where TBase : class`
  - Only types that inherit from or implement `TBase` can be selected
  - Stores the type as an assembly-qualified name (AQN)
  - Provides `HasType` and `Type` properties for runtime access

**Example**:
```csharp
[SerializeField]
SerializedType<IDamageEffect> damageType;
```

#### Non-Generic Version: `SerializedType`
- **Purpose**: Serialize any type without constraints (equivalent to `SerializedType<object>`)
- **Location**: `Runtime/SerializedType.cs`
- **Key Features**:
  - Accepts any type in the project
  - Same API as generic version
  - Use `SerializedTypeOptionsAttribute` for filtering

**Example**:
```csharp
[SerializeField]
SerializedType anyType;
```

#### Common Implementation
Both versions:
- Store types as assembly-qualified names (AQN) for serialization
- Use `[SerializeField]` on internal `aqn` field for Unity serialization
- Mark as `[Serializable]` and `[InlineProperty]` (Odin Inspector)
- Provide `Type?` property that resolves AQN to `System.Type` at runtime
- Set type via internal setter: `Type { get; internal set; }`

---

### 2. SerializedTypeOptionsAttribute - Configuration & Filtering

`SerializedTypeOptionsAttribute` controls Inspector behavior and type filtering.

**Location**: `Runtime/SerializedTypeOptionsAttribute.cs`

#### Key Properties

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `DrawerMode` | `SerializedTypeDrawerMode` | `Inline` | Chooses between Inline and Constructor drawer modes |
| `AllowGenericTypeConstruction` | `bool` | `false` | Enables UI for constructing closed generic types (e.g., `List<>` → `List<int>`) |
| `AllowSelfNesting` | `bool` | `false` | Allows recursive generic nesting (e.g., `Container<Container<T>>`) |
| `AllowOpenGenerics` | `bool` | `false` | Allows assigning open generic type definitions without construction |
| `AllowedTypeKinds` | `SerializedTypeKind` | `Concrete` | Controls which kinds of types appear (Concrete, Abstract, Interface) |
| `CustomTypeFilter` | `string` | `""` | Name of member returning `SerializedTypeFilter` or `IEnumerable<Type>` |
| `InheritsOrImplementsAll` | `Type[]?` | `null` | Types must satisfy ALL these constraints (AND logic) |
| `InheritsOrImplementsAny` | `Type[]?` | `null` | Types must satisfy AT LEAST ONE constraint (OR logic) |
| `OnTypeChanged` | `string` | `""` | Name of a parameterless instance callback invoked after selected type changes |

#### Property-Based Configuration
Uses modern C# init-only properties instead of constructor parameters:
```csharp
[SerializedTypeOptions(
    DrawerMode = SerializedTypeDrawerMode.Constructor,
    AllowGenericTypeConstruction = true,
    AllowSelfNesting = true
)]
```

#### Filter Resolution
The `CustomTypeFilter` property accepts:
- Simple member name: `"GetFilter"` (searches declaring type)
- Qualified member name: `"TypeName.GetFilter"` (explicit type reference)
- Resolved by `SerializedTypeDrawerCore.ResolveSerializedTypeFilter()`
- Supports static or instance members (methods, properties, fields)

---

### 3. Drawer Architecture - Separation of Concerns

The drawer architecture follows a **layered delegation pattern** with clear separation between:
1. **Thin Odin wrapper drawers** (entry points)
2. **Shared core logic** (type discovery, filtering, validation)
3. **Mode-specific implementations** (UI rendering strategies)

#### Component Hierarchy

```
┌─────────────────────────────────────────────────────────┐
│  Odin Entry Point Drawers                               │
│  SerializedTypeDrawer<TBase> / SerializedTypeDrawer    │
│  - Thin wrappers over Odin's OdinValueDrawer            │
│  - One drawer per SerializedType variant                │
└─────────────────────────┬───────────────────────────────┘
                          │ delegates to
┌─────────────────────────▼───────────────────────────────┐
│  SerializedTypeDrawerCore (Static Utility Class)        │
│  - RefreshAvailableTypes() - Type discovery & filtering │
│  - CreateDrawerImplementation() - Factory method        │
│  - ResolveSerializedTypeFilter() - Filter resolution    │
│  - SatisfiesGenericParameterConstraints() - Validation  │
└─────────────────────────┬───────────────────────────────┘
                          │ creates
┌─────────────────────────▼───────────────────────────────┐
│  ISerializedTypeDrawerImplementation                    │
│  (Drawer Mode Implementations)                          │
│                                                          │
│  ┌─────────────────────────────────────────────────┐   │
│  │ SerializedTypeDrawerInlineMode                  │   │
│  │ - Simple dropdown with inline generic UI        │   │
│  └─────────────────────────────────────────────────┘   │
│                                                          │
│  ┌─────────────────────────────────────────────────┐   │
│  │ SerializedTypeDrawerConstructorMode             │   │
│  │ - Expanded UI with nested type construction     │   │
│  └─────────────────────────────────────────────────┘   │
└──────────────────────────────────────────────────────────┘
```

---

### 4. SerializedTypeDrawerCore - Shared Functionality Hub

`SerializedTypeDrawerCore` is a static class containing all shared drawer logic.

**Location**: `Editor/SerializedTypeDrawerCore.cs`

#### Core Responsibilities

**1. Type Discovery and Filtering**
```csharp
public static List<Type> RefreshAvailableTypes(
    Type baseConstraint,
    SerializedTypeOptionsAttribute? options,
    InspectorProperty property)
```
- Uses Unity's `TypeCache.GetTypesDerivedFrom()` for performance
- Applies `AllowedTypeKinds` filtering (Concrete/Abstract/Interface)
- Resolves and applies `CustomTypeFilter` include/exclude rules
- Handles `InheritsOrImplementsAll` and `InheritsOrImplementsAny` constraints
- Decides whether to include generic type definitions based on options

**2. Drawer Factory**
```csharp
public static ISerializedTypeDrawerImplementation CreateDrawerImplementation(
    InspectorProperty property,
    ISerializedTypeValueAccessor accessor,
    SerializedTypeOptionsAttribute? options,
    List<Type> availableTypes)
```
- Selects drawer implementation based on `DrawerMode`
- Returns `SerializedTypeDrawerInlineMode` or `SerializedTypeDrawerConstructorMode`

**3. Generic Constraint Validation**
```csharp
public static GenericConstraintCheckResult CheckGenericParameterConstraints(
    Type candidateType,
    Type[] genericParameterConstraints)
```
- Validates generic arguments against parameter constraints
- Checks class/struct/new() constraints
- Validates base class and interface constraints
- Returns structured result: `ShowInDropdown` and `IsValidArgument` booleans
- Defers "VisibleButInvalid" for open generics with `new()` constraint

**4. Filter Resolution**
```csharp
private static SerializedTypeFilter? ResolveSerializedTypeFilter(
    string? filterMemberName,
    InspectorProperty property)
```
- Resolves string member names to actual filter objects
- Supports both `SerializedTypeFilter` and `IEnumerable<Type>` return types
- Handles static and instance members
- Uses reflection with caching for performance

---

### 5. Drawer Entry Points - Generic and Non-Generic

Each `SerializedType` variant has its own **thin Odin drawer** serving as an entry point.

**Location**: `Editor/SerializedTypeDrawer.cs`

#### Generic Entry Point: `SerializedTypeDrawer<TBase>`
```csharp
public sealed class SerializedTypeDrawer<TBase> : OdinValueDrawer<SerializedType<TBase>> 
    where TBase : class
```

**Responsibilities**:
- Derives from `OdinValueDrawer<SerializedType<TBase>>`
- Creates `SerializedTypeValueAccessor<TBase>` to wrap `PropertyValueEntry`
- Delegates all logic to `SerializedTypeDrawerCore`
- Minimal code - just initialization and delegation

#### Non-Generic Entry Point: `SerializedTypeDrawer`
```csharp
public sealed class SerializedTypeDrawer : OdinValueDrawer<SerializedType>
```

**Responsibilities**:
- Derives from `OdinValueDrawer<SerializedType>`
- Creates `SerializedTypeValueAccessor` (non-generic) to wrap `PropertyValueEntry`
- Delegates all logic to `SerializedTypeDrawerCore`
- Identical structure to generic version

#### Shared Initialization Pattern
Both drawers follow the same pattern:
```csharp
protected override void Initialize() {
    base.Initialize();
    
    var options = Property.GetAttribute<SerializedTypeOptionsAttribute>();
    var accessor = new SerializedTypeValueAccessor[<TBase>](ValueEntry);
    var availableTypes = SerializedTypeDrawerCore.RefreshAvailableTypes(
        accessor.BaseConstraint, options, Property
    );
    
    drawerImplementation = SerializedTypeDrawerCore.CreateDrawerImplementation(
        Property, accessor, options, availableTypes
    );
}
```

---

### 6. SerializedTypeValueAccessor - Unified Value Access

`ISerializedTypeValueAccessor` abstracts the differences between generic and non-generic `SerializedType` properties, allowing drawer implementations to work with both uniformly.

**Location**: `Editor/SerializedTypeValueAccessor.cs`

#### Interface Definition
```csharp
internal interface ISerializedTypeValueAccessor {
    Type? GetSelectedType();
    void SetSelectedType(Type? type);
    void ApplyChanges();
    Type BaseConstraint { get; }
}
```

#### Why This Abstraction Exists

Odin Inspector uses generic `PropertyValueEntry<T>` for different property types. Without abstraction, drawer implementations would need separate code paths for:
- `PropertyValueEntry<SerializedType>` (non-generic)
- `PropertyValueEntry<SerializedType<TBase>>` (generic)

**Value accessor solves this** by:
1. Wrapping the `PropertyValueEntry<T>` in a non-generic interface
2. Providing uniform `GetSelectedType()` / `SetSelectedType()` methods
3. Exposing `BaseConstraint` (typeof(object) for non-generic, typeof(TBase) for generic)
4. Enabling drawer implementations to be **mode-specific** rather than **type-specific**

#### Concrete Implementations

**Non-Generic**: `SerializedTypeValueAccessor`
```csharp
internal sealed class SerializedTypeValueAccessor : ISerializedTypeValueAccessor {
    readonly PropertyValueEntry<SerializedType> valueEntry;
    public Type BaseConstraint => typeof(object);
    // ... GetSelectedType, SetSelectedType, ApplyChanges
}
```

**Generic**: `SerializedTypeValueAccessor<TBase>`
```csharp
internal sealed class SerializedTypeValueAccessor<TBase> : ISerializedTypeValueAccessor 
    where TBase : class {
    readonly PropertyValueEntry<SerializedType<TBase>> valueEntry;
    public Type BaseConstraint => typeof(TBase);
    // ... GetSelectedType, SetSelectedType, ApplyChanges
}
```

---

### 7. Drawer Implementations - Mode-Specific UI Logic

Drawer implementations are **strategy classes** that define how the Inspector UI is rendered. They implement `ISerializedTypeDrawerImplementation`.

**Location**: `Editor/ISerializedTypeDrawerImplementation.cs` (interface)

#### Interface
```csharp
internal interface ISerializedTypeDrawerImplementation {
    void DrawPropertyLayout(GUIContent label);
}
```

#### Two Modes

**Inline Mode**: `SerializedTypeDrawerInlineMode`
- **Location**: `Editor/SerializedTypeDrawerInlineMode.cs`
- **UI Style**: Compact dropdown with inline generic construction
- **Use Case**: Simple type selection, basic generic construction
- **Features**:
  - Single dropdown for type selection
  - Inline generic parameter selection (if enabled)
  - Compact and space-efficient

**Constructor Mode**: `SerializedTypeDrawerConstructorMode`
- **Location**: `Editor/SerializedTypeDrawerConstructorMode.cs`
- **UI Style**: Expanded UI with nested foldouts and construction buttons
- **Use Case**: Complex generic construction, deep nesting
- **Features**:
  - Separate "Construct" button for generic types
  - Foldout UI for nested generic parameters
  - Supports arbitrarily deep generic nesting
  - More visual hierarchy

#### Implementation Rules

**What drawer implementations should contain**:
- ✅ Mode-specific UI rendering logic
- ✅ Layout and visual presentation
- ✅ User interaction handling (dropdown, buttons)
- ✅ Mode-specific state management

**What drawer implementations should NOT contain**:
- ❌ Type discovery and filtering (use `SerializedTypeDrawerCore`)
- ❌ Generic constraint validation (use `SerializedTypeDrawerCore`)
- ❌ Filter resolution (use `SerializedTypeDrawerCore`)
- ❌ Value accessor creation (handled by entry point drawers)

**Golden Rule**: If logic could be shared between drawer modes, it belongs in `SerializedTypeDrawerCore`, not in implementations.

---

## Architecture Principles

### 1. Single Responsibility
- **Entry point drawers**: Thin Odin wrappers, initialization only
- **SerializedTypeDrawerCore**: Shared logic (filtering, validation, factories)
- **Value accessors**: Generic/non-generic abstraction
- **Drawer implementations**: Mode-specific UI rendering only

### 2. Delegation Over Duplication
- Entry point drawers delegate to core
- Core delegates to implementations
- Implementations call core utilities for shared operations
- No logic is duplicated across drawer types

### 3. Interface Segregation
- `ISerializedTypeValueAccessor`: Value access abstraction
- `ISerializedTypeDrawerImplementation`: UI rendering strategy
- Each interface has a single, focused purpose

### 4. Strategy Pattern
- Drawer modes implemented as separate strategy classes
- Factory method in core selects appropriate strategy
- Easy to add new drawer modes without modifying existing code

---

## Common Patterns

### Pattern 1: Adding a New Drawer Mode

1. Create new class implementing `ISerializedTypeDrawerImplementation`
2. Add new enum value to `SerializedTypeDrawerMode`
3. Update factory method in `SerializedTypeDrawerCore.CreateDrawerImplementation()`
4. Implement `DrawPropertyLayout()` with mode-specific UI logic
5. Use `SerializedTypeDrawerCore` utilities for type operations

### Pattern 2: Adding New Filtering Logic

1. Add new property to `SerializedTypeOptionsAttribute` (if needed)
2. Implement filtering logic in `SerializedTypeDrawerCore.RefreshAvailableTypes()`
3. Use existing constraint checking utilities where possible
4. Update documentation

### Pattern 3: Supporting New Value Types

1. Create new `SerializedType` variant (if needed)
2. Implement `ISerializedTypeValueAccessor` for new variant
3. Create new entry point drawer deriving from `OdinValueDrawer<T>`
4. Follow initialization pattern from existing drawers
5. No changes needed to core or implementations

---

## Key Architectural Decisions

### Why Two SerializedType Variants?
- **Generic version**: Type safety at compile time, constrains selectable types
- **Non-generic version**: Maximum flexibility, any type allowed
- Same API surface and serialization format for consistency

### Why SerializedTypeValueAccessor?
- Allows drawer implementations to be **type-agnostic** (work with both generic and non-generic SerializedType)
- Reduces code duplication (one implementation per drawer mode, not per SerializedType variant)
- Simplifies adding new SerializedType variants in the future

### Why Static SerializedTypeDrawerCore?
- Shared utilities don't need instance state
- Easy to call from any drawer or implementation
- Clear namespace: `SerializedTypeDrawerCore.Method()`

### Why Separate Drawer Modes?
- Different use cases have different UX needs
- Inline mode optimizes for space and simplicity
- Constructor mode optimizes for complex nested generics
- Strategy pattern allows clean separation and future extensibility

---

## File Organization

```
UnityTypeSerializer/
├── Runtime/
│   ├── SerializedType.cs                      # Core serialization classes
│   ├── SerializedTypeOptionsAttribute.cs       # Configuration attribute
│   ├── SerializedTypeDrawerMode.cs             # Drawer mode enum
│   ├── SerializedTypeKind.cs                   # Type kind enum
│   └── SerializedTypeFilter.cs                 # Filter data structure
├── Editor/
│   ├── SerializedTypeDrawer.cs                 # Entry point drawers (thin wrappers)
│   ├── SerializedTypeDrawerCore.cs             # Shared core logic (utilities)
│   ├── SerializedTypeValueAccessor.cs          # Value accessor interface + implementations
│   ├── ISerializedTypeDrawerImplementation.cs  # Drawer strategy interface
│   ├── SerializedTypeDrawerInlineMode.cs       # Inline mode implementation
│   └── SerializedTypeDrawerConstructorMode.cs  # Constructor mode implementation
└── Examples/
    └── SerializedTypeExample.cs                # Usage examples
```

---

## Summary

The UnityTypeSerializer architecture is designed around **clear separation of concerns**:

1. **SerializedType** handles runtime serialization (stores AQN, resolves to Type)
2. **SerializedTypeOptionsAttribute** provides declarative configuration
3. **Entry point drawers** are thin Odin wrappers (minimal logic)
4. **SerializedTypeDrawerCore** centralizes all shared logic (filtering, validation, factories)
5. **SerializedTypeValueAccessor** abstracts generic vs. non-generic differences
6. **Drawer implementations** focus purely on mode-specific UI rendering

This architecture enables:
- ✅ Easy addition of new drawer modes (strategy pattern)
- ✅ Easy addition of new SerializedType variants (value accessor abstraction)
- ✅ Minimal code duplication (centralized shared logic)
- ✅ Clear responsibilities (single purpose per component)
- ✅ Type-safe generic support with non-generic fallback

When extending the system, follow the existing patterns and always ask: **"Is this logic specific to one drawer mode, or could it be shared?"** If it can be shared, it belongs in `SerializedTypeDrawerCore`.
