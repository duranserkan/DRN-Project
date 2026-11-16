using DRN.Framework.EntityFramework.Context;
using DRN.Framework.SharedKernel.Analyzers.Conventions;
using DRN.Framework.Utils.Entity;
using static DRN.Test.Analyzer.Tests.DiagnosticClassificationAnalyzerTests;

namespace DRN.Test.Analyzer.Tests;

public sealed class ConventionPersistenceTests
{
    private const string Imports = """
        using Microsoft.EntityFrameworkCore;
        using DRN.Framework.EntityFramework.Context;
        using DRN.Framework.EntityFramework.Attributes;
        using DRN.Framework.SharedKernel.Domain;
        using DRN.Framework.SharedKernel.Domain.Repository;
        using DRN.Framework.EntityFramework.Domain;
        using DRN.Framework.Utils.Entity;
        namespace Sample.Infra.QA;
        public class Category : AggregateRoot;

        """;

    [Fact]
    public void Drn_DbSets_Should_Expose_Only_Aggregate_Roots()
    {
        var facts = Analyze(Imports + """
            public class Child : SourceKnownEntity;
            public class Plain;
            public class DerivedCategory : Category;
            public class QAContext() : DrnContext<QAContext>(null)
            {
                public DbSet<Category> Categories => Set<Category>();
                public DbSet<DerivedCategory> Derived => Set<DerivedCategory>();
                public DbSet<Child> Children => Set<Child>();
                public DbSet<Plain> Rows => Set<Plain>();
            }
            """);
        var diagnostics = facts.Diagnostics.Where(item => item.Id == "DRNCV023").ToArray();
        diagnostics.Should().HaveCount(2);
        diagnostics.Select(item => item.Properties["Entity"]).Should().BeEquivalentTo(
            "Sample.Infra.QA.Child", "Sample.Infra.QA.Plain");
        diagnostics.Should().OnlyContain(item => item.Severity == DiagnosticSeverity.Warning);
        diagnostics.Select(item => item.Location.SourceTree!.GetText().ToString(item.Location.SourceSpan))
            .Should().BeEquivalentTo("Children", "Rows");
    }

    [Fact]
    public void Inherited_Generic_DbSets_Should_Be_Checked_After_Substitution()
    {
        var facts = Analyze(Imports + """
            public class Child : SourceKnownEntity;
            public abstract class ContextBase<TContext, TEntity>() : DrnContext<TContext>(null)
                where TContext : DrnContext<TContext>, new() where TEntity : class
            {
                public DbSet<TEntity> Entities => Set<TEntity>();
            }
            public class QAContext : ContextBase<QAContext, Child>;
            public class DerivedContext : QAContext;
            public class ReadContext : ContextBase<ReadContext, Category>;
            """);
        var diagnostic = facts.Diagnostics.Where(item => item.Id == "DRNCV023").Should().ContainSingle().Which;
        diagnostic.Properties["Entity"].Should().Be("Sample.Infra.QA.Child");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("Entities");
    }

    [Fact]
    public void Non_Drn_Contexts_And_Static_Or_Lookalike_Sets_Should_Not_Trigger_Aggregate_Rule()
    {
        Analyze(Imports + """
            public class Child : SourceKnownEntity;
            public class OtherContext : DbContext { public DbSet<Child> Children => Set<Child>(); }
            public class FakeSet<T>;
            public class QAContext() : DrnContext<QAContext>(null)
            {
                public static DbSet<Child> Shared => null!;
                public FakeSet<Child> Lookalike => new();
            }
            """).Diagnostics.Should().NotContain(item => item.Id == "DRNCV023");
    }

    [Fact]
    public void Explicit_Contradiction_Should_Report_The_Context_And_Exact_Pair()
    {
        var facts = Analyze(Imports + """
            [SourceKnownPersistence(typeof(Category))]
            [SourceKnownPersistence(typeof(Category), Inclusion = SourceKnownPersistenceInclusion.Exclude)]
            public class QAContext() : DrnContext<QAContext>(null);
            """);
        var diagnostic = facts.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNCV008");
        diagnostic.GetMessage().Should().Contain("Category").And.Contain("QAContext");
        diagnostic.Properties["Context"].Should().Be("Sample.Infra.QA.QAContext");
        diagnostic.Properties["Entity"].Should().Be("Sample.Infra.QA.Category");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("QAContext");
    }

    [Fact]
    public void Multiple_Contexts_And_Attribute_Only_Mapping_Should_Not_Require_Configuration_Classes()
    {
        Analyze(Imports + """
            [SourceKnownPersistence(typeof(Category))]
            public class QAReadContext() : DrnContext<QAReadContext>(null);
            [SourceKnownPersistence(typeof(Category))]
            public class QAWriteContext() : DrnContext<QAWriteContext>(null);
            """).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Repository_Using_An_Excluded_Entity_Should_Report_At_The_Repository()
    {
        var facts = Analyze(Imports + """
            [SourceKnownPersistence(typeof(Category), Inclusion = SourceKnownPersistenceInclusion.Exclude)]
            public class QAContext() : DrnContext<QAContext>(null);
            public class CategoryRepository(QAContext context, IEntityUtils utils)
                : SourceKnownRepository<QAContext, Category>(context, utils);
            """);
        var diagnostic = facts.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNCV011");
        diagnostic.GetMessage().Should().Contain("explicitly excludes");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("CategoryRepository");
    }

    [Fact]
    public void Inherited_Closed_Repository_Should_Report_Exclusion_Without_Reporting_The_Abstract_Open_Base()
    {
        var facts = Analyze(Imports + """
            [SourceKnownPersistence(typeof(Category), Inclusion = SourceKnownPersistenceInclusion.Exclude)]
            public class QAContext() : DrnContext<QAContext>(null);
            public abstract class GenericRepository<T>(QAContext context, IEntityUtils utils)
                : SourceKnownRepository<QAContext, T>(context, utils) where T : AggregateRoot;
            public class CategoryRepository(QAContext context, IEntityUtils utils) : GenericRepository<Category>(context, utils);
            """);
        var diagnostic = facts.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNCV011");
        diagnostic.Properties["Target"].Should().Be("T:Sample.Infra.QA.CategoryRepository");
        diagnostic.Properties["Entity"].Should().Be("Sample.Infra.QA.Category");
    }

    [Fact]
    public void Navigation_Only_Owned_Keyless_And_Unmapped_Types_Should_Not_Invent_Membership_Or_Configuration_Demands()
    {
        Analyze(Imports + """
            public class UnmappedAggregate : AggregateRoot;
            [Owned] public class Details { public string Label { get; set; } = ""; }
            [Keyless] public class ReadRow { public string Label { get; set; } = ""; }
            public class Parent : AggregateRoot
            {
                public Category Category { get; set; } = null!;
                public Details Details { get; set; } = null!;
            }
            public class QAContext() : DrnContext<QAContext>(null)
            {
                public DbSet<Parent> Parents { get; set; } = null!;
                public DbSet<ReadRow> Rows { get; set; } = null!;
            }
            """).Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCV023");
    }

    [Fact]
    public void Abstract_And_Open_Configuration_Helpers_Should_Not_Be_Treated_As_Scan_Candidates()
    {
        Analyze(Imports + """
            public class QAContext() : DrnContext<QAContext>(null);
            public abstract class AbstractConfiguration : IEntityTypeConfiguration<Category>
            {
                public abstract void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> builder);
            }
            public class GenericConfiguration<T> : IEntityTypeConfiguration<T> where T : class
            {
                public GenericConfiguration(int dependency) { }
                public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<T> builder) { }
            }
            """).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Configuration_Attribute_Should_Establish_Application_Outside_The_Default_Namespace()
    {
        var facts = Analyze("""
            using Microsoft.EntityFrameworkCore;
            using DRN.Framework.EntityFramework.Context;
            namespace Sample.Infra.QA
            {
                [EntityTypeConfiguration(typeof(Sample.Infra.Other.CategoryConfiguration))]
                public class Category : DRN.Framework.SharedKernel.Domain.AggregateRoot;
                public class QAContext() : DrnContext<QAContext>(null)
                {
                    public DbSet<Category> Categories { get; set; } = null!;
                }
            }
            namespace Sample.Infra.Other
            {
                public class CategoryConfiguration : IEntityTypeConfiguration<Sample.Infra.QA.Category>
                {
                    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Sample.Infra.QA.Category> builder) { }
                }
            }
            """, Input("Configuration", "Sample.Infra.Other.CategoryConfiguration", ("Context", "Sample.Infra.QA.QAContext")));
        facts.Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("modelBuilder.Ignore<Category>();", true)]
    [InlineData("if (System.DateTime.UtcNow.Ticks > 0) modelBuilder.Ignore<Category>();", false)]
    [InlineData("Helper(modelBuilder);", false)]
    [InlineData("modelBuilder.Ignore<Category>(); modelBuilder.Entity<Category>();", false)]
    [InlineData("modelBuilder.Ignore<Category>(); Helper(modelBuilder);", false)]
    public void Ignore_Should_Require_A_Proven_Unconditional_Ef_Call(string body, bool conflict)
    {
        var facts = Analyze(Imports + $$"""
            public class QAContext() : DrnContext<QAContext>(null)
            {
                protected override void OnModelCreating(ModelBuilder modelBuilder) { {{body}} }
                private static void Helper(ModelBuilder modelBuilder) { }
            }
            public class CategoryRepository(QAContext context, IEntityUtils utils)
                : SourceKnownRepository<QAContext, Category>(context, utils);
            """);
        facts.Diagnostics.Count(diagnostic => diagnostic.Id == "DRNCV011").Should().Be(conflict ? 1 : 0);
    }

    [Fact]
    public void A_Hidden_Model_Method_Should_Not_Replace_The_Ef_Virtual_Configuration_Path()
    {
        Analyze(Imports + """
            public class QAContext() : DrnContext<QAContext>(null)
            {
                public new void OnModelCreating(ModelBuilder modelBuilder) { modelBuilder.Ignore<Category>(); }
            }
            public class CategoryRepository(QAContext context, IEntityUtils utils)
                : SourceKnownRepository<QAContext, Category>(context, utils);
            """).Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Sample.Infra.QA.Configurations", false)]
    [InlineData("Sample.Infra.QAOther.Configurations", true)]
    public void Declared_Configuration_Should_Respect_Exact_Or_Child_Namespace(string configNamespace, bool invalid)
    {
        var source = """
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            using DRN.Framework.EntityFramework.Context;
            namespace Sample.Infra.QA
            {
                public class Category { public int Id { get; set; } }
                public class QAContext() : DrnContext<QAContext>(null);
            }
            """ + $$"""
            namespace {{configNamespace}}
            {
                public class CategoryConfiguration : IEntityTypeConfiguration<Sample.Infra.QA.Category>
                {
                    public void Configure(EntityTypeBuilder<Sample.Infra.QA.Category> builder) { }
                }
            }
            """;
        var configuration = configNamespace + ".CategoryConfiguration";
        var facts = Analyze(source, Input("Configuration", configuration, ("Context", "Sample.Infra.QA.QAContext")));
        facts.Diagnostics.Count(diagnostic => diagnostic.Id == "DRNCV009").Should().Be(invalid ? 1 : 0);
        if (invalid)
        {
            var diagnostic = facts.Diagnostics.Single();
            diagnostic.Properties["Target"].Should().Be("T:" + configuration);
            diagnostic.GetMessage().Should().Contain("CategoryConfiguration").And.Contain("QAContext");
        }
    }

    [Fact]
    public void Explicit_Configuration_Application_Should_Allow_An_External_Namespace()
    {
        var facts = Analyze(Imports + """
            public class QAContext() : DrnContext<QAContext>(null)
            {
                protected override void OnModelCreating(ModelBuilder modelBuilder)
                {
                    base.OnModelCreating(modelBuilder);
                    modelBuilder.ApplyConfiguration(new OtherConfiguration());
                }
            }
            public class OtherConfiguration : IEntityTypeConfiguration<Category>
            {
                public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> builder) { }
            }
            """, Input("Configuration", "Sample.Infra.QA.OtherConfiguration", ("Context", "Sample.Infra.QA.QAContext")));
        facts.Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("private CategoryConfiguration() { }", false)]
    [InlineData("public CategoryConfiguration(int dependency) { }", true)]
    public void Configuration_Scan_Should_Require_A_Parameterless_Constructor_Without_Rejecting_Private_Ones(string constructor, bool invalid)
    {
        var facts = Analyze(Imports + $$"""
            public class QAContext() : DrnContext<QAContext>(null);
            public class CategoryConfiguration : IEntityTypeConfiguration<Category>
            {
                {{constructor}}
                public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> builder) { }
            }
            """, Input("Configuration", "Sample.Infra.QA.CategoryConfiguration", ("Context", "Sample.Infra.QA.QAContext")));
        facts.Diagnostics.Count(diagnostic => diagnostic.Id == "DRNCV009").Should().Be(invalid ? 1 : 0);
    }

    [Theory]
    [InlineData("private CategoryConfiguration() { }", false)]
    [InlineData("public CategoryConfiguration(int dependency) { }", true)]
    public void Ordinary_Configuration_Should_Be_Checked_Without_An_Explicit_Association(string constructor, bool invalid)
    {
        var facts = Analyze(Imports + $$"""
            public class QAContext() : DrnContext<QAContext>(null);
            public class CategoryConfiguration : IEntityTypeConfiguration<Category>
            {
                {{constructor}}
                public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> builder) { }
            }
            """);
        facts.Diagnostics.Count(diagnostic => diagnostic.Id == "DRNCV009").Should().Be(invalid ? 1 : 0);
        if (invalid) facts.Diagnostics.Single().Properties["Context"].Should().Be("Sample.Infra.QA.QAContext");
    }

    [Theory]
    [DataInlineUnit("base.OnModelCreating(model);", true)]
    [DataInlineUnit("model.Ignore<Category>();", false)]
    public void Shared_Model_Override_Chains_Should_Preserve_Default_Scan_Behavior(string body, bool defaultScan)
    {
        var facts = Analyze(Imports + $$"""
            public class QAContext() : DrnContext<QAContext>(null)
            {
                protected override void OnModelCreating(ModelBuilder model) { {{body}} }
            }
            public class FirstContext : QAContext
            {
                protected override void OnModelCreating(ModelBuilder model) => base.OnModelCreating(model);
            }
            public class SecondContext : QAContext
            {
                protected override void OnModelCreating(ModelBuilder model) => base.OnModelCreating(model);
            }
            """);
        foreach (var name in new[] { "QAContext", "FirstContext", "SecondContext" })
        {
            var context = facts.Compilation.GetTypeByMetadataName("Sample.Infra.QA." + name)!;
            ConventionPersistenceAnalysis.GetModel(facts, context).DefaultScan.Should().Be(defaultScan);
        }
    }

    [Theory]
    [DataInlineUnit("DbSet", "")]
    [DataInlineUnit("Membership", "")]
    [DataInlineUnit("Repository", "")]
    [DataInlineUnit("DbSet", "protected override void OnModelCreating(ModelBuilder model) { base.OnModelCreating(model); }")]
    [DataInlineUnit("DbSet", "protected override void OnModelCreating(ModelBuilder model) => base.OnModelCreating(model);")]
    public void Unique_Positive_Membership_Should_Associate_An_Ordinary_Configuration_In_A_Sibling_Area(string evidence, string modelOverride)
    {
        var membership = evidence == "Membership" ? "[SourceKnownPersistence(typeof(Category))]" : "";
        var dbSet = evidence == "DbSet" ? "public DbSet<Category> Categories => Set<Category>();" : "";
        var repository = evidence == "Repository"
            ? "public class CategoryRepository(WriteContext context, IEntityUtils utils) : SourceKnownRepository<WriteContext, Category>(context, utils);" : "";
        var facts = Analyze($$"""
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            using DRN.Framework.EntityFramework.Attributes;
            using DRN.Framework.EntityFramework.Context;
            using DRN.Framework.EntityFramework.Domain;
            using DRN.Framework.SharedKernel.Domain;
            using DRN.Framework.Utils.Entity;
            namespace Sample.Infra.QA
            {
                public class Category : AggregateRoot;
            }
            namespace Sample.Infra.QA.Write
            {
                {{membership}}
                public class WriteContext() : DrnContext<WriteContext>(null) { {{dbSet}} {{modelOverride}} }
                {{repository}}
            }
            namespace Sample.Infra.QA.Configurations
            {
                public class CategoryConfiguration : IEntityTypeConfiguration<Category>
                {
                    public void Configure(EntityTypeBuilder<Category> builder) { }
                }
            }
            """);
        facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCV009");
        facts.Diagnostics.Single().Properties["Context"].Should().Be("Sample.Infra.QA.Write.WriteContext");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cqrs_Configuration_Association_Should_Stay_In_The_Nearest_Context_Area(bool invalidReadConfiguration)
    {
        var constructor = invalidReadConfiguration ? "public ReadConfiguration(int dependency) { }" : "";
        var facts = Analyze($$"""
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            using DRN.Framework.EntityFramework.Context;
            namespace Sample.Infra.QA { public class Category : DRN.Framework.SharedKernel.Domain.AggregateRoot; }
            namespace Sample.Infra.QA.Read
            {
                public class ReadContext() : DrnContext<ReadContext>(null)
                {
                    public DbSet<Category> Categories => Set<Category>();
                }
            }
            namespace Sample.Infra.QA.Read.Configurations
            {
                public class ReadConfiguration : IEntityTypeConfiguration<Category>
                {
                    {{constructor}}
                    public void Configure(EntityTypeBuilder<Category> builder) { }
                }
            }
            namespace Sample.Infra.QA.Write
            {
                public class WriteContext() : DrnContext<WriteContext>(null)
                {
                    public DbSet<Category> Categories => Set<Category>();
                }
            }
            namespace Sample.Infra.QA.Write.Configurations
            {
                public class WriteConfiguration : IEntityTypeConfiguration<Category>
                {
                    public void Configure(EntityTypeBuilder<Category> builder) { }
                }
            }
            namespace Sample.Infra.QA.Configurations
            {
                public class SharedConfiguration : IEntityTypeConfiguration<Category>
                {
                    public void Configure(EntityTypeBuilder<Category> builder) { }
                }
            }
            """);
        facts.Diagnostics.Should().HaveCount(invalidReadConfiguration ? 1 : 0);
        if (invalidReadConfiguration)
        {
            var diagnostic = facts.Diagnostics.Single();
            diagnostic.Id.Should().Be("DRNCV009");
            diagnostic.Properties["Context"].Should().Be("Sample.Infra.QA.Read.ReadContext");
        }
    }

    [Fact]
    public void Unrelated_Construction_Should_Not_Waive_A_Default_Discovery_Failure()
    {
        var facts = Analyze(Imports + """
            public class QAContext() : DrnContext<QAContext>(null)
            {
                public HelperConfiguration Factory() => new HelperConfiguration(32);
            }
            public class Factory
            {
                public object Make() => new HelperConfiguration(32);
            }
            public class HelperConfiguration(int length) : IEntityTypeConfiguration<Category>
            {
                public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> builder) { }
            }
            """);
        facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCV009");
    }

    [Theory]
    [InlineData("public void Unused(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> builder) => new HelperConfiguration(32).Configure(builder);")]
    [InlineData("public object Unused() => new HelperConfiguration(32);")]
    public void Unrelated_Composite_Member_Should_Not_Claim_Application(string member)
    {
        var facts = Analyze(Imports + $$"""
            public class QAContext() : DrnContext<QAContext>(null);
            public class CategoryConfiguration : IEntityTypeConfiguration<Category>
            {
                public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> builder) { }
                {{member}}
            }
            public class HelperConfiguration(int length) : IEntityTypeConfiguration<Category>
            {
                public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> builder) { }
            }
            """);
        facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCV009");
    }

    [Fact]
    public void Conditional_Composite_Configuration_Should_Remain_Possible_Application()
    {
        Analyze(Imports + """
            public class QAContext() : DrnContext<QAContext>(null);
            public class CategoryConfiguration : IEntityTypeConfiguration<Category>
            {
                public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> builder)
                {
                    if (System.Environment.GetEnvironmentVariable("APPLY_HELPER") == "true")
                        new HelperConfiguration(32).Configure(builder);
                }
            }
            public class HelperConfiguration(int length) : IEntityTypeConfiguration<Category>
            {
                public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> builder) { }
            }
            """).Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("System.Action deferred = () => new HelperConfiguration(32).Configure(builder);")]
    [InlineData("void Deferred() => new HelperConfiguration(32).Configure(builder);")]
    public void Uninvoked_Nested_Configure_Delegation_Should_Not_Waive_Discovery(string nested)
    {
        var facts = Analyze(Imports + $$"""
            public class QAContext() : DrnContext<QAContext>(null);
            public class CategoryConfiguration : IEntityTypeConfiguration<Category>
            {
                public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> builder)
                {
                    {{nested}}
                }
            }
            public class HelperConfiguration(int length) : IEntityTypeConfiguration<Category>
            {
                public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> builder) { }
            }
            """);
        facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCV009");
    }

    [Fact]
    public void A_Discoverable_Composite_Configuration_Should_Allow_A_Parameterized_Helper()
    {
        Analyze(Imports + """
            public class QAContext() : DrnContext<QAContext>(null);
            public class CategoryConfiguration : IEntityTypeConfiguration<Category>
            {
                public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> builder)
                    => new HelperConfiguration(32).Configure(builder);
            }
            public class HelperConfiguration(int length) : IEntityTypeConfiguration<Category>
            {
                public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> builder) { }
            }
            """).Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("ConfigureCore(builder);", false)]
    [InlineData("Forward(builder);", false)]
    [InlineData("void Apply(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> value) => ConfigureCore(value); Apply(builder);", false)]
    [InlineData("", true)]
    [InlineData("Cycle(builder);", false)]
    public void Helper_Delegation_Should_Require_A_Forwarding_Path_From_Configure(string body, bool warning)
    {
        var facts = Analyze(Imports + $$"""
            public class QAContext() : DrnContext<QAContext>(null);
            public class CategoryConfiguration : IEntityTypeConfiguration<Category>
            {
                public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> builder)
                {
                    {{body}}
                }
                private void Forward(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> builder)
                    => ConfigureCore(builder);
                private void ConfigureCore(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> builder)
                    => new HelperConfiguration(32).Configure(builder);
                private void Cycle(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> builder)
                {
                    if (System.Environment.GetEnvironmentVariable("APPLY_HELPER") == "true")
                        ConfigureCore(builder);
                    Cycle(builder);
                }
            }
            public class HelperConfiguration(int length) : IEntityTypeConfiguration<Category>
            {
                public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Category> builder) { }
            }
            """);
        facts.Diagnostics.Should().HaveCount(warning ? 1 : 0);
        if (warning) facts.Diagnostics.Single().Id.Should().Be("DRNCV009");
    }

    [Fact]
    public void Composition_In_One_Context_Area_Should_Not_Waive_An_Explicit_Association_To_Another()
    {
        var facts = Analyze("""
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Metadata.Builders;
            using DRN.Framework.EntityFramework.Context;
            namespace Sample.Infra.QA { public class Category { public int Id { get; set; } } }
            namespace Sample.Infra.QA.Read
            {
                public class ReadContext() : DrnContext<ReadContext>(null);
            }
            namespace Sample.Infra.QA.Read.Configurations
            {
                public class ReadConfiguration : IEntityTypeConfiguration<Category>
                {
                    public void Configure(EntityTypeBuilder<Category> builder)
                        => new HelperConfiguration(32).Configure(builder);
                }
                public class HelperConfiguration(int length) : IEntityTypeConfiguration<Category>
                {
                    public void Configure(EntityTypeBuilder<Category> builder) { }
                }
            }
            namespace Sample.Infra.QA.Write
            {
                public class WriteContext() : DrnContext<WriteContext>(null);
            }
            """, Input("Configuration", "Sample.Infra.QA.Read.Configurations.HelperConfiguration",
            ("Context", "Sample.Infra.QA.Write.WriteContext")));
        facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCV009");
        facts.Diagnostics.Single().Properties["Context"].Should().Be("Sample.Infra.QA.Write.WriteContext");
    }

    [Theory]
    [InlineData("QA/Migrations", false)]
    [InlineData("Other/Migrations", true)]
    public void Migration_And_Designer_Partials_Should_Report_One_Context_Association(string destination, bool conflict)
    {
        var facts = Analyze("""
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Infrastructure;
            using Microsoft.EntityFrameworkCore.Migrations;
            using DRN.Framework.EntityFramework.Context;
            namespace Sample.Infra.QA
            {
                public class QAContext() : DrnContext<QAContext>(null);
            }
            namespace Sample.Infra.QA.Migrations
            {
                public partial class Initial : Migration
                {
                    protected override void Up(MigrationBuilder migrationBuilder) { }
                    protected override void Down(MigrationBuilder migrationBuilder) { }
                }
                [DbContext(typeof(Sample.Infra.QA.QAContext))]
                [Migration("20260101000000_Initial")]
                public partial class Initial
                {
                    protected override void BuildTargetModel(ModelBuilder modelBuilder) { }
                }
            }
            """, Input("Context", "Sample.Infra.QA.QAContext", ("Migrations", destination)));
        facts.Diagnostics.Count(diagnostic => diagnostic.Id == "DRNCV017").Should().Be(conflict ? 1 : 0);
        if (conflict) facts.Diagnostics.Single().Properties["Target"].Should().Be("T:Sample.Infra.QA.Migrations.Initial");
    }

    [Theory]
    [InlineData("QA/Migrations", false)]
    [InlineData("Other/Migrations", true)]
    public void Snapshot_Metadata_Should_Be_Checked_Even_When_Generated(string destination, bool conflict)
    {
        var source = """
            // <auto-generated />
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Infrastructure;
            using DRN.Framework.EntityFramework.Context;
            namespace Sample.Infra.QA
            {
                public class QAContext() : DrnContext<QAContext>(null);
            }
            namespace Sample.Infra.QA.Migrations
            {
                [DbContext(typeof(Sample.Infra.QA.QAContext))]
                public class QASnapshot : ModelSnapshot
                {
                    protected override void BuildModel(ModelBuilder modelBuilder) { }
                }
            }
            """;
        var facts = Analyze(source, Input("Context", "Sample.Infra.QA.QAContext", ("Migrations", destination)));
        facts.Diagnostics.Count(diagnostic => diagnostic.Id == "DRNCV017").Should().Be(conflict ? 1 : 0);
        if (conflict)
        {
            facts.Diagnostics.Single().Location.Kind.Should().Be(LocationKind.ExternalFile);
            facts.Diagnostics.Single().AdditionalLocations.Should().NotBeEmpty();
        }
    }

    [Theory]
    [InlineData("Shop.Persistence.Sales.Migrations", "Infra/Sales/Migrations/Snapshot.cs", false)]
    [InlineData("Shop.Persistence.Infra.Sales.Migrations", "Infra/Sales/Migrations/Snapshot.cs", true)]
    [InlineData("Shop.Persistence.Sales.Migrations", "Sales/Migrations/Snapshot.cs", true)]
    public void Split_Role_Migration_Namespace_Should_Exclude_Only_The_Logical_Role_Prefix(string snapshotNamespace, string logicalPath, bool conflict)
    {
        var facts = Analyze($$"""
            using Microsoft.EntityFrameworkCore;
            using Microsoft.EntityFrameworkCore.Infrastructure;
            using DRN.Framework.EntityFramework.Context;
            namespace Shop.Persistence.Sales
            {
                public class SalesContext() : DrnContext<SalesContext>(null);
            }
            namespace {{snapshotNamespace}}
            {
                [DbContext(typeof(Shop.Persistence.Sales.SalesContext))]
                public class SalesSnapshot : ModelSnapshot
                {
                    protected override void BuildModel(ModelBuilder modelBuilder) { }
                }
            }
            """,
            Input("Role", "Infra", ("NamespacePrefix", "Shop.Persistence"), ("LogicalPathPrefix", "Infra")),
            Input("Context", "Shop.Persistence.Sales.SalesContext", ("Migrations", "Infra/Sales/Migrations")),
            Input("Path", "Models.cs", ("LogicalPath", logicalPath)));
        facts.Diagnostics.Count(diagnostic => diagnostic.Id == "DRNCV017").Should().Be(conflict ? 1 : 0);
        if (conflict)
            facts.Diagnostics.Single().GetMessage().Should().Contain("Infra/Sales/Migrations");
    }

    [Fact]
    public void Semantic_Lookalikes_Should_Not_Be_Repositories_Configurations_Or_Snapshots()
    {
        Analyze("""
            namespace Sample.Infra.QA;
            public class Repository;
            public class Snapshot;
            public interface IEntityTypeConfiguration<T>;
            public class Configuration : IEntityTypeConfiguration<Repository>;
            """).Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Separate_Migration_Assembly_Should_Use_Its_Own_Namespace_And_Exact_Declared_Assembly(bool emitted, bool wrongAssembly)
    {
        var framework = MetadataReference.CreateFromFile(typeof(DrnContext<>).Assembly.Location);
        var utils = MetadataReference.CreateFromFile(typeof(IEntityUtils).Assembly.Location);
        var producer = Compilation("""
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Sample.Infra")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "Sample.Infra")]
            namespace Sample.Infra.QA;
            public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>
            {
                public QAContext() : base(null) { }
                public QAContext(Microsoft.EntityFrameworkCore.DbContextOptions<QAContext> options) : base(options) { }
            }
            """).WithAssemblyName("Sample.Persistence").AddReferences(framework, utils);
        producer.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        MetadataReference reference;
        if (emitted)
        {
            using var image = new MemoryStream();
            var result = producer.Emit(image, options: new Microsoft.CodeAnalysis.Emit.EmitOptions(metadataOnly: true, includePrivateMembers: false));
            result.Success.Should().BeTrue();
            reference = MetadataReference.CreateFromImage(image.ToArray());
        }
        else reference = producer.ToMetadataReference();
        var consumer = Compilation("""
            // <auto-generated />
            namespace Sample.Migrations.QA.Migrations;
            [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(Sample.Infra.QA.QAContext))]
            public class QASnapshot : Microsoft.EntityFrameworkCore.Infrastructure.ModelSnapshot
            {
                protected override void BuildModel(Microsoft.EntityFrameworkCore.ModelBuilder builder) { }
            }
            """, "QASnapshot.cs").WithAssemblyName("Sample.Migrations").AddReferences(framework, utils, reference)
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText("""
                using Microsoft.Extensions.DependencyInjection;
                using DRN.Framework.Utils.DependencyInjection;
                namespace Sample.Migrations;
                public static class MigrationsModule
                {
                    public static IServiceCollection AddMigrationsServices(this IServiceCollection services)
                    {
                        services.AddServicesWithAttributes();
                        return services;
                    }
                }
                """, path: "MigrationsModule.cs"));
        consumer.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        var expectedAssembly = wrongAssembly ? "Sample.OtherMigrations" : "Sample.Migrations";
        var fixture = ConventionTestDependencies.WithRequiredLayers(consumer, "Infra");
        var input = new ProjectDiagnosticRulesTests.RuleFile(ConventionInputs.FileName, $$"""
            [assembly: DRN.Conventions.Version("1")]
            [assembly: DRN.Conventions.Reference("Inventory", Stage = "Complete")]
            [assembly: DRN.Conventions.Path("QASnapshot.cs", LogicalPath = "QA/Migrations/QASnapshot.cs")]
            [assembly: DRN.Conventions.Context("Sample.Infra.QA.QAContext", Migrations = "QA/Migrations", MigrationsAssembly = "{{expectedAssembly}}")]
            """ + "\n" + ConventionTestDependencies.Serialize(fixture.Records));
        var diagnostics = await fixture.Compilation.WithAnalyzers([new SourceKnownConventionAnalyzer()],
            new AnalyzerOptions([input], new Options("Infra", "Sample.Migrations"))).GetAnalyzerDiagnosticsAsync();
        diagnostics.Should().HaveCount(wrongAssembly ? 1 : 0);
        if (!wrongAssembly) return;
        var diagnostic = diagnostics.Single();
        diagnostic.Id.Should().Be("DRNCV017");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().Should().Be("Migration artifact 'QASnapshot' identifies context 'QAContext' inconsistently with declared destination 'Sample.OtherMigrations:QA/Migrations'.");
        diagnostic.Location.Kind.Should().Be(LocationKind.ExternalFile);
        diagnostic.Properties["Context"].Should().Be("Sample.Infra.QA.QAContext");
        var snapshotTree = consumer.SyntaxTrees.Single(tree => tree.FilePath == "QASnapshot.cs");
        diagnostic.AdditionalLocations.Should().OnlyContain(location => location.SourceTree == snapshotTree);
    }

    [Theory]
    [InlineData("Category", false)]
    [InlineData("Other", true)]
    public void Explicit_Service_Repository_Binding_Should_Use_The_Contract_Entity(string repositoryEntity, bool conflict)
    {
        var facts = AnalyzeRole("""
            using DRN.Framework.SharedKernel.Domain;
            using DRN.Framework.SharedKernel.Domain.Repository;
            namespace Sample.Application.QA;
            public class Category : AggregateRoot;
            public class Other : AggregateRoot;
            """ + $$"""
            public class CategoryService(ISourceKnownRepository<{{repositoryEntity}}> repository) : ISourceKnownService<Category>
            {
                public ISourceKnownRepository<Category> Repository => (ISourceKnownRepository<Category>)repository;
            }
            """, "Application");
        facts.Diagnostics.Count(diagnostic => diagnostic.Id == "DRNCV013").Should().Be(conflict ? 1 : 0);
        if (conflict)
            facts.Diagnostics.Single().Location.SourceTree!.GetText().ToString(facts.Diagnostics.Single().Location.SourceSpan)
                .Should().Be("Repository");
    }

    [Fact]
    public void Explicit_Multi_Aggregate_Adapter_Should_Preserve_The_Selected_Service_Repository_Binding()
    {
        var facts = AnalyzeRole("""
            using DRN.Framework.SharedKernel.Domain;
            using DRN.Framework.SharedKernel.Domain.Repository;
            namespace Sample.Application.QA;
            public class Category : AggregateRoot;
            public class Other : AggregateRoot;
            public interface IAdapter : ISourceKnownRepository<Category>, ISourceKnownRepository<Other>;
            public class CategoryService(IAdapter repository) : ISourceKnownService<Category>
            {
                public ISourceKnownRepository<Category> Repository => repository;
            }
            """, "Application");
        facts.Diagnostics.Should().BeEmpty();
    }

    private static ConventionInput Input(string kind, string identity, params (string Key, string Value)[] fields) =>
        new(kind, identity, fields.ToImmutableDictionary(field => field.Key, field => field.Value),
            Location.Create("DrnConventionInputs.g.cs", new(0, 1), new(new(0, 0), new(0, 1))));

    private static ConventionFacts Analyze(string source, params ConventionInput[] records) => AnalyzeRole(source, "Infra", records);

    private static ConventionFacts AnalyzeRole(string source, string role, params ConventionInput[] records)
    {
        var compilation = Compilation(source).WithAssemblyName("Sample." + role)
            .AddReferences(MetadataReference.CreateFromFile(typeof(DrnContext<>).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(IEntityUtils).Assembly.Location));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        var options = new Options(role);
        var project = ConventionProject.Read(compilation, options.GlobalOptions);
        var facts = new ConventionFacts(compilation, project, new([.. records], []), options, CancellationToken.None);
        ConventionPersistenceAnalysis.Analyze(facts);
        return facts;
    }

    private sealed class Options(string role, string? rootNamespace = null) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new ConventionInputTests.Config(new()
        {
            ["build_property.MSBuildProjectName"] = "Sample." + role,
            ["build_property.RootNamespace"] = rootNamespace ?? "Sample." + role
        });
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new ConventionInputTests.Config(new());
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new ConventionInputTests.Config(new());
    }
}
