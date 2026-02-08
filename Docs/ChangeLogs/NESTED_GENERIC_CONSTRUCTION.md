# SerializedType - Nested Generic Type Construction

## Overview

The SerializedType drawer now supports **nested generic type construction**, allowing you to select open generic types as type arguments and then construct them recursively.

## How It Works

### Example Scenario

You have these types:
```csharp
public class Container<T> : IMyInterface where T : class, IElement { }
public class ElementFire<TData> : IElement { }
```

### Step-by-Step Construction

#### Step 1: Select the Outer Generic Type
```
Select Type: Container<T>
```

#### Step 2: Type Constructor UI Appears
```
Constructing: Container<T>

T (where T : class, IElement)  [Select Type...]

[Construct Type]  [Cancel]
```

#### Step 3: Select an Open Generic Type
Click `[Select Type...]` and choose `ElementFire<TData>`

```
Constructing: Container<T>

T (where T : class, IElement)  [ElementFire<TData>]  [▶ Construct]

[Construct Type]  [Cancel]
```

Notice the **`[▶ Construct]`** button appears because `ElementFire<TData>` is an open generic.

#### Step 4: Click `[▶ Construct]` to Expand Nested Construction
```
Constructing: Container<T>

T (where T : class, IElement)  [ElementFire<TData>]  [▶ Construct]

    ↳ Constructing: ElementFire<TData>
    
        TData   [Select Type...]
        
        [Apply]  [Cancel]

[Construct Type]  [Cancel]
```

The UI expands inline to show the nested generic's type parameters.

#### Step 5: Select Type for Nested Parameter
Click on `[Select Type...]` for `TData` and select, for example, `int`.

```
Constructing: Container<T>

T (where T : class, IElement)  [ElementFire<int>]  [▶ Construct]

[Construct Type]  [Cancel]
```

After selecting, the nested constructor automatically applies and collapses, showing the constructed type `ElementFire<int>`.

#### Step 6: Construct the Final Type
Click `[Construct Type]` to create `Container<ElementFire<int>>`.

## UI Features

### Inline Expansion
- Nested generic construction happens **inline** within the parent constructor
- Uses indentation (↳) to show hierarchy
- Automatically collapses after successful construction

### Visual Indicators
- **`[▶ Construct]` button** appears next to open generic types
- **Indented section** shows nested construction UI
- **Bold labels** for main type, **mini labels** for nested types

### Smart Behavior
- The "Construct Type" button is **disabled** until all parameters are concrete (non-generic) types
- Clicking "Cancel" in nested constructor closes the expansion without clearing the selection
- Auto-applies nested construction when all nested parameters are selected

## Constraints

### Nested Type Constraints
Each nested generic's type parameters are **independently filtered** by their constraints:

```csharp
public class Wrapper<T> : IMyInterface where T : IElement { }
public class Element<TData> : IElement where TData : struct { }
```

When constructing `Wrapper<Element<TData>>`:
1. `T` must implement `IElement` → Shows `Element<TData>` ✅
2. When expanding `Element<TData>`, `TData` must be `struct` → Shows only value types

### Limitations
- **No new() constraint for open generics**: Open generic types are excluded when the parent parameter has a `new()` constraint
- **Single-level expansion**: Only one nested constructor is shown at a time (but you can construct deeper by applying in steps)

## Example Types for Testing

```csharp
// Outer generic
public class Container<T> : IMyInterface where T : class, IElement { }

// Nested generics
public class ElementFire<TData> : IElement { }
public class ElementWater<T1, T2> : IElement { }

// Results you can create:
// - Container<ElementFire<int>>
// - Container<ElementFire<string>>
// - Container<ElementWater<int, float>>
```

## Complete Flow Diagram

```
1. Select Container<T>
   ↓
2. UI shows: T [Select Type...]
   ↓
3. Select ElementFire<TData> (open generic)
   ↓
4. UI shows: T [ElementFire<TData>] [▶ Construct]
   ↓
5. Click [▶ Construct]
   ↓
6. Nested UI expands:
   ↳ Constructing: ElementFire<TData>
       TData [Select Type...]
       [Apply] [Cancel]
   ↓
7. Select int for TData
   ↓
8. Nested UI auto-applies and collapses
   ↓
9. UI shows: T [ElementFire<int>] [▶ Construct]
   ↓
10. Click [Construct Type]
   ↓
11. Result: Container<ElementFire<int>>
```

## Benefits

✅ **Full generic depth support**: Construct types with arbitrary nesting levels
✅ **Inline workflow**: No separate windows or complex navigation
✅ **Constraint-aware**: Each level respects its own constraints
✅ **Visual hierarchy**: Clear indentation shows nesting structure
✅ **Smart auto-apply**: Nested constructors apply automatically when complete

This feature enables powerful type composition directly in the Unity Inspector!
