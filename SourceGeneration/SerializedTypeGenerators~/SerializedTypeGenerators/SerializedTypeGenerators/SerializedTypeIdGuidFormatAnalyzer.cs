using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SerializedTypeGenerators;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SerializedTypeIdGuidFormatAnalyzer : DiagnosticAnalyzer {
    const string SERIALIZED_TYPE_ID_ATTRIBUTE_METADATA_NAME = "Hissal.UnityTypeSerializer.SerializedTypeIdAttribute";

    static readonly DiagnosticDescriptor s_nonGuidSerializedTypeIdDescriptor = new(
        id: "STG101",
        title: "SerializedTypeId should use GUID format",
        messageFormat: "SerializedTypeId value '{0}' is not a GUID. Consider using a generated GUID for long-term stability.",
        category: "SerializedTypeGenerators",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
        s_nonGuidSerializedTypeIdDescriptor);

    public override void Initialize(AnalysisContext context) {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static startContext => {
            var serializedTypeIdAttributeSymbol = startContext.Compilation.GetTypeByMetadataName(SERIALIZED_TYPE_ID_ATTRIBUTE_METADATA_NAME);
            if (serializedTypeIdAttributeSymbol is null)
                return;

            startContext.RegisterSymbolAction(symbolContext => AnalyzeNamedType(
                (INamedTypeSymbol)symbolContext.Symbol,
                serializedTypeIdAttributeSymbol,
                symbolContext), SymbolKind.NamedType);
        });
    }

    static void AnalyzeNamedType(
        INamedTypeSymbol namedType,
        INamedTypeSymbol serializedTypeIdAttributeSymbol,
        SymbolAnalysisContext context) {

        if (!HasSourceLocation(namedType))
            return;

        if (TryGetSerializedTypeIdAttributeData(namedType, serializedTypeIdAttributeSymbol) is not AttributeData attributeData)
            return;

        var typeId = GetSerializedTypeIdValue(attributeData);
        if (string.IsNullOrWhiteSpace(typeId))
            return;

        var normalizedTypeId = typeId.Trim();
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
}

