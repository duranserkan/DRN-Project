using System.Globalization;
using DRN.Framework.Hosting.Generators;
using DRN.Framework.SharedKernel.Analyzers.Conventions;
using static DRN.Framework.SharedKernel.Analyzers.ClassificationDiagnosticDescriptors;
using static DRN.Framework.SharedKernel.Analyzers.Conventions.ConventionDiagnosticDescriptors;
using static DRN.Framework.SharedKernel.Analyzers.DiagnosticDescriptors;
using static DRN.Test.Analyzer.Tests.ClassificationTestFixture;

namespace DRN.Test.Analyzer.Tests;

public sealed class ConventionDiagnosticContractTests
{
    private static MetadataReference FrameworkReference => AnalyzerTestCompilation.EntityFrameworkReference;
    private static MetadataReference UtilsReference => AnalyzerTestCompilation.UtilsReference;
    private static readonly Lazy<SyntaxTree> WideModel = new(() => CSharpSyntaxTree.ParseText(
        "namespace Sample.Contract.QA; public class Wide { " +
        string.Join("\n", Enumerable.Range(0, 4097).Select(index => $"public int Field{index};")) + " }",
        path: "Wide.cs"));
    private static readonly Lazy<(MetadataReference Reference, ConventionInput[] Records)> ItemContract =
        new(() => CreateReferenceFixture("Shared.Contract", """
            namespace Shared.Contract.QA.Items;
            public class ItemDto(DRN.Framework.SharedKernel.Domain.SourceKnownEntity? entity = null)
                : DRN.Framework.SharedKernel.Domain.Dto(entity)
            {
                public string Label { get; init; } = "";
            }
            """));
    private static readonly Lazy<(MetadataReference Reference, ConventionInput[] Records)> EmptyDomain =
        new(() => CreateReferenceFixture("Empty.Domain", """
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Empty.Domain")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "Empty.Domain")]
            namespace UnrelatedRoot { public class Marker; }
            """));
    private static readonly Lazy<(MetadataReference Reference, ConventionInput[] Records)> OrderDomain =
        new(() => CreateReferenceFixture("Order.Domain", """
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Order.Domain")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "Order.Domain")]
            namespace Order.Domain.QA.Order
            {
                public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot;
                public interface IOrderRepository : DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<OrderEntity>;
            }
            """));

    [Fact]
    public void Public_Diagnostic_Ids_Should_Keep_Their_Assigned_Prefix_And_Number()
    {
        var accessor = HostedAccessorGeneratorTests.GenerateAppRootCollision().Diagnostics
            .Select(diagnostic => diagnostic.Descriptor).Distinct().Single();
        var program = new DrnProgramNameAnalyzer().SupportedDiagnostics;
        // Append new rules. Reserve a retired slot with null so later IDs stay stable.
        var allocations = new Dictionary<string, DiagnosticDescriptor?[]>
        {
            ["DRNENTITY"] = [MissingEntityTypeAttribute, DuplicateEntityTypeValue, InvalidEntityTypeAttributeUsage,
                DuplicateEntityName, MultipleAppIdsNotPermitted, UnresolvableAppId, AppIdOutOfRange,
                UnsupportedEntityAttribute, InvalidAppIdArgument, InvalidEntityIdFormatArgument,
                HiddenEntityIdentityMember, GenericConcreteEntity, AbstractEntityMetadataArgument, UninitializedEntityOperations],
            ["DRNCLASSIFICATION"] = [MissingClassification, InvalidExemption, PathClassification, UnknownClassification,
                AnonymousClassification, DictionaryClassification, InvalidRuleFile, UnsafeLoggingCapture,
                InvalidNameExemptionComment, UnusedNameExemptionComment, IncompleteModelAnalysis, IncompleteInterfaceAnalysis],
            ["DRNAUTHORIZATION"] = [SourceKnownControllerPolicyRequired],
            ["DRNDTO"] = [EntityDto, DtoFolder, DtoClassification, DtoMessagePlacement],
            ["DRNHOSTING"] = [accessor, program.Single()],
            ["DRNARCHITECTURE"] = [IncompleteContract, Contract, Reference, ImplementationRole, ForbiddenDependency, RequiredReference],
            ["DRNOWNERSHIP"] = [MissingOwnership, CrossSubdomain, DomainSubdomain],
            ["DRNSTRUCTURE"] = [Placement, ContextName, Destination, RoleSuffix, AggregatePlacement, InfrastructureFolder, ContextOptionsName],
            ["DRNPERSISTENCE"] = [ContextConstructor, Provider, BaseModel, Persistence, Configuration, Migration, ContextContract, DbSetAggregate, ContextOptionsRequired],
            ["DRNREPOSITORY"] = [Repository, Service, AggregateContract, AggregateRepository],
            ["DRNDEPENDENCYINJECTION"] = [Registration, LifetimeRequired, ModuleScan],
            ["DRNCONVENTIONINPUT"] = [InvalidInput],
            ["DRNCONVENTIONEXCEPTION"] = [ConventionDiagnosticDescriptors.Exception]
        };
        var expected = new List<string>();
        foreach (var (prefix, slots) in allocations)
        {
            for (var index = 0; index < slots.Length; index++)
            {
                if (slots[index] is not { } rule) continue;
                var id = prefix + (index + 1).ToString("D3", CultureInfo.InvariantCulture);
                rule.Id.Should().Be(id);
                expected.Add(id);
            }
        }
        var reported = new SourceKnownEntityTypeAnalyzer().SupportedDiagnostics
            .Concat(new DiagnosticClassificationAnalyzer().SupportedDiagnostics)
            .Concat(new SourceKnownControllerAuthorizationAnalyzer().SupportedDiagnostics)
            .Concat(new SourceKnownConventionAnalyzer().SupportedDiagnostics)
            .Concat(program).Append(accessor).Select(rule => rule.Id).ToArray();
        reported.Should().OnlyHaveUniqueItems().And.BeEquivalentTo(expected);
    }

    [Theory]
    [DataInlineUnit(true, true)]
    [DataInlineUnit(true, false)]
    [DataInlineUnit(false, true)]
    [DataInlineUnit(false, false)]
    public async Task Ef_Registration_And_Module_Scan_Should_Report_Independently(bool registerContext, bool includeModule)
    {
        var registrationApi = registerContext ? "Ef" : "Lookalike";
        var scenario = new Scenario("Infra", $$"""
            using Ef = Microsoft.Extensions.DependencyInjection.EntityFrameworkServiceCollectionExtensions;
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Infra.QA;
            public class ImportContext : Microsoft.EntityFrameworkCore.DbContext;
            public static class Setup
            {
                public static void Register(IServiceCollection services) => {{registrationApi}}.AddDbContext<ImportContext>(services);
            }
            public static class Lookalike { public static void AddDbContext<T>(IServiceCollection services) { } }
            """, [], "", "", "", _ => { }, IncludeModule: includeModule);
        var diagnostics = await AnalyzeProduction(scenario);
        var findings = diagnostics.Where(item => item.Id is "DRNPERSISTENCE001" or "DRNDEPENDENCYINJECTION003")
            .Select(item => (item.Id, item.Location == Location.None ? "project" : AnalyzerTestCompilation.LocationText(item)));
        var expected = new List<(string, string)>();
        if (registerContext) expected.Add(("DRNPERSISTENCE001", "ImportContext"));
        if (!includeModule) expected.Add(("DRNDEPENDENCYINJECTION003", "project"));
        findings.Should().BeEquivalentTo(expected);
    }

    [Theory]
    [DataInline("DRNOWNERSHIP001", "", true)]
    [DataInline("DRNOWNERSHIP001", "Reviewed legacy entity ownership boundary.", false)]
    [DataInline("DRNOWNERSHIP999", "Reviewed legacy entity ownership boundary.", true)]
    public async Task Exception_Audit_Should_Report_Real_Invalid_Suppressions_And_Cannot_Suppress_Itself(
        string rule, string reason, bool invalid)
    {
        var scenario = new Scenario("Domain", $$"""
            namespace Sample.Domain.QA;
            [System.Diagnostics.CodeAnalysis.SuppressMessage("DRN.Conventions.Ownership", "{{rule}}", Justification = "{{reason}}")]
            public class OtherTarget;
            """, [], "T:Sample.Domain.QA.OtherTarget", "", "", _ => { });
        var selfException = Input("Exception", "DRNCONVENTIONEXCEPTION001", ("Target", scenario.Target),
            ("Reason", "Reviewed exact target cannot waive the exception audit itself."),
            ("Owner", "Application maintainers"), ("Review", "9999-12-31"));
        var diagnostics = await AnalyzeProduction(scenario, selfException);
        (await AnalyzeProduction(scenario with { ConventionsEnabled = false }, selfException)).Should().BeEmpty();
        diagnostics.Should().HaveCount(invalid ? 1 : 0);
        if (!invalid) return;
        var diagnostic = diagnostics.Single();
        diagnostic.Id.Should().Be("DRNCONVENTIONEXCEPTION001");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().Should().Be("Exception for '" + rule +
            "' is unknown or missing its justification; provide an exact target, meaningful reason and required ownership metadata.");
        diagnostic.Location.SourceTree!.FilePath.Should().Be("ContractFixture.cs");
        (await diagnostic.Location.SourceTree.GetTextAsync()).ToString(diagnostic.Location.SourceSpan).Should()
            .Be("System.Diagnostics.CodeAnalysis.SuppressMessage(\"DRN.Conventions.Ownership\", \"" + rule + "\", Justification = \"" + reason + "\")");
    }

    [Theory]
    [DataInline("Domain")]
    [DataInline("Infra")]
    [DataInline("Application")]
    [DataInline("Contract")]
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
                ConventionPersistenceAnalysis.AnalyzeWithContexts(facts);
                ConventionRegistrationAnalysis.Analyze(facts);
                ConventionBoundaryAnalysis.Analyze(facts);
            });
        Analyze(scenario).Diagnostics.Should().BeEmpty();
        (await AnalyzeProduction(scenario)).Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("DRNCONVENTIONINPUT001")]
    [DataInlineUnit("DRNOWNERSHIP001")]
    [DataInlineUnit("DRNSTRUCTURE001")]
    [DataInlineUnit("DRNSTRUCTURE002")]
    [DataInlineUnit("DRNPERSISTENCE001")]
    [DataInlineUnit("DRNPERSISTENCE002")]
    [DataInlineUnit("DRNPERSISTENCE003")]
    [DataInlineUnit("DRNPERSISTENCE004")]
    [DataInlineUnit("DRNPERSISTENCE005")]
    [DataInlineUnit("DRNSTRUCTURE003")]
    [DataInlineUnit("DRNREPOSITORY001")]
    [DataInlineUnit("DRNDEPENDENCYINJECTION001")]
    [DataInlineUnit("DRNREPOSITORY002")]
    [DataInlineUnit("DRNARCHITECTURE001")]
    [DataInlineUnit("DRNARCHITECTURE002")]
    [DataInlineUnit("DRNARCHITECTURE003")]
    [DataInlineUnit("DRNARCHITECTURE004")]
    [DataInlineUnit("DRNARCHITECTURE005")]
    [DataInlineUnit("DRNPERSISTENCE006")]
    [DataInlineUnit("DRNOWNERSHIP002")]
    [DataInlineUnit("DRNPERSISTENCE007")]
    [DataInlineUnit("DRNDTO001")]
    [DataInlineUnit("DRNDTO002")]
    [DataInlineUnit("DRNDTO003")]
    [DataInlineUnit("DRNDTO004")]
    [DataInlineUnit("DRNARCHITECTURE006")]
    [DataInlineUnit("DRNOWNERSHIP003")]
    [DataInlineUnit("DRNPERSISTENCE008")]
    [DataInlineUnit("DRNSTRUCTURE004")]
    [DataInlineUnit("DRNSTRUCTURE005")]
    [DataInlineUnit("DRNREPOSITORY003")]
    [DataInlineUnit("DRNREPOSITORY004")]
    [DataInlineUnit("DRNSTRUCTURE006")]
    [DataInlineUnit("DRNSTRUCTURE007")]
    [DataInlineUnit("DRNPERSISTENCE009")]
    [DataInlineUnit("DRNDEPENDENCYINJECTION002")]
    [DataInlineUnit("DRNDEPENDENCYINJECTION003")]
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
        else if (scenario.Location == "none")
        {
            diagnostic.Location.Should().Be(Location.None);
        }
        else
        {
            diagnostic.Location.SourceTree!.FilePath.Should().Be("ContractFixture.cs");
            (await diagnostic.Location.SourceTree.GetTextAsync()).ToString(diagnostic.Location.SourceSpan).Should().Be(scenario.Location);
        }

        var diagnostics = await AnalyzeProduction(scenario);
        var reported = diagnostics.Where(item => item.Id == id).Should().ContainSingle().Which;
        if (id == "DRNARCHITECTURE001")
        {
            reported.Descriptor.Category.Should().Be("DRN.Conventions.Configuration");
            reported.Properties["Target"].Should().Be(scenario.Target);
            diagnostics.Should().NotContain(item => item.Id == "DRNCONVENTIONINPUT001" || item.Id == "DRNARCHITECTURE002");
        }
        (await AnalyzeProduction(scenario with { ConventionsEnabled = false })).Should().BeEmpty();
        reported.GetMessage().Should().Be(scenario.Message);
        reported.Severity.Should().Be(DiagnosticSeverity.Warning);
        reported.Location.Kind.Should().Be(diagnostic.Location.Kind);
        if (scenario.Location == "input")
            reported.Location.GetLineSpan().Path.Should().Be(ConventionInputs.FileName);
        else if (scenario.Location == "none")
            reported.Location.Should().Be(Location.None);
        else
            (await reported.Location.SourceTree!.GetTextAsync()).ToString(reported.Location.SourceSpan).Should().Be(scenario.Location);
    }

    [Theory]
    [DataInlineUnit("DRNCONVENTIONINPUT001")]
    [DataInlineUnit("DRNOWNERSHIP001")]
    [DataInlineUnit("DRNSTRUCTURE001")]
    [DataInlineUnit("DRNSTRUCTURE002")]
    [DataInlineUnit("DRNPERSISTENCE001")]
    [DataInlineUnit("DRNPERSISTENCE002")]
    [DataInlineUnit("DRNPERSISTENCE003")]
    [DataInlineUnit("DRNPERSISTENCE004")]
    [DataInlineUnit("DRNPERSISTENCE005")]
    [DataInlineUnit("DRNSTRUCTURE003")]
    [DataInlineUnit("DRNREPOSITORY001")]
    [DataInlineUnit("DRNDEPENDENCYINJECTION001")]
    [DataInlineUnit("DRNREPOSITORY002")]
    [DataInlineUnit("DRNARCHITECTURE001")]
    [DataInlineUnit("DRNARCHITECTURE002")]
    [DataInlineUnit("DRNARCHITECTURE003")]
    [DataInlineUnit("DRNARCHITECTURE004")]
    [DataInlineUnit("DRNARCHITECTURE005")]
    [DataInlineUnit("DRNPERSISTENCE006")]
    [DataInlineUnit("DRNOWNERSHIP002")]
    [DataInlineUnit("DRNPERSISTENCE007")]
    [DataInlineUnit("DRNDTO001")]
    [DataInlineUnit("DRNDTO002")]
    [DataInlineUnit("DRNDTO003")]
    [DataInlineUnit("DRNDTO004")]
    [DataInlineUnit("DRNARCHITECTURE006")]
    [DataInlineUnit("DRNOWNERSHIP003")]
    [DataInlineUnit("DRNPERSISTENCE008")]
    [DataInlineUnit("DRNSTRUCTURE004")]
    [DataInlineUnit("DRNSTRUCTURE005")]
    [DataInlineUnit("DRNREPOSITORY003")]
    [DataInlineUnit("DRNREPOSITORY004")]
    [DataInlineUnit("DRNSTRUCTURE006")]
    [DataInlineUnit("DRNSTRUCTURE007")]
    [DataInlineUnit("DRNPERSISTENCE009")]
    [DataInlineUnit("DRNDEPENDENCYINJECTION002")]
    [DataInlineUnit("DRNDEPENDENCYINJECTION003")]
    public async Task Reviewed_Exception_Should_Cover_Only_The_Exact_Real_Violation_And_Expire_On_Review_Day(string id)
    {
        var scenario = ScenarioFor(id);
        var exception = Input("Exception", id, ("Target", scenario.Target), ("Reason", "Reviewed compatibility boundary pending the scheduled model and integration verification."),
            ("Owner", "Application maintainers"), ("Review", "2026-12-01"));
        var accepted = Analyze(scenario, exception);
        var audit = new ConventionExceptions(accepted, new DateOnly(2026, 11, 15));
        accepted.Diagnostics.Should().NotContain(diagnostic => diagnostic.Id == "DRNCONVENTIONEXCEPTION001");
        audit.Applies(accepted.Diagnostics.Single(diagnostic => diagnostic.Id == id)).Should().BeTrue();

        var unrelated = exception with { Fields = exception.Fields.SetItem("Target", "T:Sample." + scenario.Role + ".QA.OtherTarget") };
        var otherFacts = Analyze(scenario, unrelated);
        var otherAudit = new ConventionExceptions(otherFacts, new DateOnly(2026, 11, 15));
        otherFacts.Diagnostics.Should().NotContain(diagnostic => diagnostic.Id == "DRNCONVENTIONEXCEPTION001");
        otherAudit.Applies(otherFacts.Diagnostics.Single(diagnostic => diagnostic.Id == id)).Should().BeFalse();

        var dueFacts = Analyze(scenario, exception);
        var dueAudit = new ConventionExceptions(dueFacts, new DateOnly(2026, 12, 1));
        var expired = dueFacts.Diagnostics.Where(diagnostic => diagnostic.Id == "DRNCONVENTIONEXCEPTION001").Should().ContainSingle().Which;
        expired.Severity.Should().Be(DiagnosticSeverity.Warning);
        expired.GetMessage().Should().Be("Exception for '" + id + "' is malformed, unresolved, review-due or changed since its baseline; provide an exact target, meaningful reason and required ownership metadata.");
        expired.Location.Should().Be(exception.Location);
        dueAudit.Applies(expired).Should().BeFalse();
        dueAudit.Applies(dueFacts.Diagnostics.Single(diagnostic => diagnostic.Id == id)).Should().BeFalse();

        // The fixed-date audit above owns the expiry boundary. Production uses the current UTC date.
        var currentException = exception with { Fields = exception.Fields.SetItem("Review", "9999-12-31") };
        var suppressed = await AnalyzeProduction(scenario, currentException);
        suppressed.Should().NotContain(diagnostic => diagnostic.Id == id || diagnostic.Id == "DRNCONVENTIONEXCEPTION001");
        var otherTarget = currentException with { Fields = currentException.Fields.SetItem("Target", unrelated.Get("Target")) };
        var unsuppressed = await AnalyzeProduction(scenario, otherTarget);
        unsuppressed.Where(diagnostic => diagnostic.Id == id).Should().ContainSingle();
        unsuppressed.Should().NotContain(diagnostic => diagnostic.Id == "DRNCONVENTIONEXCEPTION001");
    }

    [Fact]
    public async Task Exact_Mapper_Exception_Should_Leave_The_Renamed_Property_Classification_Active()
    {
        var scenario = ScenarioFor("DRNDTO003");
        scenario = scenario with { Source = scenario.Source.Replace("ItemEntity", "OrderEntity", StringComparison.Ordinal) };
        const string target = "T:Sample.Domain.QA.OrderEntity";
        var baseline = await AnalyzeProduction(scenario);
        baseline.Where(item => item.Id.StartsWith("DRNDTO", StringComparison.Ordinal)).Select(item => item.Id)
            .Should().BeEquivalentTo("DRNDTO001", "DRNDTO003");
        var exception = Input("Exception", "DRNDTO001", ("Target", target),
            ("Reason", "The published ItemDto contract retains its name during the entity rename."),
            ("Owner", "Contract maintainers"), ("Review", "9999-12-31"));
        var reviewed = await AnalyzeProduction(scenario, exception);
        reviewed.Should().NotContain(item => item.Id == "DRNDTO001" || item.Id == "DRNCONVENTIONEXCEPTION001");
        var classification = reviewed.Where(item => item.Id == "DRNDTO003").Should().ContainSingle().Which;
        classification.GetMessage().Should().Be(scenario.Message);
        classification.Properties["Target"].Should().Be(target);
    }

    [Theory]
    [DataInline("DRNPERSISTENCE004")]
    [DataInline("DRNPERSISTENCE005")]
    [DataInline("DRNREPOSITORY001")]
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
    public void Context_Qualified_Exception_Should_Only_Cover_The_Matching_Context_For_DRNREPOSITORY004()
    {
        var baseScenario = ScenarioFor("DRNREPOSITORY004");
        var scenario = baseScenario with
        {
            Source = baseScenario.Source + """
                public sealed class OtherContextNpgsqlDbContextOptionsAttribute : DRN.Framework.EntityFramework.Attributes.NpgsqlDbContextOptionsAttribute;
                [OtherContextNpgsqlDbContextOptions]
                public class OtherContext : DRN.Framework.EntityFramework.Context.DrnContext<OtherContext>
                {
                    public OtherContext() : base(null) { }
                    public OtherContext(Microsoft.EntityFrameworkCore.DbContextOptions<OtherContext> options) : base(options) { }
                }
                """
        };
        var exact = Input("Exception", "DRNREPOSITORY004", ("Target", scenario.Target),
            ("Entity", "Order.Domain.QA.Order.OrderEntity"), ("Context", "Sample.Infra.QA.QAContext"),
            ("Reason", "Reviewed single aggregate/context repository omission."),
            ("Owner", "Persistence maintainers"), ("Review", "2026-12-01"));
        var facts = Analyze(scenario, exact);
        new ConventionExceptions(facts, new DateOnly(2026, 11, 15)).Applies(facts.Diagnostics.Single()).Should().BeTrue();
        var mismatched = Analyze(scenario, exact with { Fields = exact.Fields.SetItem("Context", "Sample.Infra.QA.OtherContext") });
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
        var scenario = ScenarioFor("DRNPERSISTENCE007");
        scenario = scenario with
        {
            Source = scenario.Source.Replace("public class QAContext", """
                [System.Diagnostics.CodeAnalysis.SuppressMessage("DRN.Conventions.Context", "DRNPERSISTENCE007",
                    Justification = "Vendor integration owns this isolated database model and does not use DRN persistence.")]
                public class QAContext
                """, StringComparison.Ordinal)
        };
        (await AnalyzeProduction(scenario)).Should().NotContain(item => item.Id == "DRNPERSISTENCE007" || item.Id == "DRNCONVENTIONEXCEPTION001");
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
            "DRNCONVENTIONINPUT001" => new("Infra", infra + Context.Replace("BODY", "", StringComparison.Ordinal).Replace("QAContext", "QAReadContext", StringComparison.Ordinal),
                [Input("Context", "Sample.Infra.QA.QAReadContext", ("Repositories", "QA/Repositories"), ("Migrations", "QA/Migrations"))],
                "T:Sample.Infra.QA.QAReadContext", "QAReadContext",
                "Convention input 'QAReadContext:Subdomain' is inconsistent with subdomain 'QA'. Use subdomain 'QA' with context 'QAContext', or subdomain 'QARead' with context 'QAReadContext'. Align the namespace and folder. Only for an intentional alias, set Subdomain=\"QA\" and Reason on its DrnConventionContext item.", ConventionContextAnalysis.Analyze),
            "DRNOWNERSHIP001" => new("Domain", """
                namespace Sample.Domain { public class Category : DRN.Framework.SharedKernel.Domain.AggregateRoot; }
                namespace Sample.Domain.QA { public class OtherTarget; }
                """, [], "T:Sample.Domain.Category", "Category",
                "'Category' needs an unambiguous subdomain; use the conventional namespace or declare its owner.", ConventionOwnershipAnalysis.Analyze),
            "DRNSTRUCTURE001" => new("Domain", "namespace Sample.Domain.QA; public class OtherTarget; public class Category : DRN.Framework.SharedKernel.Domain.AggregateRoot;",
                [Input("Path", "ContractFixture.cs", ("LogicalPath", "Other/Category.cs"))], "T:Sample.Domain.QA.Category", "Category",
                "'Category' belongs in 'QA' with namespace 'Sample.Domain.QA'; current logical placement conflicts.", ConventionOwnershipAnalysis.Analyze),
            "DRNSTRUCTURE002" => new("Infra", infra + Context.Replace("BODY", "", StringComparison.Ordinal).Replace("QAContext", "Store", StringComparison.Ordinal),
                [Input("Context", "Sample.Infra.QA.Store", ("Repositories", "QA/Repositories"), ("Migrations", "QA/Migrations"))],
                "T:Sample.Infra.QA.Store", "Store", "Context 'Store' must have a nonempty name ending in 'Context'.", ConventionContextAnalysis.Analyze),
            "DRNPERSISTENCE001" => new("Infra", infra + "public class QAContext() : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>(null);",
                [Input("Context", "Sample.Infra.QA.QAContext", ("Repositories", "QA/Repositories"), ("Migrations", "QA/Migrations"))],
                "T:Sample.Infra.QA.QAContext", "QAContext", "Context 'QAContext' needs the options constructor required by its declared registration/design-time path.", ConventionContextAnalysis.Analyze),
            "DRNPERSISTENCE002" => new("Infra", infra + Context.Replace("BODY", "", StringComparison.Ordinal).Replace(": base(options)", ": base(null)", StringComparison.Ordinal),
                [Input("Context", "Sample.Infra.QA.QAContext", ("Repositories", "QA/Repositories"), ("Migrations", "QA/Migrations"))],
                "T:Sample.Infra.QA.QAContext", "QAContext", "Context 'QAContext' has no established Npgsql setup on its declared DRN configuration path; provide or declare the supported setup.", ConventionContextAnalysis.Analyze),
            "DRNPERSISTENCE003" => new("Infra", infra + Context.Replace("BODY", "protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder model) { }", StringComparison.Ordinal),
                [Input("Context", "Sample.Infra.QA.QAContext", ("Repositories", "QA/Repositories"), ("Migrations", "QA/Migrations"))],
                "M:Sample.Infra.QA.QAContext.OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder)", "OnModelCreating",
                "Override 'OnModelCreating' has a proven path that omits the required base model configuration.", ConventionContextAnalysis.Analyze),
            "DRNPERSISTENCE004" => new("Infra", infra + """
                [DRN.Framework.EntityFramework.Attributes.SourceKnownPersistence(typeof(Category))]
                [DRN.Framework.EntityFramework.Attributes.SourceKnownPersistence(typeof(Category), Inclusion = DRN.Framework.EntityFramework.Attributes.SourceKnownPersistenceInclusion.Exclude)]
                """ + Context.Replace("BODY", "", StringComparison.Ordinal), [], "T:Sample.Infra.QA.QAContext", "QAContext",
                "Assignment of 'Category' to context 'QAContext' conflicts with 'contradictory explicit membership'; resolve or declare the intended relationship.", ConventionPersistenceAnalysis.Analyze),
            "DRNPERSISTENCE005" => new("Infra", infra + Context.Replace("BODY", "", StringComparison.Ordinal) + """
                public class CategoryConfiguration : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Category>
                {
                    public CategoryConfiguration(int dependency) { }
                    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> builder) { }
                }
                """, [Input("Configuration", "Sample.Infra.QA.CategoryConfiguration", ("Context", "Sample.Infra.QA.QAContext"))],
                "T:Sample.Infra.QA.CategoryConfiguration", "CategoryConfiguration",
                "Configuration 'CategoryConfiguration' for 'Category' is not discoverable by context 'QAContext'; use its configuration namespace/assembly or an explicit supported application path.", ConventionPersistenceAnalysis.Analyze),
            "DRNSTRUCTURE003" => new("Infra", infra + Context.Replace("BODY", "public Microsoft.EntityFrameworkCore.DbSet<Category> Categories => Set<Category>();", StringComparison.Ordinal),
                [Input("Context", "Sample.Infra.QA.QAContext", ("Repositories", "Other/Repositories"), ("Migrations", "QA/Migrations"))], "T:Sample.Infra.QA.QAContext", "input",
                "Context 'QAContext' has no valid Repositories destination. Set Repositories=\"QA/Repositories\" on its DrnConventionContext item, or provide a Reason for an alternate destination.", ConventionContextAnalysis.Analyze),
            "DRNREPOSITORY001" => new("Infra", infra + """
                [DRN.Framework.EntityFramework.Attributes.SourceKnownPersistence(typeof(Category), Inclusion = DRN.Framework.EntityFramework.Attributes.SourceKnownPersistenceInclusion.Exclude)]
                """ + Context.Replace("BODY", "", StringComparison.Ordinal) + """
                public class CategoryRepository(QAContext context, DRN.Framework.Utils.Entity.IEntityUtils utils)
                    : DRN.Framework.EntityFramework.Domain.SourceKnownRepository<QAContext, Category>(context, utils);
                """, [], "T:Sample.Infra.QA.CategoryRepository", "CategoryRepository",
                "Repository 'CategoryRepository' binds aggregate 'Category' inconsistently with contract or context 'QAContext explicitly excludes the entity'.", ConventionPersistenceAnalysis.Analyze),
            "DRNDEPENDENCYINJECTION001" => new("Application", application + """
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
            "DRNREPOSITORY002" => new("Application", application + """
                public class Other : AggregateRoot;
                public class CategoryService(ISourceKnownRepository<Other> repository) : ISourceKnownService<Category>
                {
                    public ISourceKnownRepository<Category> Repository => (ISourceKnownRepository<Category>)repository;
                }
                """, [], "T:Sample.Application.QA.CategoryService", "Repository",
                "Service 'CategoryService' has inconsistent aggregate/repository binding 'DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<Sample.Application.QA.Other>'.", ConventionPersistenceAnalysis.Analyze),
            "DRNARCHITECTURE001" => new("Contract", """
                namespace Sample.Contract.QA;
                public class OtherTarget;
                public class Response { public Wide Payload { get; set; } }
                """, [Input("Path", "ContractFixture.cs", ("LogicalPath", "QA/ContractFixture.cs")),
                    Input("Path", "Wide.cs", ("LogicalPath", "QA/Wide.cs"))], "P:Sample.Contract.QA.Response.Payload", "Payload",
                "Inspection of public contract member 'Sample.Contract.QA.Response.Payload' reached the analysis limit before verifying that its public model is free of persistence and domain types.",
                ConventionBoundaryAnalysis.Analyze, ExtraTree: WideModel.Value),
            "DRNARCHITECTURE002" => new("Contract", """
                namespace Sample.Contract.QA;
                public class OtherTarget;
                public class Response { public DRN.Framework.SharedKernel.Domain.SourceKnownEntity Entity { get; init; } }
                """, [], "P:Sample.Contract.QA.Response.Entity", "Entity",
                "Public contract member 'Sample.Contract.QA.Response.Entity' exposes persistence/domain type 'DRN.Framework.SharedKernel.Domain.SourceKnownEntity'; expose an approved DTO or value model.", ConventionBoundaryAnalysis.Analyze),
            "DRNARCHITECTURE003" => new("Contract", "namespace Sample.Contract.QA; public class OtherTarget;",
                [Input("Reference", "DRN.Framework.Hosting", ("Stage", "Package"))], "DRN.Framework.Hosting", "input",
                "Direct package reference from 'Sample.Contract' to 'DRN.Framework.Hosting' violates the declared role matrix; remove it or document the exact exception.", ConventionBoundaryAnalysis.Analyze),
            "DRNARCHITECTURE004" => new("Infra", infra, [], "T:Sample.Infra.QA.Category", "Category",
                "Implementation 'Category' (entity) belongs to role 'Domain'.", ConventionBoundaryAnalysis.Analyze),
            "DRNARCHITECTURE005" => new("Domain", """
                namespace Sample.Domain.QA;
                public class OtherTarget;
                public class Reader { private Microsoft.EntityFrameworkCore.DbContext context; }
                """, [], "T:Sample.Domain.QA.Reader", "Reader",
                "Type 'Reader' in layer 'Domain' uses persistence APIs. Remove this dependency.", ConventionBoundaryAnalysis.Analyze),
            "DRNPERSISTENCE006" => new("Infra", """
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
            "DRNOWNERSHIP002" => new("Domain", """
                namespace Sample.Domain.QA { public class OtherTarget; public class Category : DRN.Framework.SharedKernel.Domain.AggregateRoot; }
                namespace Sample.Domain.Billing { public class Reader { public Sample.Domain.QA.Category Entity => null!; } }
                """, [], "P:Sample.Domain.Billing.Reader.Entity", "Sample.Domain.QA.Category",
                "'Sample.Domain.Billing.Reader.Entity' in subdomain 'Billing' references entity 'Sample.Domain.QA.Category' from 'QA'. Use the entity's identifier or a contract from 'QA'.", ConventionBoundaryAnalysis.Analyze),
            "DRNPERSISTENCE007" => new("Application", """
                namespace Sample.Application.QA;
                public class OtherTarget;
                public class QAContext : Microsoft.EntityFrameworkCore.DbContext;
                """, [], "T:Sample.Application.QA.QAContext", "QAContext",
                "Application context 'QAContext' must implement IDrnContext or declare a narrow, justified exception.", ConventionContextAnalysis.Analyze),
            "DRNDTO001" => new("Domain", """
                namespace Sample.Domain.QA;
                public class OtherTarget;
                public class OrderEntity : DRN.Framework.SharedKernel.Domain.SourceKnownEntity;
                """, [], "T:Sample.Domain.QA.OrderEntity", "OrderEntity",
                "'OrderEntity' requires a public instance ToDto() returning OrderDto from its Contract subdomain and constructing it with this.",
                ConventionDtoAnalysis.Analyze),
            "DRNDTO002" => new("Contract", """
                namespace Sample.Contract.QA;
                public class OtherTarget;
                public class OrderDto : DRN.Framework.SharedKernel.Domain.Dto;
                """, [], "T:Sample.Contract.QA.OrderDto", "OrderDto",
                "Place 'OrderDto' under its subdomain in the 'Orders' folder and matching namespace.", ConventionDtoAnalysis.Analyze),
            "DRNDTO003" => new("Domain", """
                namespace Sample.Domain.QA;
                public class OtherTarget;
                public class ItemEntity : DRN.Framework.SharedKernel.Domain.SourceKnownEntity
                {
                    [DRN.Framework.SharedKernel.Attributes.Personal] public string Name => "";
                    public Shared.Contract.QA.Items.ItemDto ToDto() => new(this) { Label = Name };
                }
                """, [.. ItemContract.Value.Records], "T:Sample.Domain.QA.ItemEntity", "Label = Name",
                "DTO property 'Label' requires [Personal] from mapped property 'Name'.", ConventionDtoAnalysis.Analyze,
                ExtraReferences: [ItemContract.Value.Reference]),
            "DRNDTO004" => new("Contract", """
                namespace Sample.Contract.QA.Orders { public class OrderDto : DRN.Framework.SharedKernel.Domain.Dto; }
                namespace Sample.Contract.QA { public class OtherTarget; public record OrderResponse; }
                """, [Input("Path", "ContractFixture.cs", ("LogicalPath", "QA/ContractFixture.cs"))],
                "T:Sample.Contract.QA.OrderResponse", "OrderResponse",
                "Place 'OrderResponse' in 'QA/Orders' with namespace 'Sample.Contract.QA.Orders' alongside its entity DTO.",
                ConventionDtoAnalysis.Analyze),
            "DRNARCHITECTURE006" => new("Domain", "namespace Sample.Domain.QA; public class OtherTarget;",
                [Input("Reference", "Inventory", ("Stage", "Complete"))], "Sample.Domain:Contract", "input",
                "Project 'Sample.Domain' requires a direct project reference to its 'Contract' layer.", ConventionDependencyAnalysis.Analyze,
                IncludeRequiredLayers: false),
            "DRNOWNERSHIP003" => new("Infra", "namespace Sample.Infra.QA; public class OtherTarget;",
                [Input("Reference", "Inventory", ("Stage", "Complete")), .. EmptyDomain.Value.Records],
                "Sample.Infra:Domain", "input",
                "Infrastructure 'Sample.Infra' requires populated Domain subdomain '<any>'.", ConventionDependencyAnalysis.Analyze,
                IncludeRequiredLayers: false, ExtraReferences: [EmptyDomain.Value.Reference]),
            "DRNPERSISTENCE008" => new("Infra", infra + """
                public class ChildRow;
                public sealed class QAContextNpgsqlDbContextOptionsAttribute : DRN.Framework.EntityFramework.Attributes.NpgsqlDbContextOptionsAttribute;
                [QAContextNpgsqlDbContextOptions]
                """ + Context.Replace("BODY", "public Microsoft.EntityFrameworkCore.DbSet<ChildRow> Rows => Set<ChildRow>();", StringComparison.Ordinal),
                [], "P:Sample.Infra.QA.QAContext.Rows", "Rows",
                "DbSet property 'Rows' on context 'QAContext' exposes 'Sample.Infra.QA.ChildRow', which must derive from AggregateRoot.",
                ConventionPersistenceAnalysis.Analyze),
            "DRNSTRUCTURE004" => new("Domain", """
                namespace Sample.Domain.QA { public class OtherTarget; }
                namespace Sample.Domain.QA.Order
                {
                    public class Order : DRN.Framework.SharedKernel.Domain.AggregateRoot;
                    public interface IOrderRepository : DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<Order>;
                }
                """, [Input("Path", "ContractFixture.cs", ("LogicalPath", "QA/Order/ContractFixture.cs"))],
                "T:Sample.Domain.QA.Order.Order", "Order",
                "Rename 'Order' to end with 'Entity' and include a name before the suffix.", ConventionNamingAnalysis.Analyze),
            "DRNSTRUCTURE005" => new("Domain", """
                namespace Sample.Domain.QA { public class OtherTarget; }
                namespace Sample.Domain.QA.Orders
                {
                    public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot;
                    public interface IOrderRepository : DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<OrderEntity>;
                }
                """, [Input("Path", "ContractFixture.cs", ("LogicalPath", "QA/Orders/ContractFixture.cs"))],
                "T:Sample.Domain.QA.Orders.OrderEntity", "OrderEntity",
                "Place aggregate 'OrderEntity' in a folder named 'Order' and a namespace ending in '.Order'.", ConventionNamingAnalysis.Analyze),
            "DRNREPOSITORY003" => new("Domain", """
                namespace Sample.Domain.QA { public class OtherTarget; }
                namespace Sample.Domain.QA.Order
                {
                    public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot;
                }
                """, [Input("Path", "ContractFixture.cs", ("LogicalPath", "QA/Order/ContractFixture.cs"))],
                "T:Sample.Domain.QA.Order.OrderEntity", "OrderEntity",
                "Aggregate 'OrderEntity' has no matching repository interface in Domain subdomain 'QA'. Declare or move an interface extending ISourceKnownRepository<OrderEntity> into the same namespace and folder as 'OrderEntity'.",
                ConventionAggregateRepositoryAnalysis.Analyze),
            "DRNREPOSITORY004" => new("Infra", """
                namespace Sample.Infra.QA;
                public class OtherTarget;
                public sealed class QAContextNpgsqlDbContextOptionsAttribute : DRN.Framework.EntityFramework.Attributes.NpgsqlDbContextOptionsAttribute;
                [QAContextNpgsqlDbContextOptions]
                public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>
                {
                    public QAContext() : base(null) { }
                    public QAContext(Microsoft.EntityFrameworkCore.DbContextOptions<QAContext> options) : base(options) { }
                    public Microsoft.EntityFrameworkCore.DbSet<Order.Domain.QA.Order.OrderEntity> Orders => Set<Order.Domain.QA.Order.OrderEntity>();
                }
                """, [Input("Reference", "Inventory", ("Stage", "Complete")), .. OrderDomain.Value.Records],
                "T:Sample.Infra.QA.QAContext", "QAContext",
                "Aggregate 'OrderEntity' has no matching repository in Infra subdomain 'QA'. Add a concrete SourceKnownRepository implementation of its Domain repository interface.",
                ConventionAggregateRepositoryAnalysis.Analyze, IncludeRequiredLayers: false, ExtraReferences: [OrderDomain.Value.Reference]),
            "DRNSTRUCTURE006" => new("Infra", """
                namespace Sample.Infra.QA;
                public class OtherTarget;
                public class Row;
                public class CategoryConfig : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Row>
                {
                    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Row> builder) { }
                }
                """, [Input("Path", "ContractFixture.cs", ("LogicalPath", "QA/ContractFixture.cs"))],
                "T:Sample.Infra.QA.CategoryConfig", "CategoryConfig",
                "Place 'CategoryConfig' in a folder named 'Configurations' and a namespace ending in '.Configurations'.",
                ConventionInfrastructureAnalysis.Analyze),
            "DRNSTRUCTURE007" => new("Infra", infra + "public sealed class BadOptionsAttribute : DRN.Framework.EntityFramework.Attributes.NpgsqlDbContextOptionsAttribute;\n[BadOptions]\n" + Context.Replace("BODY", "", StringComparison.Ordinal),
                [], "T:Sample.Infra.QA.QAContext", "BadOptions",
                "Rename options 'BadOptionsAttribute' to start with 'QAContext' and end with 'Options'. An 'Attribute' suffix may follow. Recommended name: 'QAContextNpgsqlDbContextOptionsAttribute'.",
                ConventionInfrastructureAnalysis.Analyze),
            "DRNDEPENDENCYINJECTION002" => new("Application", "namespace Sample.Application.QA; public interface IWorker; public class Worker : IWorker; public class OtherTarget;",
                [], "T:Sample.Application.QA.Worker", "Worker",
                "'Worker' must register 'IWorker' through a matching LifetimeAttribute. Choose [Scoped<IWorker>], [Transient<IWorker>], or [Singleton<IWorker>] for its intended lifetime.",
                ConventionRegistrationAnalysis.AnalyzeLifetimeAttributes),
            "DRNPERSISTENCE009" => new("Infra", infra + Context.Replace("BODY", "", StringComparison.Ordinal),
                [], "T:Sample.Infra.QA.QAContext", "QAContext",
                "Context 'QAContext' must directly declare a custom attribute derived from NpgsqlDbContextOptionsAttribute. Recommended name: 'QAContextNpgsqlDbContextOptionsAttribute'.",
                ConventionInfrastructureAnalysis.Analyze),
            "DRNDEPENDENCYINJECTION003" => new("Application", "namespace Sample.Application.QA; public class OtherTarget;",
                [], "Sample.Application:Module", "none",
                "Project 'Sample.Application' needs a public static *Module class with a public static method accepting and returning IServiceCollection. Call services.AddServicesWithAttributes() on that parameter to scan this project's assembly.",
                static facts => ConventionRegistrationAnalysis.AnalyzeAll(facts, requireModule: true), IncludeModule: false),
            _ => throw new ArgumentOutOfRangeException(nameof(id))
        };
    }

    private static CSharpCompilation CreateCompilation(Scenario scenario)
    {
        var compilation = Compilation(scenario.Source, "ContractFixture.cs").WithAssemblyName("Sample." + scenario.Role)
            .AddReferences(FrameworkReference, UtilsReference);
        if (scenario.ExtraReferences is { Length: > 0 } extraReferences) compilation = compilation.AddReferences(extraReferences);
        return scenario.ExtraTree is { } tree ? compilation.AddSyntaxTrees(tree) : compilation;
    }

    private static ConventionFacts Analyze(Scenario scenario, params ConventionInput[] extra)
    {
        var compilation = CreateCompilation(scenario);
        compilation.AssertNoCompilerErrors();
        var options = AnalyzerTestCompilation.ConventionOptionsProvider("Sample." + scenario.Role);
        var facts = new ConventionFacts(compilation, new(true, "Sample." + scenario.Role, scenario.Role, "Sample." + scenario.Role, compilation.AssemblyName!, []),
            new([.. scenario.Inputs, .. extra], []), options, CancellationToken.None);
        scenario.Analyze(facts);
        return facts;
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeProduction(Scenario scenario, params ConventionInput[] extra)
    {
        var compilation = CreateCompilation(scenario);
        if (scenario is { IncludeModule: true, Role: ("Hosted" or "Application" or "Infra" or "Utils") })
            compilation = ConventionTestDependencies.WithModule(compilation);
        var (layered, layerRecords) = scenario.IncludeRequiredLayers
            ? ConventionTestDependencies.WithRequiredLayers(compilation, scenario.Role)
            : ((Compilation)compilation, ImmutableArray<ConventionInput>.Empty);
        var records = scenario.Inputs.Concat(extra).Concat(layerRecords).ToList();
        if (!records.Any(record => record is { Kind: "Reference", Identity: "Inventory" }))
            records.Add(Input("Reference", "Inventory", ("Stage", "Complete")));
        if (records.All(record => record.Kind != "Path"))
            records.Add(Input("Path", "ContractFixture.cs", ("LogicalPath", "QA/ContractFixture.cs")));
        var declarations = records.Select(record => "[assembly: DRN.Conventions." + record.Kind + "(" +
            SymbolDisplay.FormatLiteral(record.Identity, true) + string.Concat(record.Fields.OrderBy(field => field.Key, StringComparer.Ordinal)
                .Select(field => ", " + (record.Kind == "Exception" && field.Key == "Target" ? "SymbolTarget" : field.Key) + " = " +
                    SymbolDisplay.FormatLiteral(field.Value, true))) + ")]");
        var input = new ProjectDiagnosticRulesTests.RuleFile(ConventionInputs.FileName,
            "[assembly: DRN.Conventions.Version(\"1\")]\n" + string.Join("\n", declarations));
        var subject = scenario.ConventionsEnabled ? layered : ConventionTestDependencies.SuppressConventions(layered);
        var diagnostics = await AnalyzerTestCompilation.Analyze(subject, new SourceKnownConventionAnalyzer(),
            AnalyzerTestCompilation.ConventionOptions("Sample." + scenario.Role, files: input));
        diagnostics.Should().NotContain(diagnostic => diagnostic.Id == "AD0001");
        if (scenario.IncludeModule) diagnostics.Should().NotContain(diagnostic => diagnostic.Id == "DRNDEPENDENCYINJECTION003");
        return diagnostics;
    }

    private static (MetadataReference Reference, ConventionInput[] Records) CreateReferenceFixture(string assemblyName, string source)
    {
        var domain = Compilation(source).WithAssemblyName(assemblyName);
        domain.AssertNoCompilerErrors();
        var path = "/convention-fixtures/" + assemblyName;
        ConventionInput[] records =
        [
            Input("Reference", path + ".csproj", ("Stage", "Project"), ("Source", path + ".csproj")),
            Input("Reference", path + ".dll", ("Stage", "Resolved"), ("Project", path + ".csproj"))
        ];
        return (AnalyzerTestCompilation.EmitReference(domain, path + ".dll"), records);
    }

    private static ConventionInput Input(string kind, string identity, params (string Key, string Value)[] fields) => new(kind, identity,
        fields.ToImmutableDictionary(field => field.Key, field => field.Value),
        Location.Create("DrnConventionInputs.g.cs", new(4, 3), new(new(0, 4), new(0, 7))));

    private sealed record Scenario(string Role, string Source, ConventionInput[] Inputs, string Target, string Location,
        string Message, Action<ConventionFacts> Analyze, bool ConventionsEnabled = true, bool IncludeModule = true,
        bool IncludeRequiredLayers = true, MetadataReference[]? ExtraReferences = null, SyntaxTree? ExtraTree = null);
}
