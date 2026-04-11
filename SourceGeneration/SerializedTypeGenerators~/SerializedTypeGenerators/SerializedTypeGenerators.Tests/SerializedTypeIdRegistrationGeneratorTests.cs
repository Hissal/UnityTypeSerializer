using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace SerializedTypeGenerators.Tests;

public class SerializedTypeIdRegistrationGeneratorTests {
    private const string SourceWithTwoIds = @"
namespace Demo {

[Hissal.UnityTypeSerializer.SerializedTypeId(""beta"")]
public sealed class SecondType {}

[Hissal.UnityTypeSerializer.SerializedTypeId(""alpha"")]
public sealed class FirstType {}

}
";

    private const string SourceWithDuplicateId = @"
namespace Demo {

[Hissal.UnityTypeSerializer.SerializedTypeId(""shared"")]
public sealed class One {}

[Hissal.UnityTypeSerializer.SerializedTypeId(""shared"")]
public sealed class Two {}

}
";

    private const string RuntimeContractsSource = @"
using System;
using System.Collections.Generic;

namespace Hissal.UnityTypeSerializer {

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class SerializedTypeIdAttribute : Attribute {
    public SerializedTypeIdAttribute(string id) {
        Id = id;
    }

    public string Id { get; }
}

public interface ISerializedTypeIdRegistrationProvider {
    void Register(IDictionary<string, string> map);
}

}
";

    private const string ExternalSameNameAttributeSource = @"
using System;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class SerializedTypeIdAttribute : Attribute {
    public SerializedTypeIdAttribute(string id) {
        Id = id;
    }

    public string Id { get; }
}

namespace Demo {

[SerializedTypeId(""external-alpha"")]
public sealed class ExternalType {}

}
";

    [Fact]
    public void GeneratesRegistryEntriesWithDeterministicOrdering() {
        var generator = new SerializedTypeIdRegistrationGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(
            assemblyName: "RegistryGeneratorTests",
            syntaxTrees: new[] {
                CSharpSyntaxTree.ParseText(SourceWithTwoIds),
                CSharpSyntaxTree.ParseText(RuntimeContractsSource)
            },
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var runResult = driver.RunGenerators(compilation).GetRunResult();
        var generatedSource = runResult.GeneratedTrees
            .Single(tree => tree.FilePath.EndsWith("SerializedTypeIdRegistrationProvider.g.cs"))
            .GetText()
            .ToString();

        Assert.Contains("namespace Hissal.UnityTypeSerializer.Generated", generatedSource);
        Assert.Contains("ISerializedTypeIdRegistrationProvider", generatedSource);
        Assert.Contains("public void Register(IDictionary<string, string> map)", generatedSource);

        var alphaIndex = generatedSource.IndexOf("map[\"alpha\"]", System.StringComparison.Ordinal);
        var betaIndex = generatedSource.IndexOf("map[\"beta\"]", System.StringComparison.Ordinal);
        Assert.True(alphaIndex >= 0, "alpha entry is missing.");
        Assert.True(betaIndex >= 0, "beta entry is missing.");
        Assert.True(alphaIndex < betaIndex, "Entries are not sorted by type id.");

        Assert.Contains("Demo.FirstType, RegistryGeneratorTests", generatedSource);
        Assert.Contains("Demo.SecondType, RegistryGeneratorTests", generatedSource);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var updatedCompilation, out _);
        var compilationErrors = updatedCompilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.Empty(compilationErrors);
    }

    [Fact]
    public void GeneratesDeterministicProviderTypeNamePerAssembly() {
        var generator = new SerializedTypeIdRegistrationGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(
            assemblyName: "RegistryGeneratorTests",
            syntaxTrees: new[] {
                CSharpSyntaxTree.ParseText(SourceWithTwoIds),
                CSharpSyntaxTree.ParseText(RuntimeContractsSource)
            },
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var runResult = driver.RunGenerators(compilation).GetRunResult();
        var generatedSource = runResult.GeneratedTrees
            .Single(tree => tree.FilePath.EndsWith("SerializedTypeIdRegistrationProvider.g.cs"))
            .GetText()
            .ToString();

        Assert.Contains("SerializedTypeIdRegistrationProvider_RegistryGeneratorTests_", generatedSource);
    }

    [Fact]
    public void ReportsErrorWhenIdsAreDuplicated() {
        var generator = new SerializedTypeIdRegistrationGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(
            assemblyName: "RegistryGeneratorTests",
            syntaxTrees: new[] {
                CSharpSyntaxTree.ParseText(SourceWithDuplicateId),
                CSharpSyntaxTree.ParseText(RuntimeContractsSource)
            },
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var runResult = driver.RunGenerators(compilation).GetRunResult();
        var duplicateDiagnostics = runResult.Diagnostics.Where(d => d.Id == "STG001").ToArray();

        Assert.NotEmpty(duplicateDiagnostics);
        Assert.All(duplicateDiagnostics, diagnostic => Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity));
        Assert.Contains(duplicateDiagnostics, d => d.GetMessage().Contains("Demo.One", System.StringComparison.Ordinal));
        Assert.Contains(duplicateDiagnostics, d => d.GetMessage().Contains("Demo.Two", System.StringComparison.Ordinal));
    }

    [Fact]
    public void SkipsGenerationWhenRuntimeContractsAreMissing() {
        var generator = new SerializedTypeIdRegistrationGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(
            assemblyName: "RegistryGeneratorTests",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(SourceWithTwoIds) },
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var runResult = driver.RunGenerators(compilation).GetRunResult();
        Assert.DoesNotContain(runResult.GeneratedTrees, tree => tree.FilePath.EndsWith("SerializedTypeIdRegistrationProvider.g.cs"));
    }

    [Fact]
    public void IgnoresExternalSameNameAttributes() {
        var generator = new SerializedTypeIdRegistrationGenerator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        var compilation = CSharpCompilation.Create(
            assemblyName: "RegistryGeneratorTests",
            syntaxTrees: new[] {
                CSharpSyntaxTree.ParseText(RuntimeContractsSource),
                CSharpSyntaxTree.ParseText(ExternalSameNameAttributeSource)
            },
            references: new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var runResult = driver.RunGenerators(compilation).GetRunResult();
        Assert.DoesNotContain(runResult.GeneratedTrees, tree => tree.FilePath.EndsWith("SerializedTypeIdRegistrationProvider.g.cs"));
    }
}

