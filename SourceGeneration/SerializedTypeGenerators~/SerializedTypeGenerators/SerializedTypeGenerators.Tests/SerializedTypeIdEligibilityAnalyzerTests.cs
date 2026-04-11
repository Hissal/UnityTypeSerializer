using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace SerializedTypeGenerators.Tests;

public class SerializedTypeIdEligibilityAnalyzerTests {
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

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()));

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

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()));

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

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()));

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

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()));
        var stg100Diagnostics = diagnostics.Where(d => d.Id == "STG100").ToArray();

        Assert.Single(stg100Diagnostics);
        Assert.Contains("Demo.ServiceImpl", stg100Diagnostics[0].GetMessage(), System.StringComparison.Ordinal);
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

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()),
            ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText(
                "SerializedTypeUsageManifest.xml",
                manifestXml)));

        Assert.Contains(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Demo.ServiceImpl", System.StringComparison.Ordinal));
    }

}

