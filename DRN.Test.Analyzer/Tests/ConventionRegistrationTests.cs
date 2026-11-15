using DRN.Framework.SharedKernel.Analyzers.Conventions;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using static DRN.Test.Analyzer.Tests.DiagnosticClassificationAnalyzerTests;

namespace DRN.Test.Analyzer.Tests;

public sealed class ConventionRegistrationTests
{
    private const string Prefix = """
        using DRN.Framework.SharedKernel.Domain;
        using DRN.Framework.SharedKernel.Domain.Repository;
        using DRN.Framework.Utils.DependencyInjection;
        using DRN.Framework.Utils.DependencyInjection.Attributes;
        using Microsoft.Extensions.DependencyInjection;
        namespace Sample.Application.QA;
        public class Category : AggregateRoot;
        public interface ICategoryService : ISourceKnownService<Category>;

        """;

    private const string Scan = """
        public static class Module
        {
            public static void Register(IServiceCollection services) => services.AddServicesWithAttributes(typeof(Module).Assembly);
        }
        """;

    [Theory]
    [InlineData("", true)]
    [InlineData("[Scoped<ICategoryService>]", false)]
    public void Declared_Scanned_Service_Should_Require_An_Effective_Registration(string attribute, bool missing)
    {
        var facts = Analyze(Prefix + attribute + """
            public class CategoryService : ICategoryService
            {
                public ISourceKnownRepository<Category> Repository => null!;
            }
            """ + Scan);
        facts.Diagnostics.Count(diagnostic => diagnostic.Id == "DRNCV012").Should().Be(missing ? 1 : 0);
        if (missing)
        {
            var diagnostic = facts.Diagnostics.Single();
            diagnostic.Location.Kind.Should().Be(LocationKind.ExternalFile);
            diagnostic.GetMessage().Should().Contain("CategoryService").And.Contain("ICategoryService");
            diagnostic.Properties["Target"].Should().Be("T:Sample.Application.QA.CategoryService");
        }
    }

    [Fact]
    public void Inherited_Lifetime_Should_Count_Without_Duplicating_The_Attribute()
    {
        Analyze(Prefix + """
            [Scoped<ICategoryService>]
            public abstract class BaseService : ICategoryService
            {
                public ISourceKnownRepository<Category> Repository => null!;
            }
            public class CategoryService : BaseService;
            """ + Scan).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Manual_Registration_Should_Satisfy_The_Declared_Service()
    {
        Analyze(Prefix + """
            public class CategoryService : ICategoryService
            {
                public ISourceKnownRepository<Category> Repository => null!;
            }
            public static class Module
            {
                public static void Register(IServiceCollection services)
                {
                    services.AddServicesWithAttributes(typeof(Module).Assembly);
                    services.AddScoped<ICategoryService, CategoryService>();
                }
            }
            """).Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("read", false)]
    [InlineData("write", true)]
    public void Keyed_Selection_Should_Match_The_Declared_Key(string key, bool missing)
    {
        var facts = Analyze(Prefix + """
            [ScopedWithKey<ICategoryService>("read")]
            public class CategoryService : ICategoryService
            {
                public ISourceKnownRepository<Category> Repository => null!;
            }
            """ + Scan, key: key);
        facts.Diagnostics.Count(diagnostic => diagnostic.Id == "DRNCV012").Should().Be(missing ? 1 : 0);
    }

    [Fact]
    public void Generic_Lifetime_Optional_Key_Should_Use_The_Constructor_Parameter_Position()
    {
        Analyze(Prefix + """
            [Lifetime<ICategoryService>(ServiceLifetime.Scoped, key: "read")]
            public class CategoryService : ICategoryService
            {
                public ISourceKnownRepository<Category> Repository => null!;
            }
            """ + Scan, key: "read").Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Unobserved_External_Composition_Should_Not_Be_Reported_Missing()
    {
        Analyze(Prefix + """
            public class CategoryService : ICategoryService
            {
                public ISourceKnownRepository<Category> Repository => null!;
            }
            """).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void A_Composition_Helper_Should_Prevent_A_False_Missing_Registration()
    {
        Analyze(Prefix + """
            public class CategoryService : ICategoryService
            {
                public ISourceKnownRepository<Category> Repository => null!;
            }
            public static class Module
            {
                public static void Register(IServiceCollection services)
                {
                    services.AddServicesWithAttributes(typeof(Module).Assembly);
                    RegisterFactory(services);
                }
                private static void RegisterFactory(IServiceCollection services) =>
                    services.AddScoped<ICategoryService>(provider => new CategoryService());
            }
            """).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Reasoned_Custom_Composition_Should_Remain_A_Runtime_Verification_Obligation()
    {
        Analyze(Prefix + """
            public class CategoryService : ICategoryService
            {
                public ISourceKnownRepository<Category> Repository => null!;
            }
            """ + Scan, reason: "Application composition selects a factory verified by its container test.")
            .Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Lifetime_Lookalike_Should_Not_Satisfy_A_Declared_Scanned_Service()
    {
        Analyze(Prefix + """
            public class ScopedAttribute<T> : System.Attribute;
            [Scoped<ICategoryService>]
            public class CategoryService : ICategoryService
            {
                public ISourceKnownRepository<Category> Repository => null!;
            }
            """ + Scan).Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCV012");
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("[Scoped<ICategoryService>]", false)]
    public void Explicit_Ordinary_Orchestration_Demand_Should_Not_Require_A_SourceKnown_Service_Base(string attribute, bool missing)
    {
        var source = Prefix.Replace("public interface ICategoryService : ISourceKnownService<Category>;",
            "public interface ICategoryService { int Read(); }", StringComparison.Ordinal) + attribute + """
            public class CategoryService : ICategoryService
            {
                public int Read() => 1;
            }
            """ + Scan;
        var facts = Analyze(source);
        facts.Diagnostics.Count(diagnostic => diagnostic.Id == "DRNCV012").Should().Be(missing ? 1 : 0);
    }

    [Theory]
    [InlineData("DRN.Framework.EntityFramework.Context.DrnContextServiceRegistration", true)]
    [InlineData("CustomContextRegistration", false)]
    public void Known_Context_Registration_Should_Not_Hide_An_Unregistered_Service(string registration, bool missing)
    {
        var facts = Analyze(Prefix + $$"""
            public class CategoryService : ICategoryService
            {
                public ISourceKnownRepository<Category> Repository => null!;
            }
            public class CustomContextRegistrationAttribute : DRN.Framework.EntityFramework.Context.DrnContextServiceRegistrationAttribute;
            [{{registration}}]
            public class ContextModule;
            """ + Scan);
        facts.Diagnostics.Count(diagnostic => diagnostic.Id == "DRNCV012").Should().Be(missing ? 1 : 0);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("[Scoped<ICategoryService>]", false)]
    [InlineData("[ScopedWithKey<ICategoryService>(\"read\")]", false)]
    public void Ordinary_Paired_Service_Should_Be_Checked_Without_Explicit_Demands(string attribute, bool missing)
    {
        var source = Prefix.Replace("public interface ICategoryService : ISourceKnownService<Category>;",
            "public interface ICategoryService { int Read(); }", StringComparison.Ordinal) + attribute + """
            public class CategoryService : ICategoryService { public int Read() => 1; }
            """ + Scan;
        var diagnostics = Analyze(source, includeDemand: false).Diagnostics;
        diagnostics.Count(diagnostic => diagnostic.Id == "DRNCV012").Should().Be(missing ? 1 : 0);
        if (missing) diagnostics.Single().Location.Kind.Should().Be(LocationKind.SourceFile);
    }

    [Theory]
    [InlineData("Application")]
    [InlineData("Infra")]
    public void Ordinary_Repository_Should_Not_Require_SourceKnown_Or_Explicit_Demand(string role)
    {
        var source = """
            using DRN.Framework.Utils.DependencyInjection;
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application.QA;
            public interface IOrderRepository { int Read(); }
            public class OrderRepository : IOrderRepository { public int Read() => 1; }
            """ + Scan;
        var diagnostics = Analyze(source.Replace("Sample.Application", "Sample." + role, StringComparison.Ordinal),
            includeDemand: false, role: role).Diagnostics;
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCV012");
    }

    [Fact]
    public void Inherited_Lifetime_Should_Count_For_Ordinary_Scan_Eligibility()
    {
        Analyze(Prefix.Replace("public interface ICategoryService : ISourceKnownService<Category>;",
            "public interface ICategoryService { int Read(); }", StringComparison.Ordinal) + """
            [Scoped<ICategoryService>]
            public abstract class ServiceBase : ICategoryService { public int Read() => 1; }
            public class CategoryService : ServiceBase;
            """ + Scan, includeDemand: false).Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("AddScoped<ICategoryService, CategoryService>()")]
    [InlineData("AddKeyedScoped<ICategoryService, CategoryService>(\"read\")")]
    public void Matching_Manual_Registration_Should_Count_Without_Explicit_Demands(string registration)
    {
        Analyze(Prefix.Replace("public interface ICategoryService : ISourceKnownService<Category>;",
            "public interface ICategoryService { int Read(); }", StringComparison.Ordinal) + $$"""
            public class CategoryService : ICategoryService { public int Read() => 1; }
            public static class Module
            {
                public static void Register(IServiceCollection services)
                {
                    services.AddServicesWithAttributes(typeof(Module).Assembly);
                    services.{{registration}};
                }
            }
            """, includeDemand: false).Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("read", false)]
    [InlineData("write", true)]
    public void Observed_Constructor_Key_Should_Require_An_Exact_Registration(string key, bool missing)
    {
        var source = """
            using DRN.Framework.Utils.DependencyInjection;
            using DRN.Framework.Utils.DependencyInjection.Attributes;
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application.QA;
            public interface IReader { int Read(); }
            [ScopedWithKey<IReader>("read")]
            public class ReaderImplementation : IReader { public int Read() => 1; }
            public interface IConsumer { int Read(); }
            [Scoped<IConsumer>]
            public class Consumer([FromKeyedServices("KEY")] IReader reader) : IConsumer
            {
                public int Read() => reader.Read();
            }
            """ + Scan;
        Analyze(source.Replace("KEY", key, StringComparison.Ordinal), includeDemand: false).Diagnostics
            .Count(diagnostic => diagnostic.Id == "DRNCV012").Should().Be(missing ? 1 : 0);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("services.AddScoped<ReaderImplementation>();", false)]
    public void Concrete_Constructor_Service_Should_Support_Manual_Self_Registration(string registration, bool missing)
    {
        var source = $$"""
            using DRN.Framework.Utils.DependencyInjection;
            using DRN.Framework.Utils.DependencyInjection.Attributes;
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application.QA;
            public class ReaderImplementation { public int Read() => 1; }
            public interface IConsumer { int Read(); }
            [Scoped<IConsumer>]
            public class Consumer(ReaderImplementation reader) : IConsumer { public int Read() => reader.Read(); }
            public static class Module
            {
                public static void Register(IServiceCollection services)
                {
                    services.AddServicesWithAttributes(typeof(Module).Assembly);
                    {{registration}}
                }
            }
            """;
        Analyze(source, includeDemand: false).Diagnostics.Count(diagnostic => diagnostic.Id == "DRNCV012").Should().Be(missing ? 1 : 0);
    }

    [Fact]
    public void Data_Models_Configuration_And_Unestablished_Helper_Intent_Should_Not_Be_Inferred_As_Services()
    {
        Analyze(Prefix + """
            public interface IResponse { int Value { get; } }
            public class Response : IResponse { public int Value => 1; }
            public interface IStoredCategory { int Read(); }
            public class StoredCategory : AggregateRoot, IStoredCategory { public int Read() => 1; }
            public interface IResponseDto { int Read(); }
            public class ResponseDto : Dto, IResponseDto { public int Read() => 1; }
            public interface ISettings { int Read(); }
            [Config]
            public class Settings : ISettings { public int Read() => 1; }
            public class Helper { public int Read() => 1; }
            public interface IWorker { int Read(); }
            public class UnrelatedName : IWorker { public int Read() => 1; }
            public class GenericService<T> { public T Read() => default!; }
            """ + Scan, includeDemand: false).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Inferred_Service_Should_Reject_Lifetime_Lookalikes_And_Mismatched_Manual_Implementations()
    {
        Analyze("""
            using DRN.Framework.Utils.DependencyInjection;
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application.QA;
            public class ScopedAttribute<T> : System.Attribute;
            public interface IReader { int Read(); }
            [Scoped<IReader>]
            public class Reader : IReader { public int Read() => 1; }
            public class Alternate : IReader { public int Read() => 2; }
            public static class Module
            {
                public static void Register(IServiceCollection services)
                {
                    services.AddServicesWithAttributes(typeof(Module).Assembly);
                    services.AddScoped<IReader, Alternate>();
                }
            }
            """, includeDemand: false).Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCV012");
    }

    [Fact]
    public void Unknown_Custom_Composition_Should_Defer_Inferred_Absence()
    {
        Analyze(Prefix.Replace("public interface ICategoryService : ISourceKnownService<Category>;",
            "public interface ICategoryService { int Read(); }", StringComparison.Ordinal) + """
            public class CategoryService : ICategoryService { public int Read() => 1; }
            public static class Module
            {
                public static void Register(IServiceCollection services)
                {
                    services.AddServicesWithAttributes(typeof(Module).Assembly);
                    Configure(services);
                }
                private static void Configure(IServiceCollection services) { }
            }
            """, includeDemand: false).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Inferred_Registration_Should_Accept_An_Exact_Reasoned_Exception()
    {
        var facts = Analyze(Prefix.Replace("public interface ICategoryService : ISourceKnownService<Category>;",
            "public interface ICategoryService { int Read(); }", StringComparison.Ordinal) + """
            public class CategoryService : ICategoryService { public int Read() => 1; }
            """ + Scan, includeDemand: false);
        facts.Records.Add(new("Exception", "DRNCV012", new Dictionary<string, string>
        {
            ["Target"] = "T:Sample.Application.QA.CategoryService",
            ["Reason"] = "This implementation is constructed explicitly by the reviewed import job.",
            ["Owner"] = "Application team", ["Review"] = "9999-12-31"
        }.ToImmutableDictionary(), Location.None));
        var exceptions = new ConventionExceptions(facts);
        facts.Diagnostics.Should().ContainSingle();
        exceptions.Applies(facts.Diagnostics.Single()).Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Production_Analyzer_Should_Check_Demand_Free_Services_And_Honor_Narrow_Suppression(bool suppressed)
    {
        var suppression = suppressed ? """
            [System.Diagnostics.CodeAnalysis.SuppressMessage("DRN.Conventions.Registration", "DRNCV012",
                Justification = "The reviewed import job constructs this implementation explicitly.")]
            """ : "";
        var compilation = Compilation("""
            using DRN.Framework.Utils.DependencyInjection;
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application.QA;
            public interface IReader { int Read(); }
            """ + suppression + """
            public class Reader : IReader { public int Read() => 1; }
            """ + Scan).WithAssemblyName("Sample.Application")
            .AddReferences(MetadataReference.CreateFromFile(typeof(LifetimeAttribute).Assembly.Location));
        compilation.GetDiagnostics().Should().NotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var input = new ProjectDiagnosticRulesTests.RuleFile(ConventionInputs.FileName, """
            [assembly: DRN.Conventions.Version("1")]
            [assembly: DRN.Conventions.Reference("Inventory", Stage = "Complete")]
            """);
        var diagnostics = await compilation.WithAnalyzers([new SourceKnownConventionAnalyzer()],
            new AnalyzerOptions([input], new Options())).GetAnalyzerDiagnosticsAsync();
        diagnostics.Should().NotContain(diagnostic => diagnostic.Id == "AD0001" || diagnostic.Id == "DRNCV018");
        diagnostics.Count(diagnostic => diagnostic.Id == "DRNCV012").Should().Be(suppressed ? 0 : 1);
    }

    [Theory]
    [InlineData("public", false)]
    [InlineData("internal", true)]
    public void Inferred_Registration_Should_Respect_Runtime_Visibility(string accessibility, bool missing)
    {
        Analyze(Prefix.Replace("public interface ICategoryService : ISourceKnownService<Category>;",
            "public interface ICategoryService { int Read(); }", StringComparison.Ordinal) + $$"""
            [Scoped<ICategoryService>]
            {{accessibility}} class CategoryService : ICategoryService { public int Read() => 1; }
            """ + Scan, includeDemand: false).Diagnostics.Count(diagnostic => diagnostic.Id == "DRNCV012").Should().Be(missing ? 1 : 0);
    }

    [Theory]
    [InlineData("public Consumer(ReaderImplementation reader = null) { }")]
    [InlineData("public Consumer() { } public Consumer(ReaderImplementation reader) { }")]
    public void Optional_Or_Ambiguous_Constructors_Should_Not_Establish_Required_Demands(string constructors)
    {
        Analyze($$"""
            using DRN.Framework.Utils.DependencyInjection;
            using DRN.Framework.Utils.DependencyInjection.Attributes;
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application.QA;
            public class ReaderImplementation;
            public interface IConsumer { int Read(); }
            [Scoped<IConsumer>]
            public class Consumer : IConsumer
            {
                {{constructors}}
                public int Read() => 1;
            }
            """ + Scan, includeDemand: false).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Multiple_Implementations_Should_Not_All_Inherit_One_Constructor_Key_Demand()
    {
        Analyze("""
            using DRN.Framework.Utils.DependencyInjection;
            using DRN.Framework.Utils.DependencyInjection.Attributes;
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application.QA;
            public interface IReader { int Read(); }
            [ScopedWithKey<IReader>("read")]
            public class KeyedReader : IReader { public int Read() => 1; }
            [Scoped<IReader>]
            public class Reader : IReader { public int Read() => 1; }
            public interface IConsumer { int Read(); }
            [Scoped<IConsumer>]
            public class Consumer(IReader reader) : IConsumer { public int Read() => reader.Read(); }
            """ + Scan, includeDemand: false).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Manual_Alternative_Outside_Implicit_Eligibility_Should_Satisfy_Constructor_Selection()
    {
        Analyze("""
            using DRN.Framework.SharedKernel.Domain;
            using DRN.Framework.Utils.DependencyInjection;
            using DRN.Framework.Utils.DependencyInjection.Attributes;
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application.QA;
            public interface IReader { int Read(); }
            [ScopedWithKey<IReader>("read")]
            public class Reader : IReader { public int Read() => 1; }
            public class LegacyResponse : Dto, IReader { public int Read() => 2; }
            public interface IConsumer { int Read(); }
            [Scoped<IConsumer>]
            public class Consumer(IReader reader) : IConsumer { public int Read() => reader.Read(); }
            public static class Module
            {
                public static void Register(IServiceCollection services)
                {
                    services.AddServicesWithAttributes(typeof(Module).Assembly);
                    services.AddScoped<IReader, LegacyResponse>();
                }
            }
            """, includeDemand: false).Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Constructed_Generic_Service_Demands_Should_Not_Be_Deduplicated_By_Definition()
    {
        var diagnostics = Analyze("""
            using DRN.Framework.Utils.DependencyInjection;
            using DRN.Framework.Utils.DependencyInjection.Attributes;
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application.QA;
            public interface IReader<T> { int Read(); }
            [Scoped<IReader<int>>]
            public class Reader : IReader<int>, IReader<string> { public int Read() => 1; }
            public interface IConsumer { int Read(); }
            [Scoped<IConsumer>]
            public class Consumer(IReader<int> number, IReader<string> text) : IConsumer
            {
                public int Read() => number.Read() + text.Read();
            }
            """ + Scan, includeDemand: false).Diagnostics;
        diagnostics.Should().ContainSingle().Which.GetMessage().Should().Contain("IReader<string>");
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("[FromKeyedServices(\"\")]", false)]
    public void Empty_String_Attribute_Key_Should_Not_Satisfy_An_Unkeyed_Demand(string selector, bool missing)
    {
        Analyze($$"""
            using DRN.Framework.Utils.DependencyInjection;
            using DRN.Framework.Utils.DependencyInjection.Attributes;
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application.QA;
            public interface IReader { int Read(); }
            [ScopedWithKey<IReader>("")]
            public class Reader : IReader { public int Read() => 1; }
            public interface IConsumer { int Read(); }
            [Scoped<IConsumer>]
            public class Consumer({{selector}} IReader reader) : IConsumer { public int Read() => reader.Read(); }
            """ + Scan, includeDemand: false).Diagnostics.Count(diagnostic => diagnostic.Id == "DRNCV012").Should().Be(missing ? 1 : 0);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("[FromKeyedServices(\"\")]", false)]
    public void Empty_String_Manual_Key_Should_Not_Satisfy_An_Unkeyed_Demand(string selector, bool missing)
    {
        Analyze($$"""
            using DRN.Framework.Utils.DependencyInjection;
            using DRN.Framework.Utils.DependencyInjection.Attributes;
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application.QA;
            public interface IReader { int Read(); }
            public class Reader : IReader { public int Read() => 1; }
            public interface IConsumer { int Read(); }
            [Scoped<IConsumer>]
            public class Consumer({{selector}} IReader reader) : IConsumer { public int Read() => reader.Read(); }
            public static class Module
            {
                public static void Register(IServiceCollection services)
                {
                    services.AddServicesWithAttributes(typeof(Module).Assembly);
                    services.AddKeyedScoped<IReader, Reader>("");
                }
            }
            """, includeDemand: false).Diagnostics.Count(diagnostic => diagnostic.Id == "DRNCV012").Should().Be(missing ? 1 : 0);
    }

    private static ConventionFacts Analyze(string source, string key = "", string reason = "", bool includeDemand = true, string role = "Application")
    {
        var compilation = Compilation(source).WithAssemblyName("Sample." + role)
            .AddReferences(MetadataReference.CreateFromFile(typeof(LifetimeAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(DRN.Framework.EntityFramework.Context.IDrnContext).Assembly.Location));
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        var options = new Options(role);
        var registration = new ConventionInput("Registration", "Sample.Application.QA.CategoryService",
            new Dictionary<string, string>
            {
                ["Service"] = "Sample.Application.QA.ICategoryService", ["Key"] = key, ["Reason"] = reason
            }.ToImmutableDictionary(),
            Location.Create("DrnConventionInputs.g.cs", new(0, 1), new(new(0, 0), new(0, 1))));
        var facts = new ConventionFacts(compilation, ConventionProject.Read(compilation, options.GlobalOptions),
            new(includeDemand ? [registration] : [], []), options, CancellationToken.None);
        ConventionRegistrationAnalysis.Analyze(facts);
        return facts;
    }

    private sealed class Options(string role = "Application") : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new ConventionInputTests.Config(new()
        {
            ["build_property.MSBuildProjectName"] = "Sample." + role,
            ["build_property.RootNamespace"] = "Sample." + role
        });
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new ConventionInputTests.Config(new());
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new ConventionInputTests.Config(new());
    }
}
