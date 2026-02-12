# SerializedType Quick Reference Guide

## Basic Usage

### 1. Simple Type Selection (Concrete Types Only)
```csharp
[SerializeField]
SerializedType<IDamageEffect> damageType;
```
**Result**: Dropdown shows only concrete (non-generic) types that implement `IDamageEffect`

---

### 2. Enable Generic Type Construction
```csharp
[SerializeField]
[SerializedTypeOptions(AllowGenericTypeConstruction = true)]
SerializedType<IDamageEffect> damageType;
```
**Result**: Can select `Container<T>` and then fill in `T` with any valid type

---

### 3. Allow Self-Nesting (Recursive Types)
```csharp
[SerializeField]
[SerializedTypeOptions(
    AllowGenericTypeConstruction = true,
    AllowSelfNesting = true
)]
SerializedType<IDamageEffect> damageType;
```
**Result**: Can create structures like `Wrapper<Wrapper<Wrapper<FireDamage>>>`

---

### 4. Exclude Specific Types
```csharp
static SerializedTypeFilter GetExcludeFilter() =>
    SerializedTypeFilter.Exclude(new[] { typeof(DeprecatedDamage), typeof(OldDamage) });

[SerializeField]
[SerializedTypeOptions(CustomTypeFilter = nameof(GetExcludeFilter))]
SerializedType<IDamageEffect> damageType;
```
**Result**: Listed types won't appear in the dropdown

---

### 5. Exclude Types via Method/Property
```csharp
static SerializedTypeFilter GetDeprecatedFilter() =>
    SerializedTypeFilter.Exclude("MyClass.GetDeprecatedTypes");

[SerializeField]
[SerializedTypeOptions(CustomTypeFilter = nameof(GetDeprecatedFilter))]
SerializedType<IDamageEffect> damageType;
```
**Result**: Types returned by the method/property are excluded

---

### 6. Only Show Specific Types
```csharp
static IEnumerable<Type> GetAllowedTypes() =>
    new[] { typeof(FireDamage), typeof(IceDamage) };

[SerializeField]
[SerializedTypeOptions(CustomTypeFilter = nameof(GetAllowedTypes))]
SerializedType<IDamageEffect> damageType;
```
**Result**: ONLY the listed types appear (overrides normal filtering)

---

### 7. Include Types via Method/Property
```csharp
static IEnumerable<Type> GetAllowedDamages() {
    yield return typeof(FireDamage);
    yield return typeof(IceDamage);
    yield return typeof(Container<>);
}

[SerializeField]
[SerializedTypeOptions(
    AllowGenericTypeConstruction = true,
    CustomTypeFilter = nameof(GetAllowedDamages))]
SerializedType<IDamageEffect> damageType;
```
**Result**: ONLY types from the resolver appear

---

### 8. Combined Options
```csharp
static SerializedTypeFilter GetCombinedFilter() =>
    SerializedTypeFilter.Exclude(new[] { typeof(BrokenDamage) })
        .WithExclude("GetDeprecatedTypes");

[SerializeField]
[SerializedTypeOptions(
    AllowGenericTypeConstruction = true,
    AllowSelfNesting = true,
    CustomTypeFilter = nameof(GetCombinedFilter))]
SerializedType<IDamageEffect> damageType;
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
    SerializedType<IDamageEffect> damageType;
    
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
    [SerializedTypeOptions(AllowGenericTypeConstruction = true)]
    SerializedType<IEffect> effectType;
    
    public IEffect Create() {
        return Activator.CreateInstance(effectType.Type) as IEffect;
    }
}
```

### Registry Pattern
```csharp
public class DamageRegistry {
    [SerializeField]
    [SerializedTypeOptions(AllowGenericTypeConstruction = true)]
    SerializedType<IDamageEffect>[] registeredDamages;
    
    public void Initialize() {
        foreach (var serializedType in registeredDamages) {
            if (serializedType.HasType) {
                RegisterDamage(serializedType.Type);
            }
        }
    }
}
```

### Plugin System
```csharp
public class PluginManager {
    [SerializeField]
    [SerializedTypeOptions(
        AllowGenericTypeConstruction = true,
        CustomTypeFilter = nameof(GetPluginTypes))]
    SerializedType<IPlugin>[] plugins;
    
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
    if (mySerializedType.HasType) {
        Debug.Log($"Type: {mySerializedType.Type.Name}");
        Debug.Log($"Full Name: {mySerializedType.Type.FullName}");
        Debug.Log($"Assembly: {mySerializedType.Type.Assembly.GetName().Name}");
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
