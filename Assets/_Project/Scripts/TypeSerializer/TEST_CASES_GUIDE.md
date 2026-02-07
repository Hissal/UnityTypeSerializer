# TypeRef Test Cases Guide

## Overview
The `TypeRefExample.cs` provides 11 comprehensive test cases covering everything from basic type selection to extreme 4-level nested generic construction.

---

## 🎯 Test Case Categories

### Basic Examples

#### Test 1: Concrete Types Only
**Field:** `concreteTypesOnly`
**What it tests:** Default behavior - only non-generic types

**Available types:**
- FireDamage
- IceDamage
- PhysicalDamage
- MagicDamage
- TrueDamage

**Expected behavior:** No generic types appear in dropdown.

---

### Simple Generic Construction

#### Test 2: Single Generic Parameter
**Field:** `singleGenericType`
**What it tests:** Basic generic type with one type parameter

**Try constructing:**
- `ElementalDamage<FireElement>`
- `ElementalDamage<IceElement>`
- `ElementalDamage<LightningElement>`

**Workflow:**
1. Select `ElementalDamage<TElement>`
2. UI shows: `TElement (where TElement : IElement)` with dropdown
3. Select any element type (FireElement, IceElement, etc.)
4. Click "Construct Type"
5. Result: `ElementalDamage<FireElement>`

---

#### Test 3: Multiple Generic Parameters
**Field:** `multipleGenericParameters`
**What it tests:** Generic types with 2-3 type parameters

**Try constructing:**
- `DualElementDamage<FireElement, IceElement>`
- `TripleElementDamage<FireElement, IceElement, LightningElement>`

**Workflow:**
1. Select `DualElementDamage<TElement1, TElement2>`
2. UI shows TWO dropdowns:
   - `TElement1 (where TElement1 : IElement)`
   - `TElement2 (where TElement2 : IElement)`
3. Select different elements for each
4. Click "Construct Type"
5. Result: `DualElementDamage<FireElement, IceElement>`

---

### Nested Generic Construction

#### Test 4: One Level Nesting
**Field:** `nestedGeneric1Level`
**What it tests:** Generic type with another generic type as argument

**Try constructing:**
- `Container<ElementalDamage<FireElement>>`
- `Container<DualElementDamage<FireElement, IceElement>>`

**Workflow:**
1. Select `Container<T>`
2. Select `ElementalDamage<TElement>` for T
3. Click **[▶ Construct]** button
4. Nested UI appears with `TElement` dropdown
5. Select `FireElement`
6. Auto-applies and shows `ElementalDamage<FireElement>`
7. Click "Construct Type"
8. Result: `Container<ElementalDamage<FireElement>>`

---

#### Test 5: Two Level Nesting
**Field:** `nestedGeneric2Levels`
**What it tests:** Deeply nested generics (3 levels of angle brackets)

**Try constructing:**
- `Wrapper<Container<ElementalDamage<FireElement>>>`

**Workflow:**
1. Select `Wrapper<T>`
2. Select `Container<T>` for T → Click [▶ Construct]
3. Select `ElementalDamage<TElement>` → Click [▶ Construct]
4. Select `FireElement`
5. Work your way back clicking "Construct Type" at each level
6. Result: `Wrapper<Container<ElementalDamage<FireElement>>>`

---

#### Test 6: Complex Nested Multi-Parameter
**Field:** `complexNestedMultiParam`
**What it tests:** Multiple parameters where each can be a nested generic

**Try constructing:**
- `ComplexWrapper<ElementalDamage<FireElement>, ElementalDamage<IceElement>>`
- `ComplexWrapper<DualElementDamage<FireElement, IceElement>, TripleElementDamage<PoisonElement, HolyElement, DarkElement>>`

**Workflow:**
1. Select `ComplexWrapper<T1, T2>`
2. For T1: Select `ElementalDamage<TElement>` → Construct with `FireElement`
3. For T2: Select `ElementalDamage<TElement>` → Construct with `IceElement`
4. Click "Construct Type"
5. Result: `ComplexWrapper<ElementalDamage<FireElement>, ElementalDamage<IceElement>>`

---

### Advanced Constraints

#### Test 7: new() Constraint
**Field:** `withNewConstraint`
**What it tests:** new() constraint blocks open generic types

**Try constructing:**
- `InstantiableDamage<FireElement>` ✅
- `InstantiableDamage<IceElement>` ✅

**What you CAN'T do:**
- Select `ElementalDamage<T>` as argument ❌ (blocked by new() constraint)

**Expected behavior:**
When constructing `InstantiableDamage<T>`, only concrete types with parameterless constructors appear in the dropdown. Open generic types are excluded.

---

#### Test 8: Multiple Constraints
**Field:** `multipleConstraints`
**What it tests:** Types with class + interface constraints

**Try constructing:**
- `BuffEffect<StatusEffect<Burning>>`
- `BuffEffect<StatusEffect<Frozen>>`

**Workflow:**
1. Select `BuffEffect<TStatus>` (requires `class, IStatusEffect`)
2. Select `StatusEffect<T>` → Click [▶ Construct]
3. Select `Burning` (implements both IElement and IStatusEffect)
4. Result: `BuffEffect<StatusEffect<Burning>>`

---

### Real-World Patterns

#### Test 9: Repository Pattern
**Field:** `repositoryPattern`
**What it tests:** Common repository pattern with nested data types

**Try constructing:**
- `Repository<PlayerData<HealthStat>>`
- `Repository<PlayerData<ManaStat>>`
- `Repository<PlayerData<StaminaStat>>`

**Workflow:**
1. Select `Repository<TData>` (requires `class, IData`)
2. Select `PlayerData<TStat>` → Click [▶ Construct]
3. Select stat type (HealthStat, ManaStat, etc.)
4. Result: `Repository<PlayerData<HealthStat>>`

**Real-world use:** Type-safe repository access patterns common in game architecture.

---

#### Test 10: Strategy Pattern
**Field:** `strategyPattern`
**What it tests:** Strategy pattern with generic calculations

**Try constructing:**
- `Strategy<DamageCalculation<CriticalHit>>`
- `Strategy<DamageCalculation<ArmorPenetration>>`

**Workflow:**
1. Select `Strategy<TCalculation>`
2. Select `DamageCalculation<TModifier>` → Click [▶ Construct]
3. Select modifier (CriticalHit, ArmorPenetration, etc.)
4. Result: `Strategy<DamageCalculation<CriticalHit>>`

**Real-world use:** Pluggable strategy systems with different calculation modifiers.

---

### Extreme Nesting

#### Test 11: MASSIVE Nested Structure (4 Levels!)
**Field:** `extremeNesting`
**What it tests:** Maximum complexity - 4 levels of nested generic types

**Try constructing:**
- `OuterMost<Wrapper<Container<ElementalDamage<FireElement>>>>`
- `OuterMost<DeepContainer<Container<DualElementDamage<FireElement, IceElement>>>>`
- `MegaWrapper<Wrapper<Container<ElementalDamage<FireElement>>>, Wrapper<Container<ElementalDamage<IceElement>>>, Wrapper<Container<ElementalDamage<LightningElement>>>>`

**Workflow for 4-level structure:**
1. Select `OuterMost<T>`
2. Select `Wrapper<T>` → Click [▶ Construct]
3. Select `Container<T>` → Click [▶ Construct]
4. Select `ElementalDamage<TElement>` → Click [▶ Construct]
5. Select `FireElement`
6. Work backwards applying each level
7. Result: `OuterMost<Wrapper<Container<ElementalDamage<FireElement>>>>`

**Challenge:** Try constructing `MegaWrapper` with 3 different complex nested parameters!

---

## 🎨 Visual Workflow Example

```
1. Select MegaWrapper<T1, T2, T3>
   
   Constructing: MegaWrapper<T1, T2, T3>
   
   T1 (where T1 : IDamageEffect)  [Select Type...]
   T2 (where T2 : IDamageEffect)  [Select Type...]
   T3 (where T3 : IDamageEffect)  [Select Type...]

2. For T1, select Wrapper<T> → Click [▶ Construct]
   
   Constructing: MegaWrapper<T1, T2, T3>
   
   T1 (where T1 : IDamageEffect)  [Wrapper<T>]  [▶ Construct]
   
       ↳ Constructing: Wrapper<T>
       
           T (where T : class, IDamageEffect)  [Select Type...]
           
           [Apply]  [Cancel]
   
   T2 (where T2 : IDamageEffect)  [Select Type...]
   T3 (where T3 : IDamageEffect)  [Select Type...]

3. Select ElementalDamage<TElement> → Click [▶ Construct] again
   ... (keep nesting)

4. Eventually complete all type arguments
   Result: MegaWrapper<Wrapper<ElementalDamage<FireElement>>, Wrapper<ElementalDamage<IceElement>>, Wrapper<ElementalDamage<LightningElement>>>
```

---

## 📊 Available Type Combinations

### Elements (IElement)
- FireElement
- IceElement
- LightningElement
- PoisonElement
- HolyElement
- DarkElement
- CriticalHit
- ArmorPenetration

### Status Effects (IStatusEffect)
- Burning (also IElement)
- Frozen (also IElement)
- Stunned (also IElement)

### Stats (IStat)
- HealthStat
- ManaStat
- StaminaStat

### Wrapper Types
- Container<T>
- Wrapper<T>
- DeepContainer<T>
- OuterMost<T>

### Multi-Parameter Wrappers
- ComplexWrapper<T1, T2>
- MegaWrapper<T1, T2, T3>

### Bonus Types (For Advanced Users)
- GenericElement<T> (element that takes a type parameter)
- CompositeElement<T1, T2> (combines two elements)
- FusionDamage<TBase, TModifier> (damage with base and modifier)

---

## 🚀 Pro Tips

1. **Start Simple**: Begin with Test 1-3 to understand the basics
2. **Explore Nesting**: Tests 4-6 show the real power of nested construction
3. **Test Constraints**: Test 7 shows what gets blocked by new() constraint
4. **Go Deep**: Test 11 - see how far you can nest!
5. **Mix and Match**: Try unusual combinations like `ComplexWrapper<Repository<PlayerData<HealthStat>>, Strategy<DamageCalculation<CriticalHit>>>`

## 🎯 Achievement Ideas

- [ ] Construct a 3-level nested type
- [ ] Construct a 4-level nested type
- [ ] Create `MegaWrapper` with 3 different nested parameters
- [ ] Successfully use all element types in one session
- [ ] Construct the longest possible type name
- [ ] Test every single generic type in the example

Happy type constructing! 🎉
