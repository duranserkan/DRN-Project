using System.Diagnostics.CodeAnalysis;
using DRN.Framework.SharedKernel.Analyzers.Conventions;
using static DRN.Test.Analyzer.Tests.ClassificationTestFixture;
using static DRN.Test.Analyzer.Tests.ProjectDiagnosticRulesTests;

namespace DRN.Test.Analyzer.Tests;

[SuppressMessage("ReSharper", "AccessToDisposedClosure")]
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
        var diagnostics = await AnalyzerTestCompilation.Analyze(compilation, new SourceKnownConventionAnalyzer(), new AnalyzerOptions([]));
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Application_Project_Should_Report_Missing_Compiler_Inputs_Without_Opt_In()
    {
        var compilation = Compilation("public class Marker;");
        var options = AnalyzerTestCompilation.ConventionOptions("Sample.Domain");
        var diagnostics = await AnalyzerTestCompilation.Analyze(compilation, new SourceKnownConventionAnalyzer(), options);
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCONVENTIONINPUT001");
    }

    [Fact]
    public async Task Standard_Diagnostic_Severity_Should_Suppress_Conventions_With_Invalid_Inputs()
    {
        var compilation = Compilation("""
            namespace Sample.Domain.QA;
            public class Payload : DRN.Framework.SharedKernel.Domain.Dto;
            """);
        var invalidInput = new RuleFile(ConventionInputs.FileName, "invalid convention input");
        var diagnostics = await AnalyzerTestCompilation.Analyze(ConventionTestDependencies.SuppressConventions(compilation),
            new SourceKnownConventionAnalyzer(), AnalyzerTestCompilation.ConventionOptions("Sample.Domain", files: invalidInput));
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Source_Suppression_Should_Keep_Independent_Exception_Audit_Active()
    {
        var compilation = Compilation("""
            using System.Diagnostics.CodeAnalysis;
            namespace Sample.Domain.QA;
            [SuppressMessage("DRN.Conventions.Structure", "DRNSTRUCTURE001", Justification = "")]
            public class Thing;
            """, "Thing.cs");
        var fixture = ConventionTestDependencies.WithRequiredLayers(compilation, "Domain");
        var input = new RuleFile(ConventionInputs.FileName, """
            [assembly: DRN.Conventions.Version("1")]
            [assembly: DRN.Conventions.Reference("Inventory", Stage = "Complete")]
            [assembly: DRN.Conventions.Path("Thing.cs", LogicalPath = "Other/Thing.cs")]
            [assembly: DRN.Conventions.Ownership("Sample.Domain.QA.Thing", Subdomain = "QA", Kind = "Entity")]
            """ + "\n" + ConventionTestDependencies.Serialize(fixture.Records));
        var options = AnalyzerTestCompilation.ConventionOptions("Sample.Domain", files: input);
        var diagnostics = await AnalyzerTestCompilation.Analyze(fixture.Compilation, new SourceKnownConventionAnalyzer(), options);
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCONVENTIONEXCEPTION001");
    }

    [Fact]
    public async Task Missing_Reference_Inventory_Should_Not_Look_Complete()
    {
        var input = new RuleFile(ConventionInputs.FileName, "[assembly: DRN.Conventions.Version(\"1\")]");
        var diagnostics = await AnalyzerTestCompilation.Analyze(Compilation("public class Marker;"), new SourceKnownConventionAnalyzer(),
            AnalyzerTestCompilation.ConventionOptions("Sample.Domain", files: input));
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
        var diagnostics = await AnalyzerTestCompilation.Analyze(fixture.Compilation, new SourceKnownConventionAnalyzer(),
            AnalyzerTestCompilation.ConventionOptions("Sample.Domain", files: input));
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public async Task Consecutive_Compilations_Should_Not_Retain_Inputs_Or_Diagnostics()
    {
        var analyzer = new SourceKnownConventionAnalyzer();
        var first = Compilation("public class First;");
        var options = AnalyzerTestCompilation.ConventionOptions("Sample.Domain");
        (await AnalyzerTestCompilation.Analyze(first, analyzer, options)).Should().ContainSingle();
        var second = ConventionTestDependencies.WithRequiredLayers(Compilation("public class Second;"), "Domain");
        var file = new RuleFile(ConventionInputs.FileName, """
            [assembly: DRN.Conventions.Version("1")]
            [assembly: DRN.Conventions.Reference("Inventory", Stage = "Complete")]
            """ + "\n" + ConventionTestDependencies.Serialize(second.Records));
        (await AnalyzerTestCompilation.Analyze(second.Compilation, analyzer, AnalyzerTestCompilation.ConventionOptions("Sample.Domain", files: file)))
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
        producer.AssertNoCompilerErrors();
        MetadataReference reference = shape switch
        {
            "source" => producer.ToMetadataReference(),
            "reference-assembly" => AnalyzerTestCompilation.EmitReference(producer, options: AnalyzerTestCompilation.ReferenceAssemblyOptions),
            "assembly" => AnalyzerTestCompilation.EmitReference(producer),
            _ => throw new ArgumentOutOfRangeException(nameof(shape))
        };
        var consumer = Compilation("""
            namespace Sample.Domain.Billing;
            public class Reader { public Legacy.Models.Customer Read() => new(); }
            """, "Billing/Reader.cs").AddReferences(reference);
        consumer.AssertNoCompilerErrors();
        var fixture = ConventionTestDependencies.WithRequiredLayers(consumer, "Domain");
        var file = new RuleFile(ConventionInputs.FileName, """
            [assembly: DRN.Conventions.Version("1")]
            [assembly: DRN.Conventions.Reference("Inventory", Stage = "Complete")]
            [assembly: DRN.Conventions.Path("Billing/Reader.cs", LogicalPath = "Billing/Reader.cs")]
            """ + "\n" + ConventionTestDependencies.Serialize(fixture.Records));
        var diagnostics = await AnalyzerTestCompilation.Analyze(fixture.Compilation, new SourceKnownConventionAnalyzer(),
            AnalyzerTestCompilation.ConventionOptions("Sample.Domain", files: file));
        var diagnostic = diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNOWNERSHIP002");
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
        var diagnostics = await AnalyzerTestCompilation.Analyze(compilation, new SourceKnownConventionAnalyzer(),
            AnalyzerTestCompilation.ConventionOptions("Sample.Domain"));
        diagnostics.Should().NotContain(item => item.Id == "AD0001");
        diagnostics.Where(item => item.Id == "DRNCONVENTIONINPUT001").Select(item => item.GetMessage()).Should().BeEquivalentTo(MissingInputMessages);
        var role = diagnostics.Where(item => item.Id == "DRNARCHITECTURE004").Should().ContainSingle().Which;
        role.GetMessage().Should().Be("Implementation 'Payload' (DTO) belongs to role 'Contract'.");
        role.Severity.Should().Be(DiagnosticSeverity.Warning);
        role.Location.SourceTree!.GetText().ToString(role.Location.SourceSpan).Should().Be("Payload");
    }

    [Fact]
    public async Task PreCanceled_Token_Should_Cancel_Convention_Analysis()
    {
        var compilation = Compilation("""
            namespace Sample.Domain.QA;
            public class Payload : DRN.Framework.SharedKernel.Domain.Dto;
            """);
        compilation.AssertNoCompilerErrors();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var act = async () => await compilation.WithAnalyzers([new SourceKnownConventionAnalyzer()],
            AnalyzerTestCompilation.ConventionOptions("Sample.Domain")).GetAnalyzerDiagnosticsAsync(cancellation.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
