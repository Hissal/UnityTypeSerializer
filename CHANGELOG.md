# Changelog

## v ...

## v 0.2.1
- Added `SerializedType.IsValid` property to check if a valid type is set
- Marked `SerializedType.HasType` as obsolete (use `IsValid` instead)
- Added type caching to improve performance when accessing `SerializedType.Type` property
- Added error logging when an invalid assembly-qualified name is detected
- Cache is automatically invalidated when setting a new type

## v 0.2.0
- Added `OnTypeChanged` callback to `SerializedTypeOptions` for editor notifications when type changes