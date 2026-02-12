# SerializedType<TBase> - Attribute-Based Configuration with Generic Type Construction

> **Note:** This document contains outdated terminology and examples. For current API usage, see `SerializedTypeExample.cs` and the main README. The property names have been updated to use PascalCase property initializers (e.g., `AllowGenericTypeConstruction = true`).

## Overview
`SerializedType<TBase>` now supports attribute-based configuration for controlling which types appear in the dropdown using the `SerializedTypeOptionsAttribute`. Additionally, when you select an open generic type (e.g., `Resistance<TElement>`), the drawer allows you to construct a concrete type by selecting the generic type arguments based on their constraints.

## Usage

### Default Behavior (No Attribute)
By default, only concrete, non-generic types are shown:

```csharp
[SerializeField]
SerializedType<IMyInterface> mySerializedType;
```

This will show:
- ✅ `ConcreteClass`
- ❌ `AbstractClass` (abstract)
- ❌ `IMyInterface` (interface)
- ❌ `GenericClass<>` (open generic)
- ❌ `List<int>` (constructed generic)

### Including Open Generic Types
To include open generic type definitions (e.g., `List<>`, `Dictionary<,>`):

```csharp
[SerializeField]
[SerializedTypeOptions(AllowGenericTypeConstruction = true)]
SerializedType<IMyInterface> mySerializedType;
```

This will additionally show:
- ✅ `GenericClass<>` (open generic type definition)

### Including Constructed Generic Types
To include constructed generic types (e.g., `List<int>`, `Dictionary<string, int>`):

```csharp
[SerializeField]
[SerializedTypeOptions(AllowOpenGenerics = true)]
SerializedType<IMyInterface> mySerializedType;
```

This will additionally show:
- ✅ `List<int>` (constructed generic)

### Including All Generic Types
To include both open and constructed generic types:

```csharp
[SerializeField]
[SerializedTypeOptions(AllowGenericTypeConstruction = true, AllowOpenGenerics = true)]
SerializedType<IMyInterface> mySerializedType;
```

## Generic Type Construction

When you select an **open generic type** (e.g., `Resistance<TElement>` or `Dictionary<TKey, TValue>`), the drawer automatically switches to a **type constructor mode** that allows you to:

1. **View generic parameters and constraints**: Each generic parameter is shown with its constraints (e.g., `where T : class`)
2. **Select concrete types for each parameter**: Click on each parameter to see a filtered list of types that satisfy the constraints
3. **Construct the concrete type**: Once all parameters are selected, click "Construct Type" to create the concrete generic type
4. **Cancel if needed**: Click "Cancel" to go back to type selection

### Example Workflow

If you have:
```csharp
public class Resistance<TElement> : IResistance 
    where TElement : IElement, new()
{
    // ...
}
```

And you select `Resistance<TElement>`:

1. The drawer shows a UI like:
   ```
   Constructing: Resistance<TElement>
   
   TElement (where TElement : IElement, new())  [Select Type...]
   
   [Construct Type]  [Cancel]
   ```

2. Click on `[Select Type...]` to see only types that implement `IElement` and have a parameterless constructor
3. Select a type like `FireElement`
4. Click `[Construct Type]` to create `Resistance<FireElement>`

### Constraint Support

The type constructor respects all generic parameter constraints:

- **Interface constraints**: `where T : IMyInterface`
- **Class constraints**: `where T : MyBaseClass`
- **Reference type constraint**: `where T : class`
- **Value type constraint**: `where T : struct`
- **Constructor constraint**: `where T : new()`
- **Multiple constraints**: `where T : class, IDisposable, new()`

Only types that satisfy **all** constraints for a generic parameter will appear in the selection dropdown.

## Implementation Details

### Files Created/Modified

1. **SerializedType.cs** - Main class
   - Simple struct with assembly-qualified name storage
   - Public `Type` property for reading, internal setter for the custom drawer
   - `HasType` property for checking if a type is assigned

2. **SerializedTypeDrawer.cs** - Custom Odin drawer
   - Reads the `SerializedTypeOptionsAttribute` from the field using Odin's property system
   - Creates a dropdown with filtered types based on attribute configuration
   - Uses Odin's `GenericSelector` for a clean dropdown UI
   - **Detects open generic types** and switches to type constructor mode
   - **Shows generic parameter constraints** and filters available types accordingly
   - **Constructs concrete generic types** using `Type.MakeGenericType()`

3. **SerializedTypeOptionsAttribute.cs** - Configuration attribute
   - `IncludeGenericTypeDefinitions` - Controls open generic types
   - `IncludeConstructedGenerics` - Controls constructed generic types

4. **SerializedTypeExample.cs** - Usage examples

### How It Works

1. The custom `SerializedTypeDrawer<TBase>` is automatically picked up by Odin Inspector
2. On initialization, it reads the `SerializedTypeOptionsAttribute` from the property: `Property.GetAttribute<SerializedTypeOptionsAttribute>()`
3. It builds a filtered list of types based on the attribute settings
4. The drawer displays a dropdown button in the inspector
5. When clicked, it shows Odin's `GenericSelector` with all valid types
6. Selected types are stored as assembly-qualified names for serialization

### Key Benefits

✅ **Reliable** - Uses Odin's property system instead of fragile reflection/stack traces
✅ **Declarative** - Configure via attributes, not constructor parameters
✅ **Serialization-friendly** - No need to serialize configuration flags
✅ **Clean API** - Simple, intuitive usage
✅ **Type-safe** - Compile-time checking of configuration
✅ **Inspector-friendly** - Native Odin drawer integration

## Advanced Usage

### Runtime Type Access

```csharp
if (mySerializedType.HasType) {
    Type type = mySerializedType.Type;
    
    // Check if it's a generic type definition
    if (type.IsGenericTypeDefinition) {
        // Open generic: List<>
        var closedType = type.MakeGenericType(typeof(int)); // List<int>
    }
    
    // Create instance (if not open generic)
    if (!type.IsGenericTypeDefinition) {
        var instance = Activator.CreateInstance(type);
    }
}
```

### Type Names in Dropdown

The dropdown displays user-friendly names:
- Non-generic: `MyClass`
- Open generic: `List<T>`
- Multiple type params: `Dictionary<TKey, TValue>`
- Constructed: `List<Int32>`

## Comparison to Previous Approach

### Attribute-Based Configuration
```csharp
// Clean and serialization-friendly
[SerializeField]
[SerializedTypeOptions(AllowGenericTypeConstruction = true)]
SerializedType<IMyInterface> mySerializedType;
```
