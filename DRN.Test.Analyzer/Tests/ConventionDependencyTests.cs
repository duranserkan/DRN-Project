using DRN.Framework.SharedKernel.Analyzers.Conventions;
using static DRN.Test.Analyzer.Tests.DiagnosticClassificationAnalyzerTests;

namespace DRN.Test.Analyzer.Tests;

public sealed class ConventionDependencyTests
{
    [Theory]
    [InlineData("Orders.Hosted", "Application,Infra")]
    [InlineData("Storage.Infra", "Domain")]
    [InlineData("Business.Domain", "Contract")]
    [InlineData("Business.Application", "")]
    [InlineData("Public.Contract", "")]
    [InlineData("Common.SharedKernel", "")]
    [InlineData("Technical.Utils", "")]
    public void Required_Layers_Should_Be_Directional_And_Progressive(string project, string missing)
    {
        var fixture = Create(project);
        var diagnostics = Analyze(fixture);
        diagnostics.Select(item => item.GetMessage()).Should().BeEquivalentTo(missing.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(role => $"Project '{project}' requires a direct project reference to its '{role}' layer."));
        if (missing.Length == 0)
        {
            diagnostics.Should().BeEmpty();
            return;
        }
        diagnostics.Should().OnlyContain(item => item.Id == "DRNCV021" && item.Severity == DiagnosticSeverity.Warning &&
            item.Location.Kind == LocationKind.ExternalFile && item.Location.GetLineSpan().Path == ConventionInputs.FileName);
    }

    [Theory]
    [InlineData("Application", "Infra")]
    [InlineData("Infra", "Application")]
    public void Hosted_Should_Report_Only_The_Missing_Required_Role(string present, string missing)
    {
        var diagnostic = Analyze(Create("Presentation.Hosted", dependencies: [new("Other." + present)]))
            .Should().ContainSingle().Which;
        diagnostic.GetMessage().Should().Be($"Project 'Presentation.Hosted' requires a direct project reference to its '{missing}' layer.");
        diagnostic.Properties["Target"].Should().Be("Presentation.Hosted:" + missing);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Additional_Valid_Edges_And_Their_Order_Should_Not_Hide_A_Forbidden_Edge(bool reverse)
    {
        Dependency[] dependencies = [new("Sales.Domain"), new("Billing.Domain"), new("Public.Contract"), new("Web.Hosted")];
        if (reverse) Array.Reverse(dependencies);
        var fixture = Create("Storage.Infra", dependencies: dependencies);
        var facts = Facts(fixture);
        ConventionBoundaryAnalysis.Analyze(facts);
        var diagnostic = facts.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNCV015");
        diagnostic.Properties["Target"].Should().Be(ProjectPath("Web.Hosted"));
    }

    [Theory]
    [InlineData("Infra", true)]
    [InlineData("Application", true)]
    [InlineData("Hosted", true)]
    [InlineData("Domain", false)]
    public void Matching_Subdomains_Should_Respect_Assembly_Ownership_And_Referenced_Domain_Layers(string role, bool shared)
    {
        var fixture = Create("Consumer." + role, "namespace Consumer." + role + ".QA; public class Consumer;",
            complete: false, dependencies: [new("Independent.Domain")]);
        var facts = Facts(fixture);
        var consumer = fixture.Compilation.GetTypeByMetadataName("Consumer." + role + ".QA.Consumer")!;
        var dependency = fixture.Compilation.GetTypeByMetadataName("Independent.Domain.QA.DependencyMarker")!;
        facts.Owner(consumer)!.AssemblyName.Should().Be("Consumer." + role);
        facts.Owner(dependency)!.AssemblyName.Should().Be("Independent.Domain");
        facts.UsesOwnSubdomain(consumer, consumer).Should().BeTrue();
        facts.UsesOwnSubdomain(consumer, dependency).Should().Be(shared);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    public void Hosted_Should_Accept_One_Or_Many_Direct_Application_And_Infra_Projects(int count, int expected)
    {
        var dependencies = Enumerable.Range(1, count).SelectMany(index => new[]
        {
            new Dependency("Business.Application.Service" + index),
            new Dependency("Storage.Infrastructure.Provider" + index)
        }).ToArray();
        Analyze(Create("Presentation.Hosted", dependencies: dependencies)).Should().HaveCount(expected);
    }

    [Theory]
    [InlineData("Storage.Infra", "Business.Domain", 0, 1)]
    [InlineData("Storage.Infra", "Business.Domain", 1, 0)]
    [InlineData("Storage.Infra", "Business.Domain", 2, 0)]
    [InlineData("Business.Domain", "Public.Contracts", 0, 1)]
    [InlineData("Business.Domain", "Public.Contracts", 1, 0)]
    [InlineData("Business.Domain", "Public.Contracts", 2, 0)]
    public void Infra_And_Domain_Should_Accept_One_Or_Many_Direct_Layers_Across_Name_Prefixes(
        string project, string dependency, int count, int expected)
    {
        var dependencies = Enumerable.Range(1, count).Select(index => new Dependency(dependency + ".Partition" + index)).ToArray();
        Analyze(Create(project, dependencies: dependencies)).Should().HaveCount(expected);
    }

    [Fact]
    public void Two_Hosts_Can_Use_The_Same_Infra_Without_Requiring_A_Reverse_Reference()
    {
        var dependencies = new[] { new Dependency("Business.Application"), new Dependency("Shared.Infra") };
        var web = Create("Web.Hosted", dependencies: dependencies);
        var worker = web with { Name = "Worker.Hosted", Compilation = web.Compilation.WithAssemblyName("Worker.Hosted") };
        Analyze(web).Should().BeEmpty();
        Analyze(worker).Should().BeEmpty();
        Analyze(Create("Shared.Infra", dependencies: [new("Business.Domain")])).Should().BeEmpty();
    }

    [Theory]
    [InlineData("Package")]
    [InlineData("Transitive")]
    public void Package_And_Transitive_References_Should_Not_Satisfy_A_Direct_Project_Requirement(string kind)
    {
        var diagnostics = Analyze(Create("Business.Domain", dependencies: [new("Public.Contract", Kind: kind)]));
        diagnostics.Should().ContainSingle().Which.GetMessage().Should().Be(
            "Project 'Business.Domain' requires a direct project reference to its 'Contract' layer.");
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void Missing_Inventory_Or_Unresolved_Project_Edges_Should_Defer_Absence_Claims(bool complete, bool resolved)
    {
        Analyze(Create("Presentation.Hosted", complete: complete,
            dependencies: [new("Business.Application", Resolved: resolved)])).Should().BeEmpty();
    }

    [Fact]
    public void Invalid_Reference_Inventory_Should_Defer_Absence_Claims()
    {
        var fixture = Create("Presentation.Hosted");
        var facts = Facts(fixture);
        facts.Diagnostics.Add(ConventionProject.InputIssue("references", "incomplete", "valid inventory"));
        ConventionDependencyAnalysis.Analyze(facts);
        facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCV001");
    }

    [Fact]
    public void Domain_Without_Populated_Subdomains_Should_Report_The_Project_Requirement()
    {
        var diagnostic = Analyze(Create("Storage.Infra", dependencies: [new("Business.Domain", Subdomain: "")]))
            .Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNCV022");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().Should().Be("Infrastructure 'Storage.Infra' requires populated Domain subdomain '<any>'.");
        diagnostic.Location.Kind.Should().Be(LocationKind.ExternalFile);
        diagnostic.Location.GetLineSpan().Path.Should().Be(ConventionInputs.FileName);
    }

    [Fact]
    public void Multiple_Domain_Projects_Should_Contribute_A_Union_Of_Subdomains()
    {
        const string source = """
            namespace Storage.Infra.Sales { public class SalesContext : Microsoft.EntityFrameworkCore.DbContext; }
            namespace Storage.Infra.Billing { public class BillingContext : Microsoft.EntityFrameworkCore.DbContext; }
            """;
        Analyze(Create("Storage.Infra", source, dependencies:
            [new("Commerce.Domain", Subdomain: "Sales"), new("Finance.Domain", Subdomain: "Billing")])).Should().BeEmpty();
        var missing = Analyze(Create("Storage.Infra", source, dependencies: [new("Commerce.Domain", Subdomain: "Sales")]))
            .Should().ContainSingle().Which;
        missing.Id.Should().Be("DRNCV022");
        missing.GetMessage().Should().Be("Infrastructure 'BillingContext' requires populated Domain subdomain 'Billing'.");
        missing.Severity.Should().Be(DiagnosticSeverity.Warning);
        missing.Location.Kind.Should().Be(LocationKind.SourceFile);
        missing.Location.SourceTree!.GetText().ToString(missing.Location.SourceSpan).Should().Be("BillingContext");
        missing.Properties["Target"].Should().Be("T:Storage.Infra.Billing.BillingContext");
    }

    [Theory]
    [InlineData("Domain", "Contract,Utils,SharedKernel")]
    [InlineData("Contract", "SharedKernel")]
    [InlineData("Infra", "Domain,Contract,Utils,SharedKernel")]
    [InlineData("Application", "Domain,Contract,Infra,Utils,SharedKernel")]
    [InlineData("Hosted", "Domain,Contract,Infra,Application,Utils,SharedKernel")]
    [InlineData("Utils", "SharedKernel")]
    [InlineData("SharedKernel", "")]
    public void Every_Direct_Edge_Should_Respect_The_Directional_Role_Matrix(string from, string allowed)
    {
        string[] roles = ["Domain", "Contract", "Infra", "Application", "Hosted", "Utils", "SharedKernel"];
        var fixture = Create("Consumer." + from, complete: false,
            dependencies: roles.Select(role => new Dependency("Different." + role)).ToArray());
        var facts = Facts(fixture);
        ConventionBoundaryAnalysis.Analyze(facts);
        var permitted = allowed.Split(',', StringSplitOptions.RemoveEmptyEntries);
        facts.Diagnostics.Where(item => item.Id == "DRNCV015").Select(item => item.Properties["Target"])
            .Should().BeEquivalentTo(roles.Except(permitted).Select(role => ProjectPath("Different." + role)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Production_Analyzer_Should_Expose_Required_Edges_And_Respect_Standard_Severity(bool suppressed)
    {
        var fixture = Create("Public.Hosted");
        fixture = fixture with
        {
            Compilation = fixture.Compilation
                .AddReferences(MetadataReference.CreateFromFile(typeof(DRN.Framework.Utils.DependencyInjection.Attributes.LifetimeAttribute).Assembly.Location))
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText("""
                    using Microsoft.Extensions.DependencyInjection;
                    using DRN.Framework.Utils.DependencyInjection;
                    namespace Public.Hosted;
                    public static class HostedModule
                    {
                        public static IServiceCollection AddHostedServices(this IServiceCollection services)
                        {
                            services.AddServicesWithAttributes();
                            return services;
                        }
                    }
                    """, path: "HostedModule.cs"))
        };
        fixture.Compilation.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        var compilation = suppressed ? ConventionTestDependencies.SuppressConventions(fixture.Compilation) : fixture.Compilation;
        var input = new ProjectDiagnosticRulesTests.RuleFile(ConventionInputs.FileName,
            "[assembly: DRN.Conventions.Version(\"1\")]\n" + ConventionTestDependencies.Serialize(fixture.Records));
        var diagnostics = await compilation.WithAnalyzers([new SourceKnownConventionAnalyzer()],
            new AnalyzerOptions([input], new Options(fixture.Name))).GetAnalyzerDiagnosticsAsync();
        diagnostics.Should().NotContain(item => item.Id == "AD0001");
        if (suppressed)
        {
            diagnostics.Should().BeEmpty();
            return;
        }
        diagnostics.Should().HaveCount(2);
        diagnostics.Should().OnlyContain(item => item.Id == "DRNCV021");
        if (!suppressed)
            diagnostics.Select(item => item.GetMessage()).Should().BeEquivalentTo(
                "Project 'Public.Hosted' requires a direct project reference to its 'Application' layer.",
                "Project 'Public.Hosted' requires a direct project reference to its 'Infra' layer.");
    }

    private static List<Diagnostic> Analyze(Fixture fixture)
    {
        var facts = Facts(fixture);
        ConventionDependencyAnalysis.Analyze(facts);
        return facts.Diagnostics;
    }

    private static ConventionFacts Facts(Fixture fixture)
    {
        var options = new Options(fixture.Name);
        return new(fixture.Compilation, ConventionProject.Read(fixture.Compilation, options.GlobalOptions),
            new(fixture.Records, []), options, CancellationToken.None);
    }

    private static Fixture Create(string name, string source = "public class Consumer;", bool complete = true,
        params Dependency[] dependencies)
    {
        var compilation = Compilation(source, "Consumer.cs").WithAssemblyName(name)
            .AddReferences(MetadataReference.CreateFromFile(typeof(DRN.Framework.EntityFramework.Context.IDrnContext).Assembly.Location));
        var records = ImmutableArray.CreateBuilder<ConventionInput>();
        var location = Location.Create(ConventionInputs.FileName, new(0, 1), new(new(0, 0), new(0, 1)));
        if (complete) records.Add(new("Reference", "Inventory", ImmutableDictionary<string, string>.Empty.Add("Stage", "Complete"), location));
        foreach (var dependency in dependencies)
        {
            var ns = dependency.Name + (dependency.Subdomain.Length > 0 ? "." + dependency.Subdomain : "");
            var producer = Compilation($$"""
                [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "{{dependency.Name}}")]
                [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "{{dependency.Name}}")]
                namespace {{ns}};
                public class DependencyMarker;
                """).WithAssemblyName(dependency.Name);
            using var stream = new MemoryStream();
            producer.Emit(stream).Success.Should().BeTrue();
            var assemblyPath = "/dependency-tests/" + dependency.Name + ".dll";
            compilation = compilation.AddReferences(MetadataReference.CreateFromImage(stream.ToArray(), filePath: assemblyPath));
            if (dependency.Kind == "Project")
                records.Add(new("Reference", ProjectPath(dependency.Name), ImmutableDictionary<string, string>.Empty
                    .Add("Stage", "Project").Add("Source", ProjectPath(dependency.Name)), location));
            else if (dependency.Kind == "Package")
                records.Add(new("Reference", dependency.Name, ImmutableDictionary<string, string>.Empty.Add("Stage", "Package"), location));
            if (!dependency.Resolved) continue;
            var fields = ImmutableDictionary<string, string>.Empty.Add("Stage", "Resolved");
            if (dependency.Kind == "Project") fields = fields.Add("Project", ProjectPath(dependency.Name));
            if (dependency.Kind == "Package") fields = fields.Add("Package", dependency.Name);
            records.Add(new("Reference", assemblyPath, fields, location));
        }
        compilation.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        return new(name, compilation, records.ToImmutable());
    }

    private static string ProjectPath(string name) => "/dependency-tests/" + name + ".csproj";
    private sealed record Dependency(string Name, string Subdomain = "QA", string Kind = "Project", bool Resolved = true);
    private sealed record Fixture(string Name, CSharpCompilation Compilation, ImmutableArray<ConventionInput> Records);

    private sealed class Options(string name) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new ConventionInputTests.Config(new()
        {
            ["build_property.MSBuildProjectName"] = name,
            ["build_property.RootNamespace"] = name
        });
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
    }
}
