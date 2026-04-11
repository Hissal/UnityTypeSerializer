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
    const string SERIALIZED_TYPE_GENERIC_METADATA_NAME = "Hissal.UnityTypeSerializer.SerializedType`1";
    const string SERIALIZED_TYPE_ID_ATTRIBUTE_METADATA_NAME = "Hissal.UnityTypeSerializer.SerializedTypeIdAttribute";
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
        messageFormat: "Type '{0}' matches one or more SerializedType field constraints and should likely have [SerializedTypeId]",
        category: "SerializedTypeGenerators",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true
    );

    static readonly DiagnosticDescriptor s_nonGuidSerializedTypeIdDescriptor = new(
        id: "STG101",
        title: "SerializedTypeId should use GUID format",
        messageFormat: "SerializedTypeId value '{0}' is not a GUID. Consider using a generated GUID for long-term stability.",
        category: "SerializedTypeGenerators",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        s_missingSerializedTypeIdDescriptor,
        s_nonGuidSerializedTypeIdDescriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static startContext => {
            var serializedTypeGenericSymbol = startContext.Compilation.GetTypeByMetadataName(SERIALIZED_TYPE_GENERIC_METADATA_NAME);
            var serializedTypeIdAttributeSymbol = startContext.Compilation.GetTypeByMetadataName(SERIALIZED_TYPE_ID_ATTRIBUTE_METADATA_NAME);
            var serializedTypeOptionsAttributeSymbol = startContext.Compilation.GetTypeByMetadataName(SERIALIZED_TYPE_OPTIONS_ATTRIBUTE_METADATA_NAME);

            if (serializedTypeGenericSymbol is null || serializedTypeIdAttributeSymbol is null || serializedTypeOptionsAttributeSymbol is null)
                return;

            var constraints = CollectFieldConstraints(
                startContext.Compilation.Assembly.GlobalNamespace,
                serializedTypeGenericSymbol,
                serializedTypeOptionsAttributeSymbol);

            var externalConstraints = CollectExternalFieldConstraints(startContext.Compilation, startContext.Options);
            if (!externalConstraints.IsDefaultOrEmpty) {
                var mergedBuilder = ImmutableArray.CreateBuilder<FieldConstraint>();
                mergedBuilder.AddRange(constraints);
                mergedBuilder.AddRange(externalConstraints);
                constraints = mergedBuilder.ToImmutable();
            }

            startContext.RegisterSymbolAction(symbolContext => AnalyzeNamedType(
                (INamedTypeSymbol)symbolContext.Symbol,
                serializedTypeIdAttributeSymbol,
                constraints,
                symbolContext), SymbolKind.NamedType);
        });
    }

    static void AnalyzeNamedType(
        INamedTypeSymbol namedType,
        INamedTypeSymbol serializedTypeIdAttributeSymbol,
        ImmutableArray<FieldConstraint> constraints,
        SymbolAnalysisContext context) {

        ReportNonGuidSerializedTypeId(namedType, serializedTypeIdAttributeSymbol, context);

        if (!HasSourceLocation(namedType))
            return;

        if (HasSerializedTypeIdAttribute(namedType, serializedTypeIdAttributeSymbol))
            return;

        if (constraints.IsDefaultOrEmpty)
            return;

        if (!constraints.Any(constraint => MatchesConstraint(namedType, constraint)))
            return;

        var location = namedType.Locations.FirstOrDefault(l => l.IsInSource);
        if (location is null)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            s_missingSerializedTypeIdDescriptor,
            location,
            namedType.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat)));
    }

    static void ReportNonGuidSerializedTypeId(
        INamedTypeSymbol namedType,
        INamedTypeSymbol serializedTypeIdAttributeSymbol,
        SymbolAnalysisContext context) {

        if (TryGetSerializedTypeIdAttributeData(namedType, serializedTypeIdAttributeSymbol) is not AttributeData attributeData)
            return;

        var typeId = GetSerializedTypeIdValue(attributeData);
        if (string.IsNullOrWhiteSpace(typeId))
            return;

        var normalizedTypeId = typeId!.Trim();
        if (Guid.TryParse(normalizedTypeId, out _))
            return;

        var location = attributeData.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
            ?? namedType.Locations.FirstOrDefault(l => l.IsInSource);
        if (location is null)
            return;

        context.ReportDiagnostic(Diagnostic.Create(
            s_nonGuidSerializedTypeIdDescriptor,
            location,
            typeId));
    }

    static ImmutableArray<FieldConstraint> CollectFieldConstraints(
        INamespaceSymbol rootNamespace,
        INamedTypeSymbol serializedTypeGenericSymbol,
        INamedTypeSymbol serializedTypeOptionsAttributeSymbol) {

        var builder = ImmutableArray.CreateBuilder<FieldConstraint>();

        foreach (var fieldSymbol in EnumerateFieldSymbols(rootNamespace)) {
            if (TryCreateFieldConstraint(fieldSymbol, serializedTypeGenericSymbol, serializedTypeOptionsAttributeSymbol, out var constraint))
                builder.Add(constraint);
        }

        return builder.ToImmutable();
    }

    static ImmutableArray<FieldConstraint> CollectExternalFieldConstraints(Compilation compilation, AnalyzerOptions options) {
        var builder = ImmutableArray.CreateBuilder<FieldConstraint>();

        foreach (var additionalFile in options.AdditionalFiles) {
            var fileName = System.IO.Path.GetFileName(additionalFile.Path);
            if (!string.Equals(fileName, SERIALIZED_TYPE_USAGE_MANIFEST_FILE_NAME, StringComparison.OrdinalIgnoreCase))
                continue;

            var text = additionalFile.GetText();
            if (text is null)
                continue;

            AddExternalConstraintsFromXml(compilation, text.ToString(), builder);
        }

        if (builder.Count == 0) {
            var fallbackManifestXml = TryReadFallbackManifestXml();
            if (!string.IsNullOrWhiteSpace(fallbackManifestXml)) {
                AddExternalConstraintsFromXml(compilation, fallbackManifestXml, builder);
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

            var allowedTypeKinds = int.TryParse((string?)entryElement.Attribute("allowedTypeKinds"), out var parsedAllowedKinds)
                ? parsedAllowedKinds
                : TYPE_KIND_OBJECT;

            var inheritsAllSymbols = ResolveConstraintTypeList(compilation, (string?)entryElement.Attribute("inheritsAll"));
            var inheritsAnySymbols = ResolveConstraintTypeList(compilation, (string?)entryElement.Attribute("inheritsAny"));

            builder.Add(new FieldConstraint(
                baseConstraintSymbol,
                allowedTypeKinds,
                allowOpenGenerics,
                inheritsAllSymbols,
                inheritsAnySymbols));
        }
    }

    static ImmutableArray<INamedTypeSymbol> ResolveConstraintTypeList(Compilation compilation, string? rawValue) {
        if (string.IsNullOrWhiteSpace(rawValue))
            return ImmutableArray<INamedTypeSymbol>.Empty;

        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var entries = rawValue!.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var entry in entries) {
            var metadataName = entry.Trim();
            if (string.IsNullOrEmpty(metadataName))
                continue;

            var symbol = compilation.GetTypeByMetadataName(metadataName);
            if (symbol is not null)
                builder.Add(symbol);
        }

        return builder.ToImmutable();
    }

    static IEnumerable<IFieldSymbol> EnumerateFieldSymbols(INamespaceSymbol namespaceSymbol) {
        foreach (var member in namespaceSymbol.GetMembers()) {
            if (member is INamespaceSymbol childNamespaceSymbol) {
                foreach (var fieldSymbol in EnumerateFieldSymbols(childNamespaceSymbol))
                    yield return fieldSymbol;
                continue;
            }

            if (member is INamedTypeSymbol namedTypeSymbol) {
                foreach (var fieldSymbol in EnumerateFieldSymbols(namedTypeSymbol))
                    yield return fieldSymbol;
            }
        }
    }

    static IEnumerable<IFieldSymbol> EnumerateFieldSymbols(INamedTypeSymbol namedTypeSymbol) {
        foreach (var member in namedTypeSymbol.GetMembers()) {
            if (member is IFieldSymbol fieldSymbol)
                yield return fieldSymbol;
        }

        foreach (var nestedType in namedTypeSymbol.GetTypeMembers()) {
            foreach (var fieldSymbol in EnumerateFieldSymbols(nestedType))
                yield return fieldSymbol;
        }
    }

    static bool TryCreateFieldConstraint(
        IFieldSymbol fieldSymbol,
        INamedTypeSymbol serializedTypeGenericSymbol,
        INamedTypeSymbol serializedTypeOptionsAttributeSymbol,
        out FieldConstraint constraint) {

        constraint = default;

        if (fieldSymbol.IsStatic)
            return false;

        if (fieldSymbol.Type is not INamedTypeSymbol fieldTypeSymbol)
            return false;

        if (!SymbolEqualityComparer.Default.Equals(fieldTypeSymbol.OriginalDefinition, serializedTypeGenericSymbol))
            return false;

        if (fieldTypeSymbol.TypeArguments.Length != 1 || fieldTypeSymbol.TypeArguments[0] is not INamedTypeSymbol baseConstraintSymbol)
            return false;

        var options = ParseOptions(fieldSymbol, serializedTypeOptionsAttributeSymbol);
        if (options.HasDynamicCustomFilter)
            return false;

        constraint = new FieldConstraint(
            baseConstraintSymbol,
            options.AllowedTypeKinds,
            options.AllowOpenGenerics,
            options.InheritsOrImplementsAll,
            options.InheritsOrImplementsAny);
        return true;
    }

    static OptionsData ParseOptions(IFieldSymbol fieldSymbol, INamedTypeSymbol serializedTypeOptionsAttributeSymbol) {
        var allowedTypeKinds = TYPE_KIND_OBJECT;
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

    static bool MatchesConstraint(INamedTypeSymbol candidateType, FieldConstraint constraint) {
        if (!IsAssignableTo(candidateType, constraint.BaseConstraint))
            return false;

        if (!PassesAllowedKind(candidateType, constraint.AllowedTypeKinds))
            return false;

        if (!constraint.AllowOpenGenerics && candidateType.IsUnboundGenericType)
            return false;

        if (candidateType.IsGenericType && !constraint.AllowOpenGenerics && candidateType.TypeArguments.Any(t => t.TypeKind == TypeKind.TypeParameter))
            return false;

        if (constraint.InheritsOrImplementsAll.Any(required => !IsAssignableTo(candidateType, required)))
            return false;

        if (!constraint.InheritsOrImplementsAny.IsDefaultOrEmpty &&
            constraint.InheritsOrImplementsAny.All(required => !IsAssignableTo(candidateType, required)))
            return false;

        return true;
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

        return (isClass || isStruct) && (allowedTypeKinds & TYPE_KIND_OBJECT) != 0;
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

    static bool HasSerializedTypeIdAttribute(INamedTypeSymbol typeSymbol, INamedTypeSymbol serializedTypeIdAttributeSymbol) {
        return TryGetSerializedTypeIdAttributeData(typeSymbol, serializedTypeIdAttributeSymbol) is not null;
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
            bool allowOpenGenerics,
            ImmutableArray<INamedTypeSymbol> inheritsOrImplementsAll,
            ImmutableArray<INamedTypeSymbol> inheritsOrImplementsAny,
            bool hasDynamicCustomFilter) {

            AllowedTypeKinds = allowedTypeKinds;
            AllowOpenGenerics = allowOpenGenerics;
            InheritsOrImplementsAll = inheritsOrImplementsAll;
            InheritsOrImplementsAny = inheritsOrImplementsAny;
            HasDynamicCustomFilter = hasDynamicCustomFilter;
        }

        public int AllowedTypeKinds { get; }
        public bool AllowOpenGenerics { get; }
        public ImmutableArray<INamedTypeSymbol> InheritsOrImplementsAll { get; }
        public ImmutableArray<INamedTypeSymbol> InheritsOrImplementsAny { get; }
        public bool HasDynamicCustomFilter { get; }
    }

    readonly struct FieldConstraint {
        public FieldConstraint(
            INamedTypeSymbol baseConstraint,
            int allowedTypeKinds,
            bool allowOpenGenerics,
            ImmutableArray<INamedTypeSymbol> inheritsOrImplementsAll,
            ImmutableArray<INamedTypeSymbol> inheritsOrImplementsAny) {

            BaseConstraint = baseConstraint;
            AllowedTypeKinds = allowedTypeKinds;
            AllowOpenGenerics = allowOpenGenerics;
            InheritsOrImplementsAll = inheritsOrImplementsAll;
            InheritsOrImplementsAny = inheritsOrImplementsAny;
        }

        public INamedTypeSymbol BaseConstraint { get; }
        public int AllowedTypeKinds { get; }
        public bool AllowOpenGenerics { get; }
        public ImmutableArray<INamedTypeSymbol> InheritsOrImplementsAll { get; }
        public ImmutableArray<INamedTypeSymbol> InheritsOrImplementsAny { get; }
    }
}





