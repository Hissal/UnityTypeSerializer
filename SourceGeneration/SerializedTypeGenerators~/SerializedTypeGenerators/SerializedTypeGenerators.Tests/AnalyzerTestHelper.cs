using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace SerializedTypeGenerators.Tests;

internal static class AnalyzerTestHelper {
    internal const string RuntimeContractsSource = @"
using System;

namespace Hissal.UnityTypeSerializer {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Delegate)]
    public sealed class SerializedTypeIdAttribute : Attribute {
        public SerializedTypeIdAttribute(string id) {}
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class SerializedTypeOptionsAttribute : Attribute {
        public bool AllowGenericTypeConstruction { get; init; }
        public bool AllowOpenGenerics { get; init; }
        public SerializedTypeKind AllowedTypeKinds { get; init; } = SerializedTypeKind.Object;
        public Type[] ExplicitTypeList { get; init; }
        public Type[] ExcludedTypes { get; init; }
        public Type[] InheritsOrImplementsAll { get; init; }
        public Type[] InheritsOrImplementsAny { get; init; }
        public Type[] InheritsOrImplementsNone { get; init; }
        public string CustomTypeFilter { get; init; } = string.Empty;
    }

    [Flags]
    public enum SerializedTypeKind {
        None = 0,
        Class = 1 << 0,
        Struct = 1 << 1,
        Abstract = 1 << 2,
        Interface = 1 << 3,
        Static = 1 << 4,
        Enum = 1 << 5,
        Delegate = 1 << 6,
        Primitive = 1 << 7,
        Object = Class | Struct,
        All = Class | Struct | Abstract | Interface | Static | Enum | Delegate | Primitive,
    }

    public sealed class SerializedType<TBase> where TBase : class { }
    public sealed class SerializedType { }
}
";

    internal static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(
        string source,
        ImmutableArray<DiagnosticAnalyzer> analyzers,
        ImmutableArray<AdditionalText> additionalTexts = default,
        bool includeRuntimeContracts = true) {

        var syntaxTrees = includeRuntimeContracts
            ? new[] {
                CSharpSyntaxTree.ParseText(RuntimeContractsSource),
                CSharpSyntaxTree.ParseText(source)
            }
            : new[] {
                CSharpSyntaxTree.ParseText(source)
            };

        var compilation = CSharpCompilation.Create(
            assemblyName: "AnalyzerTests",
            syntaxTrees: syntaxTrees,
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var options = new AnalyzerOptions(additionalTexts.IsDefault ? ImmutableArray<AdditionalText>.Empty : additionalTexts);
        return await compilation.WithAnalyzers(analyzers, options).GetAnalyzerDiagnosticsAsync();
    }
}

internal sealed class InMemoryAdditionalText(string path, string content) : AdditionalText {
    readonly SourceText _text = SourceText.From(content);

    public override string Path { get; } = path;

    public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
}
