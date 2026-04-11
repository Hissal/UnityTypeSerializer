using Xunit;

namespace SerializedTypeGenerators.Tests;

public class SerializedTypeIdGuidFormatCodeFixProviderTests {
    [Fact]
    public void SupportsStg101() {
        var provider = new SerializedTypeIdGuidFormatCodeFixProvider();
        Assert.Contains("STG101", provider.FixableDiagnosticIds);
    }

    [Fact]
    public void DoesNotSupportStg100() {
        var provider = new SerializedTypeIdGuidFormatCodeFixProvider();
        Assert.DoesNotContain("STG100", provider.FixableDiagnosticIds);
    }
}

