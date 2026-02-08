using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Hissal.UnityTypeSerializer {
    /// <summary>
    /// Comprehensive test cases for TypeRef with nested generic type construction.
    /// Demonstrates various scenarios including deep nesting, multiple constraints, and real-world patterns.
    /// </summary>
    public sealed class TypeRefExample : MonoBehaviour {
        [Title("TypeRef Examples & Tests", bold: true)]
        [InfoBox("This script demonstrates TypeRef capabilities with various test scenarios.\n" +
                 "Use the button below to log type information for all configured fields.")]
        
        [Title("Drawer Mode Tests", bold: true)]
        [InfoBox("Inline Mode (Default) - Single line with multiple dropdowns\n" +
                 "This is the new default drawer mode.")]
        [SerializeField]
        [TypeRefOptions(allowGenericTypeConstruction: true)]
        TypeRef<ITypeRefExample>? inlineModeDefault;
        
        [InfoBox("Complex Constructor Mode (Opt-in) - Step-by-step nested UI\n" +
                 "Use UseComplexConstructor = true to enable the original complex constructor.")]
        [SerializeField]
        [TypeRefOptions(allowGenericTypeConstruction: true, useComplexConstructor: true)]
        TypeRef<ITypeRefExample>? complexConstructorMode;
        
        [Title("Basic Options", bold: true)]
        [InfoBox("Default behavior - only concrete types are shown.")]
        [SerializeField]
        TypeRef<ITypeRefExample>? concreteOnly;

        [InfoBox("AllowGenericTypeConstruction = true\n" +
                 "Shows generic types in dropdown. Selection forces immediate construction (no open generics allowed).")]
        [SerializeField]
        [TypeRefOptions(allowGenericTypeConstruction: true)]
        TypeRef<ITypeRefExample>? constructionRequired;

        [InfoBox("AllowOpenGenerics = true\n" +
                 "Shows generic types but allows selecting them WITHOUT construction (e.g., List<>).")]
        [SerializeField]
        [TypeRefOptions(allowOpenGenerics: true)]
        TypeRef<ITypeRefExample>? openGenericsAllowed;

        [InfoBox("Both AllowGenericTypeConstruction and AllowOpenGenerics = true\n" +
                 "Selecting a generic type assigns it immediately, but a '▶ Construct' button appears to optionally construct it.")]
        [SerializeField]
        [TypeRefOptions(allowGenericTypeConstruction: true, allowOpenGenerics: true)]
        TypeRef<ITypeRefExample>? optionalConstruction;

        [Title("Self-Nesting Option", bold: true)]
        [InfoBox("AllowSelfNesting = false (default)\n" +
                 "Prevents recursive nesting like Wrapper<Wrapper<Wrapper<...>>>")]
        [SerializeField]
        [TypeRefOptions(allowGenericTypeConstruction: true)]
        TypeRef<ITypeRefExample>? noSelfNesting;

        [InfoBox("AllowSelfNesting = true\n" +
                 "Allows recursive nesting like Wrapper<Wrapper<int>>")]
        [SerializeField]
        [TypeRefOptions(allowGenericTypeConstruction: true, allowSelfNesting: true)]
        TypeRef<ITypeRefExample>? selfNestingAllowed;

        [Title("Complex Nested Examples", bold: true)]
        [InfoBox("Deeply nested generic construction test")]
        [SerializeField]
        [TypeRefOptions(allowGenericTypeConstruction: true)]
        TypeRef<ITypeRefExample>? deeplyNested;

        [InfoBox("Extreme nesting with multiple parameters at each level")]
        [SerializeField]
        [TypeRefOptions(allowGenericTypeConstruction: true, allowSelfNesting: true)]
        TypeRef<ITypeRefExample>? extremeNesting;

        [Title("Real-World Scenarios", bold: true)]
        [InfoBox("Damage effect system - supports elemental damage with nested types")]
        [SerializeField]
        [TypeRefOptions(allowGenericTypeConstruction: true)]
        TypeRef<IDamageEffect>? damageEffect;

        [InfoBox("Repository pattern - generic data storage")]
        [SerializeField]
        [TypeRefOptions(allowGenericTypeConstruction: true)]
        TypeRef<IRepository>? repository;

        [InfoBox("Strategy pattern - pluggable algorithms")]
        [SerializeField]
        [TypeRefOptions(allowGenericTypeConstruction: true)]
        TypeRef<IStrategy>? strategy;

        [Button("Log All Type Infos", ButtonSizes.Large), GUIColor(0.4f, 0.8f, 1f)]
        void LogAllTypeInfos() {
            Debug.Log("=== TypeRef Test Cases - Type Information ===\n");
            
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
            
            Debug.Log("\n=== End of Type Information ===");
        }

        void LogTypeInfo<T>(string testName, TypeRef<T>? typeRef) where T : class {
            if (typeRef?.HasType != true) {
                Debug.Log($"[{testName}] No type selected");
                return;
            }

            var type = typeRef.Type;
            var typeDescription = type.IsGenericTypeDefinition 
                ? $"{GetFullTypeName(type)} (OPEN GENERIC)" 
                : GetFullTypeName(type);
                
            Debug.Log($"[{testName}] Selected: {typeDescription}");
            
            if (type.IsGenericType && !type.IsGenericTypeDefinition) {
                var genericArgs = type.GetGenericArguments();
                Debug.Log($"  └─ Generic arguments: {string.Join(", ", System.Array.ConvertAll(genericArgs, GetFullTypeName))}");
                Debug.Log($"  └─ Nesting depth: {GetNestingDepth(type)}");
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
    /// Marker interface for TypeRef example types.
    /// Used for basic testing of TypeRef functionality.
    /// </summary>
    public interface ITypeRefExample { }
    
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
    
    public sealed class BasicExample : ITypeRefExample { }
    public sealed class AdvancedExample : ITypeRefExample { }
    public sealed class ConcreteExample : ITypeRefExample { }
    public sealed class SimpleType : ITypeRefExample { }

    // ============================================================================
    // GENERIC TEST TYPES - Single parameter
    // ============================================================================
    
    public sealed class GenericExample<T> : ITypeRefExample 
        where T : ITypeRefExample 
    { }
    
    public sealed class Container<T> : ITypeRefExample 
        where T : ITypeRefExample 
    { }
    
    public sealed class Wrapper<T> : ITypeRefExample 
        where T : ITypeRefExample 
    { }
    
    public sealed class Holder<T> : ITypeRefExample 
        where T : ITypeRefExample 
    { }

    // ============================================================================
    // MULTI-PARAMETER GENERIC TYPES
    // ============================================================================
    
    public sealed class Pair<T1, T2> : ITypeRefExample 
        where T1 : ITypeRefExample 
        where T2 : ITypeRefExample 
    { }
    
    public sealed class Triplet<T1, T2, T3> : ITypeRefExample 
        where T1 : ITypeRefExample 
        where T2 : ITypeRefExample 
        where T3 : ITypeRefExample 
    { }
    
    public sealed class MegaWrapper<T1, T2, T3, T4, T5> : ITypeRefExample 
        where T1 : ITypeRefExample 
        where T2 : ITypeRefExample 
        where T3 : ITypeRefExample 
        where T4 : ITypeRefExample 
        where T5 : ITypeRefExample 
    { }
    
    public sealed class MegaWrapper2<T1, T2> : ITypeRefExample 
        where T1 : ITypeRefExample 
        where T2 : ITypeRefExample 
    { }
    
    public sealed class MegaWrapper3<T1, T2, T3> : ITypeRefExample 
        where T1 : ITypeRefExample 
        where T2 : ITypeRefExample 
        where T3 : ITypeRefExample 
    { }
    
    public sealed class MegaWrapper5<T1, T2, T3, T4, T5> : ITypeRefExample 
        where T1 : ITypeRefExample 
        where T2 : ITypeRefExample 
        where T3 : ITypeRefExample 
        where T4 : ITypeRefExample 
        where T5 : ITypeRefExample 
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
    
    // Concrete damage types
    public sealed class FireDamage : IDamageEffect { }
    public sealed class IceDamage : IDamageEffect { }
    public sealed class PhysicalDamage : IDamageEffect { }
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
}
