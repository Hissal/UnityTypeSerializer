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
        [Hissal.UnityTypeSerializer.SerializedTypeOptions(
            CustomTypeFilter = ""TypeProvider.GetTypes"",
            ExplicitTypeList = new[] { typeof(ServiceImpl) },
            ExcludedTypes = new[] { typeof(ServiceImpl) },
            InheritsOrImplementsNone = new[] { typeof(IService) })]
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
    public async Task ExplicitTypeListIsTheOnlyCandidateSource() {
        var source = @"
namespace Demo {
    public interface IService { }
    public sealed class IncludedService : IService { }
    public sealed class OtherService : IService { }

    public sealed class Holder {
        [Hissal.UnityTypeSerializer.SerializedTypeOptions(
            ExplicitTypeList = new[] { typeof(IncludedService) })]
        private Hissal.UnityTypeSerializer.SerializedType<IService> typeRef = new();
    }
}
";

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()));

        Assert.Contains(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Type 'Demo.IncludedService'", System.StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Type 'Demo.OtherService'", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExcludedTypesRemovesOnlyTheConfiguredDirectType() {
        var source = @"
namespace Demo {
    public interface IService { }
    public sealed class ExcludedService : IService { }
    public sealed class IncludedService : IService { }

    public sealed class Holder {
        [Hissal.UnityTypeSerializer.SerializedTypeOptions(
            ExcludedTypes = new[] { typeof(ExcludedService), typeof(IService) })]
        private Hissal.UnityTypeSerializer.SerializedType<IService> typeRef = new();
    }
}
";

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()));

        Assert.DoesNotContain(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Type 'Demo.ExcludedService'", System.StringComparison.Ordinal));
        Assert.Contains(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Type 'Demo.IncludedService'", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task InheritsOrImplementsNoneExcludesAssignableTypes() {
        var source = @"
namespace Demo {
    public interface IService { }
    public interface IBlocked { }
    public sealed class IncludedService : IService { }
    public sealed class BlockedService : IService, IBlocked { }

    public sealed class Holder {
        [Hissal.UnityTypeSerializer.SerializedTypeOptions(
            InheritsOrImplementsNone = new[] { typeof(IBlocked) })]
        private Hissal.UnityTypeSerializer.SerializedType<IService> typeRef = new();
    }
}
";

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()));

        Assert.Contains(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Type 'Demo.IncludedService'", System.StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Type 'Demo.BlockedService'", System.StringComparison.Ordinal));
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

    [Fact]
    public async Task ExternalManifestAppliesNewTypeFilters() {
        var source = @"
namespace Demo {
    public interface IService { }
    public interface IBlocked { }
    public sealed class ExcludedService : IService { }
    public sealed class IncludedService : IService { }
    public sealed class BlockedService : IService, IBlocked { }
    public sealed class OtherService : IService { }
}
";

        var manifestXml = @"
<SerializedTypeUsageManifest generatedAtUtc=""2026-08-10T00:00:00.0000000Z"">
  <Entry baseConstraint=""Demo.IService"" allowOpenGenerics=""false"" allowedTypeKinds=""3"" explicitTypes=""Demo.ExcludedService;Demo.IncludedService;Demo.BlockedService"" excludedTypes=""Demo.ExcludedService;Demo.IService"" inheritsAll="""" inheritsAny="""" inheritsNone=""Demo.IBlocked"" customTypeFilter="""" />
</SerializedTypeUsageManifest>
";

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()),
            ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText(
                "SerializedTypeUsageManifest.xml",
                manifestXml)));

        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "STG100"));
        Assert.Contains("Type 'Demo.IncludedService'", diagnostic.GetMessage(), System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReportsWarningFromExternalManifestConstraintsWithoutRuntimeContractsReference() {
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
                manifestXml)),
            includeRuntimeContracts: false);

        Assert.Contains(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Demo.ServiceImpl", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task DoesNotReportWhenSerializedTypeIdAttributeExistsByNameWithoutRuntimeContractsReference() {
        var source = @"
namespace Hissal.UnityTypeSerializer {
    [System.AttributeUsage(System.AttributeTargets.Class | System.AttributeTargets.Struct | System.AttributeTargets.Interface | System.AttributeTargets.Enum | System.AttributeTargets.Delegate)]
    public sealed class SerializedTypeIdAttribute : System.Attribute {
        public SerializedTypeIdAttribute(string id) { }
    }
}

namespace Demo {
    public interface IService { }

    [Hissal.UnityTypeSerializer.SerializedTypeId(""service"")]
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
                manifestXml)),
            includeRuntimeContracts: false);

        Assert.DoesNotContain(diagnostics, d => d.Id == "STG100");
    }

    [Fact]
    public async Task IgnoresUnconstrainedNonGenericSerializedTypeField() {
        var source = @"
namespace Demo {
    public sealed class ServiceImpl { }

    public sealed class Holder {
        private Hissal.UnityTypeSerializer.SerializedType typeRef = new();
    }
}
";

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()));

        Assert.DoesNotContain(diagnostics, d => d.Id == "STG100");
    }

    [Fact]
    public async Task ReportsForConstrainedNonGenericSerializedTypeField() {
        var source = @"
namespace Demo {
    public interface IService { }
    public sealed class ServiceImpl : IService { }

    public sealed class Holder {
        [Hissal.UnityTypeSerializer.SerializedTypeOptions(InheritsOrImplementsAll = new[] { typeof(IService) })]
        private Hissal.UnityTypeSerializer.SerializedType typeRef = new();
    }
}
";

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()));

        Assert.Contains(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Demo.ServiceImpl", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task AllowsGenericTypeConstructionToContributeGenericDefinitions() {
        var source = @"
namespace Demo {
    public interface IService { }
    public sealed class GenericService<T> : IService { }

    public sealed class Holder {
        [Hissal.UnityTypeSerializer.SerializedTypeOptions(AllowGenericTypeConstruction = true)]
        private Hissal.UnityTypeSerializer.SerializedType<IService> typeRef = new();
    }
}
";

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()));

        Assert.Contains(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Demo.GenericService<T>", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task ResolvesConcreteInheritedGenericConstraintsFromClosedDerivedType() {
        var source = @"
namespace Demo {
    public interface IGlobalEvent { }
    public sealed class ConcreteEvent : IGlobalEvent { }

    public abstract class EventHolder<TEventBase> where TEventBase : class {
        private Hissal.UnityTypeSerializer.SerializedType<TEventBase> typeRef = new();
    }

    public sealed class EventUsage : EventHolder<IGlobalEvent> { }
}
";

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()));

        Assert.Contains(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Demo.ConcreteEvent", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task AppliesUnionSemanticsAcrossMultipleSerializedTypeUsages() {
        var source = @"
namespace Demo {
    public interface IFoo { }
    public sealed class FooClass : IFoo { }
    public struct FooStruct : IFoo { }

    public sealed class BroadHolder {
        private Hissal.UnityTypeSerializer.SerializedType<IFoo> anyFoo = new();
    }

    public sealed class NarrowHolder {
        [Hissal.UnityTypeSerializer.SerializedTypeOptions(AllowedTypeKinds = Hissal.UnityTypeSerializer.SerializedTypeKind.Class)]
        private Hissal.UnityTypeSerializer.SerializedType<IFoo> classFoo = new();
    }
}
";

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()));

        Assert.Contains(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Demo.FooClass", System.StringComparison.Ordinal));
        Assert.Contains(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Demo.FooStruct", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task AllowedTypeKindsClassIncludesClassButNotStruct() {
        var source = @"
namespace Demo {
    public interface IFoo { }
    public sealed class FooClass : IFoo { }
    public struct FooStruct : IFoo { }

    public sealed class Holder {
        [Hissal.UnityTypeSerializer.SerializedTypeOptions(AllowedTypeKinds = Hissal.UnityTypeSerializer.SerializedTypeKind.Class)]
        private Hissal.UnityTypeSerializer.SerializedType<IFoo> classFoo = new();
    }
}
";

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()));

        Assert.Contains(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Demo.FooClass", System.StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Demo.FooStruct", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task AllowedTypeKindsStructIncludesStructButNotClass() {
        var source = @"
namespace Demo {
    public interface IFoo { }
    public sealed class FooClass : IFoo { }
    public struct FooStruct : IFoo { }

    public sealed class Holder {
        [Hissal.UnityTypeSerializer.SerializedTypeOptions(AllowedTypeKinds = Hissal.UnityTypeSerializer.SerializedTypeKind.Struct)]
        private Hissal.UnityTypeSerializer.SerializedType<IFoo> structFoo = new();
    }
}
";

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()));

        Assert.DoesNotContain(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Demo.FooClass", System.StringComparison.Ordinal));
        Assert.Contains(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Demo.FooStruct", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task AllowedTypeKindsObjectIncludesClassAndStruct() {
        var source = @"
namespace Demo {
    public interface IFoo { }
    public sealed class FooClass : IFoo { }
    public struct FooStruct : IFoo { }

    public sealed class Holder {
        [Hissal.UnityTypeSerializer.SerializedTypeOptions(AllowedTypeKinds = Hissal.UnityTypeSerializer.SerializedTypeKind.Object)]
        private Hissal.UnityTypeSerializer.SerializedType<IFoo> objectFoo = new();
    }
}
";

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()));

        Assert.Contains(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Demo.FooClass", System.StringComparison.Ordinal));
        Assert.Contains(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Demo.FooStruct", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task PropagatesGenericParameterConstraintTypesFromLikelySerializableGenericType() {
        var source = @"
namespace Demo {
    public interface IFactory { }
    public interface IFactoryArgument { }
    public sealed class FactoryArgumentImpl : IFactoryArgument { }

    public sealed class GenericFactory<TArg> : IFactory where TArg : IFactoryArgument { }

    public sealed class Holder {
        [Hissal.UnityTypeSerializer.SerializedTypeOptions(AllowGenericTypeConstruction = true)]
        private Hissal.UnityTypeSerializer.SerializedType<IFactory> factoryType = new();
    }
}
";

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()));

        Assert.Contains(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Demo.FactoryArgumentImpl", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task Stg100DiagnosticIncludesMatchReason() {
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
        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "STG100"));

        Assert.Contains("field 'Demo.Holder.typeRef'", diagnostic.GetMessage(), System.StringComparison.Ordinal);
        Assert.Contains("base 'Demo.IService'", diagnostic.GetMessage(), System.StringComparison.Ordinal);
        Assert.Contains("AllowedTypeKinds=Object", diagnostic.GetMessage(), System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task SourceDiagnosticIncludesSerializedTypeOptionsDetails() {
        var source = @"
namespace Demo {
    public interface IService { }
    public interface IOtherService { }
    public sealed class ServiceImpl : IService { }

    public sealed class Holder {
        [Hissal.UnityTypeSerializer.SerializedTypeOptions(
            AllowedTypeKinds = Hissal.UnityTypeSerializer.SerializedTypeKind.Class,
            InheritsOrImplementsAny = new[] { typeof(IService), typeof(IOtherService) })]
        private Hissal.UnityTypeSerializer.SerializedType typeRef = new();
    }
}
";

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()));
        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "STG100"));

        Assert.Contains("field 'Demo.Holder.typeRef'", diagnostic.GetMessage(), System.StringComparison.Ordinal);
        Assert.Contains("base 'object'", diagnostic.GetMessage(), System.StringComparison.Ordinal);
        Assert.Contains("AllowedTypeKinds=Class", diagnostic.GetMessage(), System.StringComparison.Ordinal);
        Assert.Contains("InheritsOrImplementsAny=[Demo.IService, Demo.IOtherService]", diagnostic.GetMessage(), System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalManifestSkipsObjectBaseWhenOptionConstraintsAreUnresolved() {
        var source = @"
namespace Demo {
    public sealed class Unrelated { }
}
";

        var manifestXml = @"
<SerializedTypeUsageManifest generatedAtUtc=""2026-04-11T00:00:00.0000000Z"">
  <Entry declaringType=""Playground.TestTypes"" fieldName=""fld"" baseConstraint=""System.Object"" allowGenericTypeConstruction=""false"" allowOpenGenerics=""false"" allowedTypeKinds=""3"" inheritsAll="""" inheritsAny=""Playground.IInter;Playground.IInter2;Playground.BaseClass"" customTypeFilter="""" />
</SerializedTypeUsageManifest>
";

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()),
            ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText(
                "SerializedTypeUsageManifest.xml",
                manifestXml)));

        Assert.DoesNotContain(diagnostics, d => d.Id == "STG100");
    }

    [Fact]
    public async Task ExternalManifestUsesResolvedAnyConstraintsForObjectBase() {
        var source = @"
namespace Demo {
    public interface IService { }
    public sealed class ServiceImpl : IService { }
    public sealed class Unrelated { }
}
";

        var manifestXml = @"
<SerializedTypeUsageManifest generatedAtUtc=""2026-04-11T00:00:00.0000000Z"">
  <Entry declaringType=""Demo.Holder"" fieldName=""typeRef"" baseConstraint=""System.Object"" allowGenericTypeConstruction=""false"" allowOpenGenerics=""false"" allowedTypeKinds=""1"" inheritsAll="""" inheritsAny=""Missing.IService;Demo.IService"" customTypeFilter="""" />
</SerializedTypeUsageManifest>
";

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()),
            ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText(
                "SerializedTypeUsageManifest.xml",
                manifestXml)));
        var diagnostic = Assert.Single(diagnostics.Where(d => d.Id == "STG100"));

        Assert.Contains("Demo.ServiceImpl", diagnostic.GetMessage(), System.StringComparison.Ordinal);
        Assert.DoesNotContain("Demo.Unrelated", diagnostic.GetMessage(), System.StringComparison.Ordinal);
        Assert.Contains("manifest field 'Demo.Holder.typeRef'", diagnostic.GetMessage(), System.StringComparison.Ordinal);
        Assert.Contains("base 'object'", diagnostic.GetMessage(), System.StringComparison.Ordinal);
        Assert.Contains("AllowedTypeKinds=Class", diagnostic.GetMessage(), System.StringComparison.Ordinal);
        Assert.Contains("InheritsOrImplementsAny=[Demo.IService]", diagnostic.GetMessage(), System.StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExternalManifestRespectsAllowGenericTypeConstruction() {
        var source = @"
namespace Demo {
    public interface IService { }
    public sealed class GenericService<T> : IService { }
}
";

        var manifestXml = @"
<SerializedTypeUsageManifest generatedAtUtc=""2026-04-11T00:00:00.0000000Z"">
  <Entry baseConstraint=""Demo.IService"" allowGenericTypeConstruction=""true"" allowOpenGenerics=""false"" allowedTypeKinds=""3"" inheritsAll="""" inheritsAny="""" customTypeFilter="""" />
</SerializedTypeUsageManifest>
";

        var diagnostics = await AnalyzerTestHelper.GetAnalyzerDiagnosticsAsync(
            source,
            ImmutableArray.Create<DiagnosticAnalyzer>(new SerializedTypeIdEligibilityAnalyzer()),
            ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText(
                "SerializedTypeUsageManifest.xml",
                manifestXml)));

        Assert.Contains(diagnostics, d => d.Id == "STG100" && d.GetMessage().Contains("Demo.GenericService<T>", System.StringComparison.Ordinal));
    }

}
