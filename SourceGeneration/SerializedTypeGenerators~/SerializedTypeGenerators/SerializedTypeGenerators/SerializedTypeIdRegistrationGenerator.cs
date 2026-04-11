using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace SerializedTypeGenerators;

[Generator]
public sealed class SerializedTypeIdRegistrationGenerator : IIncrementalGenerator {
    const string ATTRIBUTE_NAME = "SerializedTypeIdAttribute";
    const string SERIALIZED_TYPE_ID_ATTRIBUTE_METADATA_NAME = "Hissal.UnityTypeSerializer.SerializedTypeIdAttribute";
    const string REGISTRATION_PROVIDER_INTERFACE_METADATA_NAME = "Hissal.UnityTypeSerializer.ISerializedTypeIdRegistrationProvider";

    static readonly DiagnosticDescriptor s_duplicateIdDescriptor = new(
        id: "STG001",
        title: "Duplicate serialized type id",
        messageFormat: "SerializedTypeId '{0}' is assigned to multiple types: {1}",
        category: "SerializedTypeGenerators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    public void Initialize(IncrementalGeneratorInitializationContext context) {
        var candidates = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax typeDecl && typeDecl.AttributeLists.Count > 0,
                static (ctx, _) => TryCreateEntry(ctx))
            .Where(static entry => entry.HasValue)
            .Select(static (entry, _) => entry.GetValueOrDefault());

        var sourceInput = context.CompilationProvider.Combine(candidates.Collect());
        context.RegisterSourceOutput(sourceInput, static (spc, input) => Generate(spc, input.Left, input.Right));
    }

    static RegistryEntry? TryCreateEntry(GeneratorSyntaxContext context) {
        var declaration = (TypeDeclarationSyntax)context.Node;
        if (context.SemanticModel.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol)
            return null;

        foreach (var attribute in symbol.GetAttributes()) {
            if (attribute.AttributeClass?.Name != ATTRIBUTE_NAME)
                continue;

            var typeId = GetTypeId(attribute);
            if (string.IsNullOrWhiteSpace(typeId))
                return null;

            return new RegistryEntry(typeId!.Trim(), symbol);
        }

        return null;
    }

    static string? GetTypeId(AttributeData attribute) {
        if (attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is string ctorValue)
            return ctorValue;

        foreach (var namedArgument in attribute.NamedArguments) {
            if (namedArgument is { Key: "Id", Value.Value: string id })
                return id;
        }

        return null;
    }

    static void Generate(SourceProductionContext context, Compilation compilation, ImmutableArray<RegistryEntry> rawEntries) {
        var serializedTypeIdAttributeSymbol = compilation.GetTypeByMetadataName(SERIALIZED_TYPE_ID_ATTRIBUTE_METADATA_NAME);
        var registrationProviderInterfaceSymbol = compilation.GetTypeByMetadataName(REGISTRATION_PROVIDER_INTERFACE_METADATA_NAME);
        if (serializedTypeIdAttributeSymbol is null || registrationProviderInterfaceSymbol is null)
            return;

        var providerTypeName = BuildProviderTypeName(compilation.AssemblyName ?? "UnknownAssembly");

        if (rawEntries.IsDefaultOrEmpty)
            return;

        var entries = rawEntries
            .Select(entry => TryCreateRuntimeEntry(entry.Symbol, serializedTypeIdAttributeSymbol))
            .Where(static entry => entry.HasValue)
            .Select(static entry => entry.GetValueOrDefault())
            .GroupBy(entry => entry.Symbol, SymbolEqualityComparer.Default)
            .Select(group => group.First())
            .OrderBy(e => e.TypeId, StringComparer.Ordinal)
            .ThenBy(e => GetAssemblyQualifiedName(e.Symbol), StringComparer.Ordinal)
            .ToImmutableArray();

        if (entries.IsDefaultOrEmpty)
            return;

        foreach (var duplicate in entries.GroupBy(e => e.TypeId, StringComparer.Ordinal).Where(g => g.Count() > 1)) {
            var typeList = string.Join(", ", duplicate.Select(d => d.Symbol.ToDisplayString()).OrderBy(v => v, StringComparer.Ordinal));
            foreach (var entry in duplicate) {
                var location = entry.Symbol.Locations.FirstOrDefault();
                context.ReportDiagnostic(Diagnostic.Create(s_duplicateIdDescriptor, location, duplicate.Key, typeList));
            }
        }

        context.AddSource("SerializedTypeIdRegistrationProvider.g.cs", BuildSource(entries, providerTypeName));
    }

    static RegistryEntry? TryCreateRuntimeEntry(INamedTypeSymbol symbol, INamedTypeSymbol serializedTypeIdAttributeSymbol) {
        foreach (var attribute in symbol.GetAttributes()) {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, serializedTypeIdAttributeSymbol))
                continue;

            var typeId = GetTypeId(attribute);
            if (string.IsNullOrWhiteSpace(typeId))
                return null;

            var normalizedTypeId = typeId!.Trim();
            return new RegistryEntry(normalizedTypeId, symbol);
        }

        return null;
    }

    static SourceText BuildSource(ImmutableArray<RegistryEntry> entries, string providerTypeName) {
        using var sourceStream = new StringWriter();
        using var codeWriter = new IndentedTextWriter(sourceStream);
        
        codeWriter.WriteLine("// <auto-generated />");
        codeWriter.WriteLine("using System.Collections.Generic;");
        codeWriter.WriteLine();
        
        codeWriter.WriteLine("namespace Hissal.UnityTypeSerializer.Generated {");
        codeWriter.Indent++;
        
        codeWriter.WriteLine($"internal sealed class {providerTypeName} : global::Hissal.UnityTypeSerializer.ISerializedTypeIdRegistrationProvider {{");
        codeWriter.Indent++;
        codeWriter.WriteLine("public void Register(IDictionary<string, string> map) {");
        codeWriter.Indent++;
        foreach (var entry in entries) {
            codeWriter.WriteLine($"map[{ToLiteral(entry.TypeId)}] = {ToLiteral(GetAssemblyQualifiedName(entry.Symbol))};");
        }
        
        codeWriter.Indent--;
        codeWriter.WriteLine("}");
        
        codeWriter.Indent--;
        codeWriter.WriteLine("}");

        codeWriter.Indent--;
        codeWriter.WriteLine("}");

        return SourceText.From(sourceStream.ToString(), Encoding.UTF8);
    }

    static string BuildProviderTypeName(string assemblyName) {
        var sanitizedAssemblyName = SanitizeIdentifier(assemblyName);
        if (string.IsNullOrEmpty(sanitizedAssemblyName))
            sanitizedAssemblyName = "Assembly";

        var hash = ComputeFnv1aHashHex(assemblyName);
        return $"SerializedTypeIdRegistrationProvider_{sanitizedAssemblyName}_{hash}";
    }

    static string SanitizeIdentifier(string text) {
        var builder = new StringBuilder(text.Length);

        for (var index = 0; index < text.Length; index++) {
            var character = text[index];
            var isAllowed = character == '_' || (character >= 'A' && character <= 'Z') || (character >= 'a' && character <= 'z') || (character >= '0' && character <= '9');
            if (isAllowed) {
                builder.Append(character);
            }
            else {
                builder.Append('_');
            }
        }

        if (builder.Length == 0)
            return string.Empty;

        var firstCharacter = builder[0];
        if (firstCharacter >= '0' && firstCharacter <= '9')
            builder.Insert(0, '_');

        return builder.ToString();
    }

    static string ComputeFnv1aHashHex(string text) {
        unchecked {
            uint hash = 2166136261;
            foreach (var character in text) {
                hash ^= character;
                hash *= 16777619;
            }

            return hash.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    static string GetAssemblyQualifiedName(INamedTypeSymbol symbol) {
        var assemblyName = symbol.ContainingAssembly.Identity.ToString();
        var metadataName = GetMetadataTypeName(symbol);
        return $"{metadataName}, {assemblyName}";
    }

     static string GetMetadataTypeName(INamedTypeSymbol symbol) {
        var typeParts = new Stack<string>();
        var current = symbol;
        while (current is not null) {
            typeParts.Push(current.MetadataName);
            current = current.ContainingType;
        }

        var typeName = string.Join("+", typeParts);
        if (symbol.ContainingNamespace is null || symbol.ContainingNamespace.IsGlobalNamespace)
            return typeName;

        return $"{symbol.ContainingNamespace.ToDisplayString()}.{typeName}";
    }

    static string ToLiteral(string value) {
        return "\"" + value.Replace("\\", @"\\").Replace("\"", "\\\"") + "\"";
    }

    readonly struct RegistryEntry(string typeId, INamedTypeSymbol symbol) {
        public string TypeId { get; } = typeId;
        public INamedTypeSymbol Symbol { get; } = symbol;
    }
}