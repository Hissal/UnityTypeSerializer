# TypeRef Quick Reference Guide

## Basic Usage

### 1. Simple Type Selection (Concrete Types Only)
```csharp
[SerializeField]
TypeRef<IDamageEffect> damageType;
```
**Result**: Dropdown shows only concrete (non-generic) types that implement `IDamageEffect`

---

### 2. Enable Generic Type Construction
```csharp
[SerializeField]
[TypeRefOptions(includeGenericTypeDefinitions: true)]
TypeRef<IDamageEffect> damageType;
```
**Result**: Can select `Container<T>` and then fill in `T` with any valid type

---

### 3. Allow Self-Nesting (Recursive Types)
```csharp
[SerializeField]
[TypeRefOptions(
    includeGenericTypeDefinitions: true,
    allowSelfNesting: true
)]
TypeRef<IDamageEffect> damageType;
```
**Result**: Can create structures like `Wrapper<Wrapper<Wrapper<FireDamage>>>`

---

### 4. Exclude Specific Types
```csharp
[SerializeField]
[TypeRefOptions(ExcludeTypes = new[] { 
    typeof(DeprecatedDamage), 
    typeof(OldDamage) 
})]
TypeRef<IDamageEffect> damageType;
```
**Result**: Listed types won't appear in the dropdown

---

### 5. Exclude Types via Method/Property
```csharp
public static IEnumerable<Type> GetDeprecatedTypes() {
    yield return typeof(OldDamage);
    yield return typeof(LegacyDamage);
}

[SerializeField]
[TypeRefOptions(ExcludeTypesResolver = "MyClass.GetDeprecatedTypes")]
TypeRef<IDamageEffect> damageType;
```
**Result**: Types returned by the method/property are excluded

---

### 6. Only Show Specific Types
```csharp
[SerializeField]
[TypeRefOptions(IncludeTypes = new[] { 
    typeof(FireDamage), 
    typeof(IceDamage) 
})]
TypeRef<IDamageEffect> damageType;
```
**Result**: ONLY the listed types appear (overrides normal filtering)

---

### 7. Include Types via Method/Property
```csharp
public static IEnumerable<Type> GetAllowedDamages() {
    yield return typeof(FireDamage);
    yield return typeof(IceDamage);
    yield return typeof(Container<>);
}

[SerializeField]
[TypeRefOptions(
    includeGenericTypeDefinitions: true,
    IncludeTypesResolver = "MyClass.GetAllowedDamages"
)]
TypeRef<IDamageEffect> damageType;
```
**Result**: ONLY types from the resolver appear

---

### 8. Combined Options
```csharp
[SerializeField]
[TypeRefOptions(
    includeGenericTypeDefinitions: true,
    allowSelfNesting: true,
    ExcludeTypes = new[] { typeof(BrokenDamage) },
    ExcludeTypesResolver = "GetDeprecatedTypes"
)]
TypeRef<IDamageEffect> damageType;
```
**Result**: All options work together

---

## Runtime Usage

### Check if Type is Set
```csharp
if (damageType.HasType) {
    // Type is set
}
```

### Get the Type
```csharp
Type type = damageType.Type;
```

### Create Instance
```csharp
if (damageType.HasType) {
    var instance = Activator.CreateInstance(damageType.Type) as IDamageEffect;
}
```

### With Dependency Injection (VContainer)
```csharp
public class DamageFactory {
    readonly IObjectResolver resolver;
    
    [SerializeField]
    TypeRef<IDamageEffect> damageType;
    
    public IDamageEffect CreateDamage() {
        if (!damageType.HasType)
            return null;
            
        return resolver.Resolve(damageType.Type) as IDamageEffect;
    }
}
```

---

## Type Resolver Method Format

Resolver methods must:
1. Be **static**
2. Return `IEnumerable<Type>`
3. Be parameterless

**Format**: `"TypeName.MemberName"` or `"MemberName"` (for current class)

### Examples:

#### As Method
```csharp
public static IEnumerable<Type> GetMyTypes() {
    yield return typeof(TypeA);
    yield return typeof(TypeB);
}
```

#### As Property
```csharp
public static IEnumerable<Type> MyTypes => new[] {
    typeof(TypeA),
    typeof(TypeB)
};
```

#### As Field
```csharp
public static readonly Type[] MyTypes = {
    typeof(TypeA),
    typeof(TypeB)
};
```

---

## Common Patterns

### Factory Pattern
```csharp
public class EffectFactory : MonoBehaviour {
    [SerializeField]
    [TypeRefOptions(includeGenericTypeDefinitions: true)]
    TypeRef<IEffect> effectType;
    
    public IEffect Create() {
        return Activator.CreateInstance(effectType.Type) as IEffect;
    }
}
```

### Registry Pattern
```csharp
public class DamageRegistry {
    [SerializeField]
    [TypeRefOptions(includeGenericTypeDefinitions: true)]
    TypeRef<IDamageEffect>[] registeredDamages;
    
    public void Initialize() {
        foreach (var typeRef in registeredDamages) {
            if (typeRef.HasType) {
                RegisterDamage(typeRef.Type);
            }
        }
    }
}
```

### Plugin System
```csharp
public class PluginManager {
    [SerializeField]
    [TypeRefOptions(
        includeGenericTypeDefinitions: true,
        IncludeTypesResolver = "GetPluginTypes"
    )]
    TypeRef<IPlugin>[] plugins;
    
    static IEnumerable<Type> GetPluginTypes() {
        return AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(a => a.GetTypes())
            .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract);
    }
}
```

---

## Tips & Tricks

### Nested Generic Example
To create `Container<ElementalDamage<FireElement>>`:
1. Select `Container<T>` from dropdown
2. For `T`, select `ElementalDamage<TElement>`
3. Click "▶ Construct" next to ElementalDamage
4. For `TElement`, select `FireElement`
5. Click "Apply" to construct the ElementalDamage
6. Click "Construct Type" to finalize

### Debugging
Add a button to log type info:
```csharp
[Button("Log Type Info")]
void LogTypeInfo() {
    if (myTypeRef.HasType) {
        Debug.Log($"Type: {myTypeRef.Type.Name}");
        Debug.Log($"Full Name: {myTypeRef.Type.FullName}");
        Debug.Log($"Assembly: {myTypeRef.Type.Assembly.GetName().Name}");
    }
}
```

### Performance
- Type filtering happens once during initialization
- Dropdowns are cached per field
- No performance impact at runtime

### Constraints
Generic constraints are automatically validated:
- `where T : class` - Only reference types
- `where T : struct` - Only value types
- `where T : IInterface` - Types implementing interface
- `where T : new()` - Types with parameterless constructor
- Multiple constraints are supported

---

## Troubleshooting

### "No types available in dropdown"
- Check that types implement the base interface
- Check that types are not abstract
- Check exclusion filters
- Verify inclusion filters (if used)

### "Cannot construct generic type"
- Ensure all type arguments are selected
- Check that constraints are satisfied
- Verify new() constraint if present

### "Type not showing in nested selector"
- Check self-nesting settings
- Verify constraints on generic parameters
- Check exclusion filters

### "Resolver method not found"
- Ensure method is static
- Verify method returns `IEnumerable<Type>`
- Check method name spelling
- Use format: `"FullTypeName.MethodName"`
