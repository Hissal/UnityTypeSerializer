# Changelog

## v 0.4.3
- Improved `STG100` eligibility analysis to include significantly more likely serialized types.
- Added support for non-generic `SerializedType` usage in `STG100` analysis when constraints are provided via `SerializedTypeOptions`.
- Updated constraint matching to combine all configured constraint sources: generic base constraint (`SerializedType<TBase>`), `InheritsOrImplementsAll`, and `InheritsOrImplementsAny`.
- Fixed generic type detection so `AllowGenericTypeConstruction = true` now enables generic-type eligibility analysis even when `AllowOpenGenerics` is `false`.
- Added generic-argument constraint propagation for serializable generic types (for example `where T : IInterface`) so interface-constrained argument candidates are also considered and warned by `STG100`.

## v 0.4.2
- Moved `SerializedTypeUsageManifest.xml` generation to `Library/Hissal/UnityTypeSerializer/` so package imports no longer modify immutable package files.

## v 0.4.1
- Updated `Examples/SerializedTypeExample.cs` input handling to avoid a hard Input System dependency by using `#if ENABLE_INPUT_SYSTEM` and falling back to legacy input (`ENABLE_LEGACY_INPUT_MANAGER`) for the space-key log action.

## v 0.4.0
- Added `SerializedTypeIdAttribute` and `SerializedTypeIdRegistry` for stable id-based type resolution that survives type/assembly-qualified-name churn.
- Added full serialized type-tree persistence (`typeId` + `aqn` + nested generic nodes) with runtime fallback order: type tree -> type id -> legacy AQN.
- Added runtime registration-provider discovery via `ISerializedTypeIdRegistrationProvider` with deterministic ordering and first-wins duplicate handling.
- Added type-tree mismatch detection and repair flows (`Match TypeId` / `Match AQN`) for unsynced serialized nodes.
- Added Odin validators for `SerializedType` values and global duplicate id validation (`SerializedTypeIdUniquenessValidator`) under `#if ODIN_VALIDATOR`.
- Added Roslyn generator/analyzer/code-fix pipeline (`SerializedTypeGenerators`) with diagnostics: `STG001` (duplicate id), `STG100` (likely missing id), `STG101` (non-GUID id).
- Added serialized-usage manifest generation (`SerializedTypeUsageManifest.xml`) so analyzers can evaluate `SerializedType<>` constraints across assemblies.

## v 0.3.2
- Added shared selected-type validation in editor core so inspector drawers and Odin Validator use the same validity rules.
- Added internal raw AQN exposure on `SerializedType`/`SerializedType<TBase>` and accessor support to distinguish empty selections from unresolved serialized type references.
- Updated dropdown display behavior: empty selection remains `None`, unresolved serialized selection is shown as `Invalid`.
- Added inline inspector error messages in constructor drawer mode (inline mode already displayed validation errors).
- Added Odin Validator integration for `SerializedType` and `SerializedType<TBase>`, fully guarded with `#if ODIN_VALIDATOR`.
- Enforced options mismatches as validation errors (for example open generic selections when `AllowOpenGenerics` is disabled, type-kind/inheritance/filter mismatches).

## v 0.3.1
- Added `None` option to `SerializedType` drawers: always visible at the root type dropdown, and visible in nested generic argument dropdowns only when `AllowOpenGenerics` is enabled.
- Selecting `None` at root clears the serialized type; selecting `None` for a nested generic argument reverts the type to its open generic definition.

## v 0.3.0
- Redesigned `SerializedTypeKind` with expanded type categories (`Class`, `Struct`, `Static`, `Enum`, `Delegate`, `Primitive`, `Object`, `All`), marked `Concrete` obsolete as an alias of `Object`, and updated drawer filtering semantics.

## v 0.2.1
- Added `SerializedType.IsValid` property to check if a valid type is set
- Marked `SerializedType.HasType` as obsolete (use `IsValid` instead)
- Added type caching to improve performance when accessing `SerializedType.Type` property
- Added error logging when an invalid assembly-qualified name is detected
- Cache is automatically invalidated when setting a new type

## v 0.2.0
- Added `OnTypeChanged` callback to `SerializedTypeOptions` for editor notifications when type changes
