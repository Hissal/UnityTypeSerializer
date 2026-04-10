# Changelog

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
