extern alias SharedKernelAnalyzers;

using DRN.Framework.SharedKernel.Analyzers.Conventions;
using Microsoft.CodeAnalysis.Emit;
using ConventionInputLimits = SharedKernelAnalyzers::DRN.Framework.SharedKernel.Analyzers.Conventions.ConventionInputLimits;
using ConventionInputSyntax = SharedKernelAnalyzers::DRN.Framework.SharedKernel.Analyzers.Conventions.ConventionInputSyntax;
using static DRN.Test.Analyzer.Tests.DiagnosticClassificationAnalyzerTests;
using static DRN.Test.Analyzer.Tests.ProjectDiagnosticRulesTests;

namespace DRN.Test.Analyzer.Tests;

public sealed class ConventionInputTests
{
    [Theory]
    [InlineData("Sample.Domain", "Domain")]
    [InlineData("DRN.Nexus.Infra", "Infra")]
    [InlineData("Sample.Contract", "Contract")]
    [InlineData("Sample.Application", "Application")]
    [InlineData("Sample.Hosted", "Hosted")]
    [InlineData("Sample.Utils", "Utils")]
    [InlineData("Sample.SharedKernel", "SharedKernel")]
    [InlineData("Company.Shop.Infrastructure.Postgres", "Infra")]
    [InlineData("Company.Shop.infra.Postgres", "Infra")]
    [InlineData("Sample.Host.Web", "Hosted")]
    [InlineData("Sample.Hosting.Worker", "Hosted")]
    [InlineData("Sample.Contracts.Public", "Contract")]
    [InlineData("Infra", "Infra")]
    [InlineData("Hosted", "Hosted")]
    public void Project_Identity_Should_Preserve_Project_Names_And_Infer_Only_The_Role(string name, string role)
    {
        var project = ConventionProject.Read(Compilation("public class Marker;").WithAssemblyName("Published.Library"),
            new Config(new()
            {
                ["build_property.MSBuildProjectName"] = name,
                ["build_property.RootNamespace"] = "Custom.Logical.Home"
            }));

        project.Enabled.Should().BeTrue();
        project.Name.Should().Be(name);
        project.Role.Should().Be(role);
        project.RootNamespace.Should().Be("Custom.Logical.Home");
        project.AssemblyName.Should().Be("Published.Library");
        project.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Unmapped_Project_Names_Should_Not_Acquire_A_Role_From_Substrings()
    {
        var options = new Dictionary<string, string>
        {
            ["build_property.MSBuildProjectName"] = "DomainHelpers",
            ["build_property.RootNamespace"] = "Custom.Logical.Home"
        };
        var unknown = ConventionProject.Read(Compilation("public class Marker;"), new Config(options));
        unknown.Enabled.Should().BeFalse();
        unknown.Diagnostics.Should().BeEmpty();
        options["build_property.MSBuildProjectName"] = "Mapped.App.Infra.Postgres";
        var mapped = ConventionProject.Read(Compilation("public class Marker;"), new Config(options));
        mapped.Enabled.Should().BeTrue();
        mapped.Diagnostics.Should().BeEmpty();
        mapped.Name.Should().Be("Mapped.App.Infra.Postgres");
        mapped.Role.Should().Be("Infra");
    }

    [Theory]
    [InlineData("Library", false, 0)]
    [InlineData("Sample.domain", true, 0)]
    [InlineData("Sample.Domain.Tests", false, 0)]
    [InlineData("Sample.Test.Infra", false, 0)]
    [InlineData("Sample.Testing.Domain", false, 0)]
    [InlineData("DRN.Framework.Utils", false, 0)]
    [InlineData("DRN.Framework.SharedKernel", false, 0)]
    [InlineData("Sample.Domain.Analyzers", false, 0)]
    [InlineData("Sample.Domain.Analyzer", false, 0)]
    [InlineData("Sample.Domain.CodeFixes", false, 0)]
    [InlineData("Sample.Hosting.Generators", false, 0)]
    [InlineData("Sample.Hosting.Generator", false, 0)]
    [InlineData("Sample.Domain.Infra", false, 1)]
    [InlineData("Sample.Infra.Infrastructure", false, 1)]
    [InlineData("Infra", true, 0)]
    [InlineData("Sample.DomainHelpers", false, 0)]
    [InlineData("Sample.InfrastructureHelpers", false, 0)]
    [InlineData("Sample..Domain", true, 0)]
    public void Activation_Should_Use_Unambiguous_Role_Tokens_And_Exclude_Support_Projects(string name,
        bool expected, int errors)
    {
        var project = ConventionProject.Read(Compilation("public class Marker;"), new Config(new()
        {
            ["build_property.MSBuildProjectName"] = name,
            ["build_property.RootNamespace"] = "Sample"
        }));
        project.Enabled.Should().Be(expected);
        project.Diagnostics.Should().HaveCount(errors).And.OnlyContain(diagnostic => diagnostic.Id == "DRNCV001");
    }

    [Fact]
    public void Assembly_Name_Should_Supply_Identity_When_The_Standard_Project_Name_Is_Unavailable()
    {
        var project = ConventionProject.Read(Compilation("public class Marker;").WithAssemblyName("Sample.Infra.Postgres"),
            new Config(new() { ["build_property.RootNamespace"] = "Sample.Infra" }));
        project.Enabled.Should().BeTrue();
        project.Name.Should().Be("Sample.Infra.Postgres");
        project.Role.Should().Be("Infra");
        project.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Missing_RootNamespace_Should_Not_Be_Replaced_With_Assembly_Identity()
    {
        var project = ConventionProject.Read(Compilation("public class Marker;").WithAssemblyName("Sample.Domain"),
            new Config(new()
            {
                ["build_property.MSBuildProjectName"] = "Sample.Domain"
            }));
        project.Diagnostics.Should().ContainSingle().Which.GetMessage().Should().Contain("RootNamespace");
    }

    [Theory]
    [InlineData("QA\\Read\\Configurations", true)]
    [InlineData("QA/Read/Configurations", true)]
    [InlineData("../Outside", false)]
    [InlineData("QA/../Outside", false)]
    [InlineData("/Outside", false)]
    [InlineData("C:\\Outside", false)]
    [InlineData("QA//Configurations", false)]
    public void Logical_Paths_Should_Normalize_Separators_Without_Allowing_Traversal(string path, bool expected) =>
        ConventionProject.IsLogicalPath(path).Should().Be(expected);

    [Fact]
    public void Msbuild_SymbolTarget_Should_Preserve_The_Exception_Target()
    {
        var input = Read("""
            [assembly: DRN.Conventions.Version("1")]
            [assembly: DRN.Conventions.Exception("DRNCV019", SymbolTarget = "M:Shop.Application.Sales.Reader.Read", Reason = "Reviewed coupling")]
            """);
        input.Diagnostics.Should().BeEmpty();
        input.Records[1].Get("Target").Should().Be("M:Shop.Application.Sales.Reader.Read");
        input.Records[1].Get("Reason").Should().Be("Reviewed coupling");
    }

    [Fact]
    public void Duplicate_Exception_Target_Aliases_Should_Be_Rejected()
    {
        var input = Read("""
            [assembly: DRN.Conventions.Version("1")]
            [assembly: DRN.Conventions.Exception("DRNCV019", SymbolTarget = "T:Shop.Reader", Target = "T:Shop.Other")]
            """);
        input.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCV001");
        input.Records.Should().NotContain(record => record.Kind == "Exception");
    }

    [Fact]
    public void Input_Data_Should_Preserve_Quotes_Separators_And_Reasons_Without_Evaluation()
    {
        const string reason = "Identity alias; preserve \"named\" storage.\nReviewed owner.";
        var input = Read("[assembly: DRN.Conventions.Version(\"1\")]\n" +
                         "[assembly: DRN.Conventions.Context(\"Sample.Infra.Identity.SampleIdentityContext\", " +
                         "Reason = " + SymbolDisplay.FormatLiteral(reason, true) + ", Subdomain = \"Identity\")]");
        input.Diagnostics.Should().BeEmpty();
        input.Records[1].Get("Reason").Should().Be(reason);
        input.Records[1].Get("Subdomain").Should().Be("Identity");
        input.Records[1].Location.Kind.Should().Be(LocationKind.ExternalFile);
        input.Records[1].Location.GetLineSpan().StartLinePosition.Line.Should().Be(1);
    }

    [Theory]
    [InlineData("[assembly: DRN.Conventions.Version(1)]")]
    [InlineData("[assembly: DRN.Conventions.Version(\"2\")]")]
    [InlineData("[assembly: DRN.Conventions.Version(\"1\")][assembly: DRN.Conventions.Version(\"1\")]")]
    [InlineData("[assembly: DRN.Conventions.Context(System.Environment.MachineName)]")]
    [InlineData("[assembly: Other.Context(\"1\")]")]
    [InlineData("class Executable { }")]
    public void Malformed_Or_Nonliteral_Input_Should_Report_Incomplete_Evidence(string source) =>
        Read(source).Diagnostics.Should().NotBeEmpty().And.OnlyContain(diagnostic => diagnostic.Id == "DRNCV001");

    [Fact]
    public void Wrapped_String_Literals_Should_Preserve_Build_Fragment_Data()
    {
        var input = Read("""
            [assembly: DRN.Conventions.Version("1")]
            [assembly: DRN.Conventions.Context("Sample.Infra." + "QA.QAContext", Reason = ("Reviewed " + "mapping"))]
            """);
        input.Diagnostics.Should().BeEmpty();
        input.Records[1].Identity.Should().Be("Sample.Infra.QA.QAContext");
        input.Records[1].Get("Reason").Should().Be("Reviewed mapping");
    }

    [Fact]
    public void Missing_Duplicate_And_Overlarge_Input_Should_Not_Appear_Valid()
    {
        ConventionInputs.Read(new AnalyzerOptions([]), CancellationToken.None).Diagnostics.Should().ContainSingle();
        var file = new RuleFile(ConventionInputs.FileName, "[assembly: DRN.Conventions.Version(\"1\")]");
        ConventionInputs.Read(new AnalyzerOptions([file, file]), CancellationToken.None).Diagnostics.Should().ContainSingle();
        Read(new string(' ', 1_048_577)).Diagnostics.Should().ContainSingle();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var read = () => ConventionInputs.Read(new AnalyzerOptions([file]), cancellation.Token);
        read.Should().Throw<OperationCanceledException>();
    }

    [Fact]
    public void Ownership_Attribute_Should_Survive_Reference_Assembly_Emission()
    {
        var compilation = Compilation("""
            using DRN.Framework.SharedKernel.Attributes;
            namespace Sample.Domain.Identity;
            [SourceKnownSubdomain("Identity", Reason = "Authentication account ownership")]
            public class Account;
            """);
        compilation.GetDiagnostics().Should().NotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream, options: new EmitOptions(metadataOnly: true, includePrivateMembers: false));
        result.Success.Should().BeTrue(string.Join(Environment.NewLine, result.Diagnostics));
        var reference = MetadataReference.CreateFromImage(stream.ToArray());
        var consumer = Compilation("public class Consumer;").WithAssemblyName("Consumer").AddReferences(reference);
        var account = consumer.GetTypeByMetadataName("Sample.Domain.Identity.Account")!;
        var attribute = account.GetAttributes().Single(item => item.AttributeClass?.Name == "SourceKnownSubdomainAttribute");
        attribute.ConstructorArguments[0].Value.Should().Be("Identity");
        attribute.NamedArguments.Single().Value.Value.Should().Be("Authentication account ownership");
    }

    [Theory]
    [InlineData("[module: DRN.Conventions.Version(\"1\")]")]
    [InlineData("[assembly: DRN.Conventions.Version(Identity = \"1\")]")]
    [InlineData("[assembly: DRN.Conventions.Version(\"1\", Extra = \"x\")]")]
    [InlineData("extern alias Untrusted; [assembly: DRN.Conventions.Version(\"1\")]")]
    [InlineData("[assembly: DRN.Conventions.Version(\"1\")][assembly: Unknown.Record(\"x\")]")]
    public void Unsupported_Data_Fragment_Should_Fail_Closed(string source)
    {
        var input = Read(source);
        input.Diagnostics.Should().NotBeEmpty();
        input.Records.Should().BeEmpty();
    }

    [Fact]
    public void Identical_Records_Should_Be_Idempotent_And_Contradictory_Targets_Should_Fail_Closed()
    {
        const string source = """
            using System;
            using System.Reflection;
            [assembly: DRN.Conventions.Version(("" + "1"))]
            [assembly: DRN.Conventions.Context("Sample.Context", Subdomain = "QA", Reason = "reviewed")]
            [assembly: DRN.Conventions.Context(("Sample." + "Context"), Reason = "reviewed", Subdomain = "QA")]
            """;
        var same = Read(source);
        same.Diagnostics.Should().BeEmpty();
        same.Records.Should().HaveCount(2);
        var conflict = Read(source + "[assembly: DRN.Conventions.Context(\"Sample.Context\", Subdomain = \"Other\")]");
        conflict.Records.Should().BeEmpty();
        conflict.Diagnostics.Should().ContainSingle().Which.GetMessage().Should().Contain("contradictory");
    }

    [Fact]
    public void Record_Limit_Should_Count_Identical_Records_Before_Deduplication()
    {
        var source = "[assembly: DRN.Conventions.Version(\"1\")]" + string.Concat(Enumerable.Repeat(
            "[assembly: DRN.Conventions.Path(\"x\", LogicalPath = \"x\")]", ConventionInputLimits.MaxRecords));
        var input = Read(source);
        input.Records.Should().BeEmpty();
        input.Diagnostics.Should().ContainSingle().Which.GetMessage().Should().Contain("record limit");
    }

    [Theory]
    [InlineData("[assembly: DRN.Conventions.Path(\"/source/User.cs\", LogicalPath = \"QA/User.cs\")]", true)]
    [InlineData("[assembly: DRN.Conventions.Path(\"/source/User.cs\", LogicalPath = \"QA/../User.cs\")]", false)]
    [InlineData("[assembly: DRN.Conventions.Reference(\"Inventory\", Stage = \"Complete\")]", true)]
    [InlineData("[assembly: DRN.Conventions.Reference(\"Other\", Stage = \"Complete\")]", false)]
    [InlineData("[assembly: DRN.Conventions.Reference(\"Inventory\", Stage = \"Custom\")]", false)]
    [InlineData("[assembly: DRN.Conventions.Ownership(\"Sample.User\", Subdomain = \"QA\", Kind = \"Entity\")]", true)]
    [InlineData("[assembly: DRN.Conventions.Ownership(\"Sample.User\", Subdomain = \"QA\", Kind = \"Helper\")]", false)]
    [InlineData("[assembly: DRN.Conventions.Registration(\"Sample.Service\", Service = \"\")]", false)]
    [InlineData("[assembly: DRN.Conventions.Configuration(\"Sample.Mapping\", Context = \"\")]", false)]
    public void Generation_Should_Reject_Invalid_Structural_Field_Values(string record, bool supported)
    {
        var text = Microsoft.CodeAnalysis.Text.SourceText.From("[assembly: DRN.Conventions.Version(\"1\")]" + record);
        var root = ConventionInputSyntax.Parse(text, "DrnConventionInputs.g.cs", CancellationToken.None, out _);
        root.Should().NotBeNull();
        ConventionInputSyntax.SupportsGeneration(root!, CancellationToken.None).Should().Be(supported);
    }

    private static ConventionInputs Read(string source) => ConventionInputs.Read(
        new AnalyzerOptions([new RuleFile(ConventionInputs.FileName, source)]), CancellationToken.None);

    internal sealed class Config(Dictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
    }
}
