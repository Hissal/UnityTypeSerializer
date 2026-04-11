using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace SerializedTypeGenerators.Tests;

public class SerializedTypeIdEligibilityAnalyzerTests {
    private const string RuntimeContractsSource = @"
using System;

namespace Hissal.UnityTypeSerializer {
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface | AttributeTargets.Enum | AttributeTargets.Delegate)]
    public sealed class SerializedTypeIdAttribute : Attribute {
        public SerializedTypeIdAttribute(string id) {}
    }

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class SerializedTypeOptionsAttribute : Attribute {
        public bool AllowOpenGenerics { get; init; }
        public SerializedTypeKind AllowedTypeKinds { get; init; } = SerializedTypeKind.Object;
        public Type[] InheritsOrImplementsAll { get; init; }
        public Type[] InheritsOrImplementsAny { get; init; }
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
}
";

    [Fact]
    public async Task ReportsWarningForLikelySerializedTypeMissingId() {
        var source = @"
namespace Demo {
    public interface IService { }

    public sealed class ServiceImpl : IService { }

    public sealed class Holder {
        private Hissal.UnityTypeSerializer.SerializedType<IService> typeRef = new();
    }
}
";

        var diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Assert.Contains(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Demo.ServiceImpl", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task DoesNotReportWhenTypeHasSerializedTypeId() {
        var source = @"
namespace Demo {
    public interface IService { }

    [Hissal.UnityTypeSerializer.SerializedTypeId(""service"")]
    public sealed class ServiceImpl : IService { }

    public sealed class Holder {
        private Hissal.UnityTypeSerializer.SerializedType<IService> typeRef = new();
    }
}
";

        var diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "STG100");
    }

    [Fact]
    public async Task SkipsAnalysisWhenCustomFilterIsConfigured() {
        var source = @"
namespace Demo {
    public interface IService { }

    public sealed class ServiceImpl : IService { }

    public sealed class Holder {
        [Hissal.UnityTypeSerializer.SerializedTypeOptions(CustomTypeFilter = ""TypeProvider.GetTypes"")]
        private Hissal.UnityTypeSerializer.SerializedType<IService> typeRef = new();
    }
}
";

        var diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "STG100");
    }

    [Fact]
    public async Task ReportsSingleDiagnosticWhenMultipleFieldsMatchSameType() {
        var source = @"
namespace Demo {
    public interface IService { }

    public sealed class ServiceImpl : IService { }

    public sealed class HolderA {
        private Hissal.UnityTypeSerializer.SerializedType<IService> typeRef = new();
    }

    public sealed class HolderB {
        private Hissal.UnityTypeSerializer.SerializedType<IService> otherTypeRef = new();
    }
}
";

        var diagnostics = await GetAnalyzerDiagnosticsAsync(source);
        var stg100Diagnostics = diagnostics.Where(d => d.Id == "STG100").ToArray();

        Assert.Single(stg100Diagnostics);
        Assert.Contains("Demo.ServiceImpl", stg100Diagnostics[0].GetMessage(), System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportsInfoWhenSerializedTypeIdIsNotGuid() {
        var source = @"
namespace Demo {
    [Hissal.UnityTypeSerializer.SerializedTypeId(""not-a-guid"")]
    public sealed class ServiceImpl { }
}
";

        var diagnostics = await GetAnalyzerDiagnosticsAsync(source);
        var stg101Diagnostics = diagnostics.Where(d => d.Id == "STG101").ToArray();

        Assert.Single(stg101Diagnostics);
        Assert.Equal(DiagnosticSeverity.Info, stg101Diagnostics[0].Severity);
    }

    [Fact]
    public async Task DoesNotReportWhenSerializedTypeIdIsGuid() {
        var source = @"
namespace Demo {
    [Hissal.UnityTypeSerializer.SerializedTypeId(""8f8d68a2-27e9-42e9-97e9-f21b3182f1a4"")]
    public sealed class ServiceImpl { }
}
";

        var diagnostics = await GetAnalyzerDiagnosticsAsync(source);

        Assert.DoesNotContain(diagnostics, d => d.Id == "STG101");
    }

    [Fact]
    public void CodeFixProviderSupportsStg101() {
        var provider = new SerializedTypeIdEligibilityCodeFixProvider();
        Assert.Contains("STG101", provider.FixableDiagnosticIds);
    }

    [Fact]
    public async Task ReportsWarningFromExternalManifestConstraints() {
        var source = @"
namespace Demo {
    public interface IService { }
    public sealed class ServiceImpl : IService { }
}
";

        var manifestXml = @"
<SerializedTypeUsageManifest generatedAtUtc=""2026-04-11T00:00:00.0000000Z"">
  <Entry baseConstraint=""Demo.IService"" allowOpenGenerics=""false"" allowedTypeKinds=""3"" inheritsAll="""" inheritsAny="""" customTypeFilter="""" />
</SerializedTypeUsageManifest>
";

        var diagnostics = await GetAnalyzerDiagnosticsAsync(source, ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText(
            "SerializedTypeUsageManifest.xml",
            manifestXml)));

        Assert.Contains(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Demo.ServiceImpl", System.StringComparison.Ordinal));
    }

    static async Task<ImmutableArray<Diagnostic>> GetAnalyzerDiagnosticsAsync(string source, ImmutableArray<AdditionalText> additionalTexts = default) {
        var compilation = CSharpCompilation.Create(
            assemblyName: "AnalyzerTests",
            syntaxTrees: new[] {
                CSharpSyntaxTree.ParseText(RuntimeContractsSource),
                CSharpSyntaxTree.ParseText(source)
            },
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var analyzer = new SerializedTypeIdEligibilityAnalyzer();
        var analyzers = ImmutableArray.Create<DiagnosticAnalyzer>(analyzer);
        var options = new AnalyzerOptions(additionalTexts.IsDefault ? ImmutableArray<AdditionalText>.Empty : additionalTexts);

        return await compilation.WithAnalyzers(analyzers, options).GetAnalyzerDiagnosticsAsync();
    }

    sealed class InMemoryAdditionalText(string path, string content) : AdditionalText {
        readonly SourceText _text = SourceText.From(content);

        public override string Path { get; } = path;

        public override SourceText GetText(System.Threading.CancellationToken cancellationToken = default) => _text;
    }
}

