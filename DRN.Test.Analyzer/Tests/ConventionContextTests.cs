using DRN.Framework.SharedKernel.Analyzers.Conventions;
using static DRN.Test.Analyzer.Tests.DiagnosticClassificationAnalyzerTests;

namespace DRN.Test.Analyzer.Tests;

public sealed class ConventionContextTests
{
    [Fact]
    public void Concrete_Ef_Contexts_Should_Require_IDrnContext_Without_Diagnosing_Abstract_Bases()
    {
        var facts = Facts("""
            namespace Sample.Infra.QA;
            public abstract class ExternalBaseContext : Microsoft.EntityFrameworkCore.DbContext;
            public class QAContext : ExternalBaseContext;
            public class GenericContext<T> : ExternalBaseContext;
            public class LookalikeContext;
            """);
        ConventionContextAnalysis.Analyze(facts);
        var diagnostics = facts.Diagnostics.Where(item => item.Id == "DRNCV020").ToArray();
        diagnostics.Should().HaveCount(2);
        diagnostics.Select(item => item.Properties["Target"]).Should().BeEquivalentTo(
            "T:Sample.Infra.QA.QAContext", "T:Sample.Infra.QA.GenericContext`1");
        diagnostics.Should().OnlyContain(item => item.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Direct_And_Inherited_Context_Interfaces_Should_Satisfy_Policy_Without_A_Drn_Base()
    {
        var facts = Facts("""
            namespace Sample.Infra.QA;
            public class QAContext : Microsoft.EntityFrameworkCore.DbContext, DRN.Framework.EntityFramework.Context.IDrnContext
            {
                public Microsoft.EntityFrameworkCore.DbSet<T> GetEntities<T>() where T : DRN.Framework.SharedKernel.Domain.SourceKnownEntity => Set<T>();
                public void ConfigureDesignTimeServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services) { }
            }
            public class ReadContext : QAContext;
            """);
        ConventionContextAnalysis.Analyze(facts);
        facts.Diagnostics.Should().NotContain(item => item.Id == "DRNCV020" || item.Id == "DRNCV005");
    }

    [Fact]
    public void Context_Metadata_Should_Recognize_A_Noncompliant_Ef_Context_And_Report_The_Policy()
    {
        var facts = Facts("namespace Sample.Infra.QA; public class QAContext : Microsoft.EntityFrameworkCore.DbContext;",
            new ConventionInput("Context", "Sample.Infra.QA.QAContext", ImmutableDictionary<string, string>.Empty
                .Add("Repositories", "QA/Repositories").Add("Migrations", "QA/Migrations"), Location.None));
        ConventionContextAnalysis.Analyze(facts);
        facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCV020");
    }

    [Theory]
    [InlineData("QAContext", false)]
    [InlineData("QAReadContext", false)]
    [InlineData("QAWriteContext", false)]
    [InlineData("Context", true)]
    [InlineData("QAStore", true)]
    public void Context_Names_Should_Require_A_Nonempty_Stem_Without_Limiting_Cqrs(string name, bool warning)
    {
        var facts = Facts(ContextSource(name));
        ConventionContextAnalysis.Analyze(facts);
        var diagnostics = facts.Diagnostics.Where(item => item.Id == "DRNCV004").ToArray();
        diagnostics.Should().HaveCount(warning ? 1 : 0);
        if (!warning) return;
        diagnostics[0].GetMessage().Should().Contain(name);
        diagnostics[0].Location.SourceTree!.GetText().ToString(diagnostics[0].Location.SourceSpan).Should().Be(name);
    }

    [Fact]
    public void Context_Lookalikes_And_Open_Abstract_Intermediates_Should_Not_Be_Diagnosed()
    {
        var facts = Facts("""
            namespace Sample.Infra.QA;
            public class Store;
            public abstract class BaseStore<T> : DRN.Framework.EntityFramework.Context.DrnContext<T>
                where T : BaseStore<T>, new()
            {
                protected BaseStore(Microsoft.EntityFrameworkCore.DbContextOptions<T> options) : base(options) { }
            }
            """);
        ConventionContextAnalysis.Analyze(facts);
        facts.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Cqrs_Context_Alias_Should_Require_A_Reasoned_Owner_Without_Limiting_Context_Count()
    {
        var source = ContextSource("QAReadContext") + ContextSource("QAWriteContext").Replace("namespace Sample.Infra.QA;", "");
        var unmapped = Facts(source);
        ConventionContextAnalysis.Analyze(unmapped);
        unmapped.Diagnostics.Count(item => item.Id == "DRNCV001" && item.GetMessage().Contains("alias")).Should().Be(2);
        var fields = ImmutableDictionary<string, string>.Empty.Add("Subdomain", "QA").Add("Reason", "Separate CQRS context in QA.");
        var mapped = Facts(source, new ConventionInput("Context", "Sample.Infra.QA.QAReadContext", fields, Location.None),
            new ConventionInput("Context", "Sample.Infra.QA.QAWriteContext", fields, Location.None));
        ConventionContextAnalysis.Analyze(mapped);
        mapped.Diagnostics.Should().NotContain(item => item.Id == "DRNCV001");
    }

    [Fact]
    public void Missing_Options_Constructor_Should_Have_One_Context_Diagnostic()
    {
        var facts = Facts("""
            namespace Sample.Infra.QA;
            public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>
            {
                public QAContext() : base(null) { }
            }
            """);
        ConventionContextAnalysis.Analyze(facts);
        facts.Diagnostics.Where(item => item.Id == "DRNCV005").Should().ContainSingle();
    }

    [Fact]
    public void ByRef_Options_Overload_Should_Not_Satisfy_The_Conventional_Constructor_Signature()
    {
        var facts = Facts("""
            namespace Sample.Infra.QA;
            public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>
            {
                public QAContext() : base(null) { }
                public QAContext(ref Microsoft.EntityFrameworkCore.DbContextOptions<QAContext> options) : base(options) { }
            }
            """);
        ConventionContextAnalysis.Analyze(facts);
        facts.Diagnostics.Where(item => item.Id == "DRNCV005").Should().ContainSingle();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Ef_Registration_Should_Accept_Options_With_Additional_Injected_Dependencies(bool conventions, bool optionsLast)
    {
        var registration = conventions
            ? "DRN.Framework.EntityFramework.Extensions.ServiceCollectionExtensions.AddDbContextWithConventions"
            : "Microsoft.Extensions.DependencyInjection.EntityFrameworkServiceCollectionExtensions.AddDbContext";
        var parameters = optionsLast
            ? "ITenant tenant, Microsoft.EntityFrameworkCore.DbContextOptions<QAContext> options"
            : "Microsoft.EntityFrameworkCore.DbContextOptions<QAContext> options, ITenant tenant";
        var facts = Facts($$"""
            namespace Sample.Infra.QA;
            public interface ITenant;
            public class QAContext : Microsoft.EntityFrameworkCore.DbContext, DRN.Framework.EntityFramework.Context.IDrnContext
            {
                public QAContext({{parameters}}) : base(options) { }
                public Microsoft.EntityFrameworkCore.DbSet<T> GetEntities<T>() where T : DRN.Framework.SharedKernel.Domain.SourceKnownEntity => Set<T>();
                public void ConfigureDesignTimeServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services) { }
            }
            public static class Composition
            {
                public static void Register(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                    => {{registration}}<QAContext>(services);
            }
            """);
        ConventionContextAnalysis.Analyze(facts);
        facts.Diagnostics.Should().NotContain(item => item.Id == "DRNCV005");
    }

    [Fact]
    public void Drn_Design_Time_Factory_Should_Still_Require_An_Options_Only_Constructor()
    {
        var facts = Facts("""
            namespace Sample.Infra.QA;
            public interface ITenant;
            public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>
            {
                public QAContext() : base(null) { }
                public QAContext(Microsoft.EntityFrameworkCore.DbContextOptions<QAContext> options, ITenant tenant) : base(options) { }
            }
            """);
        ConventionContextAnalysis.Analyze(facts);
        facts.Diagnostics.Where(item => item.Id == "DRNCV005").Should().ContainSingle();
    }

    [Fact]
    public void Reasoned_Custom_Factory_Should_Preserve_The_Construction_Exception()
    {
        var facts = Facts("""
            namespace Sample.Infra.QA;
            public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>
            {
                public QAContext() : base(null) { }
            }
            """, new ConventionInput("Context", "Sample.Infra.QA.QAContext",
                ImmutableDictionary<string, string>.Empty.Add("Factory", "Reviewed external factory")
                    .Add("Reason", "Host supplies options through its tested factory."), Location.None));
        ConventionContextAnalysis.Analyze(facts);
        facts.Diagnostics.Should().NotContain(item => item.Id == "DRNCV005");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Configure(builder);")]
    public void Provider_Analysis_Should_Not_Confuse_Local_Absence_With_Complete_Composition(string optionsBody)
    {
        var facts = Facts(ContextSource("QAContext") + $$"""

            public static class Composition
            {
                public static void Register(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
                {
                    Microsoft.Extensions.DependencyInjection.EntityFrameworkServiceCollectionExtensions.AddDbContext<QAContext>(
                        services, builder => { {{optionsBody}} });
                }
                private static void Configure(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder builder) { }
            }
            """);
        ConventionContextAnalysis.Analyze(facts);
        facts.Diagnostics.Should().NotContain(item => item.Id == "DRNCV006");
    }

    [Theory]
    [InlineData("options", false)]
    [InlineData("null", true)]
    [InlineData("new Microsoft.EntityFrameworkCore.DbContextOptions<QAContext>()", true)]
    [InlineData("Configure(options)", false)]
    public void Selected_Drn_Factory_Should_Report_Provably_Discarded_Provider_Options(string forwarded, bool warning)
    {
        var facts = Facts(DiscardingContext(forwarded));
        ConventionContextAnalysis.Analyze(facts);
        var diagnostics = facts.Diagnostics.Where(item => item.Id == "DRNCV006").ToArray();
        diagnostics.Should().HaveCount(warning ? 1 : 0);
        if (!warning) return;
        diagnostics[0].GetMessage().Should().Contain("QAContext").And.Contain("Npgsql");
        diagnostics[0].Location.SourceTree!.GetText().ToString(diagnostics[0].Location.SourceSpan).Should().Be("QAContext");
        facts.Diagnostics.Should().NotContain(item => item.Id == "DRNCV005");
    }

    [Fact]
    public void Custom_OnConfiguring_Should_Prevent_A_Provider_Absence_Claim()
    {
        var facts = Facts(DiscardingContext("null", """
            protected override void OnConfiguring(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder builder)
            {
                ConfigureProvider(builder);
            }
            private static void ConfigureProvider(Microsoft.EntityFrameworkCore.DbContextOptionsBuilder builder) { }
            """));
        ConventionContextAnalysis.Analyze(facts);
        facts.Diagnostics.Should().NotContain(item => item.Id == "DRNCV006");
    }

    [Fact]
    public void Reasoned_Custom_Factory_Should_Defer_The_Drn_Provider_Path()
    {
        var facts = Facts(DiscardingContext("null"), new ConventionInput("Context", "Sample.Infra.QA.QAContext",
            ImmutableDictionary<string, string>.Empty.Add("Factory", "Alternative reviewed construction")
                .Add("Reason", "Model validation covers the independently configured host factory."), Location.None));
        ConventionContextAnalysis.Analyze(facts);
        facts.Diagnostics.Should().NotContain(item => item.Id == "DRNCV006");
    }

    private static string DiscardingContext(string forwarded, string members = "") => $$"""
        namespace Sample.Infra.QA;
        public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>
        {
            public QAContext() : base(null) { }
            public QAContext(Microsoft.EntityFrameworkCore.DbContextOptions<QAContext> options) : base({{forwarded}}) { }
            private static Microsoft.EntityFrameworkCore.DbContextOptions<QAContext> Configure(
                Microsoft.EntityFrameworkCore.DbContextOptions<QAContext> options) => options;
            {{members}}
        }
        """;

    [Theory]
    [InlineData("", true)]
    [InlineData("return;", true)]
    [InlineData("base.OnModelCreating(model);", false)]
    [InlineData("Forward(model);", false)]
    [InlineData("Unknown(model);", false)]
    public void Base_Model_Proof_Should_Distinguish_Omission_From_Forwarding_And_Unknown_Helpers(string body, bool warning)
    {
        var source = ContextSource("QAContext", $$"""
            protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder model) { {{body}} }
            private void Forward(Microsoft.EntityFrameworkCore.ModelBuilder model) { base.OnModelCreating(model); }
            protected virtual void Unknown(Microsoft.EntityFrameworkCore.ModelBuilder model) { }
            """);
        var facts = Facts(source);
        ConventionContextAnalysis.Analyze(facts);
        facts.Diagnostics.Count(item => item.Id == "DRNCV007").Should().Be(warning ? 1 : 0);
    }

    [Fact]
    public void Destinations_Should_Be_Tracked_Metadata_Without_Migration_Files()
    {
        var input = new ConventionInput("Context", "Sample.Infra.QA.QAContext",
            ImmutableDictionary<string, string>.Empty.Add("Repositories", "QA/Repositories")
                .Add("Migrations", "QA/Migrations"), Location.None);
        var facts = Facts(ContextSource("QAContext"), input);
        ConventionContextAnalysis.Analyze(facts);
        facts.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Missing_Destinations_Should_Report_Each_Kind_And_Bind_The_Context()
    {
        var facts = Facts(ContextSource("QAContext"));
        ConventionContextAnalysis.Analyze(facts);
        var diagnostics = facts.Diagnostics.Where(item => item.Id == "DRNCV010").ToArray();
        diagnostics.Should().HaveCount(2);
        diagnostics.Should().OnlyContain(item => item.Properties["Target"] == "T:Sample.Infra.QA.QAContext");
        diagnostics.Select(item => item.GetMessage()).Should().Contain(item => item.Contains("QA/Repositories"))
            .And.Contain(item => item.Contains("QA/Migrations"));
    }

    private static string ContextSource(string name, string members = "") => $$"""
        namespace Sample.Infra.QA;
        public class {{name}} : DRN.Framework.EntityFramework.Context.DrnContext<{{name}}>
        {
            public {{name}}() : base(null) { }
            public {{name}}(Microsoft.EntityFrameworkCore.DbContextOptions<{{name}}> options) : base(options) { }
            {{members}}
        }
        """;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Identity_Context_Should_Preserve_Base_Model_And_Reasoned_Prefix_Alias(bool forwards)
    {
        var facts = Facts($$"""
            namespace Sample.Infra.Identity;
            public class AuthenticationUser : Microsoft.AspNetCore.Identity.IdentityUser;
            [DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomain("Identity", Reason = "Preserve the deployed authentication context identity.")]
            public class DrnIdentityContext : DRN.Framework.EntityFramework.Context.Identity.DrnContextIdentity<DrnIdentityContext, AuthenticationUser>
            {
                public DrnIdentityContext() : base(null) { }
                public DrnIdentityContext(Microsoft.EntityFrameworkCore.DbContextOptions<DrnIdentityContext> options) : base(options) { }
                protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder builder)
                {
                    {{(forwards ? "base.OnModelCreating(builder);" : "")}}
                }
            }
            """, new ConventionInput("Context", "Sample.Infra.Identity.DrnIdentityContext", ImmutableDictionary<string, string>.Empty
                .Add("Repositories", "Identity/Repositories").Add("Migrations", "Identity/Migrations"), Location.None));
        ConventionContextAnalysis.Analyze(facts);
        facts.Diagnostics.Should().HaveCount(forwards ? 0 : 1);
        if (forwards) return;
        var diagnostic = facts.Diagnostics.Single();
        diagnostic.Id.Should().Be("DRNCV007");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Warning);
        diagnostic.GetMessage().Should().Be("Override 'OnModelCreating' has a proven path that omits the required base model configuration.");
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan).Should().Be("OnModelCreating");
    }

    [Fact]
    public void DataProtection_Shaped_Schema_Customization_Should_Preserve_The_Base_Call()
    {
        var facts = Facts("""
            namespace Sample.Infra.DataProtection;
            [DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomain("DataProtection", Reason = "Preserve the deployed key storage context identity.")]
            public class DrnDataProtectionContext : DRN.Framework.EntityFramework.Context.DrnContext<DrnDataProtectionContext>
            {
                public DrnDataProtectionContext() : base(null) { }
                public DrnDataProtectionContext(Microsoft.EntityFrameworkCore.DbContextOptions<DrnDataProtectionContext> options) : base(options) { }
                protected override void OnModelCreating(Microsoft.EntityFrameworkCore.ModelBuilder builder)
                {
                    base.OnModelCreating(builder);
                    Microsoft.EntityFrameworkCore.RelationalModelBuilderExtensions.HasDefaultSchema(builder, "__drn_data_protection_context");
                }
            }
            """, new ConventionInput("Context", "Sample.Infra.DataProtection.DrnDataProtectionContext", ImmutableDictionary<string, string>.Empty
                .Add("Repositories", "DataProtection/Repositories").Add("Migrations", "DataProtection/Migrations"), Location.None));
        ConventionContextAnalysis.Analyze(facts);
        facts.Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Closed_Generic_And_Nested_Contexts_Should_Use_Constructed_Options(bool nested)
    {
        var context = """
            public class QAContext : Intermediate<QAContext>
            {
                public QAContext() : base(null) { }
                public QAContext(Microsoft.EntityFrameworkCore.DbContextOptions<QAContext> options) : base(options) { }
            }
            """;
        var source = """
            namespace Sample.Infra.QA;
            public abstract class Intermediate<T> : DRN.Framework.EntityFramework.Context.DrnContext<T>
                where T : Intermediate<T>, new()
            {
                protected Intermediate(Microsoft.EntityFrameworkCore.DbContextOptions<T> options) : base(options) { }
            }
            """ + (nested ? "public class Container { " + context + " }" : context);
        var identity = "Sample.Infra.QA." + (nested ? "Container+" : "") + "QAContext";
        var facts = Facts(source, new ConventionInput("Context", identity, ImmutableDictionary<string, string>.Empty
            .Add("Repositories", "QA/Repositories").Add("Migrations", "QA/Migrations"), Location.None));
        ConventionContextAnalysis.Analyze(facts);
        facts.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Direct_And_Inherited_IDrnContext_Should_Require_Context_Names_Without_Inventing_A_Factory()
    {
        var facts = Facts("""
            namespace Sample.Infra.QA;
            public class Store : Microsoft.EntityFrameworkCore.DbContext, DRN.Framework.EntityFramework.Context.IDrnContext
            {
                public Microsoft.EntityFrameworkCore.DbSet<T> GetEntities<T>() where T : DRN.Framework.SharedKernel.Domain.SourceKnownEntity => Set<T>();
                public void ConfigureDesignTimeServices(Microsoft.Extensions.DependencyInjection.IServiceCollection services) { }
            }
            public class Descendant : Store;
            """);
        ConventionContextAnalysis.Analyze(facts);
        var names = facts.Diagnostics.Where(item => item.Id == "DRNCV004").ToArray();
        names.Should().HaveCount(2);
        names.Select(item => item.GetMessage()).Should().BeEquivalentTo(
            "Context 'Store' must have a nonempty name ending in 'Context'.",
            "Context 'Descendant' must have a nonempty name ending in 'Context'.");
        names.Should().OnlyContain(item => item.Severity == DiagnosticSeverity.Warning);
        facts.Diagnostics.Should().NotContain(item => item.Id == "DRNCV005" || item.Id == "DRNCV006");
    }

    [Fact]
    public void Compiler_New_Constraint_Error_Should_Not_Be_Duplicated_By_Construction_Diagnostic()
    {
        var compilation = Compilation("""
            namespace Sample.Infra.QA;
            public class QAContext : DRN.Framework.EntityFramework.Context.DrnContext<QAContext>
            {
                private QAContext() : base(null) { }
            }
            """).AddReferences(MetadataReference.CreateFromFile(typeof(DRN.Framework.EntityFramework.Context.DrnContext<>).Assembly.Location));
        compilation.GetDiagnostics().Where(item => item.Severity == DiagnosticSeverity.Error).Should().ContainSingle()
            .Which.Id.Should().Be("CS0310");
        var facts = new ConventionFacts(compilation, new(true, "Sample.Infra", "Infra", "Sample.Infra", compilation.AssemblyName!, []),
            new([], []), new Options(), CancellationToken.None);
        ConventionContextAnalysis.Analyze(facts);
        facts.Diagnostics.Should().NotContain(item => item.Id == "DRNCV005");
    }

    [Fact]
    public void A_Context_Inside_An_Open_Generic_Container_Should_Not_Be_Treated_As_A_Constructible_Context()
    {
        var facts = Facts("""
            namespace Sample.Infra.QA;
            public class Container<T>
            {
                public class Store : DRN.Framework.EntityFramework.Context.DrnContext<Store>
                {
                    public Store() : base(null) { }
                    public Store(Microsoft.EntityFrameworkCore.DbContextOptions<Store> options) : base(options) { }
                }
            }
            """);
        ConventionContextAnalysis.Analyze(facts);
        facts.Diagnostics.Should().BeEmpty();
    }

    private static ConventionFacts Facts(string source, params ConventionInput[] inputs)
    {
        var compilation = Compilation(source, "QA/Context.cs").AddReferences(
            MetadataReference.CreateFromFile(typeof(DRN.Framework.EntityFramework.Context.DrnContext<>).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(DRN.Framework.Utils.Settings.IAppSettings).Assembly.Location));
        compilation.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        var project = new ConventionProject(true, "Sample.Infra", "Infra", "Sample.Infra", compilation.AssemblyName!, []);
        return new(compilation, project, new([.. inputs], []), new Options(), CancellationToken.None);
    }

    private sealed class Options : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new ConventionInputTests.Config(new());
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => GlobalOptions;
    }
}
