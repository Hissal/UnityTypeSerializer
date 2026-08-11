# Changelog

## v 0.5.1

- Pass a persistent `ProjectSettings` usage-manifest snapshot directly to Roslyn analyzers and migrate unsafe `csc.rsp` self-references.

## v 0.5.0
- Preserve private-repository authentication and report failed pushes when release automation prepares the next CHANGELOG section.
- Treat a missing usage manifest as empty by keeping `csc.rsp` compiler inputs valid across fresh or deleted `Library` folders.
- Add project-scoped, asset-driven `csc.rsp` setup for enabling usage-manifest analysis in selected Assets assemblies without removing existing compiler options.
- Skip automatic usage-manifest reflection scans when a domain reload has no compilation or code-assembly invalidation.

## v 0.4.8
- Add subtle Inspector and Odin Validator warnings for obsolete `SerializedType` references, and mark obsolete picker entries at the bottom of type dropdowns.
- Add a per-user preference for disabling automatic usage manifest rebuilds while retaining manual rebuild actions.
- Add `ExplicitTypeList`, `ExcludedTypes`, and `InheritsOrImplementsNone` filtering across drawers, validation, usage manifests, and `STG100` analysis.
- Make `CustomTypeFilter` the sole configurable filter when set while retaining base-type and generic validity requirements.
- Add inspector examples with unambiguous types for explicit, exact-type exclusion, and inheritance-based exclusion filters.

## v 0.4.7
- Improved `STG100` and Odin eligibility messages to include serialized field origins and `SerializedTypeOptions` constraints.
- Fixed object-based non-generic `SerializedType` manifest entries so unresolved option constraints no longer warn every object type.

## v 0.4.6
- Optimized `SerializedType` type resolution performance by caching resolved `System.Type` instances and invalidating the cache when the serialized type changes.
- Added Odin Validator for Type serialization eligibility, with support for all `SerializedTypeOptions` constraints and detailed error messages, fully guarded with `#if ODIN_VALIDATOR`.

## v 0.4.5
- Fixed `STSG100` treating both type kinds `Class` and `Struct` as `Object`.

## v 0.4.4
- Improved `STG100` analyzer eligibility checks so external manifest constraints are still processed when runtime contracts are not referenced locally.
- Added resilient `SerializedTypeId` attribute detection paths for unresolved symbols, including both short/long attribute names and metadata names.
- Added analyzer test coverage for no-runtime-contract scenarios and updated test helpers to support explicitly excluding runtime contracts.

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
