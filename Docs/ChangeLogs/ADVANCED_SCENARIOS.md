# SerializedType Generic Type Construction - Advanced Scenarios

## Scenario 1: Multiple Generic Parameters

✅ **FULLY SUPPORTED**

### Example
```csharp
public class MultiGenericType<T1, T2> : IMyInterface 
    where T1 : IInterface1 
    where T2 : IInterface2 
{ }
```

### How It Works
When you select `MultiGenericType<T1, T2>`:

1. The drawer shows **both parameters** with their constraints:
   ```
   Constructing: MultiGenericType<T1, T2>
   
   T1 (where T1 : IInterface1)    [Select Type...]
   T2 (where T2 : IInterface2)    [Select Type...]
   
   [Construct Type]  [Cancel]
   ```

2. Each parameter gets its own dropdown filtered by its specific constraints
3. You select `TypeA` for `T1` and `TypeB` for `T2`
4. Click "Construct Type" to create `MultiGenericType<TypeA, TypeB>`

### Notes
- Each parameter is evaluated **independently** with its own constraints
- The "Construct Type" button is **disabled** until **all** parameters are selected
- Supports any number of generic parameters (T1, T2, T3, ...)

---

## Scenario 2: Open Generic Types as Type Arguments

✅ **SUPPORTED WITH CONSTRAINTS**

### Example
```csharp
// A container that accepts any class implementing IElement
public class Container<T> : IMyInterface 
    where T : class, IElement 
{ }

// An open generic type that implements IElement
public class ElementFire<T> : IElement, IFire<T> { }
```

### How It Works
When you select `Container<T>`:

1. The type argument selector for `T` will show:
   - ✅ `FireElement` (concrete type implementing IElement)
   - ✅ `WaterElement` (concrete type implementing IElement)
   - ✅ `ElementFire<T>` (open generic implementing IElement)
   - ❌ `ConcreteType` (doesn't implement IElement)

2. If you select `ElementFire<T>`, you create `Container<ElementFire<T>>`
   - This is a **partially constructed** generic type
   - The outer type is constructed: `Container<...>`
   - The inner type remains open: `ElementFire<T>`

### Important Constraints

#### ✅ Open Generics ARE ALLOWED When:
- No `new()` constraint is present
- Only interface/class constraints are specified
- `class` or `struct` constraints are satisfied

#### ❌ Open Generics ARE NOT ALLOWED When:
- **`new()` constraint is present** - We can't verify the constructed type will have a parameterless constructor

### Example: new() Constraint Blocks Open Generics

```csharp
// Requires new() constraint
public class Resistance<TElement> : IMyInterface 
    where TElement : IElement, new() 
{ }
```

When you select `Resistance<TElement>`:
- ✅ `FireElement` is shown (has parameterless constructor)
- ❌ `ElementFire<T>` is **NOT shown** (can't verify `new()` constraint)

---

## Scenario 3: Nested Generic Construction

### Example Flow

**Step 1:** Select an outer generic type
```csharp
[SerializeField]
[SerializedTypeOptions(includeGenericTypeDefinitions: true)]
SerializedType<IMyInterface> myType;

// Select: Container<T> where T : class, IElement
```

**Step 2:** Select an open generic for the type argument
```
Constructing: Container<T>

T (where T : class, IElement)  [ElementFire<T>]  ← Select an open generic

[Construct Type]
```

**Step 3:** Result
```csharp
myType.Type = typeof(Container<ElementFire<T>>)
myType.Type.IsGenericType = true
myType.Type.IsGenericTypeDefinition = false  // Outer is constructed
myType.Type.GetGenericArguments()[0] = typeof(ElementFire<T>)
myType.Type.GetGenericArguments()[0].IsGenericTypeDefinition = true  // Inner is still open
```

---

## Constraint Checking Details

### For Open Generic Types

The system checks:

1. **Interface Constraints**: 
   - Checks if the open generic implements the interface
   - Handles both generic and non-generic interface constraints
   - Example: `ElementFire<T> : IElement, IFire<T>` ✅

2. **Special Constraints**:
   - `class` - Checks if type is reference type ✅
   - `struct` - Checks if type is value type ✅
   - `new()` - **Blocks open generics** ❌ (can't verify)

3. **Base Class Constraints**:
   - Checks if open generic inherits from required base class ✅

### For Concrete Types

All constraint checks are performed normally:
- Interface/class inheritance via `IsAssignableFrom()`
- `new()` constraint via `GetConstructor(Type.EmptyTypes)`
- `struct` constraint via `IsValueType`
- `class` constraint via `!IsValueType`

---

## Testing Your Setup

Use the provided `SerializedTypeExample.cs` to test:

### Test 1: Multiple Generic Parameters
```csharp
[SerializedTypeOptions(includeGenericTypeDefinitions: true)]
SerializedType<IMyInterface> multiGeneric;

// Select MultiGenericType<T1, T2>
// Pick TypeA for T1
// Pick TypeB for T2
// Result: MultiGenericType<TypeA, TypeB>
```

### Test 2: Open Generic with new() (Should Block)
```csharp
[SerializedTypeOptions(includeGenericTypeDefinitions: true)]
SerializedType<IMyInterface> resistance;

// Select Resistance<TElement> (has new() constraint)
// Should see: FireElement, WaterElement, EarthElement
// Should NOT see: ElementFire<T>
```

### Test 3: Open Generic without new() (Should Allow)
```csharp
[SerializedTypeOptions(includeGenericTypeDefinitions: true)]
SerializedType<IMyInterface> container;

// Select Container<T> (no new() constraint)
// Should see: FireElement, WaterElement, EarthElement
// Should ALSO see: ElementFire<T>  ← This is allowed!
```

---

## Summary

| Scenario | Supported | Notes |
|----------|-----------|-------|
| Multiple generic parameters (T1, T2, ...) | ✅ Yes | Each parameter filtered independently |
| Open generic as type argument | ✅ Yes* | *Blocked if `new()` constraint present |
| Nested generics (Container<ElementFire<T>>) | ✅ Yes | Creates partially constructed types |
| new() constraint on parameter | ⚠️ Blocks open generics | Safety measure - can't verify constructor |
| Interface constraints | ✅ Full support | Works with generic and non-generic interfaces |
| class/struct constraints | ✅ Full support | Properly checked for open generics |

