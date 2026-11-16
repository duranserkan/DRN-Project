using DRN.Framework.SharedKernel.Analyzers.Conventions;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using static DRN.Test.Analyzer.Tests.DiagnosticClassificationAnalyzerTests;

namespace DRN.Test.Analyzer.Tests;

public sealed class ConventionRegistrationTests
{
    private static readonly MetadataReference UtilsReference = MetadataReference.CreateFromFile(typeof(LifetimeAttribute).Assembly.Location);
    private static readonly MetadataReference FrameworkReference = MetadataReference.CreateFromFile(
        typeof(DRN.Framework.EntityFramework.Context.IDrnContext).Assembly.Location);

    [Theory]
    [DataInlineUnit("typeof(ProjectModule).Assembly", true)]
    [DataInlineUnit("typeof(IServiceCollection).Assembly", false)]
    public void Reordered_Scan_Arguments_Should_Preserve_Registration_Evidence(string assembly, bool missing)
    {
        var facts = Analyze($$"""
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application;
            public interface IWorker { void Run(); }
            public class Worker : IWorker { public void Run() { } }
            public static class ProjectModule
            {
                public static IServiceCollection Register(IServiceCollection services)
                {
                    DRN.Framework.Utils.DependencyInjection.ServiceCollectionExtensions.AddServicesWithAttributes(
                        assembly: {{assembly}}, sc: services);
                    return services;
                }
            }
            """, includeDemand: false);
        facts.Diagnostics.Count(item => item.Id == "DRNCV012").Should().Be(missing ? 1 : 0);
    }

    [Theory]
    [DataInlineUnit("Hosted", "", true)]
    [DataInlineUnit("Application", "", true)]
    [DataInlineUnit("Infra", "", true)]
    [DataInlineUnit("Utils", "", true)]
    [DataInlineUnit("Domain", "", false)]
    [DataInlineUnit("Contract", "", false)]
    [DataInlineUnit("Application", "services.AddServicesWithAttributes();", false)]
    [DataInlineUnit("Infra", "services.AddServicesWithAttributes(typeof(ProjectModule).Assembly);", false)]
    [DataInlineUnit("Infra", "services.AddServicesWithAttributes(assembly: typeof(ProjectModule).Assembly);", false)]
    [DataInlineUnit("Utils", "DRN.Framework.Utils.DependencyInjection.ServiceCollectionExtensions.AddServicesWithAttributes(assembly: typeof(ProjectModule).Assembly, sc: services);", false)]
    [DataInlineUnit("Utils", "DRN.Framework.Utils.DependencyInjection.ServiceCollectionExtensions.AddServicesWithAttributes(assembly: typeof(IServiceCollection).Assembly, sc: services);", true)]
    [DataInlineUnit("Utils", "DRN.Framework.Utils.DependencyInjection.ServiceCollectionExtensions.AddServicesWithAttributes(services);", false)]
    [DataInlineUnit("Hosted", "services.AddServicesWithAttributes(typeof(IServiceCollection).Assembly);", true)]
    [DataInlineUnit("Application", "System.Action unused = () => services.AddServicesWithAttributes();", true)]
    [DataInlineUnit("Application", "new ServiceCollection().AddServicesWithAttributes();", true)]
    public void Composition_Roles_Should_Require_A_Module_Scanning_Its_Own_Assembly(string role, string body, bool missing)
    {
        var facts = Analyze($$"""
            using Microsoft.Extensions.DependencyInjection;
            using DRN.Framework.Utils.DependencyInjection;
            namespace Sample.{{role}};
            public static class ProjectModule
            {
                public static IServiceCollection Register(IServiceCollection services)
                {
                    {{body}}
                    return services;
                }
            }
            """, includeDemand: false, role: role);
        facts.Diagnostics.Clear();
        ConventionRegistrationAnalysis.AnalyzeAll(facts, requireModule: true);
        facts.Diagnostics.Count(item => item.Id == "DRNCV032").Should().Be(missing ? 1 : 0);
    }

    [Theory]
    [DataInlineUnit("public static class Helper", "public static IServiceCollection", "return services;", true)]
    [DataInlineUnit("internal static class ProjectModule", "public static IServiceCollection", "return services;", true)]
    [DataInlineUnit("public static class ProjectModule", "private static IServiceCollection", "return services;", true)]
    [DataInlineUnit("public static class ProjectModule", "public static void", "", true)]
    [DataInlineUnit("public static class ProjectModule", "public static IServiceCollection", "return services;", false)]
    public void Module_Scan_Should_Require_The_Public_Composition_Shape(string type, string method, string result, bool missing)
    {
        var facts = Analyze($$"""
            using Microsoft.Extensions.DependencyInjection;
            using DRN.Framework.Utils.DependencyInjection;
            namespace Sample.Application;
            {{type}}
            {
                {{method}} Register(IServiceCollection services)
                {
                    services.AddServicesWithAttributes();
                    {{result}}
                }
            }
            """, includeDemand: false);
        facts.Diagnostics.Clear();
        ConventionRegistrationAnalysis.AnalyzeAll(facts, requireModule: true);
        facts.Diagnostics.Count(item => item.Id == "DRNCV032").Should().Be(missing ? 1 : 0);
    }

    [Theory]
    [DataInlineUnit("", "IWorker", "DRNCV031")]
    [DataInlineUnit("[Scoped<object>]", "IWorker", "DRNCV031")]
    [DataInlineUnit("[ScopedWithKey<IWorker>(\"read\")]", "IWorker", "DRNCV012")]
    [DataInlineUnit("[Scoped<IWorker>]", "IOther", "DRNCV012")]
    [DataInlineUnit("[Scoped<IWorker>]", "IWorker", "")]
    public void Combined_Analysis_Should_Separate_Binding_Failures_From_Demand_Mismatches(
        string attribute, string dependency, string expected)
    {
        var facts = Analyze($$"""
            using DRN.Framework.Utils.DependencyInjection;
            using DRN.Framework.Utils.DependencyInjection.Attributes;
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application.QA;
            public interface IWorker { void Run(); }
            public interface IOther;
            {{attribute}}
            public class Worker : IWorker, IOther { public void Run() { } }
            public interface IConsumer;
            [Scoped<IConsumer>]
            public class Consumer({{dependency}} worker) : IConsumer;
            """ + Scan, includeDemand: false);
        facts.Diagnostics.Clear();
        ConventionRegistrationAnalysis.AnalyzeAll(facts, requireModule: true);
        facts.Diagnostics.Select(diagnostic => diagnostic.Id).Should().Equal(
            expected.Length == 0 ? Array.Empty<string>() : new[] { expected });
    }

    [Theory]
    [DataInlineUnit("public class CustomAttribute() : LifetimeAttribute(ServiceLifetime.Scoped, typeof(IWorker), true, null);", "[Custom]", false)]
    [DataInlineUnit("public class CustomAttribute : LifetimeAttribute { public CustomAttribute() : base(ServiceLifetime.Scoped, typeof(IWorker), true, null) { } }", "[Custom]", false)]
    [DataInlineUnit("public class CustomAttribute(System.Type service) : LifetimeAttribute(ServiceLifetime.Scoped, service, true, null);", "[Custom(typeof(IWorker))]", false)]
    [DataInlineUnit("public class CustomAttribute : LifetimeAttribute { public CustomAttribute() : this(typeof(IWorker)) { } private CustomAttribute(System.Type service) : base(key: null, tryAdd: true, serviceType: service, serviceLifetime: ServiceLifetime.Scoped) { } }", "[Custom]", false)]
    [DataInlineUnit("public class CustomAttribute() : LifetimeAttribute(ServiceLifetime.Scoped, typeof(object), true, null);", "[Custom]", true)]
    [DataInlineUnit("public class CustomAttribute() : LifetimeAttribute(ServiceLifetime.Scoped, Resolve(), true, null) { private static System.Type Resolve() => typeof(IWorker); }", "[Custom]", true)]
    public void Custom_Lifetime_Constructor_Bindings_Should_Be_Proven_And_Shared(string declaration, string attribute, bool missing)
    {
        var facts = Analyze($$"""
            using DRN.Framework.Utils.DependencyInjection;
            using DRN.Framework.Utils.DependencyInjection.Attributes;
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application.QA;
            public interface IWorker { void Run(); }
            {{declaration}}
            {{attribute}}
            public class Worker : IWorker { public void Run() { } }
            """ + Scan, includeDemand: false);
        var worker = facts.Compilation.GetTypeByMetadataName("Sample.Application.QA.Worker")!;
        var cached = ConventionRegistrationAnalysis.GetLifetime(facts, worker);
        var count = facts.Lifetimes.Count;
        ConventionRegistrationAnalysis.AnalyzeLifetimeAttributes(facts);
        facts.Lifetimes.Count.Should().Be(count);
        ConventionRegistrationAnalysis.GetLifetime(facts, worker).Should().Be(cached);
        facts.Diagnostics.Count(item => item.Id == "DRNCV031").Should().Be(missing ? 1 : 0);
        if (!missing) facts.Diagnostics.Should().BeEmpty();

        using var cancellation = new CancellationTokenSource();
        var cancelable = new ConventionFacts(facts.Compilation, facts.Project, new([], []), new Options(), cancellation.Token);
        ConventionRegistrationAnalysis.GetLifetime(cancelable, worker).Should().Be(cached);
        cancellation.Cancel();
        var read = () => ConventionRegistrationAnalysis.GetLifetime(cancelable, worker);
        read.Should().Throw<OperationCanceledException>();
    }

    [Theory]
    [DataInlineUnit("")]
    [DataInlineUnit("[Scoped<IWorker>]")]
    public void Shared_Candidates_Should_Preserve_Diagnostics_Without_Duplicate_Binding_Failures(string attribute)
    {
        var facts = Analyze($$"""
            using DRN.Framework.Utils.DependencyInjection;
            using DRN.Framework.Utils.DependencyInjection.Attributes;
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application.QA;
            public interface IWorker { void Run(); }
            {{attribute}}
            public class Worker : IWorker { public void Run() { } }
            public interface IHelper { void Run(); }
            public abstract class Helper : IHelper { public abstract void Run(); }
            """ + Scan, includeDemand: false);
        facts.Diagnostics.Clear();
        ConventionRegistrationAnalysis.AnalyzeLifetimeAttributes(facts);
        ConventionRegistrationAnalysis.Analyze(facts);
        var expected = facts.Diagnostics.Where(diagnostic => diagnostic.Id != "DRNCV012").Select(diagnostic =>
            (diagnostic.Id, diagnostic.GetMessage(), diagnostic.Location.SourceSpan, diagnostic.Severity,
                diagnostic.Properties["Target"])).ToArray();
        facts.Diagnostics.Clear();
        ConventionRegistrationAnalysis.AnalyzeAll(facts, requireModule: true);
        facts.Diagnostics.Select(diagnostic =>
            (diagnostic.Id, diagnostic.GetMessage(), diagnostic.Location.SourceSpan, diagnostic.Severity,
                diagnostic.Properties["Target"])).Should().Equal(expected);
        facts.Diagnostics.Select(diagnostic => diagnostic.Id).Should().Equal(
            attribute.Length == 0 ? new[] { "DRNCV031" } : Array.Empty<string>());

        using var cancellation = new CancellationTokenSource();
        var canceledFacts = new ConventionFacts(facts.Compilation, facts.Project, new([], []),
            new Options(), cancellation.Token);
        cancellation.Cancel();
        var analyze = () => ConventionRegistrationAnalysis.AnalyzeAll(canceledFacts, requireModule: true);
        analyze.Should().Throw<OperationCanceledException>();
        canceledFacts.Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("", "Application", true)]
    [DataInlineUnit("", "Infra", true)]
    [DataInlineUnit("", "Domain", false)]
    [DataInlineUnit("[Scoped<IWorker>]", "Application", false)]
    [DataInlineUnit("[Transient<IWorker>]", "Application", false)]
    [DataInlineUnit("[Singleton<IWorker>]", "Application", false)]
    [DataInlineUnit("[CustomLifetime]", "Application", false)]
    [DataInlineUnit("[Scoped]", "Application", true)]
    [DataInlineUnit("[Scoped<Worker>]", "Application", true)]
    [DataInlineUnit("[Scoped<IOther>]", "Application", true)]
    [DataInlineUnit("[ScopedWithKey<IWorker>(\"worker\")]", "Application", false)]
    [DataInlineUnit("[ScopedWithKey<IOther>(\"worker\")]", "Application", true)]
    [DataInlineUnit("[Scoped<IWorker>, Transient<IWorker>]", "Application", true)]
    public void Paired_Implementations_Should_Declare_Lifetime_Without_Composition_Evidence(string attribute, string role, bool missing)
    {
        var facts = Analyze($$"""
            using DRN.Framework.Utils.DependencyInjection.Attributes;
            namespace Sample.{{role}}.QA;
            public interface IWorker;
            public interface IOther;
            public class ScopedAttribute : ScopedAttribute<object>;
            public class CustomLifetimeAttribute : ScopedAttribute<IWorker>;
            {{attribute}}
            public class Worker : IWorker, IOther;
            """, includeDemand: false, role: role);
        ConventionRegistrationAnalysis.AnalyzeLifetimeAttributes(facts);
        facts.Diagnostics.Count(item => item.Id == "DRNCV031").Should().Be(missing ? 1 : 0);
        if (missing)
            facts.Diagnostics.Single(item => item.Id == "DRNCV031").GetMessage().Should().Be(
                "'Worker' must register 'IWorker' through a matching LifetimeAttribute. Choose [Scoped<IWorker>], [Transient<IWorker>], or [Singleton<IWorker>] for its intended lifetime.");
    }

    [Fact]
    public void Lifetime_Requirement_Should_Respect_Inheritance_And_Implementation_Scope()
    {
        var facts = Analyze("""
            using DRN.Framework.Utils.DependencyInjection.Attributes;
            namespace Sample.Application.QA;
            public interface IWorker;
            [Scoped<IWorker>] public abstract class WorkerBase;
            public class Worker : WorkerBase, IWorker;
            public interface IAbstractWorker;
            public abstract class AbstractWorker : IAbstractWorker;
            public interface IGenericWorker<T>;
            public class GenericWorker<T> : IGenericWorker<T>;
            public interface IDifferent;
            public class Helper : IDifferent;
            public interface IProfilePage;
            public class ProfilePage : Microsoft.AspNetCore.Mvc.RazorPages.PageModel, IProfilePage;
            """, includeDemand: false);
        ConventionRegistrationAnalysis.AnalyzeLifetimeAttributes(facts);
        facts.Diagnostics.Should().NotContain(item => item.Id == "DRNCV031");
    }

    [Theory]
    [DataInlineUnit("")]
    [DataInlineUnit("[DRN.Framework.Utils.DependencyInjection.Attributes.Scoped<object>]")]
    public void Manual_And_Unknown_Composition_Should_Not_Waive_The_Lifetime_Convention(string attribute)
    {
        var facts = Analyze($$"""
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application.QA;
            public interface IWorker;
            {{attribute}}
            public class Worker : IWorker;
            public static class Module
            {
                public static void Register(IServiceCollection services)
                {
                    services.AddScoped<IWorker, Worker>();
                    Custom(services);
                }
                private static void Custom(IServiceCollection services) { }
            }
            """, includeDemand: false);
        ConventionRegistrationAnalysis.AnalyzeLifetimeAttributes(facts);
        facts.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCV031");
    }

    [Theory]
    [DataInlineUnit("IWorker<int>", "", false)]
    [DataInlineUnit("IWorker<string>", "", true)]
    [DataInlineUnit("IWorker<int>", "[Scoped<IWorker<string>>]", true)]
    [DataInlineUnit("IWorker<string>", "[Scoped<IWorker<int>>]", false)]
    public void Lifetime_Binding_Should_Preserve_Constructed_Interfaces_And_Direct_Precedence(
        string inheritedService, string directAttribute, bool missing)
    {
        var facts = Analyze($$"""
            using DRN.Framework.Utils.DependencyInjection.Attributes;
            namespace Sample.Application.QA;
            public interface IWorker<T>;
            [Scoped<{{inheritedService}}>]
            public abstract class WorkerBase;
            {{directAttribute}}
            public class Worker : WorkerBase, IWorker<int>;
            """, includeDemand: false);
        ConventionRegistrationAnalysis.AnalyzeLifetimeAttributes(facts);
        facts.Diagnostics.Count(item => item.Id == "DRNCV031").Should().Be(missing ? 1 : 0);
    }

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
            public static IServiceCollection Register(IServiceCollection services)
            {
                services.AddServicesWithAttributes(typeof(Module).Assembly);
                return services;
            }
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
    [DataInlineUnit("", true)]
    [DataInlineUnit("var custom = new CustomRegistration(services);", false)]
    [DataInlineUnit("CustomRegistration custom = new(services);", false)]
    public void Composition_Scan_Should_Preserve_Unknown_Object_Creation(string creation, bool missing)
    {
        var facts = Analyze(Prefix + $$"""
            public class CategoryService : ICategoryService
            {
                public ISourceKnownRepository<Category> Repository => null!;
            }
            public class CustomRegistration
            {
                public CustomRegistration(IServiceCollection services) { }
            }
            public static class Module
            {
                public static void Register(IServiceCollection services)
                {
                    services.AddServicesWithAttributes(typeof(Module).Assembly);
                    {{creation}}
                }
            }
            """);
        facts.Diagnostics.Count(item => item.Id == "DRNCV012").Should().Be(missing ? 1 : 0);
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
    [DataInlineUnit("DRNCV012", false)]
    [DataInlineUnit("DRNCV012", true)]
    [DataInlineUnit("DRNCV031", false)]
    [DataInlineUnit("DRNCV031", true)]
    public async Task Production_Analyzer_Should_Check_Demand_Free_Services_And_Honor_Narrow_Suppression(string rule, bool suppressed)
    {
        var suppression = suppressed ? $$"""
            [System.Diagnostics.CodeAnalysis.SuppressMessage("DRN.Conventions.Registration", "{{rule}}",
                Justification = "The reviewed import job constructs this implementation explicitly.")]
            """ : "";
        // A matching attribute satisfies DRNCV031, but runtime scanning excludes internal classes.
        var declaration = rule == "DRNCV012" ? "[Scoped<IReaderService>] internal" : "public";
        var compilation = Compilation("""
            using DRN.Framework.Utils.DependencyInjection;
            using DRN.Framework.Utils.DependencyInjection.Attributes;
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application.QA;
            public interface IReaderService { int Read(); }
            """ + suppression + $$"""
            {{declaration}} class ReaderService : IReaderService { public int Read() => 1; }
            public static class Module
            {
                public static IServiceCollection Register(IServiceCollection services)
                {
                    services.AddServicesWithAttributes();
                    return services;
                }
            }
            """).WithAssemblyName("Sample.Application")
            .AddReferences(UtilsReference);
        compilation.GetDiagnostics().Should().NotContain(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        var input = new ProjectDiagnosticRulesTests.RuleFile(ConventionInputs.FileName, """
            [assembly: DRN.Conventions.Version("1")]
            [assembly: DRN.Conventions.Reference("Inventory", Stage = "Complete")]
            """);
        var diagnostics = await compilation.WithAnalyzers([new SourceKnownConventionAnalyzer()],
            new AnalyzerOptions([input], new Options())).GetAnalyzerDiagnosticsAsync();
        diagnostics.Should().NotContain(diagnostic => diagnostic.Id == "AD0001" || diagnostic.Id == "DRNCV018");
        diagnostics.Should().HaveCount(suppressed ? 0 : 1);
        diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == rule);
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
    public void Repeated_Service_Demands_Should_Preserve_Distinct_Keys()
    {
        var diagnostics = Analyze("""
            using DRN.Framework.Utils.DependencyInjection;
            using DRN.Framework.Utils.DependencyInjection.Attributes;
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application.QA;
            public interface IReader { int Read(); }
            [ScopedWithKey<IReader>("read")]
            public class Reader : IReader { public int Read() => 1; }
            public interface IConsumer { int Read(); }
            [Scoped<IConsumer>]
            public class Consumer([FromKeyedServices("read")] IReader first,
                [FromKeyedServices("read")] IReader second,
                [FromKeyedServices("write")] IReader third) : IConsumer
            {
                public int Read() => first.Read() + second.Read() + third.Read();
            }
            """ + Scan, includeDemand: false).Diagnostics;
        diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCV012");
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

    [Fact]
    public void Unrelated_Call_Beside_A_Scan_Should_Not_Hide_Missing_Registrations()
    {
        var source = Prefix.Replace("public interface ICategoryService : ISourceKnownService<Category>;",
            "public interface ICategoryService { int Read(); }", StringComparison.Ordinal) + """
            public class CategoryService : ICategoryService { public int Read() => 1; }
            public static class Module
            {
                public static void Register(IServiceCollection services)
                {
                    services.AddServicesWithAttributes(typeof(Module).Assembly);
                    System.Math.Abs(-1);
                }
            }
            """;
        Analyze(source, includeDemand: false).Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNCV012");
    }

    [Theory]
    [DataInlineUnit("[HostedService]", "", false)]
    [DataInlineUnit("", "", true)]
    [DataInlineUnit("[HostedService]", "[FromKeyedServices(\"worker\")]", true)]
    [DataInlineUnit("[Singleton<Worker>]", "", true)]
    public void Hosted_Lifetime_Should_Satisfy_Only_An_Unkeyed_Hosted_Service_Demand(string attribute, string key, bool missing)
    {
        var source = $$"""
            using DRN.Framework.Utils.DependencyInjection;
            using DRN.Framework.Utils.DependencyInjection.Attributes;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.Hosting;
            namespace Sample.Application.QA;
            {{attribute}}
            public class Worker : IHostedService
            {
                public System.Threading.Tasks.Task StartAsync(System.Threading.CancellationToken token) => System.Threading.Tasks.Task.CompletedTask;
                public System.Threading.Tasks.Task StopAsync(System.Threading.CancellationToken token) => System.Threading.Tasks.Task.CompletedTask;
            }
            public interface IConsumer { int Read(); }
            [Scoped<IConsumer>]
            public class Consumer({{key}} IHostedService worker) : IConsumer { public int Read() => 1; }
            """ + Scan;
        Analyze(source, includeDemand: false).Diagnostics.Count(diagnostic => diagnostic.Id == "DRNCV012").Should().Be(missing ? 1 : 0);
    }

    [Fact]
    public void NonDomain_Framework_Interfaces_Should_Not_Trigger_Paired_Lifetime_Diagnostics()
    {
        var diagnostics = Analyze("""
            using DRN.Framework.Utils.DependencyInjection;
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application.QA;
            public sealed class Disposable : System.IDisposable
            {
                public void Dispose() { }
            }
            public sealed class Comparer : System.Collections.Generic.IComparer<int>
            {
                public int Compare(int x, int y) => x.CompareTo(y);
            }
            """ + Scan, includeDemand: false, includeLifetime: true).Diagnostics;
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Ambiguous_Lifetime_Attributes_Should_Report_DRNCV012_On_Unpaired_Demands()
    {
        var diagnostics = Analyze("""
            using DRN.Framework.Utils.DependencyInjection;
            using DRN.Framework.Utils.DependencyInjection.Attributes;
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application.QA;
            public interface IWorker { int Read(); }
            public interface IExtra { int Read(); }
            [Scoped<IWorker>]
            [Transient<IExtra>]
            public sealed class Worker : IWorker, IExtra { public int Read() => 1; }
            public interface IConsumer { int Read(); }
            [Scoped<IConsumer>]
            public sealed class Consumer(IExtra extra) : IConsumer { public int Read() => extra.Read(); }
            """ + Scan, includeDemand: false, includeLifetime: true).Diagnostics;
        diagnostics.Should().Contain(item => item.Id == "DRNCV031" && item.GetMessage().Contains("IWorker", StringComparison.Ordinal));
        diagnostics.Should().Contain(item => item.Id == "DRNCV012" && item.GetMessage().Contains("IExtra", StringComparison.Ordinal));
    }

    [Theory]
    [DataInlineUnit("public", "", "DRNCV031")]
    [DataInlineUnit("internal", "[Scoped<Common.SharedKernel.IWorker>]", "DRNCV012")]
    [DataInlineUnit("public", "[Scoped<Common.SharedKernel.IWorker>]", "")]
    public void SharedKernel_Paired_Interfaces_Should_Enforce_Lifetime_And_Registration_Conventions(
        string accessibility, string attribute, string expectedRule)
    {
        var sharedKernel = Compilation("""
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "Common.SharedKernel")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "Common.SharedKernel")]
            namespace Common.SharedKernel;
            public interface IWorker { int Read(); }
            """).WithAssemblyName("Common.SharedKernel");
        using var image = new MemoryStream();
        sharedKernel.Emit(image).Success.Should().BeTrue();
        var sharedReference = MetadataReference.CreateFromImage(image.ToArray());

        var diagnostics = Analyze($$"""
            using DRN.Framework.Utils.DependencyInjection;
            using DRN.Framework.Utils.DependencyInjection.Attributes;
            using Microsoft.Extensions.DependencyInjection;
            namespace Sample.Application.QA;
            {{attribute}}
            {{accessibility}} sealed class Worker : Common.SharedKernel.IWorker { public int Read() => 1; }
            """ + Scan, includeDemand: false, includeLifetime: true, extraReferences: [sharedReference]).Diagnostics;
        if (expectedRule.Length == 0) diagnostics.Should().BeEmpty();
        else diagnostics.Should().ContainSingle().Which.Id.Should().Be(expectedRule);
    }

    private static ConventionFacts Analyze(string source, string key = "", string reason = "", bool includeDemand = true,
        string role = "Application", bool includeLifetime = false, MetadataReference[]? extraReferences = null)
    {
        var compilation = Compilation(source).WithAssemblyName("Sample." + role)
            .AddReferences(UtilsReference, FrameworkReference);
        if (extraReferences is { Length: > 0 }) compilation = compilation.AddReferences(extraReferences);
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
        if (includeLifetime) ConventionRegistrationAnalysis.AnalyzeAll(facts);
        else ConventionRegistrationAnalysis.Analyze(facts);
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
