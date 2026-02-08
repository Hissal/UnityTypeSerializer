# TypeRef Drawer Modes

## Overview

TypeRef now supports two drawer modes:
1. **Inline Mode** (default) - Simple, single-line UI with multiple dropdowns
2. **Complex Constructor Mode** (opt-in) - Detailed step-by-step nested constructor UI

---

## Inline Mode (Default)

The inline mode displays the entire type on a single line with multiple dropdowns.

### Features
- ✅ Single-line layout
- ✅ One dropdown for base type
- ✅ One dropdown per generic argument
- ✅ Nested generics displayed inline
- ✅ Visual de-emphasis for generic arguments (smaller font)
- ✅ Clear validation errors above the field
- ✅ Fast workflow for common cases

### Usage

Inline mode is the **default**. Simply use TypeRef without any special configuration:

```csharp
[SerializeField]
[TypeRefOptions(allowGenericTypeConstruction: true)]
TypeRef<IMyInterface> myType;
```

### Inline Mode with Options

```csharp
// Allow generic construction (inline)
[SerializeField]
[TypeRefOptions(allowGenericTypeConstruction: true)]
TypeRef<IContainer> container;

// Allow open generics (inline)
[SerializeField]
[TypeRefOptions(allowOpenGenerics: true)]
TypeRef<IContainer> container;

// Both options (inline)
[SerializeField]
[TypeRefOptions(
    allowGenericTypeConstruction: true,
    allowOpenGenerics: true
)]
TypeRef<IContainer> container;
```

### Visual Example

For a type like `Container<ElementalDamage<FireElement>>`, inline mode shows:

```
[Container<T>] < [ElementalDamage<TElement>] < [FireElement] > >
```

All in one line, with each part being a clickable dropdown.

---

## Complex Constructor Mode (Opt-in)

The complex constructor mode provides a detailed, step-by-step UI for constructing generic types with nested expansion.

### Features
- ✅ Step-by-step construction
- ✅ Expandable nested generic arguments
- ✅ Type preview showing current construction state
- ✅ Detailed feedback for each level
- ✅ Apply/Cancel buttons for nested constructions
- ✅ Better for complex multi-parameter generics

### Usage

Enable complex constructor mode by setting `useComplexConstructor: true`:

```csharp
[SerializeField]
[TypeRefOptions(
    allowGenericTypeConstruction: true,
    useComplexConstructor: true
)]
TypeRef<IContainer> container;
```

### Complex Constructor with Options

```csharp
// Complex constructor with all options
[SerializeField]
[TypeRefOptions(
    allowGenericTypeConstruction: true,
    allowOpenGenerics: true,
    allowSelfNesting: true,
    useComplexConstructor: true
)]
TypeRef<IContainer> container;
```

### Visual Example

For `Container<ElementalDamage<FireElement>>`, complex constructor shows:

```
┌─────────────────────────────────────────────┐
│ Type Preview: Container<ElementalDamage<FireElement>> │
├─────────────────────────────────────────────┤
│ T [Select Type...]                          │
│   └─> ElementalDamage<TElement>             │
│       TElement [Select Type...]              │
│         └─> FireElement                      │
│       [Apply] [Cancel]                       │
│ [Construct Type] [Cancel]                   │
└─────────────────────────────────────────────┘
```

---

## When to Use Each Mode

### Use Inline Mode When:
- ✅ You understand the generic structure you want
- ✅ You want a quick, streamlined workflow
- ✅ You're working with simple 1-2 parameter generics
- ✅ You need to see all selections at once
- ✅ You want minimal UI overhead

### Use Complex Constructor Mode When:
- ✅ You need detailed feedback at each construction step
- ✅ You're working with deeply nested generics
- ✅ You prefer a guided, step-by-step approach
- ✅ You want to see type previews during construction
- ✅ You're constructing types with many generic parameters

---

## Option Compatibility

Both drawer modes respect all TypeRefOptions:

| Option | Inline Mode | Complex Mode |
|--------|-------------|--------------|
| `AllowGenericTypeConstruction` | ✅ Inline dropdowns | ✅ Nested UI |
| `AllowOpenGenerics` | ✅ Validation + errors | ✅ Visual feedback |
| `AllowSelfNesting` | ✅ Filter types | ✅ Filter types |
| `ExcludeTypes` | ✅ | ✅ |
| `IncludeTypes` | ✅ | ✅ |
| `ExcludeTypesResolver` | ✅ | ✅ |
| `IncludeTypesResolver` | ✅ | ✅ |

---

## Validation & Error Messages

Both modes provide clear error messages when the selected type violates the rules:

### Example: Open Generic Not Allowed

```csharp
[SerializeField]
[TypeRefOptions(allowGenericTypeConstruction: true)]  // But NOT allowOpenGenerics
TypeRef<IContainer> container;
```

If you try to leave the type as `Container<T>` (open generic):

**Inline Mode:**
```
⚠️ Open generic types are not allowed for this field.
   Type 'Container<T>' must be fully constructed.
```

**Complex Constructor Mode:**
```
⚠️ Cannot construct - one or more type arguments contain generic parameters.
   Enable AllowOpenGenerics to construct types with generic parameters.
```

---

## Migration Guide

If you're using the old TypeRef implementation:

### No Changes Needed
- Inline mode is the new default
- All existing TypeRef fields will automatically use inline mode
- No code changes required

### To Keep Old Behavior
If you prefer the complex constructor UI:

```csharp
// Change from this:
[TypeRefOptions(allowGenericTypeConstruction: true)]

// To this:
[TypeRefOptions(
    allowGenericTypeConstruction: true,
    useComplexConstructor: true
)]
```

---

## Examples

### Example 1: Simple Generic Construction (Inline)

```csharp
[SerializeField]
[TypeRefOptions(allowGenericTypeConstruction: true)]
TypeRef<IDamageEffect> damage;
```

**Workflow:**
1. Select `ElementalDamage<T>` from first dropdown
2. Select `FireElement` from second dropdown
3. Done! Type is `ElementalDamage<FireElement>`

### Example 2: Nested Generics (Inline)

```csharp
[SerializeField]
[TypeRefOptions(allowGenericTypeConstruction: true)]
TypeRef<IDamageEffect> damage;
```

**Workflow:**
1. Select `Container<T>` from first dropdown
2. Select `ElementalDamage<TElement>` from second dropdown
3. Select `FireElement` from third dropdown
4. Done! Type is `Container<ElementalDamage<FireElement>>`

### Example 3: Complex Nested Generics (Complex Constructor)

```csharp
[SerializeField]
[TypeRefOptions(
    allowGenericTypeConstruction: true,
    useComplexConstructor: true
)]
TypeRef<IDamageEffect> damage;
```

**Workflow:**
1. Select `MegaWrapper<T1, T2, T3, T4, T5>` from dropdown
2. For T1, select `Container<T>`
3. Click "▶ Construct" next to Container
4. For T, select `FireElement`
5. Click "Apply"
6. Repeat for T2-T5...
7. Click "Construct Type"

---

## Performance

- Both modes have identical runtime performance (zero overhead)
- Type filtering happens once during initialization
- Dropdowns are cached per field
- No GC allocations during normal usage

---

## API Reference

### TypeRefOptionsAttribute

```csharp
public TypeRefOptionsAttribute(
    bool allowGenericTypeConstruction = false,
    bool allowSelfNesting = false,
    bool allowOpenGenerics = false,
    bool useComplexConstructor = false)
```

#### Parameters

- `allowGenericTypeConstruction` - Enables generic type construction UI
- `allowSelfNesting` - Allows recursive types like `Wrapper<Wrapper<T>>`
- `allowOpenGenerics` - Allows open generics like `Container<T>` as final result
- `useComplexConstructor` - **NEW**: Uses complex constructor instead of inline mode (default: false)

#### Properties

- `ExcludeTypes` - Types to exclude from dropdown
- `ExcludeTypesResolver` - Method/property that returns types to exclude
- `IncludeTypes` - Only these types appear in dropdown
- `IncludeTypesResolver` - Method/property that returns types to include

---

## Troubleshooting

### Issue: "I don't see the inline dropdowns"

**Solution:** Make sure `allowGenericTypeConstruction` is enabled:
```csharp
[TypeRefOptions(allowGenericTypeConstruction: true)]
```

### Issue: "I want the old UI back"

**Solution:** Enable complex constructor mode:
```csharp
[TypeRefOptions(
    allowGenericTypeConstruction: true,
    useComplexConstructor: true
)]
```

### Issue: "Error: Open generic types are not allowed"

**Solution:** Enable `allowOpenGenerics`:
```csharp
[TypeRefOptions(
    allowGenericTypeConstruction: true,
    allowOpenGenerics: true
)]
```

### Issue: "I can't nest the same type inside itself"

**Solution:** Enable `allowSelfNesting`:
```csharp
[TypeRefOptions(
    allowGenericTypeConstruction: true,
    allowSelfNesting: true
)]
```

---

## Summary

- ✅ Inline mode is the **new default** - fast and streamlined
- ✅ Complex constructor is **opt-in** - detailed and guided
- ✅ Both modes respect all TypeRefOptions
- ✅ Clear validation errors in both modes
- ✅ No breaking changes - existing code works as-is
- ✅ Set `useComplexConstructor: true` to use the old UI
