using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace SerializedTypeGenerators.Tests;

public class SerializedTypeIdGuidFormatAnalyzerTests {
    [Fact]
    public async Task ReportsInfoWhenSerializedTypeIdIsNotGuid() {
        var source = @"
namespace Demo {
    [Hissal.UnityTypeSerializer.SerializedTypeId(""not-a-guid"")]
    public sealed class ServiceImpl { }
}
";

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdGuidFormatAnalyzer()));
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

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdGuidFormatAnalyzer()));

        Assert.DoesNotContain(diagnostics, d => d.Id == "STG101");
    }

}

