using DRN.Framework.SharedKernel.Analyzers.Conventions;
using static DRN.Test.Analyzer.Tests.DiagnosticClassificationAnalyzerTests;

namespace DRN.Test.Analyzer.Tests;

public sealed class ConventionDiagnosticContractTests
{
    [Fact]
    public async Task Missing_Module_Should_Report_A_Project_Diagnostic_In_Production()
    {
        var scenario = new Scenario("Application", "namespace Sample.Application; public class Helper;",
            [], "", "", "", _ => { }, IncludeModule: false);
        var diagnostics = await AnalyzeProduction(scenario);
        var diagnostic = diagnostics.Where(item => item.Id == "DRNCV032").Should().ContainSingle().Which;
        diagnostic.Location.Should().Be(Location.None);
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().Should().Be("Project 'Sample.Application' needs a public static *Module class with a public static method accepting and returning IServiceCollection. Call services.AddServicesWithAttributes() on that parameter to scan this project's assembly.");
        (await AnalyzeProduction(scenario with { ConventionsEnabled = false })).Should().BeEmpty();
    }

    [Theory]
    [InlineData("DRNCV002", "", true)]
    [InlineData("DRNCV002", "Reviewed legacy entity ownership boundary.", false)]
    [InlineData("DRNCV999", "Reviewed legacy entity ownership boundary.", true)]
    public async Task Exception_Audit_Should_Report_Real_Invalid_Suppressions_And_Cannot_Suppress_Itself(
        string rule, string reason, bool invalid)
    {
        var scenario = new Scenario("Domain", $$"""
            namespace Sample.Domain.QA;
            [System.Diagnostics.CodeAnalysis.SuppressMessage("DRN.Conventions.Ownership", "{{rule}}", Justification = "{{reason}}")]
            public class OtherTarget;
            """, [], "T:Sample.Domain.QA.OtherTarget", "", "", _ => { });
        var selfException = Input("Exception", "DRNCV018", ("Target", scenario.Target),
            ("Reason", "Reviewed exact target cannot waive the exception audit itself."),
            ("Owner", "Application maintainers"), ("Review", "9999-12-31"));
        var diagnostics = await AnalyzeProduction(scenario, selfException);
        (await AnalyzeProduction(scenario with { ConventionsEnabled = false }, selfException)).Should().BeEmpty();
        diagnostics.Should().HaveCount(invalid ? 1 : 0);
        if (!invalid) return;
        var diagnostic = diagnostics.Single();
        diagnostic.Id.Should().Be("DRNCV018");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().Should().Be("Exception for '" + rule +
            "' is unknown or missing its justification; provide an exact target, meaningful reason and required ownership metadata.");
        diagnostic.Location.SourceTree!.FilePath.Should().Be("ContractFixture.cs");
        diagnostic.Location.SourceTree.GetText().ToString(diagnostic.Location.SourceSpan).Should()
            .Be("System.Diagnostics.CodeAnalysis.SuppressMessage(\"DRN.Conventions.Ownership\", \"" + rule + "\", Justification = \"" + reason + "\")");
    }

    [Theory]
    [InlineData("Domain")]
    [InlineData("Infra")]
    [InlineData("Application")]
    [InlineData("Contract")]
    public async Task Semantic_Lookalikes_Should_Not_Acquire_Framework_Diagnostic_Obligations(string role)
    {
        var scenario = new Scenario(role, $$"""
            namespace Sample.{{role}}.QA;
            public class SourceKnownEntity;
            public class AggregateRoot : SourceKnownEntity;
            public interface IDrnContext;
            public class DbContext { protected virtual void OnModelCreating(object model) { } }
            public class Store : DbContext, IDrnContext { protected override void OnModelCreating(object model) { } }
            public interface IEntityTypeConfiguration<T>;
            public class Configuration : IEntityTypeConfiguration<AggregateRoot>;
            public interface ISourceKnownRepository<T>;
            public class SourceKnownRepository<TContext,TEntity> : ISourceKnownRepository<TEntity>;
            public interface ISourceKnownService<T>;
            public class Service : ISourceKnownService<AggregateRoot>;
            public class ModelSnapshot;
            public class Snapshot : ModelSnapshot;
            public class Response { public SourceKnownEntity Entity { get; init; } }
            public static class ProjectModule
            {
                public static Microsoft.Extensions.DependencyInjection.IServiceCollection Register(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    DRN.Framework.Utils.DependencyInjection.ServiceCollectionExtensions.AddServicesWithAttributes(services);
                    return services;
                }
            }
            """, [], "", "", "", facts =>
            {
                ConventionOwnershipAnalysis.Analyze(facts);
                ConventionContextAnalysis.Analyze(facts);
                ConventionPersistenceAnalysis.Analyze(facts);
                ConventionRegistrationAnalysis.Analyze(facts);
                ConventionBoundaryAnalysis.Analyze(facts);
            });
        Analyze(scenario).Diagnostics.Should().BeEmpty();
        (await AnalyzeProduction(scenario)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("DRNCV001")]
    [InlineData("DRNCV002")]
    [InlineData("DRNCV003")]
    [InlineData("DRNCV004")]
    [InlineData("DRNCV005")]
    [InlineData("DRNCV006")]
    [InlineData("DRNCV007")]
    [InlineData("DRNCV008")]
    [InlineData("DRNCV009")]
    [InlineData("DRNCV010")]
    [InlineData("DRNCV011")]
    [InlineData("DRNCV012")]
    [InlineData("DRNCV013")]
    [InlineData("DRNCV014")]
    [InlineData("DRNCV015")]
    [InlineData("DRNCV016")]
    [InlineData("DRNCV017")]
    [InlineData("DRNCV019")]
    [InlineData("DRNCV020")]
    [InlineData("DRNCV030")]
    [InlineData("DRNCV031")]
    public async Task Real_Violation_Should_Have_Exact_Public_Diagnostic_Contract(string id)
    {
        var scenario = ScenarioFor(id);
        var facts = Analyze(scenario);
        var diagnostic = facts.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be(id);
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().Should().Be(scenario.Message);
        if (scenario.Location == "input")
        {
            diagnostic.Location.Kind.Should().Be(LocationKind.ExternalFile);
            diagnostic.Location.GetLineSpan().Path.Should().Be("DrnConventionInputs.g.cs");
            diagnostic.Location.SourceSpan.Should().Be(new Microsoft.CodeAnalysis.Text.TextSpan(4, 3));
        }
        else
        {
            diagnostic.Location.SourceTree!.FilePath.Should().Be("ContractFixture.cs");
            diagnostic.Location.SourceTree.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be(scenario.Location);
        }

        var reported = (await AnalyzeProduction(scenario)).Where(item => item.Id == id).Should().ContainSingle().Which;
        (await AnalyzeProduction(scenario with { ConventionsEnabled = false })).Should().BeEmpty();
        reported.GetMessage().Should().Be(scenario.Message);
        reported.Severity.Should().Be(DiagnosticSeverity.Warning);
        reported.Location.Kind.Should().Be(diagnostic.Location.Kind);
        if (scenario.Location == "input")
            reported.Location.GetLineSpan().Path.Should().Be(ConventionInputs.FileName);
        else
            reported.Location.SourceTree!.GetText().ToString(reported.Location.SourceSpan).Should().Be(scenario.Location);
    }

    [Theory]
    [InlineData("DRNCV001")]
    [InlineData("DRNCV002")]
    [InlineData("DRNCV003")]
    [InlineData("DRNCV004")]
    [InlineData("DRNCV005")]
    [InlineData("DRNCV006")]
    [InlineData("DRNCV007")]
    [InlineData("DRNCV008")]
    [InlineData("DRNCV009")]
    [InlineData("DRNCV010")]
    [InlineData("DRNCV011")]
    [InlineData("DRNCV012")]
    [InlineData("DRNCV013")]
    [InlineData("DRNCV014")]
    [InlineData("DRNCV015")]
    [InlineData("DRNCV016")]
    [InlineData("DRNCV017")]
    [InlineData("DRNCV019")]
    [InlineData("DRNCV020")]
    [InlineData("DRNCV030")]
    [InlineData("DRNCV031")]
    public async Task Reviewed_Exception_Should_Cover_Only_The_Exact_Real_Violation_And_Expire_On_Review_Day(string id)
    {
        var scenario = ScenarioFor(id);
        var exception = Input("Exception", id, ("Target", scenario.Target), ("Reason", "Reviewed compatibility boundary pending the scheduled model and integration verification."),
            ("Owner", "Application maintainers"), ("Review", "2026-12-01"));
        var accepted = Analyze(scenario, exception);
        var audit = new ConventionExceptions(accepted, new DateOnly(2026, 11, 15));
        accepted.Diagnostics.Should().NotContain(diagnostic => diagnostic.Id == "DRNCV018");
        audit.Applies(accepted.Diagnostics.Single(diagnostic => diagnostic.Id == id)).Should().BeTrue();

        var unrelated = exception with { Fields = exception.Fields.SetItem("Target", "T:Sample." + scenario.Role + ".QA.OtherTarget") };
        var otherFacts = Analyze(scenario, unrelated);
        var otherAudit = new ConventionExceptions(otherFacts, new DateOnly(2026, 11, 15));
        otherFacts.Diagnostics.Should().NotContain(diagnostic => diagnostic.Id == "DRNCV018");
        otherAudit.Applies(otherFacts.Diagnostics.Single(diagnostic => diagnostic.Id == id)).Should().BeFalse();

        var dueFacts = Analyze(scenario, exception);
        var dueAudit = new ConventionExceptions(dueFacts, new DateOnly(2026, 12, 1));
        var expired = dueFacts.Diagnostics.Where(diagnostic => diagnostic.Id == "DRNCV018").Should().ContainSingle().Which;
        expired.Severity.Should().Be(DiagnosticSeverity.Warning);
        expired.GetMessage().Should().Be("Exception for '" + id + "' is malformed, unresolved, review-due or changed since its baseline; provide an exact target, meaningful reason and required ownership metadata.");
        expired.Location.Should().Be(exception.Location);
        dueAudit.Applies(expired).Should().BeFalse();
        dueAudit.Applies(dueFacts.Diagnostics.Single(diagnostic => diagnostic.Id == id)).Should().BeFalse();

        // The fixed-date audit above owns the expiry boundary. Production uses the current UTC date.
        var currentException = exception with { Fields = exception.Fields.SetItem("Review", "9999-12-31") };
        var suppressed = await AnalyzeProduction(scenario, currentException);
        suppressed.Should().NotContain(diagnostic => diagnostic.Id == id || diagnostic.Id == "DRNCV018");
        var otherTarget = currentException with { Fields = currentException.Fields.SetItem("Target", unrelated.Get("Target")) };
        var unsuppressed = await AnalyzeProduction(scenario, otherTarget);
        unsuppressed.Where(diagnostic => diagnostic.Id == id).Should().ContainSingle();
        unsuppressed.Should().NotContain(diagnostic => diagnostic.Id == "DRNCV018");
    }

    [Theory]
    [InlineData("DRNCV008")]
    [InlineData("DRNCV009")]
    [InlineData("DRNCV011")]
    public void Entity_Qualified_Exception_Should_Not_Cover_A_Different_Persistence_Pair(string id)
    {
        var scenario = ScenarioFor(id);
        var exact = Input("Exception", id, ("Target", scenario.Target), ("Entity", "Sample.Infra.QA.Category"),
            ("Context", "Sample.Infra.QA.QAContext"), ("Reason", "Reviewed single entity/context compatibility relationship."),
            ("Owner", "Persistence maintainers"), ("Review", "2026-12-01"));
        var facts = Analyze(scenario, exact);
        new ConventionExceptions(facts, new DateOnly(2026, 11, 15)).Applies(facts.Diagnostics.Single()).Should().BeTrue();
        var mismatched = Analyze(scenario, exact with { Fields = exact.Fields.SetItem("Entity", "Sample.Infra.QA.OtherTarget") });
        new ConventionExceptions(mismatched, new DateOnly(2026, 11, 15)).Applies(mismatched.Diagnostics.Single()).Should().BeFalse();
    }

    [Fact]
    public void Convention_Diagnostics_Should_Link_To_Their_Dedicated_Public_Reference()
    {
        new SourceKnownConventionAnalyzer().SupportedDiagnostics.Should().OnlyContain(descriptor =>
            descriptor.HelpLinkUri == "https://github.com/duranserkan/DRN-Project/blob/master/DRN.Framework.SharedKernel/CONVENTION-DIAGNOSTICS.md");
    }

    [Fact]
    public async Task Specialized_Context_Should_Allow_An_Exact_Reasoned_Symbol_Suppression()
    {
        var scenario = ScenarioFor("DRNCV020");
        scenario = scenario with
        {
            Source = scenario.Source.Replace("public class QAContext", """
                [System.Diagnostics.CodeAnalysis.SuppressMessage("DRN.Conventions.Context", "DRNCV020",
                    Justification = "Vendor integration owns this isolated database model and does not use DRN persistence.")]
                public class QAContext
                """, StringComparison.Ordinal)
        };
        (await AnalyzeProduction(scenario)).Should().NotContain(item => item.Id == "DRNCV020" || item.Id == "DRNCV018");
    }

    private const string Context = """
        public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>
        {
            public QAContext() : base(null) { }
            public QAContext(Microsoft.EntityFrameworkCore.DbContextOptions<QAContext> options) : base(options) { }
            BODY
        }
        """;

    private static Scenario ScenarioFor(string id)
    {
        var infra = "namespace Sample.Infra.QA; public class OtherTarget; public class Category : DRN.Framework.SharedKernel.Domain.AggregateRoot;\n";
        var application = """
            using DRN.Framework.SharedKernel.Domain;
            using DRN.Framework.SharedKernel.Domain.Repository;
            using DRN.Framework.Utils.DependencyInjection;
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application.QA;
            public class OtherTarget;
            public class Category : AggregateRoot;

            """;
        return id switch
        {
            "DRNCV001" => new("Infra", infra + Context.Replace("BODY", "", StringComparison.Ordinal).Replace("QAContext", "QAReadContext", StringComparison.Ordinal),
                [Input("Context", "Sample.Infra.QA.QAReadContext", ("Repositories", "QA/Repositories"), ("Migrations", "QA/Migrations"))],
                "T:Sample.Infra.QA.QAReadContext", "QAReadContext",
                "Convention input 'QAReadContext:Subdomain' is inconsistent with subdomain 'QA'. Use subdomain 'QA' with context 'QAContext', or subdomain 'QARead' with context 'QAReadContext'. Align the namespace and folder. Only for an intentional alias, set Subdomain=\"QA\" and Reason on its DrnConventionContext item.", ConventionContextAnalysis.Analyze),
            "DRNCV002" => new("Domain", """
                namespace Sample.Domain { public class Category : DRN.Framework.SharedKernel.Domain.AggregateRoot; }
                namespace Sample.Domain.QA { public class OtherTarget; }
                """, [], "T:Sample.Domain.Category", "Category",
                "'Category' needs an unambiguous subdomain; use the conventional namespace or declare its owner.", ConventionOwnershipAnalysis.Analyze),
            "DRNCV003" => new("Domain", "namespace Sample.Domain.QA; public class OtherTarget; public class Category : DRN.Framework.SharedKernel.Domain.AggregateRoot;",
                [Input("Path", "ContractFixture.cs", ("LogicalPath", "Other/Category.cs"))], "T:Sample.Domain.QA.Category", "Category",
                "'Category' belongs in 'QA' with namespace 'Sample.Domain.QA'; current logical placement conflicts.", ConventionOwnershipAnalysis.Analyze),
            "DRNCV004" => new("Infra", infra + Context.Replace("BODY", "", StringComparison.Ordinal).Replace("QAContext", "Store", StringComparison.Ordinal),
                [Input("Context", "Sample.Infra.QA.Store", ("Repositories", "QA/Repositories"), ("Migrations", "QA/Migrations"))],
                "T:Sample.Infra.QA.Store", "Store", "Context 'Store' must have a nonempty name ending in 'Context'.", ConventionContextAnalysis.Analyze),
            "DRNCV005" => new("Infra", infra + "public class QAContext() : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>(null);",
                [Input("Context", "Sample.Infra.QA.QAContext", ("Repositories", "QA/Repositories"), ("Migrations", "QA/Migrations"))],
                "T:Sample.Infra.QA.QAContext", "QAContext", "Context 'QAContext' needs the options constructor required by its declared registration/design-time path.", ConventionContextAnalysis.Analyze),
            "DRNCV006" => new("Infra", infra + Context.Replace("BODY", "", StringComparison.Ordinal).Replace(": base(options)", ": base(null)", StringComparison.Ordinal),
                [Input("Context", "Sample.Infra.QA.QAContext", ("Repositories", "QA/Repositories"), ("Migrations", "QA/Migrations"))],
                "T:Sample.Infra.QA.QAContext", "QAContext", "Context 'QAContext' has no established Npgsql setup on its declared DRN configuration path; provide or declare the supported setup.", ConventionContextAnalysis.Analyze),
            "DRNCV007" => new("Infra", infra + Context.Replace("BODY", "protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder model) { }", StringComparison.Ordinal),
                [Input("Context", "Sample.Infra.QA.QAContext", ("Repositories", "QA/Repositories"), ("Migrations", "QA/Migrations"))],
                "M:Sample.Infra.QA.QAContext.OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder)", "OnModelCreating",
                "Override 'OnModelCreating' has a proven path that omits the required base model configuration.", ConventionContextAnalysis.Analyze),
            "DRNCV008" => new("Infra", infra + """
                [DRN.Framework.EntityFramework.Attributes.SourceKnownPersistence(typeof(Category))]
                [DRN.Framework.EntityFramework.Attributes.SourceKnownPersistence(typeof(Category), Inclusion = DRN.Framework.EntityFramework.Attributes.SourceKnownPersistenceInclusion.Exclude)]
                """ + Context.Replace("BODY", "", StringComparison.Ordinal), [], "T:Sample.Infra.QA.QAContext", "QAContext",
                "Assignment of 'Category' to context 'QAContext' conflicts with 'contradictory explicit membership'; resolve or declare the intended relationship.", ConventionPersistenceAnalysis.Analyze),
            "DRNCV009" => new("Infra", infra + Context.Replace("BODY", "", StringComparison.Ordinal) + """
                public class CategoryConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Category>
                {
                    public CategoryConfiguration(int dependency) { }
                    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> builder) { }
                }
                """, [Input("Configuration", "Sample.Infra.QA.CategoryConfiguration", ("Context", "Sample.Infra.QA.QAContext"))],
                "T:Sample.Infra.QA.CategoryConfiguration", "CategoryConfiguration",
                "Configuration 'CategoryConfiguration' for 'Category' is not discoverable by context 'QAContext'; use its configuration namespace/assembly or an explicit supported application path.", ConventionPersistenceAnalysis.Analyze),
            "DRNCV010" => new("Infra", infra + Context.Replace("BODY", "public Microsoft.EntityFrameworkCore.DbSet<Category> Categories => Set<Category>();", StringComparison.Ordinal),
                [Input("Context", "Sample.Infra.QA.QAContext", ("Repositories", "Other/Repositories"), ("Migrations", "QA/Migrations"))], "T:Sample.Infra.QA.QAContext", "input",
                "Context 'QAContext' has no valid Repositories destination. Set Repositories=\"QA/Repositories\" on its DrnConventionContext item, or provide a Reason for an alternate destination.", ConventionContextAnalysis.Analyze),
            "DRNCV011" => new("Infra", infra + """
                [DRN.Framework.EntityFramework.Attributes.SourceKnownPersistence(typeof(Category), Inclusion = DRN.Framework.EntityFramework.Attributes.SourceKnownPersistenceInclusion.Exclude)]
                """ + Context.Replace("BODY", "", StringComparison.Ordinal) + """
                public class CategoryRepository(QAContext context, DRN.Framework.Utils.Entity.IEntityUtils utils)
                    : DRN.Framework.EntityFramework.Domain.SourceKnownRepository<QAContext, Category>(context, utils);
                """, [], "T:Sample.Infra.QA.CategoryRepository", "CategoryRepository",
                "Repository 'CategoryRepository' binds aggregate 'Category' inconsistently with contract or context 'QAContext explicitly excludes the entity'.", ConventionPersistenceAnalysis.Analyze),
            "DRNCV012" => new("Application", application + """
                public interface ICategoryService : ISourceKnownService<Category>;
                [DRN.Framework.Utils.DependencyInjection.Attributes.ScopedWithKey<ICategoryService>("read")]
                public class CategoryService : ICategoryService { public ISourceKnownRepository<Category> Repository => null!; }
                public static class Module
                {
                    public static void Register(IServiceCollection services) => services.AddServicesWithAttributes(typeof(Module).Assembly);
                }
                """, [Input("Registration", "Sample.Application.QA.CategoryService", ("Service", "Sample.Application.QA.ICategoryService"))],
                "T:Sample.Application.QA.CategoryService", "input",
                "'CategoryService' has no effective registration for declared service 'Sample.Application.QA.ICategoryService' on the supported composition path.", ConventionRegistrationAnalysis.Analyze),
            "DRNCV013" => new("Application", application + """
                public class Other : AggregateRoot;
                public class CategoryService(ISourceKnownRepository<Other> repository) : ISourceKnownService<Category>
                {
                    public ISourceKnownRepository<Category> Repository => (ISourceKnownRepository<Category>)repository;
                }
                """, [], "T:Sample.Application.QA.CategoryService", "Repository",
                "Service 'CategoryService' has inconsistent aggregate/repository binding 'DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<Sample.Application.QA.Other>'.", ConventionPersistenceAnalysis.Analyze),
            "DRNCV014" => new("Contract", """
                namespace Sample.Contract.QA;
                public class OtherTarget;
                public class Response { public DRN.Framework.SharedKernel.Domain.SourceKnownEntity Entity { get; init; } }
                """, [], "P:Sample.Contract.QA.Response.Entity", "Entity",
                "Public contract member 'Sample.Contract.QA.Response.Entity' exposes persistence/domain type 'DRN.Framework.SharedKernel.Domain.SourceKnownEntity'; expose an approved DTO or value model.", ConventionBoundaryAnalysis.Analyze),
            "DRNCV015" => new("Contract", "namespace Sample.Contract.QA; public class OtherTarget;",
                [Input("Reference", "DRN.Framework.Hosting", ("Stage", "Package"))], "DRN.Framework.Hosting", "input",
                "Direct package reference from 'Sample.Contract' to 'DRN.Framework.Hosting' violates the declared role matrix; remove it or document the exact exception.", ConventionBoundaryAnalysis.Analyze),
            "DRNCV016" => new("Infra", infra, [], "T:Sample.Infra.QA.Category", "Category",
                "Implementation 'Category' (entity) belongs to role 'Domain'.", ConventionBoundaryAnalysis.Analyze),
            "DRNCV017" => new("Infra", """
                namespace Sample.Infra.QA
                {
                    public class OtherTarget;
                    public class QAContext() : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>(null);
                }
                namespace Sample.Infra.QA.Migrations
                {
                    [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(Sample.Infra.QA.QAContext))]
                    public class QASnapshot : Microsoft.EntityFrameworkCore.Infrastructure.ModelSnapshot
                    {
                        protected override void BuildModel(Microsoft.EntityFrameworkCore.ModelBuilder builder) { }
                    }
                }
                """, [Input("Context", "Sample.Infra.QA.QAContext", ("Migrations", "Elsewhere/Migrations"))],
                "T:Sample.Infra.QA.Migrations.QASnapshot", "input",
                "Migration artifact 'QASnapshot' identifies context 'QAContext' inconsistently with declared destination 'Sample.Infra:Elsewhere/Migrations'.", ConventionPersistenceAnalysis.Analyze),
            "DRNCV019" => new("Domain", """
                namespace Sample.Domain.QA { public class OtherTarget; public class Category : DRN.Framework.SharedKernel.Domain.AggregateRoot; }
                namespace Sample.Domain.Billing { public class Reader { public Sample.Domain.QA.Category Entity => null!; } }
                """, [], "P:Sample.Domain.Billing.Reader.Entity", "Sample.Domain.QA.Category",
                "'Sample.Domain.Billing.Reader.Entity' in subdomain 'Billing' references entity 'Sample.Domain.QA.Category' from 'QA'. Use the entity's identifier or a contract from 'QA'.", ConventionBoundaryAnalysis.Analyze),
            "DRNCV020" => new("Application", """
                namespace Sample.Application.QA;
                public class OtherTarget;
                public class QAContext : Microsoft.EntityFrameworkCore.DbContext;
                """, [], "T:Sample.Application.QA.QAContext", "QAContext",
                "Application context 'QAContext' must implement IDrnContext or declare a narrow, justified exception.", ConventionContextAnalysis.Analyze),
            "DRNCV031" => new("Application", "namespace Sample.Application.QA; public interface IWorker; public class Worker : IWorker; public class OtherTarget;",
                [], "T:Sample.Application.QA.Worker", "Worker",
                "'Worker' must register 'IWorker' through a matching LifetimeAttribute. Choose [Scoped<IWorker>], [Transient<IWorker>], or [Singleton<IWorker>] for its intended lifetime.",
                ConventionRegistrationAnalysis.AnalyzeLifetimeAttributes),
            "DRNCV030" => new("Infra", infra + Context.Replace("BODY", "", StringComparison.Ordinal),
                [], "T:Sample.Infra.QA.QAContext", "QAContext",
                "Context 'QAContext' must directly declare a custom attribute derived from NpgsqlDbContextOptionsAttribute. Recommended name: 'QAContextNpgsqlDbContextOptionsAttribute'.",
                ConventionInfrastructureAnalysis.Analyze),
            _ => throw new ArgumentOutOfRangeException(nameof(id))
        };
    }

    private static ConventionFacts Analyze(Scenario scenario, params ConventionInput[] extra)
    {
        var compilation = Compilation(scenario.Source, "ContractFixture.cs").WithAssemblyName("Sample." + scenario.Role)
            .AddReferences(MetadataReference.CreateFromFile(typeof(DRN.Framework.EntityFramework.Context.DrnContext<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(DRN.Framework.Utils.Entity.IEntityUtils).Assembly.Location));
        compilation.GetDiagnostics().Should().NotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var options = new Options();
        var facts = new ConventionFacts(compilation, new(true, "Sample." + scenario.Role, scenario.Role, "Sample." + scenario.Role, compilation.AssemblyName!, []),
            new([.. scenario.Inputs, .. extra], []), options, CancellationToken.None);
        scenario.Analyze(facts);
        return facts;
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeProduction(Scenario scenario, params ConventionInput[] extra)
    {
        var compilation = Compilation(scenario.Source, "ContractFixture.cs").WithAssemblyName("Sample." + scenario.Role)
            .AddReferences(MetadataReference.CreateFromFile(typeof(DRN.Framework.EntityFramework.Context.DrnContext<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(DRN.Framework.Utils.Entity.IEntityUtils).Assembly.Location));
        if (scenario.IncludeModule && scenario.Role is ("Hosted" or "Application" or "Infra" or "Utils"))
            compilation = ConventionTestDependencies.WithModule(compilation);
        compilation.GetDiagnostics().Should().NotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var fixture = ConventionTestDependencies.WithRequiredLayers(compilation, scenario.Role);
        var records = scenario.Inputs.Concat(extra).Concat(fixture.Records).ToList();
        records.Add(Input("Reference", "Inventory", ("Stage", "Complete")));
        if (!records.Any(record => record.Kind == "Path"))
            records.Add(Input("Path", "ContractFixture.cs", ("LogicalPath", "QA/ContractFixture.cs")));
        var declarations = records.Select(record => "[assembly: DRN.Conventions." + record.Kind + "(" +
            SymbolDisplay.FormatLiteral(record.Identity, true) + string.Concat(record.Fields.OrderBy(field => field.Key, StringComparer.Ordinal)
                .Select(field => ", " + (record.Kind == "Exception" && field.Key == "Target" ? "SymbolTarget" : field.Key) + " = " +
                    SymbolDisplay.FormatLiteral(field.Value, true))) + ")]");
        var input = new ProjectDiagnosticRulesTests.RuleFile(ConventionInputs.FileName,
            "[assembly: DRN.Conventions.Version(\"1\")]\n" + string.Join("\n", declarations));
        var subject = scenario.ConventionsEnabled ? fixture.Compilation : ConventionTestDependencies.SuppressConventions(fixture.Compilation);
        var diagnostics = await subject.WithAnalyzers([new SourceKnownConventionAnalyzer()],
            new AnalyzerOptions([input], new Options(scenario.Role))).GetAnalyzerDiagnosticsAsync();
        diagnostics.Should().NotContain(diagnostic => diagnostic.Id == "AD0001");
        if (scenario.IncludeModule) diagnostics.Should().NotContain(diagnostic => diagnostic.Id == "DRNCV032");
        return diagnostics;
    }

    private static ConventionInput Input(string kind, string identity, params (string Key, string Value)[] fields) => new(kind, identity,
        fields.ToImmutableDictionary(field => field.Key, field => field.Value),
        Location.Create("DrnConventionInputs.g.cs", new(4, 3), new(new(0, 4), new(0, 7))));

    private sealed record Scenario(string Role, string Source, ConventionInput[] Inputs, string Target, string Location,
        string Message, Action<ConventionFacts> Analyze, bool ConventionsEnabled = true, bool IncludeModule = true);

    private sealed class Options(string role = "Domain") : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new ConventionInputTests.Config(new()
        {
            ["build_property.MSBuildProjectName"] = "Sample." + role,
            ["build_property.RootNamespace"] = "Sample." + role
        });
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
    }
}
