using Xunit;

namespace SerializedTypeGenerators.Tests;

public class SerializedTypeIdEligibilityCodeFixProviderTests {
    [Fact]
    public void SupportsStg100() {
        var provider = new SerializedTypeIdEligibilityCodeFixProvider();
        Assert.Contains("STG100", provider.FixableDiagnosticIds);
    }

    [Fact]
    public void DoesNotSupportStg101() {
        var provider = new SerializedTypeIdEligibilityCodeFixProvider();
        Assert.DoesNotContain("STG101", provider.FixableDiagnosticIds);
    }
}


