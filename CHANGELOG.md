# Changelog

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
