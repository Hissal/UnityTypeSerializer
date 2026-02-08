# Unity Type Serializer

A Unity package providing serializable type references with support for generic type construction and inspector integration.

## Features

- **Serializable Type References**: Store and serialize references to types that derive from a specified base class or interface
- **Generic Type Construction**: Build closed generic types from open generic definitions in the Unity Inspector
- **Flexible Configuration**: Control type filtering, generic handling, and validation through attributes
- **Inspector Integration**: Seamless Unity Editor integration with Odin Inspector support

## Core Types

- `SerializedType<TBase>`: A fully serializable, inspector-constructible representation of a type
- `SerializedTypeOptionsAttribute`: Configure filtering and behavior for SerializedType fields
- `SerializedTypeDrawer`: Custom Unity Editor drawer with inline and complex constructor modes

## Usage

See `SerializedTypeExample.cs` for comprehensive examples and test cases.

## Requirements

- Unity 6000.3 or later
- Odin Inspector (required for editor integration)

