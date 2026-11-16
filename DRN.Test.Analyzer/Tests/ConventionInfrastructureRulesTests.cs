using DRN.Framework.SharedKernel.Analyzers.Conventions;
using static DRN.Test.Analyzer.Tests.DiagnosticClassificationAnalyzerTests;

namespace DRN.Test.Analyzer.Tests;

public sealed class ConventionInfrastructureRulesTests
{
    private static readonly MetadataReference Framework = MetadataReference.CreateFromFile(
        typeof(DRN.Framework.EntityFramework.Context.DrnContext<>).Assembly.Location);
    private static readonly MetadataReference Utils = MetadataReference.CreateFromFile(
        typeof(DRN.Framework.Utils.Entity.IEntityUtils).Assembly.Location);

    [Theory]
    [DataInlineUnit("Repositories", "QA/Repositories/Types.cs", false)]
    [DataInlineUnit("Repositories", "QA/Types.cs", true)]
    [DataInlineUnit("Other", "QA/Repositories/Types.cs", true)]
    [DataInlineUnit("Repositories", "QA/Repositories/Nested/Types.cs", true)]
    public void Repository_Implementations_Should_Use_Repositories(string ns, string path, bool invalid)
    {
        var facts = Facts($$"""
            namespace Sample.Infra.QA.{{ns}};
            public class OrderEntity : DRN.Framework.SharedKernel.Domain.AggregateRoot;
            public class QAContextNpgsqlDbContextOptionsAttribute : DRN.Framework.EntityFramework.Attributes.NpgsqlDbContextOptionsAttribute;
            [QAContextNpgsqlDbContextOptions]
            public class QAContext() : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>(null);
            public class OrderRepository(QAContext context, DRN.Framework.Utils.Entity.IEntityUtils utils)
                : DRN.Framework.EntityFramework.Domain.SourceKnownRepository<QAContext, OrderEntity>(context, utils);
            """, path);
        ConventionInfrastructureAnalysis.Analyze(facts);
        AssertFolder(facts, invalid);
    }

    [Theory]
    [DataInlineUnit("OrderConfig", "Configurations", "QA/Configurations/Types.cs", false, false)]
    [DataInlineUnit("OrderConfiguration", "Configurations", "QA/Configurations/Types.cs", true, false)]
    [DataInlineUnit("OrderConfig", "Other", "QA/Configurations/Types.cs", false, true)]
    [DataInlineUnit("OrderConfig", "Configurations", "QA/Types.cs", false, true)]
    public void Configurations_Should_Use_Config_Suffix_And_Configurations_Folder(string name, string ns, string path,
        bool invalidName, bool invalidFolder)
    {
        var facts = Facts($$"""
            namespace Sample.Infra.QA.{{ns}};
            public class Model;
            public class {{name}} : Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<Model>
            {
                public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Model> builder) { }
            }
            """, path);
        ConventionNamingAnalysis.Analyze(facts);
        ConventionInfrastructureAnalysis.Analyze(facts);
        facts.Diagnostics.Count(item => item.Id == "DRNCV024").Should().Be(invalidName ? 1 : 0);
        facts.Diagnostics.Count(item => item.Id == "DRNCV028").Should().Be(invalidFolder ? 1 : 0);
        facts.Diagnostics.Should().HaveCount((invalidName ? 1 : 0) + (invalidFolder ? 1 : 0));
    }

    [Theory]
    [DataInlineUnit("QAContextOptions", false)]
    [DataInlineUnit("QAContextNpgsqlDbContextOptionsAttribute", false)]
    [DataInlineUnit("QAContextNpgsqlDbContextOptions", false)]
    [DataInlineUnit("OtherContextOptionsAttribute", true)]
    [DataInlineUnit("QAContextSettingsAttribute", true)]
    public void Custom_Options_Should_Start_With_Context_And_End_With_Options(string name, bool invalid)
    {
        var facts = Facts($$"""
            namespace Sample.Infra.QA;
            public class {{name}} : DRN.Framework.EntityFramework.Attributes.NpgsqlDbContextOptionsAttribute;
            [{{name}}]
            public class QAContext() : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>(null);
            """, "QA/Types.cs");
        ConventionInfrastructureAnalysis.Analyze(facts);
        if (!invalid) facts.Diagnostics.Should().BeEmpty();
        else facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCV029");
    }

    [Theory]
    [DataInlineUnit("Migrations", "QA/Migrations/Snapshot.cs", false, false)]
    [DataInlineUnit("Migrations", "QA/Other/Snapshot.cs", false, true)]
    [DataInlineUnit("Other", "QA/Migrations/Snapshot.cs", false, true)]
    [DataInlineUnit("Migrations", "QA/Other/Snapshot.cs", true, true)]
    public void Drn_Snapshots_Should_Use_Migrations_Including_Generated_Artifacts(string ns, string path, bool generated, bool invalid)
    {
        var compilation = Compilation("""
            namespace Sample.Infra.QA;
            public class QAContextNpgsqlDbContextOptionsAttribute : DRN.Framework.EntityFramework.Attributes.NpgsqlDbContextOptionsAttribute;
            [QAContextNpgsqlDbContextOptions]
            public class QAContext() : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>(null);
            """, "Context.cs").AddSyntaxTrees(CSharpSyntaxTree.ParseText((generated ? "// <auto-generated/>\n" : "") + $$"""
            namespace Sample.Infra.QA.{{ns}};
            [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(Sample.Infra.QA.QAContext))]
            public class QAContextModelSnapshot : Microsoft.EntityFrameworkCore.Infrastructure.ModelSnapshot
            {
                protected override void BuildModel(Microsoft.EntityFrameworkCore.ModelBuilder builder) { }
            }
            """, path: "Linked/Snapshot.cs"));
        var facts = Facts(compilation, [PathInput("Context.cs", "QA/Context.cs"), PathInput("Linked/Snapshot.cs", path)]);
        ConventionInfrastructureAnalysis.Analyze(facts);
        AssertFolder(facts, invalid);
        if (generated && invalid) facts.Diagnostics.Single().Location.SourceTree!.FilePath.Should().Be("Context.cs");
    }

    private static void AssertFolder(ConventionFacts facts, bool invalid)
    {
        if (invalid) facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCV028");
        else facts.Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("QA/Migrations/Initial.cs", false)]
    [DataInlineUnit("QA/Initial.cs", true)]
    public void Drn_Migrations_Should_Use_Migrations_Folder(string path, bool invalid)
    {
        var facts = Facts("""
            namespace Sample.Infra.QA
            {
                public class QAContextNpgsqlDbContextOptionsAttribute : DRN.Framework.EntityFramework.Attributes.NpgsqlDbContextOptionsAttribute;
                [QAContextNpgsqlDbContextOptions]
                public class QAContext() : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>(null);
            }
            namespace Sample.Infra.QA.Migrations
            {
                [Microsoft.EntityFrameworkCore.Infrastructure.DbContext(typeof(Sample.Infra.QA.QAContext))]
                public class Initial : Microsoft.EntityFrameworkCore.Migrations.Migration
                {
                    protected override void Up(Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder builder) { }
                    protected override void Down(Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder builder) { }
                }
            }
            """, path);
        ConventionInfrastructureAnalysis.Analyze(facts);
        AssertFolder(facts, invalid);
    }

    [Theory]
    [DataInlineUnit("", true)]
    [DataInlineUnit("[QAContextNpgsqlDbContextOptions]", false)]
    [DataInlineUnit("[Lookalike]", true)]
    [DataInlineUnit("[DRN.Framework.EntityFramework.Attributes.DrnContextDefaults]", true)]
    public void Concrete_Context_Should_Declare_Real_Custom_Options(string attribute, bool missing)
    {
        var facts = Facts($$"""
            namespace Sample.Infra.QA;
            public class QAContextNpgsqlDbContextOptionsAttribute : DRN.Framework.EntityFramework.Attributes.NpgsqlDbContextOptionsAttribute;
            public class LookalikeAttribute : System.Attribute;
            {{attribute}}
            public class QAContext() : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>(null);
            """, "QA/Types.cs");
        ConventionInfrastructureAnalysis.Analyze(facts);
        if (!missing) facts.Diagnostics.Should().BeEmpty();
        else
        {
            var diagnostic = facts.Diagnostics.Should().ContainSingle().Which;
            diagnostic.Id.Should().Be("DRNCV030");
            diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
            diagnostic.Properties["Target"].Should().Be("T:Sample.Infra.QA.QAContext");
            diagnostic.GetMessage().Should().Contain("QAContextNpgsqlDbContextOptionsAttribute");
            diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("QAContext");
        }
    }

    [Theory]
    [DataInlineUnit("", true)]
    [DataInlineUnit("[QAContextNpgsqlDbContextOptions]", false)]
    public void Inherited_Custom_Options_Should_Not_Replace_A_Direct_Declaration(string attribute, bool missing)
    {
        var facts = Facts($$"""
            namespace Sample.Infra.QA;
            public class QAContextNpgsqlDbContextOptionsAttribute : DRN.Framework.EntityFramework.Attributes.NpgsqlDbContextOptionsAttribute;
            [QAContextNpgsqlDbContextOptions]
            public abstract class ContextBase<T>() : DRN.Framework.EntityFramework.Context.DrnContext<T>(null)
                where T : DRN.Framework.EntityFramework.Context.DrnContext<T>, new();
            {{attribute}}
            public class QAContext : ContextBase<QAContext>;
            """, "QA/Types.cs");
        ConventionInfrastructureAnalysis.Analyze(facts);
        facts.Diagnostics.Count(item => item.Id == "DRNCV030").Should().Be(missing ? 1 : 0);
        facts.Diagnostics.Should().OnlyContain(item => item.Id == "DRNCV030");
    }

    [Fact]
    public void Abstract_Open_And_Unrelated_Contexts_Should_Not_Require_Options()
    {
        var facts = Facts("""
            namespace Sample.Infra.QA;
            public abstract class ContextBase<T>() : DRN.Framework.EntityFramework.Context.DrnContext<T>(null)
                where T : DRN.Framework.EntityFramework.Context.DrnContext<T>, new();
            public class OpenContext<T>() : DRN.Framework.EntityFramework.Context.DrnContext<OpenContext<T>>(null);
            public class PlainContext : Microsoft.EntityFrameworkCore.DbContext;
            public class OtherContext;
            """, "QA/Types.cs");
        ConventionInfrastructureAnalysis.Analyze(facts);
        facts.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Options_On_Another_Partial_Declaration_Should_Count_Through_Indirect_Inheritance()
    {
        var compilation = Compilation("""
            namespace Sample.Infra.QA;
            public abstract class OptionsBaseAttribute : DRN.Framework.EntityFramework.Attributes.NpgsqlDbContextOptionsAttribute;
            public class QAContextNpgsqlDbContextOptionsAttribute : OptionsBaseAttribute;
            public partial class QAContext() : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>(null);
            """, "Context.cs").AddSyntaxTrees(CSharpSyntaxTree.ParseText("""
            namespace Sample.Infra.QA;
            [QAContextNpgsqlDbContextOptions]
            public partial class QAContext;
            """, path: "Options.cs"));
        var facts = Facts(compilation, [PathInput("Context.cs", "QA/Context.cs"), PathInput("Options.cs", "QA/Options.cs")]);
        ConventionInfrastructureAnalysis.Analyze(facts);
        facts.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Identity_Context_Should_Also_Require_Direct_Custom_Options()
    {
        var facts = Facts("""
            namespace Sample.Infra.QA;
            public class QAContext() : DRN.Framework.EntityFramework.Context.Identity.DrnContextIdentity<QAContext, Microsoft.AspNetCore.Identity.IdentityUser>(null);
            """, "QA/Context.cs");
        ConventionInfrastructureAnalysis.Analyze(facts);
        facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCV030");
    }

    private static ConventionFacts Facts(string source, string path) =>
        Facts(Compilation(source), [PathInput("Models.cs", path)]);

    private static ConventionFacts Facts(CSharpCompilation compilation, ImmutableArray<ConventionInput> paths)
    {
        compilation = compilation.WithAssemblyName("Sample.Infra").AddReferences(Framework, Utils);
        compilation.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        return new(compilation, new(true, "Sample.Infra", "Infra", "Sample.Infra", compilation.AssemblyName!, []),
            new(paths, []), new Options(), CancellationToken.None);
    }

    private static ConventionInput PathInput(string source, string logical) => new("Path", source,
        ImmutableDictionary<string, string>.Empty.Add("LogicalPath", logical), Location.None);

    private sealed class Options : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new ConventionInputTests.Config(new());
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
    }
}
