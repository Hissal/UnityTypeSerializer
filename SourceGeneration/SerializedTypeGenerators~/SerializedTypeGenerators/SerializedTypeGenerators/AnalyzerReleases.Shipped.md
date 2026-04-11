; Shipped analyzer releases
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### Release 0.4.0

#### New Rules

| Rule ID | Category                 | Severity | Notes                                           |
|---------|--------------------------|----------|-------------------------------------------------|
| STG001  | SerializedTypeGenerators | Error    | SerializedTypeIdRegistryGenerator Duplicate ID  |
| STG100  | SerializedTypeGenerators | Warning  | Likely serialized type missing SerializedTypeId |
| STG101  | SerializedTypeGenerators | Info     | SerializedTypeId should use GUID format         |
