using DRN.Framework.SharedKernel.Analyzers.Conventions;
using static DRN.Test.Analyzer.Tests.ClassificationTestFixture;

namespace DRN.Test.Analyzer.Tests;

public sealed class ConventionOwnershipTests
{
    [Theory]
    [DataInline("Sample.Domain.QA", "", false)]
    [DataInline("Sample.Domain", "", true)]
    [DataInline("Sample.Domain", "[DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomain(\"QA\", Reason = \"Published aggregate CLR identity is retained.\")]", false)]
    public void Aggregate_Ownership_Should_Accept_Conventions_Or_A_Reasoned_Explicit_Alias(string ns, string attribute, bool missing)
    {
        var compilation = Compilation($$"""
            namespace {{ns}};
            {{attribute}}
            public class Category : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }
            """, "/outside/Category.cs");
        compilation.AssertNoCompilerErrors();
        var input = new ConventionInput("Path", "/outside/Category.cs", ImmutableDictionary<string, string>.Empty
            .Add("LogicalPath", "QA/Category.cs"), Location.None);
        var facts = new ConventionFacts(compilation, Project(compilation), new([input], []), AnalyzerTestCompilation.EmptyOptionsProvider, CancellationToken.None);
        ConventionOwnershipAnalysis.Analyze(facts);
        var ownership = facts.Diagnostics.Where(item => item.Id == "DRNOWNERSHIP001").ToArray();
        ownership.Should().HaveCount(missing ? 1 : 0);
        if (!missing) return;
        ownership[0].Severity.Should().Be(DiagnosticSeverity.Warning);
        ownership[0].GetMessage().Should().Be("'Category' needs an unambiguous subdomain; use the conventional namespace or declare its owner.");
        ownership[0].Location.SourceTree!.GetText().ToString(ownership[0].Location.SourceSpan).Should().Be("Category");
    }

    [Fact]
    public void Nested_Type_Suppression_Should_Be_Audited_Only_Once()
    {
        var facts = Facts("""
            namespace Sample.Domain.QA;
            public class Thing
            {
                [System.Diagnostics.CodeAnalysis.SuppressMessage("DRN.Conventions.Structure", "DRNSTRUCTURE001")]
                public class Nested;
            }
            """, "QA/Thing.cs");
        _ = new ConventionExceptions(facts, new DateOnly(2026, 11, 15));
        var diagnostic = facts.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNCONVENTIONEXCEPTION001");
        diagnostic.GetMessage().Should().Contain("missing its justification");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Contain("SuppressMessage");
    }

    [Fact]
    public void Unrelated_And_Lookalike_Suppression_Attributes_Should_Not_Be_Audited()
    {
        var facts = Facts("""
            [assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("DRN.Classification", "DRNCLASSIFICATION003")]
            namespace Sample.Domain.QA;
            public class SuppressMessageAttribute(string category, string check) : System.Attribute;
            [SuppressMessage("DRN.Conventions.Structure", "DRNSTRUCTURE001")]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1822")]
            [System.Diagnostics.CodeAnalysis.SuppressMessage("DRN.Domain", "DRNENTITY013")]
            public class Thing;
            """, "QA/Thing.cs");
        _ = new ConventionExceptions(facts, new DateOnly(2026, 11, 15));
        facts.Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("disable DRNSTRUCTURE001 // Reviewed compatibility declaration", "not restored")]
    [DataInlineUnit("restore DRNSTRUCTURE001", "without a matching")]
    [DataInlineUnit("disable DRNOWNERSHIP999", "unknown")]
    public void Invalid_Local_Pragmas_Should_Remain_Visible(string pragma, string message)
    {
        var facts = Facts($$"""
            namespace Sample.Domain.QA;
            #pragma warning {{pragma}}
            public class Thing;
            """, "QA/Thing.cs");
        _ = new ConventionExceptions(facts, new DateOnly(2026, 11, 15));
        var diagnostic = facts.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNCONVENTIONEXCEPTION001");
        diagnostic.GetMessage().Should().Contain(message);
    }

    [Theory]
    [DataInline("QA/Thing.cs", false)]
    [DataInline("Other/Thing.cs", true)]
    [DataInline("qa/Thing.cs", true)]
    [DataInline("QA\\Thing.cs", false)]
    public void Placement_Should_Use_Logical_Path_And_Ordinal_Case(string path, bool violation)
    {
        var facts = Facts("namespace Sample.Domain.QA; public class Thing;", path);
        ConventionOwnershipAnalysis.Analyze(facts);
        var diagnostics = facts.Diagnostics.Where(item => item.Id == "DRNSTRUCTURE001").ToArray();
        diagnostics.Should().HaveCount(violation ? 1 : 0);
        if (violation)
        {
            diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
            diagnostics[0].GetMessage().Should().Contain("Sample.Domain.QA");
            diagnostics[0].Location.SourceTree!.GetText().ToString(diagnostics[0].Location.SourceSpan).Should().Be("Thing");
        }
    }

    [Fact]
    public void Unknown_Logical_Path_Should_Not_Be_Compared_As_A_Physical_Path()
    {
        var facts = Facts("namespace Sample.Domain.QA; public class Thing;", null);
        ConventionOwnershipAnalysis.Analyze(facts);
        facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCONVENTIONINPUT001");
    }

    [Fact]
    public void Unclassified_Lookalike_Should_Not_Acquire_Organization_Warnings()
    {
        var compilation = Compilation("namespace Sample.Domain; public class ThingRepository;", "/outside/Anything.cs");
        var facts = new ConventionFacts(compilation, Project(compilation), new([], []), AnalyzerTestCompilation.EmptyOptionsProvider, CancellationToken.None);
        ConventionOwnershipAnalysis.Analyze(facts);
        facts.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Exact_Project_Exception_Should_Not_Waive_Another_Type_Or_Rule()
    {
        var facts = Facts("namespace Sample.Domain.QA; public class Thing;", "Other/Thing.cs",
            new ConventionInput("Exception", "DRNSTRUCTURE001", ImmutableDictionary<string, string>.Empty
                .Add("Target", "T:Sample.Domain.QA.Thing").Add("Reason", "Linked compatibility source remains in its existing logical home.")
                .Add("Owner", "Application maintainers").Add("Review", "2027-01-01"), Location.None));
        var exceptions = new ConventionExceptions(facts, new DateOnly(2026, 11, 15));
        ConventionOwnershipAnalysis.Analyze(facts);
        var placement = facts.Diagnostics.Single(item => item.Id == "DRNSTRUCTURE001");
        exceptions.Applies(placement).Should().BeTrue();
        exceptions.Applies(Diagnostic.Create(ConventionDiagnosticDescriptors.MissingOwnership, placement.Location, "Thing"))
            .Should().BeFalse();
    }

    [Theory]
    [DataInline("", "T:Sample.Domain.QA.Thing")]
    [DataInline("Reviewed", "T:Sample.Domain.QA.*")]
    [DataInline("Reviewed", "T:Sample.Domain.QA.Missing")]
    public void Invalid_Project_Exception_Should_Remain_Visible(string reason, string target)
    {
        var facts = Facts("namespace Sample.Domain.QA; public class Thing;", "QA/Thing.cs",
            new ConventionInput("Exception", "DRNSTRUCTURE001", ImmutableDictionary<string, string>.Empty.Add("Target", target)
                .Add("Reason", reason).Add("Owner", "Maintainers").Add("Review", "2027-01-01"), Location.None));
        _ = new ConventionExceptions(facts, new DateOnly(2026, 11, 15));
        facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCONVENTIONEXCEPTION001");
    }

    [Fact]
    public void Review_Due_And_Changed_Baselines_Should_Not_Waive_Placement()
    {
        const string source = "namespace Sample.Domain.QA; public class Thing;";
        var declaration = Compilation(source).GetTypeByMetadataName("Sample.Domain.QA.Thing")!
            .DeclaringSyntaxReferences.Single().GetSyntax().ToFullString();
        var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes("Other/Thing.cs\n" + declaration))).ToLowerInvariant();
        var input = new ConventionInput("Exception", "DRNSTRUCTURE001", ImmutableDictionary<string, string>.Empty
            .Add("Target", "T:Sample.Domain.QA.Thing").Add("Reason", "Reviewed linked compatibility declaration")
            .Add("Owner", "Maintainers").Add("Review", "2027-01-01").Add("Fingerprint", fingerprint), Location.None);
        var valid = Facts(source, "Other/Thing.cs", input);
        var exceptions = new ConventionExceptions(valid, new DateOnly(2026, 11, 15));
        ConventionOwnershipAnalysis.Analyze(valid);
        exceptions.Applies(valid.Diagnostics.Single()).Should().BeTrue();

        var due = Facts(source, "Other/Thing.cs", input);
        _ = new ConventionExceptions(due, new DateOnly(2027, 1, 1));
        due.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCONVENTIONEXCEPTION001");

        var changed = Facts("namespace Sample.Domain.QA; public class Thing { public int Added; }", "Other/Thing.cs", input);
        _ = new ConventionExceptions(changed, new DateOnly(2026, 11, 15));
        changed.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCONVENTIONEXCEPTION001");

        var moved = Facts(source, "Legacy/Thing.cs", input);
        _ = new ConventionExceptions(moved, new DateOnly(2026, 11, 15));
        moved.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCONVENTIONEXCEPTION001");
    }

    [Fact]
    public void A_Reviewed_Exception_Should_Not_Automatically_Baseline_A_New_Sibling_Declaration()
    {
        var facts = Facts("""
            namespace Sample.Domain.QA;
            public class Thing;
            public class AddedAggregate : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }
            """, "Wrong/Thing.cs",
            new ConventionInput("Exception", "DRNSTRUCTURE001", ImmutableDictionary<string, string>.Empty
                .Add("Target", "T:Sample.Domain.QA.Thing").Add("Reason", "Reviewed linked declaration only.")
                .Add("Owner", "Maintainers").Add("Review", "2027-01-01"), Location.None));
        var exceptions = new ConventionExceptions(facts, new DateOnly(2026, 11, 15));
        ConventionOwnershipAnalysis.Analyze(facts);
        var diagnostics = facts.Diagnostics.Where(item => item.Id == "DRNSTRUCTURE001").ToArray();
        diagnostics.Should().HaveCount(2);
        exceptions.Applies(diagnostics.Single(item => item.GetMessage().StartsWith("'Thing'", StringComparison.Ordinal))).Should().BeTrue();
        exceptions.Applies(diagnostics.Single(item => item.GetMessage().StartsWith("'AddedAggregate'", StringComparison.Ordinal))).Should().BeFalse();
    }

    [Theory]
    [DataInlineUnit("namespaceanddescendants", "~N:Sample.Domain.QA", true)]
    [DataInlineUnit("namespace", "~N:Sample.Domain.QA", true)]
    [DataInlineUnit("member", "~T:Sample.Domain.QA.Thing", true)]
    [DataInlineUnit("type", "~T:Sample.Domain.QA.Thing", false)]
    [DataInlineUnit("Type", "~T:Sample.Domain.QA.Thing", false)]
    [DataInlineUnit("Member", "~P:Sample.Domain.QA.Thing.Value", false)]
    [DataInlineUnit("Member", "~P:Sample.Domain.QA.Thing.Item", true)]
    public void Assembly_Suppression_Should_Require_An_Exact_Type_Or_Member(string scope, string target, bool invalid)
    {
        var facts = Facts($$"""
            [assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("DRN.Conventions.Ownership", "DRNOWNERSHIP002",
                Scope = "{{scope}}", Target = "{{target}}", Justification = "Reviewed exact compatibility dependency")]
            namespace Sample.Domain.QA;
            public class Thing
            {
                public int Value { get; }
                public int Item { get; }
            }
            """, "QA/Thing.cs");
        _ = new ConventionExceptions(facts, new DateOnly(2026, 11, 15));
        facts.Diagnostics.Count(diagnostic => diagnostic.Id == "DRNCONVENTIONEXCEPTION001").Should().Be(invalid ? 1 : 0);
    }

    [Theory]
    [DataInlineUnit("namespace", "~N:Sample.Domain.QA", "Reviewed exact compatibility dependency", 1)]
    [DataInlineUnit("type", "~T:Sample.Domain.QA.Thing", "", 1)]
    [DataInlineUnit("Type", "~T:Sample.Domain.QA.Thing", "Reviewed exact compatibility dependency", 0)]
    [DataInlineUnit("Member", "~P:Sample.Domain.QA.Thing.Value", "Reviewed exact compatibility dependency", 0)]
    public void Module_Suppression_Should_Follow_The_Same_Audit_Rules_As_Assembly_Suppression(
        string scope, string target, string justification, int expectedErrors)
    {
        var facts = Facts($$"""
            [module: System.Diagnostics.CodeAnalysis.SuppressMessage("DRN.Conventions.Ownership", "DRNOWNERSHIP002",
                Scope = "{{scope}}", Target = "{{target}}", Justification = "{{justification}}")]
            namespace Sample.Domain.QA;
            public class Thing
            {
                public int Value { get; }
            }
            """, "QA/Thing.cs");
        _ = new ConventionExceptions(facts, new DateOnly(2026, 11, 15));
        facts.Diagnostics.Count(diagnostic => diagnostic.Id == "DRNCONVENTIONEXCEPTION001").Should().Be(expectedErrors);
    }

    [Theory]
    [DataInline("DRNSTRUCTURE001", "", false)]
    [DataInline("DRNSTRUCTURE001", "public class Other;", true)]
    [DataInline("DRNAUTHORIZATION001", "public class Other;", false)]
    [DataInline("", "", true)]
    public void Pragma_Audit_Should_Reject_Blanket_And_Multiple_Declaration_Scopes(string rule, string other, bool invalid)
    {
        var facts = Facts($$"""
            namespace Sample.Domain.QA;
            // This fixture reviews a declaration whose diagnostic is attached outside an attribute target.
            #pragma warning disable {{rule}}
            public class Thing;
            {{other}}
            #pragma warning restore {{rule}}
            """, "QA/Thing.cs");
        _ = new ConventionExceptions(facts, new DateOnly(2026, 11, 15));
        facts.Diagnostics.Count(diagnostic => diagnostic.Id == "DRNCONVENTIONEXCEPTION001").Should().Be(invalid ? 1 : 0);
    }

    [Theory]
    [DataInline("Sample.Application.Services", "Services/Service.cs", 2)]
    [DataInline("Sample.Application.Services.QA", "Services/QA/Service.cs", 0)]
    public void Repository_Backed_Orchestration_And_Its_Entity_Contract_Should_Have_Ownership(
        string ns, string path, int missingOwners)
    {
        var compilation = Compilation($$"""
            namespace {{ns}}
            {
                public interface ICategoryService
                {
                    System.Threading.Tasks.Task<Sample.Domain.QA.Category?> Read();
                }
                public class CategoryService(Sample.Domain.QA.ICategoryRepository repository) : ICategoryService
                {
                    public System.Threading.Tasks.Task<Sample.Domain.QA.Category?> Read()
                        => System.Threading.Tasks.Task.FromResult<Sample.Domain.QA.Category?>(null);
                }
                public class UnboundService;
            }
            """, "Service.cs").AddSyntaxTrees(CSharpSyntaxTree.ParseText("""
            namespace Sample.Domain.QA;
            public class Category : DRN.Framework.SharedKernel.Domain.AggregateRoot { public override DRN.Framework.SharedKernel.Domain.Dto ToDto() => new(this); }
            public interface ICategoryRepository : DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<Category>;
            """, path: "Category.cs"));
        compilation.AssertNoCompilerErrors();
        var input = new ConventionInput("Path", "Service.cs", ImmutableDictionary<string, string>.Empty
            .Add("LogicalPath", path), Location.None);
        var domain = new ConventionInput("Role", "Domain", ImmutableDictionary<string, string>.Empty
            .Add("NamespacePrefix", "Sample.Domain").Add("LogicalPathPrefix", "Domain"), Location.None);
        var domainPath = new ConventionInput("Path", "Category.cs", ImmutableDictionary<string, string>.Empty
            .Add("LogicalPath", "Domain/QA/Category.cs"), Location.None);
        var facts = new ConventionFacts(compilation,
            new(true, "Sample.Application", "Application", "Sample.Application", compilation.AssemblyName!, []),
            new([input, domain, domainPath], []), AnalyzerTestCompilation.EmptyOptionsProvider, CancellationToken.None);
        ConventionOwnershipAnalysis.Analyze(facts);
        facts.Diagnostics.Count(diagnostic => diagnostic.Id == "DRNOWNERSHIP001").Should().Be(missingOwners,
            "only the service and its contract may lack ownership. Diagnostics: {0}",
            string.Join(Environment.NewLine, facts.Diagnostics));
        facts.Diagnostics.Select(diagnostic => diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan))
            .Should().BeEquivalentTo(missingOwners == 0 ? Array.Empty<string>() : ["ICategoryService", "CategoryService"]);
        facts.Diagnostics.Should().NotContain(diagnostic => diagnostic.GetMessage().Contains("UnboundService"));
    }

    [Fact]
    public void Divergent_Partial_Homes_Should_Report_Once_With_Conflicting_Part_Locations()
    {
        var compilation = Compilation("namespace Sample.Domain.QA; public partial class Thing;", "/outside/first.cs")
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText("namespace Sample.Domain.QA; public partial class Thing;",
                path: "/outside/second.cs"));
        compilation.AssertNoCompilerErrors();
        var records = new ConventionInput[]
        {
            new("Ownership", "Sample.Domain.QA.Thing", ImmutableDictionary<string, string>.Empty
                .Add("Subdomain", "QA").Add("Kind", "Entity"), Location.None),
            new("Path", "/outside/first.cs", ImmutableDictionary<string, string>.Empty.Add("LogicalPath", "QA/Thing.cs"), Location.None),
            new("Path", "/outside/second.cs", ImmutableDictionary<string, string>.Empty.Add("LogicalPath", "Wrong/Thing.More.cs"), Location.None)
        };
        var facts = new ConventionFacts(compilation, Project(compilation), new([.. records], []), AnalyzerTestCompilation.EmptyOptionsProvider, CancellationToken.None);
        ConventionOwnershipAnalysis.Analyze(facts);
        var diagnostic = facts.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNSTRUCTURE001");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().Should().Be("'Thing' belongs in 'QA' with namespace 'Sample.Domain.QA'; current logical placement conflicts.");
        diagnostic.Location.SourceTree!.FilePath.Should().Be("/outside/first.cs");
        diagnostic.AdditionalLocations.Should().ContainSingle().Which.SourceTree!.FilePath.Should().Be("/outside/second.cs");
        diagnostic.AdditionalLocations[0].SourceTree!.GetText().ToString(diagnostic.AdditionalLocations[0].SourceSpan).Should().Be("Thing");
    }

    private static ConventionFacts Facts(string source, string? logicalPath, params ConventionInput[] extra)
    {
        var compilation = Compilation(source, "/outside/linked.cs");
        compilation.AssertNoCompilerErrors();
        var records = new List<ConventionInput>
        {
            new("Ownership", "Sample.Domain.QA.Thing", ImmutableDictionary<string, string>.Empty
                .Add("Subdomain", "QA").Add("Kind", "Entity"), Location.None)
        };
        if (logicalPath is not null)
            records.Add(new("Path", "/outside/linked.cs", ImmutableDictionary<string, string>.Empty
                .Add("LogicalPath", logicalPath), Location.None));
        records.AddRange(extra);
        return new(compilation, Project(compilation), new([.. records], []), AnalyzerTestCompilation.EmptyOptionsProvider, CancellationToken.None);
    }

    private static ConventionProject Project(Compilation compilation) => new(true, "Sample.Domain", "Domain", "Sample.Domain", compilation.AssemblyName!, []);
}
