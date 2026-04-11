using System;
using System.Collections.Generic;
using Hissal.UnityTypeSerializer;
using Sirenix.OdinInspector;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Hissal.UnityTypeSerializer.Examples {
    /// <summary>
    /// Comprehensive test cases for SerializedType with nested generic type construction.
    /// Demonstrates various scenarios including deep nesting, multiple constraints, and real-world patterns.
    /// </summary>
    public sealed class SerializedTypeExample : MonoBehaviour {
        public void Update() {
            if (WasLogKeyPressedThisFrame()) {
                Debug.Log("[SerializedTypeExample] Space key pressed - logging all type infos...");
                LogAllTypeInfos();
            }
        }

        static bool WasLogKeyPressedThisFrame() {
#if ENABLE_INPUT_SYSTEM
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Space);
#else
            return false;
#endif
        }

        [Title("SerializedType Examples & Tests", bold: true)]
        [InfoBox("This script demonstrates SerializedType capabilities with various test scenarios.\n" +
                 "Use the button below to log type information for all configured fields.")]
        
        [Title("Drawer Mode Tests", bold: true)]
        [InfoBox("Inline Mode (Default) - Single line with multiple dropdowns\n" +
                 "This is the default drawer mode.")]
        [SerializeField]
        [SerializedTypeOptions(AllowGenericTypeConstruction = true)]
        SerializedType<ISerializedTypeExample>? inlineModeDefault;
        
        [InfoBox("Complex Constructor Mode - Step-by-step nested UI\n" +
                 "Use drawerMode parameter to specify the complex constructor.")]
        [SerializeField]
        [SerializedTypeOptions(AllowGenericTypeConstruction = true, DrawerMode = SerializedTypeDrawerMode.Constructor)]
        SerializedType<ISerializedTypeExample>? complexConstructorMode;
        
        [Title("On Type Changed Callback", bold: true)]
        [InfoBox("OnTypeChanged callback example - logs type info whenever selection changes.")]
        [SerializeField]
        [SerializedTypeOptions(AllowGenericTypeConstruction = true, OnTypeChanged = nameof(OnTypeChanged))]
        SerializedType<ISerializedTypeExample>? onTypeChangedExample;
        
        [SerializeField]
        [SerializedTypeOptions(
            DrawerMode = SerializedTypeDrawerMode.Constructor,
            AllowGenericTypeConstruction = true, 
            OnTypeChanged = nameof(OnTypeChangedConstructor))]
        SerializedType<ISerializedTypeExample>? onTypeChangedConstructorExample;
        
        void OnTypeChanged() {
            Debug.Log("[OnTypeChanged] Type selection changed!");
            if (onTypeChangedExample?.HasType == true) {
                var type = onTypeChangedExample.Type;
                var typeDescription = type.IsGenericTypeDefinition 
                    ? $"{GetFullTypeName(type)} (OPEN GENERIC)" 
                    : GetFullTypeName(type);
                
                Debug.Log($"[OnTypeChanged] New selection: {typeDescription}");
            }
            else {
                Debug.Log("[OnTypeChanged] No type selected");
            }
        }

        void OnTypeChangedConstructor() {
            Debug.Log("[OnTypeChangedConstructor] Type selection changed in constructor mode!");
            if (onTypeChangedConstructorExample?.HasType == true) {
                var type = onTypeChangedConstructorExample.Type;
                var typeDescription = type.IsGenericTypeDefinition 
                    ? $"{GetFullTypeName(type)} (OPEN GENERIC)" 
                    : GetFullTypeName(type);
                
                Debug.Log($"[OnTypeChangedConstructor] New selection: {typeDescription}");
            }
            else {
                Debug.Log("[OnTypeChangedConstructor] No type selected");
            }
        }
        
        [Title("Custom Filtering", bold: true)]
        [SerializeField]
        [SerializedTypeOptions(
            AllowGenericTypeConstruction = true,
            CustomTypeFilter = nameof(GetCustomIncludeTypes))]
        SerializedType<ISerializedTypeExample>? customFilteredAnyType;
        
        IEnumerable<Type> GetCustomIncludeTypes() {
            return new Type[] {
                typeof(BasicExample), 
                typeof(AdvancedExample),
                typeof(ConcreteExample),
                typeof(ChainedStrategy<,>), 
                typeof(Repository<>)
            };
        }
        
        IEnumerable<Type> GetCustomExcludeTypes() {
            return new Type[] {
                typeof(BasicExample), 
                typeof(AdvancedExample),
            };
        }
        
        static IEnumerable<Type> GetFilteredAnyTypes() {
            return new Type[] { typeof(BasicExample), typeof(AdvancedExample), typeof(ConcreteExample) };
        }
        
        [Title("Type Kind Filtering", bold: true)]
        [InfoBox("Default behavior - only concrete (non-abstract, non-interface) types are shown.")]
        [SerializeField]
        SerializedType<ISerializedTypeExample>? concreteOnlyDefault;

        [InfoBox("AllowedTypeKinds = Object | Abstract\n" +
                 "Shows object-like types and abstract classes (interfaces excluded).")]
        [SerializeField]
        [SerializedTypeOptions(AllowedTypeKinds = SerializedTypeKind.Object | SerializedTypeKind.Abstract)]
        SerializedType<ISerializedTypeExample>? concreteAndAbstract;

        [InfoBox("AllowedTypeKinds = Interface\n" +
                 "Shows only interface types (concrete and abstract classes excluded).")]
        [SerializeField]
        [SerializedTypeOptions(AllowedTypeKinds = SerializedTypeKind.Interface)]
        SerializedType<ISerializedTypeExample>? interfacesOnly;

        [InfoBox("AllowedTypeKinds = All\n" +
                 "Shows all supported type kinds.")]
        [SerializeField]
        [SerializedTypeOptions(AllowedTypeKinds = SerializedTypeKind.All)]
        SerializedType<ISerializedTypeExample>? allTypeKinds;

        [Title("Basic Options", bold: true)]
        [InfoBox("Default behavior - only concrete types are shown.")]
        [SerializeField]
        SerializedType<ISerializedTypeExample>? concreteOnly;

        [InfoBox("AllowGenericTypeConstruction = true\n" +
                 "Shows generic types in dropdown. Selection forces immediate construction (no open generics allowed).")]
        [SerializeField]
        [SerializedTypeOptions(AllowGenericTypeConstruction = true)]
        SerializedType<ISerializedTypeExample>? constructionRequired;

        [InfoBox("AllowOpenGenerics = true\n" +
                 "Shows generic types but allows selecting them WITHOUT construction (e.g., List<>).")]
        [SerializeField]
        [SerializedTypeOptions(AllowOpenGenerics = true)]
        SerializedType<ISerializedTypeExample>? openGenericsAllowed;

        [InfoBox("Both AllowGenericTypeConstruction and AllowOpenGenerics = true\n" +
                 "Selecting a generic type assigns it immediately, but a '▶ Construct' button appears to optionally construct it.")]
        [SerializeField]
        [SerializedTypeOptions(AllowGenericTypeConstruction = true, AllowOpenGenerics = true)]
        SerializedType<ISerializedTypeExample>? optionalConstruction;

        [Title("Self-Nesting Option", bold: true)]
        [InfoBox("AllowSelfNesting = false (default)\n" +
                 "Prevents recursive nesting like Wrapper<Wrapper<Wrapper<...>>>")]
        [SerializeField]
        [SerializedTypeOptions(AllowGenericTypeConstruction = true)]
        SerializedType<ISerializedTypeExample>? noSelfNesting;

        [InfoBox("AllowSelfNesting = true\n" +
                 "Allows recursive nesting like Wrapper<Wrapper<int>>")]
        [SerializeField]
        [SerializedTypeOptions(AllowGenericTypeConstruction = true, AllowSelfNesting = true)]
        SerializedType<ISerializedTypeExample>? selfNestingAllowed;

        [Title("Complex Nested Examples", bold: true)]
        [InfoBox("Deeply nested generic construction test")]
        [SerializeField]
        [SerializedTypeOptions(AllowGenericTypeConstruction = true)]
        SerializedType<ISerializedTypeExample>? deeplyNested;

        [InfoBox("Extreme nesting with multiple parameters at each level")]
        [SerializeField]
        [SerializedTypeOptions(AllowGenericTypeConstruction = true, AllowSelfNesting = true)]
        SerializedType<ISerializedTypeExample>? extremeNesting;

        [Title("Real-World Scenarios", bold: true)]
        [InfoBox("Damage effect system - supports elemental damage with nested types")]
        [SerializeField]
        [SerializedTypeOptions(AllowGenericTypeConstruction = true)]
        SerializedType<IDamageEffect>? damageEffect;

        [InfoBox("Repository pattern - generic data storage")]
        [SerializeField]
        [SerializedTypeOptions(AllowGenericTypeConstruction = true)]
        SerializedType<IRepository>? repository;

        [InfoBox("Strategy pattern - pluggable algorithms")]
        [SerializeField]
        [SerializedTypeOptions(AllowGenericTypeConstruction = true)]
        SerializedType<IStrategy>? strategy;

        [InfoBox("Strategy pattern - pluggable algorithms")]
        [SerializeField]
        [SerializedTypeOptions(AllowGenericTypeConstruction = true, DrawerMode = SerializedTypeDrawerMode.Constructor)]
        SerializedType<IStrategy>? strategyCtorMode;
        
        [Title("Non-Generic SerializedType (New)", bold: true)]
        [InfoBox("Non-Generic SerializedType - Accepts any type\n" +
                 "This is a convenience type equivalent to SerializedType<object>.")]
        [SerializeField]
        SerializedType? anyType;

        [InfoBox("Non-Generic with CustomTypeFilter\n" +
                 "Only shows specific types via CustomTypeFilter attribute property.")]
        [SerializeField]
        [SerializedTypeOptions(CustomTypeFilter = nameof(GetFilteredAnyTypes))]
        SerializedType? filteredAnyType;

        [InfoBox("Non-Generic with generic construction enabled\n" +
                 "Shows generic types and allows construction.")]
        [SerializeField]
        [SerializedTypeOptions(AllowGenericTypeConstruction = true)]
        SerializedType? anyTypeWithGenerics;

        [Title("Generic Constraint Examples", bold: true)]
        [InfoBox("Interface constraint only: where T : IConstraintExample")]
        [SerializeField]
        [SerializedTypeOptions(AllowGenericTypeConstruction = true)]
        SerializedType<IConstraintExample>? constraintInterfaceOnly;

        [InfoBox("new() constraint: where T : IConstraintExample, new()\n" +
                 "Should exclude ConstraintNoDefaultConstructor.")]
        [SerializeField]
        [SerializedTypeOptions(AllowGenericTypeConstruction = true)]
        SerializedType<IConstraintExample>? constraintNewOnly;

        [InfoBox("class constraint: where T : class, IConstraintExample\n" +
                 "Should exclude struct implementations.")]
        [SerializeField]
        [SerializedTypeOptions(AllowGenericTypeConstruction = true)]
        SerializedType<IConstraintExample>? constraintClassOnly;

        [InfoBox("struct constraint: where T : struct, IConstraintExample\n" +
                 "Should include only struct implementations.")]
        [SerializeField]
        [SerializedTypeOptions(AllowGenericTypeConstruction = true)]
        SerializedType<IConstraintExample>? constraintStructOnly;

        [InfoBox("Combined: class + interface + new()\n" +
                 "where T : class, IConstraintExample, new()")]
        [SerializeField]
        [SerializedTypeOptions(AllowGenericTypeConstruction = true)]
        SerializedType<IConstraintExample>? constraintClassNew;

        [Button("Log All Type Infos", ButtonSizes.Large), GUIColor(0.4f, 0.8f, 1f)]
        void LogAllTypeInfos() {
            Debug.LogError("=== SerializedType Test Cases - Type Information ===\n");
            
            LogTypeInfo("Inline Mode (Default)", inlineModeDefault);
            LogTypeInfo("Complex Constructor Mode", complexConstructorMode);
            LogTypeInfo("Concrete Only", concreteOnly);
            LogTypeInfo("Construction Required", constructionRequired);
            LogTypeInfo("Open Generics Allowed", openGenericsAllowed);
            LogTypeInfo("Optional Construction", optionalConstruction);
            LogTypeInfo("No Self Nesting", noSelfNesting);
            LogTypeInfo("Self Nesting Allowed", selfNestingAllowed);
            LogTypeInfo("Deeply Nested", deeplyNested);
            LogTypeInfo("Extreme Nesting", extremeNesting);
            LogTypeInfo("Damage Effect", damageEffect);
            LogTypeInfo("Repository", repository);
            LogTypeInfo("Strategy", strategy);
            LogTypeInfoNonGeneric("Any Type", anyType);
            LogTypeInfoNonGeneric("Filtered Any Type", filteredAnyType);
            LogTypeInfoNonGeneric("Any Type With Generics", anyTypeWithGenerics);
            LogTypeInfo("Constraint Interface Only", constraintInterfaceOnly);
            LogTypeInfo("Constraint new() Only", constraintNewOnly);
            LogTypeInfo("Constraint class Only", constraintClassOnly);
            LogTypeInfo("Constraint struct Only", constraintStructOnly);
            LogTypeInfo("Constraint class + new()", constraintClassNew);
            
            Debug.LogError("\n=== End of Type Information ===");
        }

        void LogTypeInfo<T>(string testName, SerializedType<T>? serializedType) where T : class {
            if (serializedType?.HasType != true) {
                Debug.LogError($"[{testName}] No type selected");
                return;
            }

            var type = serializedType.Type;
            var typeDescription = type.IsGenericTypeDefinition 
                ? $"{GetFullTypeName(type)} (OPEN GENERIC)" 
                : GetFullTypeName(type);
                
            Debug.LogError($"[{testName}] Selected: {typeDescription}");
            
            if (type.IsGenericType && !type.IsGenericTypeDefinition) {
                var genericArgs = type.GetGenericArguments();
                Debug.LogError($"  └─ Generic arguments: {string.Join(", ", System.Array.ConvertAll(genericArgs, GetFullTypeName))}");
                Debug.LogError($"  └─ Nesting depth: {GetNestingDepth(type)}");
            }
        }

        void LogTypeInfoNonGeneric(string testName, SerializedType? serializedType) {
            if (serializedType?.HasType != true) {
                Debug.LogError($"[{testName}] No type selected");
                return;
            }

            var type = serializedType.Type;
            var typeDescription = type.IsGenericTypeDefinition 
                ? $"{GetFullTypeName(type)} (OPEN GENERIC)" 
                : GetFullTypeName(type);
                
            Debug.LogError($"[{testName}] Selected: {typeDescription}");
            
            if (type.IsGenericType && !type.IsGenericTypeDefinition) {
                var genericArgs = type.GetGenericArguments();
                Debug.LogError($"  └─ Generic arguments: {string.Join(", ", System.Array.ConvertAll(genericArgs, GetFullTypeName))}");
                Debug.LogError($"  └─ Nesting depth: {GetNestingDepth(type)}");
            }
        }

        string GetFullTypeName(Type type) {
            if (type.IsGenericTypeDefinition) {
                var baseName = type.Name.Split('`')[0];
                var argCount = type.GetGenericArguments().Length;
                return $"{baseName}<{new string(',', argCount - 1).Replace(",", "T,")}T>";
            }
            
            if (!type.IsGenericType)
                return type.Name;

            var genericBaseName = type.Name.Split('`')[0];
            var args = string.Join(", ", Array.ConvertAll(type.GetGenericArguments(), GetFullTypeName));
            return $"{genericBaseName}<{args}>";
        }

        int GetNestingDepth(Type type) {
            if (!type.IsGenericType)
                return 0;

            int maxDepth = 0;
            foreach (var arg in type.GetGenericArguments())
                maxDepth = Mathf.Max(maxDepth, GetNestingDepth(arg));
            
            return maxDepth + 1;
        }
    }


    // ============================================================================
    // MARKER INTERFACES
    // ============================================================================

    /// <summary>
    /// Marker interface for SerializedType example types.
    /// Used for basic testing of SerializedType functionality.
    /// </summary>
    [SerializedTypeId("9aa7e154-b9a4-4fb3-94d2-70264716b425")]
    public interface ISerializedTypeExample { }

    // Real-world pattern interfaces
    public interface IDamageEffect { }
    public interface IRepository { }
    public interface IStrategy { }
    public interface IElement { }
    public interface IData { }
    public interface IStat { }
    public interface ICalculation { }
    public interface IModifier { }

    // ============================================================================
    // BASIC TEST TYPES - Concrete implementations
    // ============================================================================
    
    [SerializedTypeId("d76694a7-cd8a-4a92-b123-c441afbf66a8")]
    public sealed class BasicExample : ISerializedTypeExample { }
    [SerializedTypeId("99a88135-33b0-4449-9bed-7333024a64fa")]
    public sealed class AdvancedExample : ISerializedTypeExample { }
    [SerializedTypeId("fca50236-aa99-4fe9-98f5-77cd521be124")]
    public sealed class ConcreteExample : ISerializedTypeExample { }
    [SerializedTypeId("910984a1-6886-49ab-b5a0-07e4d6973cbb")]
    public sealed class SimpleType : ISerializedTypeExample { }

    // ============================================================================
    // GENERIC TEST TYPES - Single parameter
    // ============================================================================
    
    [SerializedTypeId("e6b3582c-5131-4588-9574-63c38d1add93")]
    public sealed class GenericExample<T> : ISerializedTypeExample 
        where T : ISerializedTypeExample 
    { }
    
    [SerializedTypeId("b5fe5059-8fa3-4faf-9801-89b7303e348f")]
    public sealed class Container<T> : ISerializedTypeExample 
        where T : ISerializedTypeExample 
    { }
    
    [SerializedTypeId("130255ba-1449-4f5c-ad9f-1f5915356047")]
    public sealed class Wrapper<T> : ISerializedTypeExample 
        where T : ISerializedTypeExample 
    { }
    
    [SerializedTypeId("732f452a-e705-4590-8a52-dbdd5f33fbce")]
    public sealed class Holder<T> : ISerializedTypeExample 
        where T : ISerializedTypeExample 
    { }
    
    // ============================================================================
    // MULTI-PARAMETER GENERIC TYPES
    // ============================================================================
    
    [SerializedTypeId("94c47571-43db-4bb6-9b87-a1e1b7d3346e")]
    public sealed class Pair<T1, T2> : ISerializedTypeExample 
        where T1 : ISerializedTypeExample 
        where T2 : ISerializedTypeExample 
    { }
    
    [SerializedTypeId("2e0a9acd-e632-4e27-a3dd-0525c9252a5d")]
    public sealed class Triplet<T1, T2, T3> : ISerializedTypeExample 
        where T1 : ISerializedTypeExample 
        where T2 : ISerializedTypeExample 
        where T3 : ISerializedTypeExample 
    { }

    [SerializedTypeId("b175c025-1834-451d-be19-859aca5343e8")]
    public sealed class MegaWrapper<T1, T2, T3, T4, T5> : ISerializedTypeExample
            where T1 : ISerializedTypeExample
            where T2 : ISerializedTypeExample
            where T3 : ISerializedTypeExample
            where T4 : ISerializedTypeExample
            where T5 : ISerializedTypeExample
    { }

    [SerializedTypeId("4c863361-c3d1-4433-8bb6-014de5cbd684")]
    public sealed class MegaWrapper2<T1, T2> : ISerializedTypeExample
            where T1 : ISerializedTypeExample
            where T2 : ISerializedTypeExample
    { }

    [SerializedTypeId("7dbe9a4b-20b1-4fe4-b150-988bbe263c37")]
    public sealed class MegaWrapper3<T1, T2, T3> : ISerializedTypeExample
            where T1 : ISerializedTypeExample
            where T2 : ISerializedTypeExample
            where T3 : ISerializedTypeExample
    { }

    [SerializedTypeId("668eff23-73d3-4eb5-a69d-e0fcdd12c3d3")]
    public sealed class MegaWrapper5<T1, T2, T3, T4, T5> : ISerializedTypeExample
            where T1 : ISerializedTypeExample
            where T2 : ISerializedTypeExample
            where T3 : ISerializedTypeExample
            where T4 : ISerializedTypeExample
            where T5 : ISerializedTypeExample
    { }

    // ============================================================================
    // REAL-WORLD SCENARIO 1: DAMAGE SYSTEM
    // ============================================================================

    // Element types
    public sealed class FireElement : IElement { }
    public sealed class IceElement : IElement { }
    public sealed class LightningElement : IElement { }
    public sealed class PhysicalElement : IElement { }
    public sealed class BurningElement : IElement { }

    [SerializedTypeId("9fe30de9-d454-4fc8-9b20-8c68ddc8b940")]

    // Concrete damage types
    public sealed class FireDamage : IDamageEffect { }

    [SerializedTypeId("7d82e8fc-6d97-4331-b6c0-c284f18a9b0d")]
    public sealed class IceDamage : IDamageEffect { }

    [SerializedTypeId("6892d196-46ac-4300-90e2-834e39b764d6")]
    public sealed class PhysicalDamage : IDamageEffect { }

    [SerializedTypeId("eebf4f8d-33f9-4632-a814-98b3dc00be1b")]
    public sealed class PureDamage : IDamageEffect { }

    // Generic damage types
    public sealed class ElementalDamage<TElement> : IDamageEffect 
        where TElement : IElement 
    { }
    
    public sealed class DualElementDamage<TElement1, TElement2> : IDamageEffect 
        where TElement1 : IElement 
        where TElement2 : IElement 
    { }
    
    public sealed class TripleElementDamage<TElement1, TElement2, TElement3> : IDamageEffect 
        where TElement1 : IElement 
        where TElement2 : IElement 
        where TElement3 : IElement 
    { }
    
    // Nested damage wrapper
    public sealed class DamageWrapper<TDamage> : IDamageEffect 
        where TDamage : IDamageEffect 
    { }
    
    public sealed class DamageCombo<TDamage1, TDamage2> : IDamageEffect 
        where TDamage1 : IDamageEffect 
        where TDamage2 : IDamageEffect 
    { }
    
    // Complex nested types for deep testing
    public sealed class ComplexDamage<T1, T2, T3> : IDamageEffect 
        where T1 : IDamageEffect 
        where T2 : IDamageEffect 
        where T3 : IDamageEffect 
    { }

    // ============================================================================
    // REAL-WORLD SCENARIO 2: REPOSITORY PATTERN
    // ============================================================================
    
    public sealed class PlayerData : IData { }
    public sealed class EnemyData : IData { }
    public sealed class ItemData : IData { }
    
    public sealed class HealthStat : IStat { }
    public sealed class ManaStat : IStat { }
    public sealed class StaminaStat : IStat { }
    
    // Generic repository
    public sealed class Repository<TData> : IRepository 
        where TData : class, IData 
    { }
    
    // Generic data with stats
    public sealed class StatData<TStat> : IData 
        where TStat : IStat 
    { }
    
    // Nested repository example: Repository<StatData<HealthStat>>
    public sealed class AdvancedRepository<TData, TStat> : IRepository 
        where TData : IData 
        where TStat : IStat 
    { }
    
    public sealed class CachedRepository<TData> : IRepository 
        where TData : class, IData 
    { }

    // ============================================================================
    // REAL-WORLD SCENARIO 3: STRATEGY PATTERN
    // ============================================================================
    
    public sealed class DamageCalculation : ICalculation { }
    public sealed class HealingCalculation : ICalculation { }
    public sealed class DefenseCalculation : ICalculation { }
    
    public sealed class CriticalModifier : IModifier { }
    public sealed class ArmorModifier : IModifier { }
    public sealed class SpeedModifier : IModifier { }
    
    // Generic strategy
    public sealed class Strategy<TCalculation> : IStrategy 
        where TCalculation : ICalculation 
    { }
    
    // Generic calculation with modifier
    public sealed class ModifiedCalculation<TModifier> : ICalculation 
        where TModifier : IModifier 
    { }
    
    // Nested strategy example: Strategy<ModifiedCalculation<CriticalModifier>>
    public sealed class ComplexStrategy<TCalc, TMod> : IStrategy 
        where TCalc : ICalculation 
        where TMod : IModifier 
    { }
    
    public sealed class ChainedStrategy<TStrat1, TStrat2> : IStrategy 
        where TStrat1 : IStrategy 
        where TStrat2 : IStrategy 
    { }

    // ============================================================================
    // GENERIC CONSTRAINT EXAMPLES
    // Covers: class, struct, new(), interface, and combined constraints.
    // Used as manual inspection targets and potential test fixtures.
    // ============================================================================

    /// <summary>Shared marker interface for constraint example types.</summary>
    public interface IConstraintExample { }

    /// <summary>Concrete class implementing <see cref="IConstraintExample"/>.</summary>
    [SerializedTypeId("e696d4af-51f4-44ea-832c-90edcfcc87ac")]
    public sealed class ConcreteConstraintImpl : IConstraintExample { }

    /// <summary>Struct implementing <see cref="IConstraintExample"/>.</summary>
    [SerializedTypeId("e98b1b58-0719-4d44-a68e-38c188cf4793")]
    public struct ConstraintStructImpl : IConstraintExample { }

    /// <summary>Interface-only constraint: <c>where T : IConstraintExample</c>.</summary>
    public sealed class ConstraintInterface<T> : IConstraintExample
        where T : IConstraintExample
    { }

    /// <summary>Interface + new() constraint: <c>where T : IConstraintExample, new()</c>.</summary>
    public sealed class ConstraintNew<T> : IConstraintExample
        where T : IConstraintExample, new()
    { }

    /// <summary>Class + interface constraint: <c>where T : class, IConstraintExample</c>.</summary>
    public sealed class ConstraintClass<T> : IConstraintExample
        where T : class, IConstraintExample
    { }

    /// <summary>Struct + interface constraint: <c>where T : struct, IConstraintExample</c>.</summary>
    public sealed class ConstraintStruct<T> : IConstraintExample
        where T : struct, IConstraintExample
    { }

    /// <summary>Struct wrapper with interface constraint: <c>where T : IConstraintExample</c> (struct itself).</summary>
    public struct ConstraintStructWrapper<T> : IConstraintExample
        where T : IConstraintExample
    { }

    /// <summary>
    /// Class with no public parameterless constructor.
    /// Should be excluded by <c>new()</c> constraint filtering.
    /// </summary>
    [SerializedTypeId("23f53efb-4e66-48cf-ab92-c8e9996a0499")]
    public sealed class ConstraintNoDefaultConstructor : IConstraintExample
    {
        public ConstraintNoDefaultConstructor(int value) { _ = value; }
    }

    /// <summary>Class + interface + new() constraint: <c>where T : class, IConstraintExample, new()</c>.</summary>
    public sealed class ConstraintClassNew<T> : IConstraintExample
        where T : class, IConstraintExample, new()
    { }

    /// <summary>Struct + interface + new() constraint: <c>where T : struct, IConstraintExample</c> (new() implied).</summary>
    public sealed class ConstraintStructNew<T> : IConstraintExample
        where T : struct, IConstraintExample
    { }
}
