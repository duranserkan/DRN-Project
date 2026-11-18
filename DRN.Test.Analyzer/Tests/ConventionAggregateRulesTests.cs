using System.Diagnostics.CodeAnalysis;
using DRN.Framework.SharedKernel.Analyzers.Conventions;
using static DRN.Test.Analyzer.Tests.DiagnosticClassificationAnalyzerTests;

namespace DRN.Test.Analyzer.Tests;

public sealed class ConventionAggregateRulesTests
{
    private static MetadataReference FrameworkReference => AnalyzerTestCompilation.EntityFrameworkReference;
    private static MetadataReference UtilsReference => AnalyzerTestCompilation.UtilsReference;
    private static readonly Lazy<MetadataReference> OrderDomainReference = new(() =>
    {
        var domain = Compilation(Domain + Contract).WithAssemblyName("Sample.Domain");
        domain.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        return domain.ToMetadataReference();
    });
    private static readonly Lazy<MetadataReference> GenericConfigurationReference = new(() =>
        AnalyzerTestCompilation.EmitReference(Compilation("namespace Sample.Infra.QA; " + GenericConfiguration)
            .WithAssemblyName("ConfigurationFixture")));
    private static readonly Lazy<ImmutableArray<MetadataReference>> MultiEntityConfigurationReferences =
        new(CreateMultiEntityConfigurationReferences);
    private const string Domain = """
        namespace Sample.Domain.QA.Order;
        public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot;
        """;
    private const string Contract = """
        public interface IOrderRepository : DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<OrderEntity>;
        """;
    private const string GenericConfiguration = """
        public class GenericConfig<T> : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<T> where T : class
        {
            public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> builder) { }
        }
        """;
    private const string Infra = """
        using DRN.Framework.EntityFramework.Context;
        using DRN.Framework.EntityFramework.Domain;
        using DRN.Framework.Utils.Entity;
        using Microsoft.EntityFrameworkCore;
        using Sample.Domain.QA.Order;
        namespace Sample.Infra.QA;
        public class QAContext() : DrnContext<QAContext>(null)
        {
            public DbSet<OrderEntity> Orders => Set<OrderEntity>();
        }
        """;

    [Theory]
    [InlineData("OrderEntity", "Order", "QA/Order/OrderEntity.cs", null)]
    [InlineData("Order", "Order", "QA/Order/Order.cs", "DRNSTRUCTURE004")]
    [InlineData("Entity", "Order", "QA/Order/Entity.cs", "DRNSTRUCTURE004")]
    [InlineData("OrderEntity", "Orders", "QA/Orders/OrderEntity.cs", "DRNSTRUCTURE005")]
    [InlineData("OrderEntity", "Order", "QA/Orders/OrderEntity.cs", "DRNSTRUCTURE005")]
    public void Aggregate_Name_And_Placement_Should_Match(string name, string area, string path, string? expected)
    {
        var source = $"namespace Sample.Domain.QA.{area}; public class {name} : DRN.Framework.SharedKernel.Domain.AggregateRoot;";
        var facts = Facts(Compilation(source, path).WithAssemblyName("Sample.Domain"), "Domain", path);
        ConventionNamingAnalysis.Analyze(facts);
        if (expected is null) facts.Diagnostics.Should().BeEmpty();
        else facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be(expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Semantic_Repository_Service_And_Controller_Types_Should_Require_Suffixes(bool valid)
    {
        var source = Domain + """
            public interface IStore : DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<OrderEntity>;
            public interface IHandler : DRN.Framework.SharedKernel.Domain.ISourceKnownService<OrderEntity>;
            public class Endpoint : Microsoft.AspNetCore.Mvc.ControllerBase;
            public class UnrelatedStore;
            """;
        if (valid) source = source.Replace("IStore", "IOrderRepository", StringComparison.Ordinal)
            .Replace("IHandler", "IOrderService", StringComparison.Ordinal).Replace("Endpoint", "OrdersController", StringComparison.Ordinal);
        var facts = Facts(Compilation(source).WithAssemblyName("Sample.Domain"), "Domain", "QA/Order/Models.cs");
        ConventionNamingAnalysis.Analyze(facts);
        if (valid)
        {
            facts.Diagnostics.Should().BeEmpty();
            return;
        }
        facts.Diagnostics.Should().HaveCount(3).And.OnlyContain(item => item.Id == "DRNSTRUCTURE004");
        facts.Diagnostics.Select(item => item.GetMessage()).Should().BeEquivalentTo(
            "Rename 'IStore' to end with 'Repository' and include a name before the suffix.",
            "Rename 'IHandler' to end with 'Service' and include a name before the suffix.",
            "Rename 'Endpoint' to end with 'Controller' and include a name before the suffix.");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Domain_Aggregate_Should_Require_Its_Repository_Contract(bool declared)
    {
        var facts = Facts(Compilation(Domain + (declared ? Contract : "")).WithAssemblyName("Sample.Domain"), "Domain");
        ConventionAggregateRepositoryAnalysis.Analyze(facts);
        if (declared) facts.Diagnostics.Should().BeEmpty();
        else facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNREPOSITORY003");
    }

    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void Infra_Should_Require_A_Concrete_Repository_Implementing_The_Domain_Interface(bool repository, bool implements, bool missing)
    {
        var source = Infra + (repository ? """
            public class OrderRepository(QAContext context, IEntityUtils utils)
                : SourceKnownRepository<QAContext, OrderEntity>(context, utils)
            """ + (implements ? ", IOrderRepository;" : ";") : "");
        var compilation = Compilation(source).WithAssemblyName("Sample.Infra").AddReferences(OrderDomainReference.Value,
            FrameworkReference, UtilsReference);
        var facts = Facts(compilation, "Infra");
        ConventionAggregateRepositoryAnalysis.Analyze(facts);
        if (missing) facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNREPOSITORY004");
        else facts.Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("QA/Order", "Sample.Domain.QA.Order", false)]
    [InlineData("QA/Repositories", "Sample.Domain.QA.Order", true)]
    [InlineData("QA/Order", "Sample.Domain.QA.Repositories", true)]
    public void Domain_Repository_Should_Share_Aggregate_Namespace_And_Logical_Folder(string folder, string ns, bool missing)
    {
        var compilation = Compilation(Domain, "OrderEntity.cs").WithAssemblyName("Sample.Domain")
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText($$"""
                namespace {{ns}};
                public interface IOrderRepository : DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<Sample.Domain.QA.Order.OrderEntity>;
                """, path: "Linked/IOrderRepository.cs"));
        compilation.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        var facts = new ConventionFacts(compilation,
            new(true, "Sample.Domain", "Domain", "Sample.Domain", compilation.AssemblyName!, []),
            new([
                new("Path", "OrderEntity.cs", ImmutableDictionary<string, string>.Empty
                    .Add("LogicalPath", "QA/Order/OrderEntity.cs"), Location.None),
                new("Path", "Linked/IOrderRepository.cs", ImmutableDictionary<string, string>.Empty
                    .Add("LogicalPath", folder + "/IOrderRepository.cs"), Location.None)
            ], []), new Options(), CancellationToken.None);
        ConventionAggregateRepositoryAnalysis.Analyze(facts);
        if (missing) facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNREPOSITORY003");
        else facts.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Abstract_And_Open_Generic_Helpers_Should_Not_Require_Aggregate_Names_Or_Repositories()
    {
        var facts = Facts(Compilation("""
            namespace Sample.Domain.QA;
            public abstract class Base : DRN.Framework.SharedKernel.Domain.AggregateRoot;
            public class Generic<T> : DRN.Framework.SharedKernel.Domain.AggregateRoot;
            """).WithAssemblyName("Sample.Domain"), "Domain");
        ConventionNamingAnalysis.Analyze(facts);
        ConventionAggregateRepositoryAnalysis.Analyze(facts);
        facts.Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("include", true)]
    [InlineData("exclude", false)]
    [InlineData("configuration", true)]
    public void Repository_Requirement_Should_Use_Explicit_And_Configuration_Assignment(string evidence, bool missing)
    {
        var attribute = evidence == "configuration" ? "" :
            "[DRN.Framework.EntityFramework.Attributes.SourceKnownPersistence(typeof(OrderEntity)" +
            (evidence == "exclude" ? ", Inclusion = DRN.Framework.EntityFramework.Attributes.SourceKnownPersistenceInclusion.Exclude" : "") + ")]";
        var source = """
            using DRN.Framework.EntityFramework.Context;
            using Microsoft.EntityFrameworkCore;
            using Sample.Domain.QA.Order;
            namespace Sample.Infra.QA;
            """ + attribute + "public class QAContext() : DrnContext<QAContext>(null);";
        if (evidence == "configuration") source += """
            public class OrderConfiguration : IEntityTypeConfiguration<OrderEntity>
            {
                public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<OrderEntity> builder) { }
            }
            """;
        var facts = Facts(Compilation(source).WithAssemblyName("Sample.Infra")
            .AddReferences(OrderDomainReference.Value, FrameworkReference, UtilsReference), "Infra");
        ConventionAggregateRepositoryAnalysis.Analyze(facts);
        if (missing) facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNREPOSITORY004");
        else facts.Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Sample.Infra.QA.Configurations", false, true)]
    [InlineData("Sample.Infra.QA", false, true)]
    [InlineData("Sample.Infra.QAOther", false, false)]
    [InlineData("Sample.Infra.QB.Configurations", false, false)]
    [InlineData("Sample.Infra.QA.Configurations", true, false)]
    public void Configuration_Assignment_Should_Respect_Namespace_And_Deduplicate_Contexts(string ns, bool abstractConfiguration, bool missing)
    {
        var source = $$"""
            using DRN.Framework.EntityFramework.Context;
            using Microsoft.EntityFrameworkCore;
            using Sample.Domain.QA.Order;
            namespace Sample.Infra.QA
            {
                public class QAContext() : DrnContext<QAContext>(null);
                public class SecondContext() : DrnContext<SecondContext>(null);
            }
            namespace {{ns}}
            {
                public {{(abstractConfiguration ? "abstract " : "")}}class OrderConfig : IEntityTypeConfiguration<OrderEntity>
                {
                    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<OrderEntity> builder) { }
                }
            }
            """;
        var facts = Facts(Compilation(source).WithAssemblyName("Sample.Infra")
            .AddReferences(OrderDomainReference.Value, FrameworkReference, UtilsReference), "Infra");
        ConventionAggregateRepositoryAnalysis.Analyze(facts);
        if (missing) facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNREPOSITORY004");
        else facts.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Repository_Contract_In_Another_Subdomain_Should_Not_Satisfy_The_Aggregate()
    {
        var compilation = Compilation("""
            namespace Sample.Domain.QA.Order { public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot; }
            namespace Sample.Domain.Billing
            {
                public interface IOrderRepository : DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<Sample.Domain.QA.Order.OrderEntity>;
            }
            """).WithAssemblyName("Sample.Domain");
        var facts = Facts(compilation, "Domain");
        ConventionAggregateRepositoryAnalysis.Analyze(facts);
        facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNREPOSITORY003");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [SuppressMessage("ReSharper", "RedundantArgumentDefaultValue")]
    public async Task Production_Analyzer_Should_Report_New_Rules_And_Respect_Narrow_Suppression(bool suppress)
    {
        var attribute = suppress
            ? "[System.Diagnostics.CodeAnalysis.SuppressMessage(\"DRN.Conventions.Structure\", \"DRNSTRUCTURE004\", Justification = \"Preserve published aggregate name during migration.\")]"
            : "";
        var compilation = Compilation("namespace Sample.Domain.QA; " + attribute +
            "public class Order : DRN.Framework.SharedKernel.Domain.AggregateRoot;").WithAssemblyName("Sample.Domain");
        compilation.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        var diagnostics = await compilation.WithAnalyzers([new SourceKnownConventionAnalyzer()],
            new AnalyzerOptions([], new Options("Domain"))).GetAnalyzerDiagnosticsAsync();
        diagnostics.Should().NotContain(item => item.Id == "AD0001");
        diagnostics.Count(item => item.Id == "DRNSTRUCTURE004").Should().Be(suppress ? 0 : 1);
        diagnostics.Count(item => item.Id == "DRNREPOSITORY003").Should().Be(1);
    }

    [Fact]
    public async Task Property_Exception_Should_Not_Waive_Other_Non_Aggregate_DbSets()
    {
        var compilation = Compilation("""
            namespace Sample.Infra.QA;
            public class FrameworkStorage;
            public class QAContext() : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>(null)
            {
                [System.Diagnostics.CodeAnalysis.SuppressMessage("DRN.Conventions.Persistence", "DRNPERSISTENCE008",
                    Justification = "This integration property must expose the framework-owned storage contract.")]
                public Microsoft.EntityFrameworkCore.DbSet<FrameworkStorage> IntegrationKeys => Set<FrameworkStorage>();
                public Microsoft.EntityFrameworkCore.DbSet<FrameworkStorage> OtherRows => Set<FrameworkStorage>();
            }
            """).WithAssemblyName("Sample.Infra").AddReferences(FrameworkReference, UtilsReference);
        compilation.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        var diagnostics = await compilation.WithAnalyzers([new SourceKnownConventionAnalyzer()],
            new AnalyzerOptions([], new Options("Infra"))).GetAnalyzerDiagnosticsAsync();
        diagnostics.Should().NotContain(item => item.Id == "AD0001" || item.Id == "DRNCONVENTIONEXCEPTION001");
        var diagnostic = diagnostics.Where(item => item.Id == "DRNPERSISTENCE008").Should().ContainSingle().Which;
        diagnostic.GetMessage().Should().Contain("OtherRows");
    }

    [Fact]
    public void Unassigned_Aggregates_Should_Not_Require_A_Repository_In_Every_Infra()
    {
        var facts = Facts(Compilation("namespace Sample.Infra.QA; public class Marker;").WithAssemblyName("Sample.Infra")
            .AddReferences(OrderDomainReference.Value), "Infra");
        ConventionAggregateRepositoryAnalysis.Analyze(facts);
        facts.Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit(false, false)]
    [DataInlineUnit(true, false)]
    [DataInlineUnit(false, true)]
    [DataInlineUnit(true, true)]
    public void Explicit_Closed_Generic_Configuration_Should_Assign_An_Aggregate_Without_A_DbSet(bool referenced, bool ignored)
    {
        var source = $$"""
            using Microsoft.EntityFrameworkCore;
            using Sample.Domain.QA.Order;
            namespace Sample.Infra.QA;
            public class QAContext() : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>(null)
            {
                protected override void OnModelCreating(ModelBuilder model)
                {
                    base.OnModelCreating(model);
                    model.ApplyConfiguration(new GenericConfig<OrderEntity>());
                    {{(ignored ? "model.Ignore<OrderEntity>();" : "")}}
                }
            }
            """;
        var compilation = Compilation(source + (referenced ? "" : GenericConfiguration)).WithAssemblyName("Sample.Infra")
            .AddReferences(OrderDomainReference.Value, FrameworkReference, UtilsReference);
        if (referenced) compilation = compilation.AddReferences(GenericConfigurationReference.Value);
        var facts = Facts(compilation, "Infra");
        ConventionAggregateRepositoryAnalysis.Analyze(facts);
        if (ignored) facts.Diagnostics.Should().BeEmpty();
        else facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNREPOSITORY004");
    }

    [Theory]
    [DataInlineUnit(false, false)]
    [DataInlineUnit(true, false)]
    [DataInlineUnit(false, true)]
    [DataInlineUnit(true, true)]
    public void Entity_Bound_Interface_Naming_Should_Include_Inherited_Methods(bool inherited, bool valid)
    {
        var domain = Compilation(Domain).WithAssemblyName("Sample.Domain");
        var name = valid ? "IOrderService" : "IHandler";
        var declaration = inherited ? ": IQuery<Sample.Domain.QA.Order.OrderEntity>;" :
            "{ System.Threading.Tasks.Task<Sample.Domain.QA.Order.OrderEntity> Read(); }";
        var source = $$"""
            namespace Sample.Application.QA;
            public interface IQuery<T> { System.Threading.Tasks.Task<T> Read(); }
            public interface {{name}} {{declaration}}
            public interface IUnrelated : IQuery<string>;
            """;
        var facts = Facts(Compilation(source).WithAssemblyName("Sample.Application")
            .AddReferences(domain.ToMetadataReference()), "Application");
        ConventionNamingAnalysis.Analyze(facts);
        if (valid) facts.Diagnostics.Should().BeEmpty();
        else
        {
            var diagnostic = facts.Diagnostics.Should().ContainSingle().Which;
            diagnostic.Id.Should().Be("DRNSTRUCTURE004");
            diagnostic.Properties["Target"].Should().Be("T:Sample.Application.QA.IHandler");
        }
    }

    [Theory]
    [DataInlineUnit("OrderEntity", "", "OrderEntity")]
    [DataInlineUnit("OtherEntity", "", "OtherEntity")]
    [DataInlineUnit("OrderEntity", "OtherEntity", "OrderEntity")]
    [DataInlineUnit("OtherEntity", "OrderEntity", "OtherEntity")]
    [DataInlineUnit("OrderEntity", "OrderEntity", "")]
    [DataInlineUnit("OtherEntity", "OtherEntity", "")]
    public void Referenced_Multi_Entity_Configuration_Should_Assign_Only_The_Selected_Entity(
        string selected, string ignored, string expected)
    {
        var source = $$"""
            using Microsoft.EntityFrameworkCore;
            using Sample.Domain.QA.Order;
            namespace Sample.Infra.QA;
            public class QAContext() : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>(null)
            {
                protected override void OnModelCreating(ModelBuilder model)
                {
                    base.OnModelCreating(model);
                    model.ApplyConfiguration<{{selected}}>(new External.SharedConfig());
                    {{(ignored.Length == 0 ? "" : "model.Ignore<" + ignored + ">();")}}
                }
            }
            """;
        var facts = Facts(Compilation(source).WithAssemblyName("Sample.Infra")
            .AddReferences(MultiEntityConfigurationReferences.Value).AddReferences(FrameworkReference, UtilsReference), "Infra");
        ConventionAggregateRepositoryAnalysis.Analyze(facts);
        if (expected.Length == 0) facts.Diagnostics.Should().BeEmpty();
        else
        {
            var diagnostic = facts.Diagnostics.Should().ContainSingle().Which;
            diagnostic.Id.Should().Be("DRNREPOSITORY004");
            diagnostic.Properties["Entity"].Should().Be("Sample.Domain.QA.Order." + expected);
        }
    }

    private static ImmutableArray<MetadataReference> CreateMultiEntityConfigurationReferences()
    {
        var domain = Compilation(Domain + Contract + """
            public class OtherEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot;
            public interface IOtherRepository : DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<OtherEntity>;
            """).WithAssemblyName("Sample.Domain");
        using var domainImage = new MemoryStream();
        domain.Emit(domainImage).Success.Should().BeTrue();
        var domainReference = MetadataReference.CreateFromImage(domainImage.ToArray());
        var dependency = Compilation("""
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            using Sample.Domain.QA.Order;
            namespace External;
            public class SharedConfig : IEntityTypeConfiguration<OrderEntity>, IEntityTypeConfiguration<OtherEntity>
            {
                public void Configure(EntityTypeBuilder<OrderEntity> builder) { }
                public void Configure(EntityTypeBuilder<OtherEntity> builder) { }
            }
            """).WithAssemblyName("ConfigurationFixture").AddReferences(domainReference);
        using var image = new MemoryStream();
        dependency.Emit(image).Success.Should().BeTrue();
        return [domainReference, MetadataReference.CreateFromImage(image.ToArray())];
    }

    private static ConventionFacts Facts(CSharpCompilation compilation, string role, string logicalPath = "QA/Models.cs")
    {
        compilation.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        return new(compilation, new(true, "Sample." + role, role, "Sample." + role, compilation.AssemblyName!, []),
            new([new("Path", compilation.SyntaxTrees.Single().FilePath,
                ImmutableDictionary<string, string>.Empty.Add("LogicalPath", logicalPath), Location.None)], []),
            new Options(), CancellationToken.None);
    }

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
