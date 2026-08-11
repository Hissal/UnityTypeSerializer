# SerializedTypeGenerators

`SerializedTypeGenerators` contains the Roslyn generator/analyzer/code-fix logic used by UnityTypeSerializer.

## What this project does

- **Source generator**: emits `ISerializedTypeIdRegistrationProvider` implementations so `[SerializedTypeId]` types are registered at runtime.
- **Analyzer**: validates `SerializedType<>` usage and id hygiene.
- **Code fix**: assists with adding missing `SerializedTypeId` attributes.

## Diagnostics

- `STG001` (error): duplicate `SerializedTypeId` value.
- `STG100` (warning): candidate type is likely missing `SerializedTypeId`.
- `STG101` (info): `SerializedTypeId` value is not GUID-shaped.

## Manifest dependency

Cross-assembly checks use the generated Unity manifest:

- `ProjectSettings/Hissal/UnityTypeSerializer/SerializedTypeUsageManifest.xml`

The analyzer reads this file by name from Roslyn additional files. It does not perform ambient filesystem reads.

## Build and Unity integration

This repository keeps generator sources under `SerializedTypeGenerators~` so Unity does not import project sources directly.

After building the analyzer locally, copy the produced `SerializedTypeGenerators.dll` (and optional `.pdb`) to:

- `Assets/_Project/SourceGeneration/`

Unity imports that DLL as a Roslyn analyzer via:

- `Assets/_Project/SourceGeneration/SerializedTypeGenerators.dll.meta` (`RoslynAnalyzer` label)
