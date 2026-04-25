using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SerializedTypeGenerators;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SerializedTypeIdEligibilityAnalyzer : DiagnosticAnalyzer {
    const string SERIALIZED_TYPE_NON_GENERIC_METADATA_NAME = "Hissal.UnityTypeSerializer.SerializedType";
    const string SERIALIZED_TYPE_GENERIC_METADATA_NAME = "Hissal.UnityTypeSerializer.SerializedType`1";
    const string SERIALIZED_TYPE_ID_ATTRIBUTE_METADATA_NAME = "Hissal.UnityTypeSerializer.SerializedTypeIdAttribute";
    const string SERIALIZED_TYPE_ID_ATTRIBUTE_NAME = "SerializedTypeIdAttribute";
    const string SERIALIZED_TYPE_ID_SHORT_ATTRIBUTE_NAME = "SerializedTypeId";
    const string SERIALIZED_TYPE_OPTIONS_ATTRIBUTE_METADATA_NAME = "Hissal.UnityTypeSerializer.SerializedTypeOptionsAttribute";
    const string SERIALIZED_TYPE_USAGE_MANIFEST_FILE_NAME = "SerializedTypeUsageManifest.xml";
    const string MANIFEST_FALLBACK_RELATIVE_PATH = "Library/Hissal/UnityTypeSerializer/SerializedTypeUsageManifest.xml";

    const int TYPE_KIND_CLASS = 1 << 0;
    const int TYPE_KIND_STRUCT = 1 << 1;
    const int TYPE_KIND_ABSTRACT = 1 << 2;
    const int TYPE_KIND_INTERFACE = 1 << 3;
    const int TYPE_KIND_STATIC = 1 << 4;
    const int TYPE_KIND_ENUM = 1 << 5;
    const int TYPE_KIND_DELEGATE = 1 << 6;
    const int TYPE_KIND_PRIMITIVE = 1 << 7;
    const int TYPE_KIND_OBJECT = TYPE_KIND_CLASS | TYPE_KIND_STRUCT;
    const int TYPE_KIND_ALL = TYPE_KIND_CLASS | TYPE_KIND_STRUCT | TYPE_KIND_ABSTRACT | TYPE_KIND_INTERFACE | TYPE_KIND_STATIC | TYPE_KIND_ENUM | TYPE_KIND_DELEGATE | TYPE_KIND_PRIMITIVE;

    static readonly DiagnosticDescriptor s_missingSerializedTypeIdDescriptor = new(
        id: "STG100",
        title: "Likely serialized type is missing SerializedTypeId",
        messageFormat: "Type '{0}' matches SerializedType constraints ({1}) and should likely have [SerializedTypeId]",
        category: "SerializedTypeGenerators",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        s_missingSerializedTypeIdDescriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static startContext => {
            var serializedTypeNonGenericSymbol = startContext.Compilation.GetTypeByMetadataName(SERIALIZED_TYPE_NON_GENERIC_METADATA_NAME);
            var serializedTypeGenericSymbol = startContext.Compilation.GetTypeByMetadataName(SERIALIZED_TYPE_GENERIC_METADATA_NAME);
            var serializedTypeIdAttributeSymbol = startContext.Compilation.GetTypeByMetadataName(SERIALIZED_TYPE_ID_ATTRIBUTE_METADATA_NAME);
            var serializedTypeOptionsAttributeSymbol = startContext.Compilation.GetTypeByMetadataName(SERIALIZED_TYPE_OPTIONS_ATTRIBUTE_METADATA_NAME);
            var objectTypeSymbol = startContext.Compilation.GetSpecialType(SpecialType.System_Object);

            var allTypes = EnumerateNamedTypes(startContext.Compilation.Assembly.GlobalNamespace)
                .ToImmutableArray();

            var constraintsBuilder = ImmutableArray.CreateBuilder<FieldConstraint>();

            var canCollectLocalFieldConstraints = serializedTypeGenericSymbol is not null
                && serializedTypeOptionsAttributeSymbol is not null
                && objectTypeSymbol is not null;
            if (canCollectLocalFieldConstraints) {
                constraintsBuilder.AddRange(CollectFieldConstraints(
                    allTypes,
                    objectTypeSymbol!,
                    serializedTypeNonGenericSymbol,
                    serializedTypeGenericSymbol!,
                    serializedTypeOptionsAttributeSymbol!));
            }

            var externalConstraints = CollectExternalFieldConstraints(startContext.Compilation, startContext.Options);
            if (!externalConstraints.IsDefaultOrEmpty) {
                constraintsBuilder.AddRange(externalConstraints);
            }

            var constraints = constraintsBuilder.ToImmutable();
            if (constraints.IsDefaultOrEmpty)
                return;

            var propagatedConstraintTypes = CollectPropagatedGenericParameterConstraints(allTypes, constraints);

            startContext.RegisterSymbolAction(symbolContext => AnalyzeNamedType(
                (INamedTypeSymbol)symbolContext.Symbol,
                serializedTypeIdAttributeSymbol,
                constraints,
                propagatedConstraintTypes,
                symbolContext), SymbolKind.NamedType);
        });
    }

    static void AnalyzeNamedType(
        INamedTypeSymbol namedType,
        INamedTypeSymbol? serializedTypeIdAttributeSymbol,
        ImmutableArray<FieldConstraint> constraints,
        ImmutableArray<INamedTypeSymbol> propagatedConstraintTypes,
        SymbolAnalysisContext context) {

        if (!HasSourceLocation(namedType))
            return;

        if (HasSerializedTypeIdAttribute(namedType, serializedTypeIdAttributeSymbol))
            return;

        if (constraints.IsDefaultOrEmpty)
            return;

        var reasons = ImmutableArray.CreateBuilder<string>();
        foreach (var constraint in constraints) {
            if (MatchesConstraint(namedType, constraint, out var matchReason))
                reasons.Add(matchReason);
        }

        foreach (var propagatedConstraintType in propagatedConstraintTypes) {
            var isSelfConstraintMatch = SymbolEqualityComparer.Default.Equals(namedType, propagatedConstraintType);
            if (isSelfConstraintMatch && !IsConcretePropagatedConstraintType(propagatedConstraintType))
                continue;

            if (IsAssignableTo(namedType, propagatedConstraintType)) {
                reasons.Add($"generic parameter constraint '{propagatedConstraintType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)}'");
            }
        }

        if (reasons.Count == 0)
            return;

        var location = namedType.Locations.FirstOrDefault(l => l.IsInSource);
        if (location is null)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            s_missingSerializedTypeIdDescriptor,
            location,
            namedType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
            BuildReasonText(reasons)));
    }


    static ImmutableArray<FieldConstraint> CollectFieldConstraints(
        ImmutableArray<INamedTypeSymbol> allTypes,
        INamedTypeSymbol objectTypeSymbol,
        INamedTypeSymbol? serializedTypeNonGenericSymbol,
        INamedTypeSymbol serializedTypeGenericSymbol,
        INamedTypeSymbol serializedTypeOptionsAttributeSymbol) {

        var builder = ImmutableArray.CreateBuilder<FieldConstraint>();

        foreach (var fieldSymbol in EnumerateFieldSymbols(allTypes)) {
            AddFieldConstraints(
                fieldSymbol,
                allTypes,
                objectTypeSymbol,
                serializedTypeNonGenericSymbol,
                serializedTypeGenericSymbol,
                serializedTypeOptionsAttributeSymbol,
                builder);
        }

        return builder.ToImmutable();
    }

    static ImmutableArray<FieldConstraint> CollectExternalFieldConstraints(Compilation compilation, AnalyzerOptions options) {
        var builder = ImmutableArray.CreateBuilder<FieldConstraint>();
        var sawManifestAdditionalFile = false;

        foreach (var additionalFile in options.AdditionalFiles) {
            var fileName = System.IO.Path.GetFileName(additionalFile.Path);
            if (!string.Equals(fileName, SERIALIZED_TYPE_USAGE_MANIFEST_FILE_NAME, StringComparison.OrdinalIgnoreCase))
                continue;

            sawManifestAdditionalFile = true;
            var text = additionalFile.GetText();
            if (text is null)
                continue;

            var xmlContent = text.ToString();
            if (string.IsNullOrWhiteSpace(xmlContent))
                continue;

            AddExternalConstraintsFromXml(compilation, xmlContent, builder);
        }

        if (builder.Count == 0 && !sawManifestAdditionalFile) {
            var fallbackManifestXml = TryReadFallbackManifestXml();
            if (!string.IsNullOrWhiteSpace(fallbackManifestXml)) {
                AddExternalConstraintsFromXml(compilation, fallbackManifestXml!, builder);
            }
        }

        return builder.ToImmutable();
    }

#pragma warning disable RS1035 // Intentional opt-in fallback for Unity where AdditionalFiles are not globally wired.
    static string? TryReadFallbackManifestXml() {
        var fallbackManifestPath = FindFallbackManifestPath();
        if (string.IsNullOrEmpty(fallbackManifestPath))
            return null;

        try {
            return global::System.IO.File.ReadAllText(fallbackManifestPath);
        }
        catch {
            return null;
        }
    }

    static string? FindFallbackManifestPath() {
        var currentDirectory = global::System.IO.Directory.GetCurrentDirectory();
        if (string.IsNullOrEmpty(currentDirectory))
            return null;

        var directoryInfo = new global::System.IO.DirectoryInfo(currentDirectory);
        while (directoryInfo is not null) {
            var libraryCandidate = global::System.IO.Path.Combine(directoryInfo.FullName, MANIFEST_FALLBACK_RELATIVE_PATH);
            if (global::System.IO.File.Exists(libraryCandidate))
                return libraryCandidate;

            directoryInfo = directoryInfo.Parent;
        }


        return null;
    }
#pragma warning restore RS1035

    static void AddExternalConstraintsFromXml(Compilation compilation, string xmlContent, ImmutableArray<FieldConstraint>.Builder builder) {
        if (string.IsNullOrWhiteSpace(xmlContent))
            return;

        XDocument document;
        try {
            document = XDocument.Parse(xmlContent);
        }
        catch {
            return;
        }

        var root = document.Root;
        if (root is null)
            return;

        foreach (var entryElement in root.Elements("Entry")) {
            var customTypeFilter = (string?)entryElement.Attribute("customTypeFilter") ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(customTypeFilter))
                continue;

            var baseConstraintMetadataName = (string?)entryElement.Attribute("baseConstraint");
            if (string.IsNullOrWhiteSpace(baseConstraintMetadataName))
                continue;

            var baseConstraintSymbol = compilation.GetTypeByMetadataName(baseConstraintMetadataName!);
            if (baseConstraintSymbol is null)
                continue;

            var allowOpenGenerics = bool.TryParse((string?)entryElement.Attribute("allowOpenGenerics"), out var parsedAllowOpenGenerics)
                ? parsedAllowOpenGenerics
                : false;
            var allowGenericTypeConstruction = bool.TryParse((string?)entryElement.Attribute("allowGenericTypeConstruction"), out var parsedAllowGenericConstruction)
                ? parsedAllowGenericConstruction
                : false;

            var allowedTypeKinds = int.TryParse((string?)entryElement.Attribute("allowedTypeKinds"), out var parsedAllowedKinds)
                ? parsedAllowedKinds
                : TYPE_KIND_OBJECT;

            var inheritsAllSymbols = ResolveConstraintTypeList(compilation, (string?)entryElement.Attribute("inheritsAll"));
            if (inheritsAllSymbols.HasUnresolvedTypes)
                continue;

            var inheritsAnySymbols = ResolveConstraintTypeList(compilation, (string?)entryElement.Attribute("inheritsAny"));
            if (inheritsAnySymbols.HasRequestedTypes && inheritsAnySymbols.ResolvedTypes.IsDefaultOrEmpty)
                continue;

            if (IsSystemObject(baseConstraintSymbol) &&
                inheritsAllSymbols.ResolvedTypes.IsDefaultOrEmpty &&
                inheritsAnySymbols.ResolvedTypes.IsDefaultOrEmpty) {
                continue;
            }

            var declaringType = (string?)entryElement.Attribute("declaringType") ?? string.Empty;
            var fieldName = (string?)entryElement.Attribute("fieldName") ?? string.Empty;

            builder.Add(new FieldConstraint(
                baseConstraintSymbol,
                allowedTypeKinds,
                allowGenericTypeConstruction,
                allowOpenGenerics,
                inheritsAllSymbols.ResolvedTypes,
                inheritsAnySymbols.ResolvedTypes,
                BuildManifestConstraintReason(
                    baseConstraintSymbol,
                    allowedTypeKinds,
                    allowGenericTypeConstruction,
                    allowOpenGenerics,
                    inheritsAllSymbols.ResolvedTypes,
                    inheritsAnySymbols.ResolvedTypes,
                    declaringType,
                    fieldName)));
        }
    }

    static ResolvedConstraintTypeList ResolveConstraintTypeList(Compilation compilation, string? rawValue) {
        if (string.IsNullOrWhiteSpace(rawValue))
            return new ResolvedConstraintTypeList(ImmutableArray<INamedTypeSymbol>.Empty, false, false);

        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var entries = rawValue!.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        var hasRequestedTypes = false;
        var hasUnresolvedTypes = false;
        foreach (var entry in entries) {
            var metadataName = entry.Trim();
            if (string.IsNullOrEmpty(metadataName))
                continue;

            hasRequestedTypes = true;
            var symbol = compilation.GetTypeByMetadataName(metadataName);
            if (symbol is not null)
                builder.Add(symbol);
            else
                hasUnresolvedTypes = true;
        }

        return new ResolvedConstraintTypeList(builder.ToImmutable(), hasRequestedTypes, hasUnresolvedTypes);
    }

    static IEnumerable<INamedTypeSymbol> EnumerateNamedTypes(INamespaceSymbol namespaceSymbol) {
        foreach (var member in namespaceSymbol.GetMembers()) {
            if (member is INamespaceSymbol childNamespaceSymbol) {
                foreach (var nestedType in EnumerateNamedTypes(childNamespaceSymbol))
                    yield return nestedType;
                continue;
            }

            if (member is INamedTypeSymbol namedTypeSymbol) {
                yield return namedTypeSymbol;
                foreach (var nestedType in EnumerateNestedNamedTypes(namedTypeSymbol))
                    yield return nestedType;
            }
        }
    }

    static IEnumerable<INamedTypeSymbol> EnumerateNestedNamedTypes(INamedTypeSymbol namedTypeSymbol) {
        foreach (var nestedType in namedTypeSymbol.GetTypeMembers()) {
            yield return nestedType;
            foreach (var nestedNestedType in EnumerateNestedNamedTypes(nestedType))
                yield return nestedNestedType;
        }
    }

    static IEnumerable<IFieldSymbol> EnumerateFieldSymbols(IEnumerable<INamedTypeSymbol> namedTypes) {
        foreach (var namedType in namedTypes) {
            foreach (var member in namedType.GetMembers()) {
                if (member is IFieldSymbol fieldSymbol)
                    yield return fieldSymbol;
            }
        }
    }

    static void AddFieldConstraints(
        IFieldSymbol fieldSymbol,
        ImmutableArray<INamedTypeSymbol> allTypes,
        INamedTypeSymbol objectTypeSymbol,
        INamedTypeSymbol? serializedTypeNonGenericSymbol,
        INamedTypeSymbol serializedTypeGenericSymbol,
        INamedTypeSymbol serializedTypeOptionsAttributeSymbol,
        ImmutableArray<FieldConstraint>.Builder builder) {

        if (fieldSymbol.IsStatic)
            return;

        if (fieldSymbol.Type is not INamedTypeSymbol fieldTypeSymbol)
            return;

        var options = ParseOptions(fieldSymbol, serializedTypeOptionsAttributeSymbol);
        if (options.HasDynamicCustomFilter)
            return;

        if (SymbolEqualityComparer.Default.Equals(fieldTypeSymbol.OriginalDefinition, serializedTypeGenericSymbol)) {
            if (fieldTypeSymbol.TypeArguments.Length != 1)
                return;

            var baseConstraintType = fieldTypeSymbol.TypeArguments[0];
            foreach (var baseConstraint in ResolveBaseConstraintTypes(baseConstraintType, fieldSymbol.ContainingType, allTypes)) {
                builder.Add(new FieldConstraint(
                    baseConstraint,
                    options.AllowedTypeKinds,
                    options.AllowGenericTypeConstruction,
                    options.AllowOpenGenerics,
                    options.InheritsOrImplementsAll,
                    options.InheritsOrImplementsAny,
                    BuildSourceConstraintReason(baseConstraint, options, fieldSymbol)));
            }

            return;
        }

        if (serializedTypeNonGenericSymbol is null ||
            !SymbolEqualityComparer.Default.Equals(fieldTypeSymbol, serializedTypeNonGenericSymbol)) {
            return;
        }

        if (!HasMeaningfulNonGenericConstraints(options))
            return;

        builder.Add(new FieldConstraint(
            objectTypeSymbol,
            options.AllowedTypeKinds,
            options.AllowGenericTypeConstruction,
            options.AllowOpenGenerics,
            options.InheritsOrImplementsAll,
            options.InheritsOrImplementsAny,
            BuildSourceConstraintReason(objectTypeSymbol, options, fieldSymbol)));
    }

    static OptionsData ParseOptions(IFieldSymbol fieldSymbol, INamedTypeSymbol serializedTypeOptionsAttributeSymbol) {
        var allowedTypeKinds = TYPE_KIND_OBJECT;
        var allowGenericTypeConstruction = false;
        var allowOpenGenerics = false;
        var inheritsAllBuilder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var inheritsAnyBuilder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var hasDynamicCustomFilter = false;

        foreach (var attribute in fieldSymbol.GetAttributes()) {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, serializedTypeOptionsAttributeSymbol))
                continue;

            foreach (var namedArgument in attribute.NamedArguments) {
                switch (namedArgument.Key) {
                    case "AllowedTypeKinds":
                        if (namedArgument.Value.Value is int kindValue)
                            allowedTypeKinds = kindValue;
                        break;
                    case "AllowOpenGenerics":
                        if (namedArgument.Value.Value is bool allowOpen)
                            allowOpenGenerics = allowOpen;
                        break;
                    case "AllowGenericTypeConstruction":
                        if (namedArgument.Value.Value is bool allowConstruction)
                            allowGenericTypeConstruction = allowConstruction;
                        break;
                    case "InheritsOrImplementsAll":
                        AddConstraintTypes(namedArgument.Value, inheritsAllBuilder);
                        break;
                    case "InheritsOrImplementsAny":
                        AddConstraintTypes(namedArgument.Value, inheritsAnyBuilder);
                        break;
                    case "CustomTypeFilter":
                        if (namedArgument.Value.Value is string customFilter && !string.IsNullOrWhiteSpace(customFilter))
                            hasDynamicCustomFilter = true;
                        break;
                }
            }
        }

        return new OptionsData(
            allowedTypeKinds,
            allowGenericTypeConstruction,
            allowOpenGenerics,
            inheritsAllBuilder.ToImmutable(),
            inheritsAnyBuilder.ToImmutable(),
            hasDynamicCustomFilter);
    }

    static void AddConstraintTypes(TypedConstant typedConstant, ImmutableArray<INamedTypeSymbol>.Builder builder) {
        if (!typedConstant.IsNull && typedConstant.Kind == TypedConstantKind.Array) {
            foreach (var element in typedConstant.Values) {
                if (element.Value is INamedTypeSymbol namedTypeSymbol)
                    builder.Add(namedTypeSymbol);
            }
        }
    }

    static ImmutableArray<INamedTypeSymbol> ResolveBaseConstraintTypes(
        ITypeSymbol baseConstraintType,
        INamedTypeSymbol containingType,
        ImmutableArray<INamedTypeSymbol> allTypes) {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        if (baseConstraintType is INamedTypeSymbol namedConstraint) {
            builder.Add(namedConstraint);
            return builder.ToImmutable();
        }

        if (baseConstraintType is not ITypeParameterSymbol typeParameterSymbol) {
            return builder.ToImmutable();
        }

        foreach (var typeArgument in ResolveConcreteTypeArgumentsForFieldContainingType(containingType, typeParameterSymbol, allTypes)) {
            if (typeArgument is INamedTypeSymbol namedTypeArgument)
                builder.Add(namedTypeArgument);
        }

        foreach (var constraintType in typeParameterSymbol.ConstraintTypes) {
            if (constraintType is INamedTypeSymbol namedConstraintType)
                builder.Add(namedConstraintType);
        }

        return builder
            .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
            .ToImmutableArray();
    }

    static IEnumerable<ITypeSymbol> ResolveConcreteTypeArgumentsForFieldContainingType(
        INamedTypeSymbol declaringType,
        ITypeParameterSymbol typeParameterSymbol,
        ImmutableArray<INamedTypeSymbol> allTypes) {
        var declaringDefinition = declaringType.OriginalDefinition;
        var typeParameterIndex = typeParameterSymbol.Ordinal;

        foreach (var candidateType in allTypes) {
            if (candidateType.IsAbstract || candidateType.IsUnboundGenericType)
                continue;

            if (candidateType.TypeArguments.Any(a => a.TypeKind == TypeKind.TypeParameter))
                continue;

            for (var currentBase = candidateType; currentBase is not null; currentBase = currentBase.BaseType) {
                if (!SymbolEqualityComparer.Default.Equals(currentBase.OriginalDefinition, declaringDefinition))
                    continue;

                if (typeParameterIndex >= 0 && typeParameterIndex < currentBase.TypeArguments.Length) {
                    yield return currentBase.TypeArguments[typeParameterIndex];
                }

                break;
            }
        }
    }

    static bool MatchesConstraint(INamedTypeSymbol candidateType, FieldConstraint constraint, out string reason) {
        reason = string.Empty;

        if (!IsAssignableTo(candidateType, constraint.BaseConstraint))
            return false;

        if (!PassesAllowedKind(candidateType, constraint.AllowedTypeKinds))
            return false;

        if (ShouldRejectOpenGenericCandidate(candidateType, constraint))
            return false;

        if (constraint.InheritsOrImplementsAll.Any(required => !IsAssignableTo(candidateType, required)))
            return false;

        if (!constraint.InheritsOrImplementsAny.IsDefaultOrEmpty &&
            constraint.InheritsOrImplementsAny.All(required => !IsAssignableTo(candidateType, required)))
            return false;

        reason = constraint.Reason;
        return true;
    }

    static bool ShouldRejectOpenGenericCandidate(INamedTypeSymbol candidateType, FieldConstraint constraint) {
        if (constraint.AllowOpenGenerics || constraint.AllowGenericTypeConstruction)
            return false;

        if (candidateType.IsUnboundGenericType)
            return true;

        return candidateType.IsGenericType && candidateType.TypeArguments.Any(t => t.TypeKind == TypeKind.TypeParameter);
    }

    static bool IsConcretePropagatedConstraintType(INamedTypeSymbol typeSymbol) {
        if (typeSymbol.TypeKind == TypeKind.Interface)
            return false;

        var isStaticClass = typeSymbol.TypeKind == TypeKind.Class && typeSymbol.IsAbstract && typeSymbol.IsSealed;
        if (isStaticClass || (typeSymbol.TypeKind == TypeKind.Class && typeSymbol.IsAbstract))
            return false;

        if (typeSymbol.IsUnboundGenericType)
            return false;

        return !typeSymbol.IsGenericType || typeSymbol.TypeArguments.All(t => t.TypeKind != TypeKind.TypeParameter);
    }

    static bool HasMeaningfulNonGenericConstraints(OptionsData options) {
        return !options.InheritsOrImplementsAll.IsDefaultOrEmpty || !options.InheritsOrImplementsAny.IsDefaultOrEmpty;
    }

    static string BuildSourceConstraintReason(INamedTypeSymbol baseConstraint, OptionsData options, IFieldSymbol fieldSymbol) {
        return BuildConstraintReason(
            $"field '{fieldSymbol.ContainingType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)}.{fieldSymbol.Name}'",
            baseConstraint,
            options.AllowedTypeKinds,
            options.AllowGenericTypeConstruction,
            options.AllowOpenGenerics,
            options.InheritsOrImplementsAll,
            options.InheritsOrImplementsAny);
    }

    static string BuildManifestConstraintReason(
        INamedTypeSymbol baseConstraint,
        int allowedTypeKinds,
        bool allowGenericTypeConstruction,
        bool allowOpenGenerics,
        ImmutableArray<INamedTypeSymbol> inheritsOrImplementsAll,
        ImmutableArray<INamedTypeSymbol> inheritsOrImplementsAny,
        string declaringType,
        string fieldName) {

        var sourceDescription = !string.IsNullOrWhiteSpace(declaringType) && !string.IsNullOrWhiteSpace(fieldName)
            ? $"manifest field '{declaringType}.{fieldName}'"
            : "manifest entry";

        return BuildConstraintReason(
            sourceDescription,
            baseConstraint,
            allowedTypeKinds,
            allowGenericTypeConstruction,
            allowOpenGenerics,
            inheritsOrImplementsAll,
            inheritsOrImplementsAny);
    }

    static string BuildConstraintReason(
        string sourceDescription,
        INamedTypeSymbol baseConstraint,
        int allowedTypeKinds,
        bool allowGenericTypeConstruction,
        bool allowOpenGenerics,
        ImmutableArray<INamedTypeSymbol> inheritsOrImplementsAll,
        ImmutableArray<INamedTypeSymbol> inheritsOrImplementsAny) {

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(sourceDescription))
            parts.Add(sourceDescription);

        parts.Add($"base '{baseConstraint.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)}'");
        parts.Add($"AllowedTypeKinds={FormatAllowedTypeKinds(allowedTypeKinds)}");

        if (!inheritsOrImplementsAll.IsDefaultOrEmpty)
            parts.Add($"InheritsOrImplementsAll=[{FormatSymbolList(inheritsOrImplementsAll)}]");

        if (!inheritsOrImplementsAny.IsDefaultOrEmpty)
            parts.Add($"InheritsOrImplementsAny=[{FormatSymbolList(inheritsOrImplementsAny)}]");

        if (allowGenericTypeConstruction)
            parts.Add("AllowGenericTypeConstruction=true");

        if (allowOpenGenerics)
            parts.Add("AllowOpenGenerics=true");

        return string.Join(", ", parts);
    }

    static string BuildReasonText(ImmutableArray<string>.Builder reasons) {
        if (reasons.Count == 0)
            return "no reason";

        var uniqueReasons = reasons
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .ToArray();

        return string.Join("; ", uniqueReasons);
    }

    static string FormatSymbolList(ImmutableArray<INamedTypeSymbol> typeSymbols) {
        return string.Join(", ", typeSymbols.Select(typeSymbol =>
            typeSymbol.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
    }

    static string FormatAllowedTypeKinds(int allowedTypeKinds) {
        if (allowedTypeKinds == 0)
            return "None";

        if ((allowedTypeKinds & TYPE_KIND_ALL) == TYPE_KIND_ALL && (allowedTypeKinds & ~TYPE_KIND_ALL) == 0)
            return "All";

        if (allowedTypeKinds == TYPE_KIND_OBJECT)
            return "Object";

        var names = new List<string>();
        AddKindName(names, allowedTypeKinds, TYPE_KIND_CLASS, "Class");
        AddKindName(names, allowedTypeKinds, TYPE_KIND_STRUCT, "Struct");
        AddKindName(names, allowedTypeKinds, TYPE_KIND_ABSTRACT, "Abstract");
        AddKindName(names, allowedTypeKinds, TYPE_KIND_INTERFACE, "Interface");
        AddKindName(names, allowedTypeKinds, TYPE_KIND_STATIC, "Static");
        AddKindName(names, allowedTypeKinds, TYPE_KIND_ENUM, "Enum");
        AddKindName(names, allowedTypeKinds, TYPE_KIND_DELEGATE, "Delegate");
        AddKindName(names, allowedTypeKinds, TYPE_KIND_PRIMITIVE, "Primitive");

        var unknownFlags = allowedTypeKinds & ~TYPE_KIND_ALL;
        if (unknownFlags != 0)
            names.Add(unknownFlags.ToString());

        return names.Count == 0
            ? allowedTypeKinds.ToString()
            : string.Join("|", names);
    }

    static void AddKindName(List<string> names, int allowedTypeKinds, int flag, string name) {
        if ((allowedTypeKinds & flag) != 0)
            names.Add(name);
    }

    static ImmutableArray<INamedTypeSymbol> CollectPropagatedGenericParameterConstraints(
        ImmutableArray<INamedTypeSymbol> allTypes,
        ImmutableArray<FieldConstraint> constraints) {
        var propagatedBuilder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        foreach (var type in allTypes) {
            if (!type.IsGenericType)
                continue;

            if (!constraints.Any(c => MatchesConstraint(type, c, out _)))
                continue;

            foreach (var typeParameter in type.TypeParameters) {
                foreach (var constraintType in typeParameter.ConstraintTypes) {
                    if (constraintType is INamedTypeSymbol namedConstraintType)
                        propagatedBuilder.Add(namedConstraintType);
                }
            }
        }

        return propagatedBuilder
            .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default)
            .ToImmutableArray();
    }

    static bool IsAssignableTo(ITypeSymbol sourceType, ITypeSymbol targetType) {
        if (SymbolEqualityComparer.Default.Equals(sourceType, targetType))
            return true;

        if (sourceType is not INamedTypeSymbol sourceNamedType)
            return false;

        for (var current = sourceNamedType; current is not null; current = current.BaseType) {
            if (IsTypeMatch(current, targetType))
                return true;
        }

        foreach (var interfaceType in sourceNamedType.AllInterfaces) {
            if (IsTypeMatch(interfaceType, targetType))
                return true;
        }

        return false;
    }

    static bool IsTypeMatch(INamedTypeSymbol left, ITypeSymbol right) {
        if (SymbolEqualityComparer.Default.Equals(left, right))
            return true;

        if (right is not INamedTypeSymbol rightNamedType)
            return false;

        if (SymbolEqualityComparer.Default.Equals(left.OriginalDefinition, rightNamedType))
            return true;

        if (SymbolEqualityComparer.Default.Equals(left, rightNamedType.OriginalDefinition))
            return true;

        return SymbolEqualityComparer.Default.Equals(left.OriginalDefinition, rightNamedType.OriginalDefinition);
    }

    static bool PassesAllowedKind(INamedTypeSymbol typeSymbol, int allowedTypeKinds) {
        if ((allowedTypeKinds & TYPE_KIND_ALL) == TYPE_KIND_ALL)
            return true;

        var isInterface = typeSymbol.TypeKind == TypeKind.Interface;
        var isStaticClass = typeSymbol.TypeKind == TypeKind.Class && typeSymbol.IsAbstract && typeSymbol.IsSealed;
        var isAbstractClass = typeSymbol.TypeKind == TypeKind.Class && typeSymbol.IsAbstract && !typeSymbol.IsSealed;
        var isEnum = typeSymbol.TypeKind == TypeKind.Enum;
        var isDelegate = typeSymbol.TypeKind == TypeKind.Delegate;
        var isPrimitive = IsPrimitiveSpecialType(typeSymbol.SpecialType);
        var isStruct = typeSymbol.IsValueType && !isPrimitive && !isEnum;
        var isClass = typeSymbol.TypeKind == TypeKind.Class && !isStaticClass && !isAbstractClass;

        if (isClass && (allowedTypeKinds & TYPE_KIND_CLASS) != 0)
            return true;
        if (isStruct && (allowedTypeKinds & TYPE_KIND_STRUCT) != 0)
            return true;
        if (isAbstractClass && (allowedTypeKinds & TYPE_KIND_ABSTRACT) != 0)
            return true;
        if (isInterface && (allowedTypeKinds & TYPE_KIND_INTERFACE) != 0)
            return true;
        if (isStaticClass && (allowedTypeKinds & TYPE_KIND_STATIC) != 0)
            return true;
        if (isEnum && (allowedTypeKinds & TYPE_KIND_ENUM) != 0)
            return true;
        if (isDelegate && (allowedTypeKinds & TYPE_KIND_DELEGATE) != 0)
            return true;
        if (isPrimitive && (allowedTypeKinds & TYPE_KIND_PRIMITIVE) != 0)
            return true;

        return (isClass || isStruct) && (allowedTypeKinds & TYPE_KIND_OBJECT) == TYPE_KIND_OBJECT;
    }

    static bool IsPrimitiveSpecialType(SpecialType specialType) {
        switch (specialType) {
            case SpecialType.System_Boolean:
            case SpecialType.System_Byte:
            case SpecialType.System_SByte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_Decimal:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Char:
                return true;
            default:
                return false;
        }
    }

    static bool IsSystemObject(ITypeSymbol typeSymbol) {
        return typeSymbol.SpecialType == SpecialType.System_Object;
    }

    static bool HasSerializedTypeIdAttribute(INamedTypeSymbol typeSymbol, INamedTypeSymbol? serializedTypeIdAttributeSymbol) {
        if (serializedTypeIdAttributeSymbol is not null)
            return TryGetSerializedTypeIdAttributeData(typeSymbol, serializedTypeIdAttributeSymbol) is not null;

        foreach (var attribute in typeSymbol.GetAttributes()) {
            var attributeClass = attribute.AttributeClass;
            if (attributeClass is null)
                continue;

            if (IsSerializedTypeIdAttributeName(attributeClass))
                return true;
        }

        return false;
    }

    static bool IsSerializedTypeIdAttributeName(INamedTypeSymbol attributeClass) {
        var displayName = attributeClass.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        if (string.Equals(displayName, SERIALIZED_TYPE_ID_ATTRIBUTE_METADATA_NAME, StringComparison.Ordinal))
            return true;

        var containingNamespace = attributeClass.ContainingNamespace?.ToDisplayString();
        if (!string.Equals(containingNamespace, "Hissal.UnityTypeSerializer", StringComparison.Ordinal))
            return false;

        return string.Equals(attributeClass.Name, SERIALIZED_TYPE_ID_ATTRIBUTE_NAME, StringComparison.Ordinal) ||
               string.Equals(attributeClass.Name, SERIALIZED_TYPE_ID_SHORT_ATTRIBUTE_NAME, StringComparison.Ordinal) ||
               string.Equals(attributeClass.MetadataName, SERIALIZED_TYPE_ID_ATTRIBUTE_NAME, StringComparison.Ordinal) ||
               string.Equals(attributeClass.MetadataName, SERIALIZED_TYPE_ID_SHORT_ATTRIBUTE_NAME, StringComparison.Ordinal);
    }

    static AttributeData? TryGetSerializedTypeIdAttributeData(INamedTypeSymbol typeSymbol, INamedTypeSymbol serializedTypeIdAttributeSymbol) {
        foreach (var attribute in typeSymbol.GetAttributes()) {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, serializedTypeIdAttributeSymbol))
                return attribute;
        }

        return null;
    }

    static string? GetSerializedTypeIdValue(AttributeData attributeData) {
        if (attributeData.ConstructorArguments.Length > 0 &&
            attributeData.ConstructorArguments[0].Value is string ctorValue) {
            return ctorValue;
        }

        foreach (var namedArgument in attributeData.NamedArguments) {
            if (namedArgument.Key == "Id" && namedArgument.Value.Value is string idValue)
                return idValue;
        }

        return null;
    }

    static bool HasSourceLocation(INamedTypeSymbol typeSymbol) {
        return typeSymbol.Locations.Any(location => location.IsInSource);
    }


    readonly struct OptionsData {
        public OptionsData(
            int allowedTypeKinds,
            bool allowGenericTypeConstruction,
            bool allowOpenGenerics,
            ImmutableArray<INamedTypeSymbol> inheritsOrImplementsAll,
            ImmutableArray<INamedTypeSymbol> inheritsOrImplementsAny,
            bool hasDynamicCustomFilter) {

            AllowedTypeKinds = allowedTypeKinds;
            AllowGenericTypeConstruction = allowGenericTypeConstruction;
            AllowOpenGenerics = allowOpenGenerics;
            InheritsOrImplementsAll = inheritsOrImplementsAll;
            InheritsOrImplementsAny = inheritsOrImplementsAny;
            HasDynamicCustomFilter = hasDynamicCustomFilter;
        }

        public int AllowedTypeKinds { get; }
        public bool AllowGenericTypeConstruction { get; }
        public bool AllowOpenGenerics { get; }
        public ImmutableArray<INamedTypeSymbol> InheritsOrImplementsAll { get; }
        public ImmutableArray<INamedTypeSymbol> InheritsOrImplementsAny { get; }
        public bool HasDynamicCustomFilter { get; }
    }

    readonly struct ResolvedConstraintTypeList {
        public ResolvedConstraintTypeList(
            ImmutableArray<INamedTypeSymbol> resolvedTypes,
            bool hasRequestedTypes,
            bool hasUnresolvedTypes) {

            ResolvedTypes = resolvedTypes;
            HasRequestedTypes = hasRequestedTypes;
            HasUnresolvedTypes = hasUnresolvedTypes;
        }

        public ImmutableArray<INamedTypeSymbol> ResolvedTypes { get; }
        public bool HasRequestedTypes { get; }
        public bool HasUnresolvedTypes { get; }
    }

    readonly struct FieldConstraint {
        public FieldConstraint(
            INamedTypeSymbol baseConstraint,
            int allowedTypeKinds,
            bool allowGenericTypeConstruction,
            bool allowOpenGenerics,
            ImmutableArray<INamedTypeSymbol> inheritsOrImplementsAll,
            ImmutableArray<INamedTypeSymbol> inheritsOrImplementsAny,
            string reason) {

            BaseConstraint = baseConstraint;
            AllowedTypeKinds = allowedTypeKinds;
            AllowGenericTypeConstruction = allowGenericTypeConstruction;
            AllowOpenGenerics = allowOpenGenerics;
            InheritsOrImplementsAll = inheritsOrImplementsAll;
            InheritsOrImplementsAny = inheritsOrImplementsAny;
            Reason = reason;
        }

        public INamedTypeSymbol BaseConstraint { get; }
        public int AllowedTypeKinds { get; }
        public bool AllowGenericTypeConstruction { get; }
        public bool AllowOpenGenerics { get; }
        public ImmutableArray<INamedTypeSymbol> InheritsOrImplementsAll { get; }
        public ImmutableArray<INamedTypeSymbol> InheritsOrImplementsAny { get; }
        public string Reason { get; }
    }
}
