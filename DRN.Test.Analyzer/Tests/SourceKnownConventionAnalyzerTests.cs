using DRN.Framework.SharedKernel.Analyzers.Conventions;
using static DRN.Test.Analyzer.Tests.DiagnosticClassificationAnalyzerTests;
using static DRN.Test.Analyzer.Tests.ProjectDiagnosticRulesTests;

namespace DRN.Test.Analyzer.Tests;

public sealed class SourceKnownConventionAnalyzerTests
{
    private static readonly string[] MissingInputMessages =
    [
        "Convention input 'DrnConventionInputs.g.cs' is missing. Declare one valid build-generated convention input file.",
        "Convention input 'Sample.Domain.QA.Payload' is missing logical source placement. Declare one valid Compile or Link path."
    ];
    [Fact]
    public async Task Unidentified_Project_Should_Leave_Conventions_Inactive()
    {
        var compilation = Compilation("public class Marker;");
        var diagnostics = await compilation.WithAnalyzers([new SourceKnownConventionAnalyzer()], new AnalyzerOptions([]))
            .GetAnalyzerDiagnosticsAsync();
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Application_Project_Should_Report_Missing_Compiler_Inputs_Without_Opt_In()
    {
        var compilation = Compilation("public class Marker;");
        var options = new AnalyzerOptions([], new Options());
        var diagnostics = await compilation.WithAnalyzers([new SourceKnownConventionAnalyzer()], options).GetAnalyzerDiagnosticsAsync();
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCV001");
    }

    [Fact]
    public async Task Standard_Diagnostic_Severity_Should_Suppress_Conventions_With_Invalid_Inputs()
    {
        var compilation = Compilation("""
            namespace Sample.Domain.QA;
            public class Payload : DRN.Framework.SharedKernel.Domain.Dto;
            """);
        var invalidInput = new RuleFile(ConventionInputs.FileName, "invalid convention input");
        var diagnostics = await ConventionTestDependencies.SuppressConventions(compilation)
            .WithAnalyzers([new SourceKnownConventionAnalyzer()],
                new AnalyzerOptions([invalidInput], new Options())).GetAnalyzerDiagnosticsAsync();
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Source_Suppression_Should_Keep_Independent_Exception_Audit_Active()
    {
        var compilation = Compilation("""
            using System.Diagnostics.CodeAnalysis;
            namespace Sample.Domain.QA;
            [SuppressMessage("DRN.Conventions.Structure", "DRNCV003", Justification = "")]
            public class Thing;
            """, "Thing.cs");
        var fixture = ConventionTestDependencies.WithRequiredLayers(compilation, "Domain");
        var input = new RuleFile(ConventionInputs.FileName, """
            [assembly: DRN.Conventions.Version("1")]
            [assembly: DRN.Conventions.Reference("Inventory", Stage = "Complete")]
            [assembly: DRN.Conventions.Path("Thing.cs", LogicalPath = "Other/Thing.cs")]
            [assembly: DRN.Conventions.Ownership("Sample.Domain.QA.Thing", Subdomain = "QA", Kind = "Entity")]
            """ + "\n" + ConventionTestDependencies.Serialize(fixture.Records));
        var options = new AnalyzerOptions([input], new Options());
        var diagnostics = await fixture.Compilation.WithAnalyzers([new SourceKnownConventionAnalyzer()], options).GetAnalyzerDiagnosticsAsync();
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCV018");
    }

    [Fact]
    public async Task Missing_Reference_Inventory_Should_Not_Look_Complete()
    {
        var input = new RuleFile(ConventionInputs.FileName, "[assembly: DRN.Conventions.Version(\"1\")]");
        var diagnostics = await Compilation("public class Marker;").WithAnalyzers([new SourceKnownConventionAnalyzer()],
            new AnalyzerOptions([input], new Options())).GetAnalyzerDiagnosticsAsync();
        diagnostics.Should().ContainSingle().Which.GetMessage().Should().Contain("inventory provenance");
    }

    [Fact]
    public async Task Identical_Reference_Inventory_Records_Should_Be_Idempotent()
    {
        var fixture = ConventionTestDependencies.WithRequiredLayers(Compilation("public class Marker;"), "Domain");
        var input = new RuleFile(ConventionInputs.FileName, """
            [assembly: DRN.Conventions.Version("1")]
            [assembly: DRN.Conventions.Reference("Inventory", Stage = "Complete")]
            [assembly: DRN.Conventions.Reference("Inventory", Stage = "Complete")]
            """ + "\n" + ConventionTestDependencies.Serialize(fixture.Records));
        var diagnostics = await fixture.Compilation.WithAnalyzers([new SourceKnownConventionAnalyzer()],
            new AnalyzerOptions([input], new Options())).GetAnalyzerDiagnosticsAsync();
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Consecutive_Compilations_Should_Not_Retain_Inputs_Or_Diagnostics()
    {
        var analyzer = new SourceKnownConventionAnalyzer();
        var first = Compilation("public class First;");
        var options = new AnalyzerOptions([], new Options());
        (await first.WithAnalyzers([analyzer], options).GetAnalyzerDiagnosticsAsync()).Should().ContainSingle();
        var second = ConventionTestDependencies.WithRequiredLayers(Compilation("public class Second;"), "Domain");
        var file = new RuleFile(ConventionInputs.FileName, """
            [assembly: DRN.Conventions.Version("1")]
            [assembly: DRN.Conventions.Reference("Inventory", Stage = "Complete")]
            """ + "\n" + ConventionTestDependencies.Serialize(second.Records));
        (await second.Compilation.WithAnalyzers([analyzer], new AnalyzerOptions([file], new Options())).GetAnalyzerDiagnosticsAsync())
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("source")]
    [InlineData("assembly")]
    [InlineData("reference-assembly")]
    public async Task Producer_Ownership_Should_Survive_Source_And_Emitted_Reference_Boundaries(string shape)
    {
        var producer = Compilation("""
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Domain")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Domain")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Legacy.Models.Customer.Subdomain", "QA")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Legacy.Models.Customer.Kind", "Entity")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.Ownership.Legacy.Models.Customer.Reason", "Preserve a published model namespace while retaining QA ownership.")]
            namespace Legacy.Models;
            public class Customer : DRN.Framework.SharedKernel.Domain.SourceKnownEntity;
            """).WithAssemblyName("Published.Domain");
        producer.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        MetadataReference reference;
        if (shape == "source") reference = producer.ToMetadataReference();
        else
        {
            using var image = new MemoryStream();
            var result = producer.Emit(image, options: new Microsoft.CodeAnalysis.Emit.EmitOptions(
                metadataOnly: shape == "reference-assembly", includePrivateMembers: shape != "reference-assembly"));
            result.Diagnostics.Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
            result.Success.Should().BeTrue();
            reference = MetadataReference.CreateFromImage(image.ToArray());
        }
        var consumer = Compilation("""
            namespace Sample.Domain.Billing;
            public class Reader { public Legacy.Models.Customer Read() => new(); }
            """, "Billing/Reader.cs").AddReferences(reference);
        consumer.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        var fixture = ConventionTestDependencies.WithRequiredLayers(consumer, "Domain");
        var file = new RuleFile(ConventionInputs.FileName, """
            [assembly: DRN.Conventions.Version("1")]
            [assembly: DRN.Conventions.Reference("Inventory", Stage = "Complete")]
            [assembly: DRN.Conventions.Path("Billing/Reader.cs", LogicalPath = "Billing/Reader.cs")]
            """ + "\n" + ConventionTestDependencies.Serialize(fixture.Records));
        var diagnostics = await fixture.Compilation.WithAnalyzers([new SourceKnownConventionAnalyzer()],
            new AnalyzerOptions([file], new Options())).GetAnalyzerDiagnosticsAsync();
        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNCV019");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.Properties["Entity"].Should().Be("Legacy.Models.Customer");
        diagnostic.Properties["Consumer"].Should().Be("M:Sample.Domain.Billing.Reader.Read");
        diagnostic.GetMessage().Should().Be("'Sample.Domain.Billing.Reader.Read()' in subdomain 'Billing' references entity 'Legacy.Models.Customer' from 'QA'. Use the entity's identifier or a contract from 'QA'.");
        diagnostic.Location.SourceTree.Should().Be(consumer.SyntaxTrees.Single());
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("Legacy.Models.Customer");
    }

    [Fact]
    public async Task Excluded_Compiler_Inputs_Should_Report_Incompleteness_Without_Disabling_Independent_Rules()
    {
        var compilation = Compilation("""
            namespace Sample.Domain.QA;
            public class Payload : DRN.Framework.SharedKernel.Domain.Dto;
            """);
        var diagnostics = await compilation.WithAnalyzers([new SourceKnownConventionAnalyzer()],
            new AnalyzerOptions([], new Options())).GetAnalyzerDiagnosticsAsync();
        diagnostics.Should().NotContain(item => item.Id == "AD0001");
        diagnostics.Where(item => item.Id == "DRNCV001").Select(item => item.GetMessage()).Should().BeEquivalentTo(MissingInputMessages);
        var role = diagnostics.Where(item => item.Id == "DRNCV016").Should().ContainSingle().Which;
        role.GetMessage().Should().Be("Implementation 'Payload' (DTO) belongs to role 'Contract'.");
        role.Severity.Should().Be(DiagnosticSeverity.Warning);
        role.Location.SourceTree!.GetText().ToString(role.Location.SourceSpan).Should().Be("Payload");
    }

    private sealed class Options : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new ConventionInputTests.Config(new()
        {
            ["build_property.MSBuildProjectName"] = "Sample.Domain",
            ["build_property.RootNamespace"] = "Sample.Domain"
        });
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
    }
}
