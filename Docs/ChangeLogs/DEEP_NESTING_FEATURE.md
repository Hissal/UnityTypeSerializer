# Deep Nesting Feature for SerializedType

## Overview
The SerializedType drawer now supports **infinite recursive nesting** of generic types, allowing you to construct extremely complex nested structures like:

```
MegaWrapper<
    OuterMost<
        Wrapper<
            Container<
                ElementalDamage<FireElement>
            >
        >
    >
>
```

## What Was Added

### 1. Generic Type Selection at All Levels
Previously, nested type selections (level 2+) could only choose concrete types. Now, open generic types can be selected at any nesting depth.

**Before:**
```
Level 1: Can select Wrapper<T> ✅
Level 2: Can select FireElement ✅ BUT NOT Container<T> ❌
```

**After:**
```
Level 1: Can select Wrapper<T> ✅
Level 2: Can select Container<T> ✅
Level 3: Can select ElementalDamage<T> ✅
Level 4: Can select FireElement ✅
... infinite levels supported
```

### 2. Recursive Construction UI
Added "▶ Construct" buttons that appear when you select an open generic type at any level. Clicking these buttons expands the type to allow configuration of its type arguments.

### 3. Visual Hierarchy
The UI shows the nesting depth with visual indicators:
- Level 2: `  ↳ Constructing: Wrapper<T>`
- Level 3: `    ↳↳ Constructing: Container<T>`
- Each level is indented and uses helpboxes for visual grouping

## How to Use

### Basic Workflow

1. **Select an open generic at top level**
   ```
   SerializedType Field: [Dropdown ▼] → Select "MegaWrapper<T1, T2, T3>"
   ```

2. **UI shows construction interface**
   ```
   Constructing: MegaWrapper<T1, T2, T3>
   
   T1 (where T1 : IDamageEffect) [Select Type... ▼]
   T2 (where T2 : IDamageEffect) [Select Type... ▼]
   T3 (where T3 : IDamageEffect) [Select Type... ▼]
   
   [Construct Type] [Cancel]
   ```

3. **Click a parameter dropdown and select another generic**
   ```
   T1 (where T1 : IDamageEffect) [Wrapper<T> ▼] [▶ Construct]
   ```

4. **Click "▶ Construct" to expand**
   ```
   T1 (where T1 : IDamageEffect) [Wrapper<T> ▼] [▶ Construct]
       ↳ Constructing: Wrapper<T>
       
       T (where T : IDamageEffect) [Select Type... ▼]
       
       [Apply] [Cancel]
   ```

5. **Continue nesting as deep as needed**
   ```
   T1 [Wrapper<T>] [▶ Construct]
       ↳ Constructing: Wrapper<T>
       
       T [Container<T>] [▶ Construct]
           ↳↳ Constructing: Container<T>
           
           T [ElementalDamage<T>] [▶ Construct]
               ↳↳↳ Constructing: ElementalDamage<TElement>
               
               TElement (where TElement : IElement) [FireElement ▼]
               
               [Apply] [Cancel]
   ```

6. **Work from innermost to outermost**
   - Configure innermost type → Click "Apply"
   - It collapses back to parent level with the constructed type
   - Continue with next level up
   - Repeat until all levels are configured

## Technical Implementation

### Key Components

1. **`GenericConstructionState` Class**
   - Manages state for constructing nested generic types at arbitrary depth
   - Uses a **path-based approach** to support infinite nesting:
     - Path represented as `List<int>`: `[0, 2, 1]` = root arg 0 → nested arg 2 → nested arg 1
     - `argumentCache: Dictionary<string, Type?[]>` - Stores type arguments for each path
     - `expandedArgument: Dictionary<string, int?>` - Tracks which argument is expanded at each path
   - Methods:
     - `GetArguments(path)` / `SetArguments(path, args)` - Manage cached type arguments
     - `GetExpandedIndex(path)` / `SetExpandedIndex(path, index)` - Track expansion state
     - `ClearPath(path)` - Recursively clear a path and all its children

2. **`DrawGenericConstructorRecursive(openGenericType, path, depth, targetArgsArray)`**
   - **Single method handles ALL nesting levels** (no depth limit!)
   - Parameters:
     - `path: List<int>` - The path to this constructor (e.g., `[0, 2]` for root arg 0, nested arg 2)
     - `depth: int` - Current nesting depth (used only for indentation/UI)
     - `targetArgsArray: Type?[]` - The parent's argument array to update
   - Recursive behavior:
     - When user clicks "▶ Construct" on an open generic, calls itself with extended path
     - When user clicks "Apply", constructs the type and updates parent's cache
     - Keeps state in `GenericConstructionState` across calls
   - Visual hierarchy:
     - Uses depth for indentation: `new string(' ', depth * 20)`
     - Arrow indicators: `new string('↳', depth)` shows nesting level

3. **`ShowTypeArgumentSelectorRecursive(path, argIndex, genericParameter, targetArgsArray)`**
   - Handles type selection for generic parameters at **any nesting depth**
   - Validates constraints against open generic types:
     - For concrete types: normal `IsAssignableFrom` checks
     - For generic type definitions: checks if interfaces could satisfy constraints
   - Updates the cache at the specified path when selection is made

### How Infinite Nesting Works

The key insight is the **path-based state management**:

```csharp
// Root level: path = []
DrawGenericConstructorRecursive(MegaWrapper<T1, T2>, [], 0, selectedTypeArguments)

// User selects Container<T> for T1, clicks "▶ Construct"
// Nested level 1: path = [0]
DrawGenericConstructorRecursive(Container<T>, [0], 1, selectedTypeArguments)

// User selects ElementalDamage<T> for T, clicks "▶ Construct"
// Nested level 2: path = [0, 0]
DrawGenericConstructorRecursive(ElementalDamage<T>, [0, 0], 2, args_at_path_0)

// User selects FireElement for T, clicks "Apply"
// Constructs: ElementalDamage<FireElement>
// Updates cache at path [0, 0], collapses back to path [0]

// ... continues recursively for any depth
```

Each recursive call has its own path, so the system can track state at unlimited depth.

### Constraint Handling for Generic Types

When selecting an open generic type for a constrained parameter, the drawer validates that the generic's interfaces could satisfy the constraints:

```csharp
// Example: T where T : IDamageEffect
// Can select: ElementalDamage<T> because it implements IDamageEffect
// Cannot select: Repository<T> because it implements IRepository, not IDamageEffect

if (t.IsGenericTypeDefinition) {
    var interfaces = t.GetInterfaces();
    foreach (var constraint in constraints) {
        if (constraint.IsGenericType) {
            // Check if any interface matches the constraint's generic definition
            var constraintDef = constraint.GetGenericTypeDefinition();
            satisfies = interfaces.Any(i => 
                i.IsGenericType && 
                i.GetGenericTypeDefinition() == constraintDef
            );
        }
        // ... other validation
    }
}
```

## Practical Limits

While the system **technically supports infinite nesting** through a fully recursive approach, there are practical considerations:

1. **UI Complexity**: Each level adds indentation and nested boxes. Beyond 4-5 levels, the UI becomes very wide and hard to read.

2. **Performance**: Each nested level requires type reflection and constraint validation. Very deep nesting (10+ levels) may have noticeable lag.

3. **Current Implementation**: The code uses a **single recursive method** (`DrawGenericConstructorRecursive`) that handles **all nesting depths**. This means:
   - There is no hard limit on nesting depth
   - The same method draws level 1, 2, 3, 4, 5... infinitely
   - State is managed through a path-based system (e.g., `[0, 2, 1]` represents root arg 0 → nested arg 2 → nested arg 1)
   - Each path can be arbitrarily long, supporting unlimited nesting

**Recommendation**: Use 2-3 levels for practical game development. More than that likely indicates an overly complex type structure.

## Example Use Cases

### 1. Damage System with Elements
```csharp
Container<ElementalDamage<FireElement>>
```
- Container provides pooling/management
- ElementalDamage provides damage calculation
- FireElement provides specific fire behavior

### 2. Multi-Element Fusion
```csharp
ComplexWrapper<
    DualElementDamage<FireElement, IceElement>,
    DualElementDamage<LightningElement, PoisonElement>
>
```
- Supports two different fusion types
- Each fusion has two elements

### 3. Strategy Pattern with Data
```csharp
Strategy<Repository<PlayerData<HealthStat>>>
```
- Strategy pattern for behavior
- Repository for data access
- PlayerData as the data structure
- HealthStat as the specific stat type

## Testing

Test cases to verify functionality:

```csharp
// 2-Level Nesting
Container<ElementalDamage<FireElement>>
Container<DualElementDamage<FireElement, IceElement>>

// 3-Level Nesting
Wrapper<Container<ElementalDamage<FireElement>>>
OuterMost<Container<DualElementDamage<FireElement, IceElement>>>

// 4-Level Nesting (Maximum practical depth)
MegaWrapper<Wrapper<Container<ElementalDamage<FireElement>>>>
OuterMost<Wrapper<Container<DualElementDamage<FireElement, IceElement>>>>

// Complex Multi-Parameter at Multiple Levels
ComplexWrapper<
    DualElementDamage<FireElement, IceElement>,
    TripleElementDamage<LightningElement, PoisonElement, HolyElement>
>
```

## Future Enhancements

Potential improvements:

1. **Fully Recursive Implementation**: Replace level-specific methods with a single recursive method that handles arbitrary depth
2. **Horizontal Layout Option**: For very deep nesting, use a breadcrumb-style UI instead of vertical nesting
3. **Preset Templates**: Save/load commonly used nested generic configurations
4. **Visual Graph View**: Show the nesting structure as a tree diagram
5. **Type Validation Preview**: Show which constraints are satisfied/violated before construction

## Related Files

- `SerializedTypeDrawer.cs` - Main implementation
- `SerializedType.cs` - The type reference wrapper
- `SerializedTypeOptionsAttribute.cs` - Configuration attribute
- `SerializedTypeExample.cs` - Test cases and examples
- `BUG_FIX_NESTED_MULTI_PARAMS.md` - Bug fix documentation
