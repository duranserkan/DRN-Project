using System.Text;
using DRN.Framework.Hosting.Generators;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Test.Analyzer.Tests;

public class HostedAccessorGeneratorTests
{
    private static readonly string[] ExpectedProbeActions =
    [
        "AcceptedAction", "CustomMethodsAction", "CustomRouteAction", "CustomVerbAction", "DeleteAction", "GetAction", "HeadAction",
        "InheritedAction", "InterfaceAction", "MultipleRoutesAction", "NamedActionAsync", "OptionsAction",
        "OverriddenAction", "PatchAction", "PostAction", "PutAction", "RouteAction", "UndocumentedAction"
    ];

    private const string ProgramSource = """
        public class TestProgram : DRN.Framework.Hosting.DrnProgram.DrnProgramBase<TestProgram>, DRN.Framework.Hosting.DrnProgram.IDrnProgram
        {
            public static System.Threading.Tasks.Task Main(string[] args) => System.Threading.Tasks.Task.CompletedTask;
            protected override System.Threading.Tasks.Task AddServicesAsync(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder,
                DRN.Framework.Utils.Settings.IAppSettings settings, DRN.Framework.Utils.Logging.IScopedLog log)
                => System.Threading.Tasks.Task.CompletedTask;
        }
        """;

    [Fact]
    public void Generator_Should_Discover_Programs_Without_An_OptIn()
    {
        var result = Generate(ProgramSource);
        result.GeneratedTrees.Should().HaveCount(2);
        result.Diagnostics.Should().BeEmpty();
        result.Results.Single().GeneratedSources.Select(source => source.HintName).Should().Equal(
            "Helpers/EndpointFor/Helpers.EndpointFor.TestEndpointFor.g.cs",
            "Helpers/PageFor/Helpers.PageFor.TestPageFor.g.cs");
    }

    [Fact]
    public void Generator_Should_Resolve_Aliases_And_Deduplicate_Partial_Programs()
    {
        var source = "using ProgramBase = DRN.Framework.Hosting.DrnProgram.DrnProgramBase<TestProgram>;" +
            ProgramSource.Replace("public class TestProgram", "public partial class TestProgram", StringComparison.Ordinal)
                .Replace("DRN.Framework.Hosting.DrnProgram.DrnProgramBase<TestProgram>", "ProgramBase", StringComparison.Ordinal) +
            "public partial class TestProgram : DRN.Framework.Hosting.DrnProgram.IDrnProgram {}";

        var result = Generate(source);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedTrees.Should().HaveCount(2);
    }

    [Fact]
    public void Generator_Should_Escape_Keyword_Namespaces_Without_Escaping_Hint_Paths()
    {
        var result = Generate("namespace @event {" + ProgramSource + "}");

        result.Diagnostics.Should().BeEmpty();
        Generated(result, "TestEndpointFor.g.cs").Should().Contain("namespace @event.Helpers.EndpointFor;");
        Generated(result, "TestPageFor.g.cs").Should().Contain("namespace @event.Helpers.PageFor;");
        result.Results.Single().GeneratedSources.Select(source => source.HintName).Should().Equal(
            "Helpers/EndpointFor/event.Helpers.EndpointFor.TestEndpointFor.g.cs",
            "Helpers/PageFor/event.Helpers.PageFor.TestPageFor.g.cs");
    }

    [Fact]
    public void Generator_Should_Discover_Only_SelfTyped_Concrete_Programs_Through_Generic_Bases()
    {
        var source = """
            using DRN.Framework.Hosting.DrnProgram;
            public abstract class IntermediateProgram<TProgram> : DrnProgramBase<TProgram>
                where TProgram : DrnProgramBase<TProgram>, IDrnProgram, new()
            {
                protected override System.Threading.Tasks.Task AddServicesAsync(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder,
                    DRN.Framework.Utils.Settings.IAppSettings settings, DRN.Framework.Utils.Logging.IScopedLog log)
                    => System.Threading.Tasks.Task.CompletedTask;
            }
            public class IndirectProgram : IntermediateProgram<IndirectProgram>, IDrnProgram
            {
                public static System.Threading.Tasks.Task Main(string[] args) => System.Threading.Tasks.Task.CompletedTask;
            }
            public class DescendantProgram : IndirectProgram {}
            """;

        var result = Generate(source);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedTrees.Should().HaveCount(2);
        Generated(result, "IndirectEndpointFor.g.cs").Should().Contain("EndpointCollectionBase<global::IndirectProgram, IndirectEndpointFor>");
    }

    [Theory]
    [DataInlineUnit("internal")]
    [DataInlineUnit("nested")]
    [DataInlineUnit("generic")]
    [DataInlineUnit("file")]
    [DataInlineUnit("suffix")]
    public void Generator_Should_Not_Read_Razor_For_Ineligible_Programs(string shape)
    {
        var source = shape switch
        {
            "internal" => ProgramSource.Replace("public class TestProgram", "internal class TestProgram", StringComparison.Ordinal)
                .Replace("public static", "public TestProgram() {} public static", StringComparison.Ordinal),
            "nested" => "public class Container {" + ProgramSource + "}",
            "generic" => ProgramSource.Replace("TestProgram", "TestProgram<T>", StringComparison.Ordinal),
            "file" => ProgramSource.Replace("public class TestProgram", "file class TestProgram", StringComparison.Ordinal)
                .Replace("public static", "public TestProgram() {} public static", StringComparison.Ordinal),
            _ => ProgramSource.Replace("TestProgram", "Testprogram", StringComparison.Ordinal)
        };

        var result = Generate(source, [new UnreadablePage()]);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_Should_Ignore_Abstract_Programs_And_Lookalike_Bases()
    {
        const string source = """
            public abstract class AbstractProgram<T> : DRN.Framework.Hosting.DrnProgram.DrnProgramBase<T>
                where T : DRN.Framework.Hosting.DrnProgram.DrnProgramBase<T>, DRN.Framework.Hosting.DrnProgram.IDrnProgram, new() {}
            namespace Lookalike
            {
                public class DrnProgramBase<T> {}
                public class FakeProgram : DrnProgramBase<FakeProgram> {}
            }
            """;

        var result = Generate(source, [new UnreadablePage()]);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_Should_Not_Discover_Referenced_Programs()
    {
        var referenced = CreateCompilation(ProgramSource, "ReferencedProgram");
        var reference = AnalyzerTestCompilation.EmitReference(referenced);

        var result = Generate("public class Consumer {}", [new UnreadablePage()], references: [reference]);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_Should_Use_App_For_Program_And_Reject_Root_Collisions()
    {
        var source = ProgramSource.Replace("TestProgram", "Program", StringComparison.Ordinal);
        var result = Generate(source);

        result.Diagnostics.Should().BeEmpty();
        Generated(result, "AppEndpointFor.g.cs").Should().Contain("EndpointCollectionBase<global::Program, AppEndpointFor>");
        Generated(result, "AppPageFor.g.cs").Should().Contain("PageCollectionBase<AppPageFor>");

        var collision = GenerateAppRootCollision();
        collision.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "DRNHOSTING001" && diagnostic.GetMessage().Contains("collides"));
    }

    internal static GeneratorDriverRunResult GenerateAppRootCollision() =>
        Generate(ProgramSource.Replace("TestProgram", "Program", StringComparison.Ordinal) +
                 ProgramSource.Replace("TestProgram", "AppProgram", StringComparison.Ordinal));

    [Fact]
    public void Generated_Accessors_Should_Use_Stored_References_Without_PerAccess_Lookups()
    {
        var source = "public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase { public void Read() {} }" + ProgramSource;
        var result = Generate(source, [new Page("Pages/Home.cshtml", "@page")]);

        result.Diagnostics.Should().BeEmpty();
        Generated(result, "TestEndpointFor.g.cs").Should().Contain("public ItemsFor Items { get; } = new();");
        Generated(result, "TestPageFor.g.cs").Should().Contain("public RootPageFor Root { get; } = new();");
        Generated(result, "ItemsFor.g.cs").Should().Contain("public ApiEndpoint Read { get; private set; } = null!;");
        Generated(result, "RootPageFor.g.cs").Should().Contain("public string Home { get; init; } = \"/Home\";");
        foreach (var tree in result.GeneratedTrees)
        {
            var properties = tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax>();
            properties.Should().OnlyContain(property => property.ExpressionBody == null && property.AccessorList != null &&
                property.AccessorList.Accessors.All(accessor => accessor.Body == null && accessor.ExpressionBody == null));
        }
    }

    [Fact]
    public void Generator_Should_Inherit_The_Endpoint_Singleton_With_The_Generated_Root_Type()
    {
        var source = """
            using DRN.Framework.Hosting.Endpoints;
            using Helpers.EndpointFor;
            public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase { public void Read() {} }
            public class Consumer
            {
                public TestEndpointFor Collection => TestEndpointFor.EndpointCollection;
                public ApiEndpoint Read() => TestEndpointFor.EndpointCollection.Items.Read;
                public EndpointCollection<TestProgram> BaseCollection => TestEndpointFor.EndpointCollection;
            }
            """ + ProgramSource;

        var result = Generate(source);

        result.Diagnostics.Should().BeEmpty();
        var root = Generated(result, "TestEndpointFor.g.cs");
        root.Should().Contain("EndpointCollectionBase<global::TestProgram, TestEndpointFor>")
            .And.NotContain("InvalidOperationException");
        var syntax = CSharpSyntaxTree.ParseText(root).GetRoot();
        syntax.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax>()
            .Should().NotContain(property => property.Identifier.ValueText == "EndpointCollection");
        syntax.DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.FieldDeclarationSyntax>().Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit(false, true)]
    [DataInlineUnit(true, false)]
    public void Generator_Should_Not_Read_Razor_Text_Without_An_Eligible_Web_Program(bool hasProgram, bool hosted)
    {
        var result = Generate(hasProgram ? ProgramSource : "public class OrdinaryProgram {}",
            [new UnreadablePage()], hosted: hosted);

        result.GeneratedTrees.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Generator_Should_Not_Index_Nested_Lookalike_Customization_Attributes()
    {
        var source = """
            using DRN.Framework.Hosting.Endpoints;
            [assembly: DRN.Framework.Hosting.Endpoints.Lookalikes.AccessorMember(typeof(TestProgram), "Endpoint.Missing.Read")]
            namespace DRN.Framework.Hosting.Endpoints
            {
                public static class Lookalikes
                {
                    [System.AttributeUsage(System.AttributeTargets.Assembly)]
                    public sealed class AccessorMemberAttribute(System.Type program, string path) : System.Attribute {}
                }
            }
            """ + ProgramSource;

        var result = Generate(source);

        result.Diagnostics.Should().BeEmpty();
        Generated(result, "TestEndpointFor.g.cs").Should().Contain("class TestEndpointFor");
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void Generator_Should_Emit_An_Empty_Page_Collection_When_No_Razor_Pages_Exist(bool includeNonPages)
    {
        var source = """
            using DRN.Framework.Hosting.Endpoints;
            public class Consumer
            {
                public Helpers.PageFor.TestPageFor Pages => Helpers.PageFor.TestPageFor.PageCollection;
                public int Count => Pages.All.Count;
            }
            """ + ProgramSource;
        AdditionalText[] pages = includeNonPages
            ? [new Page("Pages/Shared/_Layout.cshtml", "@*\n@page\n*@\n<html></html>"),
                new Page("Views/Home/Index.cshtml", "<h1>View</h1>")]
            : [];

        var result = Generate(source, pages);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedTrees.Should().HaveCount(2);
        var root = Generated(result, "TestPageFor.g.cs");
        root.Should().Contain("PageCollectionBase<TestPageFor>");
        CSharpSyntaxTree.ParseText(root).GetRoot().DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax>().Should().BeEmpty();
    }

    [Fact]
    public void Generator_Should_Infer_Omitted_Paths_While_Preserving_Explicit_Overrides()
    {
        var source = """
            using DRN.Framework.Hosting.Endpoints;
            using Microsoft.AspNetCore.Mvc;
            [assembly: AccessorController(typeof(TestProgram), typeof(ItemsController), TypeName = "LegacyItemsFor", RouteTemplate = "")]
            [Route("/items")]
            public class ItemsController : ControllerBase { public void Read() {} }
            public class Consumer { public ApiEndpoint Read => new Helpers.EndpointFor.TestEndpointFor().Items.Read; }
            """ + ProgramSource;

        var result = Generate(source);
        result.Diagnostics.Should().BeEmpty();
        Generated(result, "LegacyItemsFor.g.cs").Should().Contain("base(\"\")")
            .And.Contain("[EndpointAction(\"Read\")]").And.Contain("public ApiEndpoint Read")
            .And.NotContain("Sensitive");
        Generated(result, "TestEndpointFor.g.cs").Should().Contain("LegacyItemsFor Items");

        var invalid = Generate(source.Replace("typeof(ItemsController), TypeName", "typeof(ItemsController), \"\", TypeName", StringComparison.Ordinal), checkCompilation: false);
        invalid.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "DRNHOSTING001" && diagnostic.GetMessage().Contains("nonempty"));
    }

    [Fact]
    public void Generator_Should_Keep_Framework_Bindings_When_Authored_And_Generated_Names_Collide()
    {
        var source = """
            using DRN.Framework.Hosting.Endpoints;
            [assembly: AccessorController(typeof(TestProgram), typeof(ItemsController), "Group.Items")]
            [assembly: AccessorGroup(typeof(TestProgram), "Group", "ApiEndpoint")]
            public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase { public void Read() {} }
            namespace Helpers
            {
                public class ControllerForBase<T> {}
                public class EndpointActionAttribute : System.Attribute {}
            }
            public class Consumer
            {
                public DRN.Framework.Hosting.Endpoints.ApiEndpoint Read => new Helpers.EndpointFor.TestEndpointFor().Group.Items.Read;
            }
            """ + ProgramSource;

        var result = Generate(source);
        result.Diagnostics.Should().BeEmpty();
        Generated(result, "ItemsFor.g.cs").Should().Contain("global::DRN.Framework.Hosting.Endpoints.ApiEndpoint Read")
            .And.Contain("global::DRN.Framework.Hosting.Endpoints.ControllerForBase<")
            .And.Contain("global::DRN.Framework.Hosting.Endpoints.EndpointActionAttribute(")
            .And.NotContain("using global::DRN.Framework.Hosting.Endpoints;");
        Generated(result, "ApiEndpoint.g.cs").Should().NotContain("using global::DRN.Framework.Hosting.Endpoints;");
    }

    [Fact]
    public async Task Generated_Accessors_Should_Not_Require_Classification_Exemptions()
    {
        var source = """
            using DRN.Framework.Hosting.Endpoints;
            public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase { public void Password() {} }
            public class Consumer
            {
                public ApiEndpoint Get() => new Helpers.EndpointFor.TestEndpointFor().Items.Password;
                public string Read() => new Helpers.PageFor.TestPageFor().Root.Token;
            }
            """ + ProgramSource;
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new HostedAccessorGenerator().AsSourceGenerator()],
            additionalTexts: [new Page("Pages/Token.cshtml", "@page")], optionsProvider: new PageOptions(true));
        driver = driver.RunGeneratorsAndUpdateCompilation(CreateCompilation(source), out var updated, out _);

        driver.GetRunResult().Diagnostics.Should().BeEmpty();
        driver.GetRunResult().GeneratedTrees.Should().NotContain(tree => tree.ToString().Contains("Sensitive", StringComparison.Ordinal));
        var diagnostics = await AnalyzerTestCompilation.Analyze(updated, new DiagnosticClassificationAnalyzer());
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Generator_Should_Include_Only_Explicitly_Mapped_Referenced_Controllers()
    {
        var referenced = CreateCompilation("""
            namespace Referenced
            {
                public class IncludedController : Microsoft.AspNetCore.Mvc.ControllerBase
                {
                    public void Read(System.Guid id) {}
                }
                public class OtherController : Microsoft.AspNetCore.Mvc.ControllerBase { public void Read() {} }
            }
            namespace Shadows { public class ApiEndpoint {} }
            """, "ReferencedControllers");
        var reference = AnalyzerTestCompilation.EmitReference(referenced);
        var source = """
            global using Shadows;
            using DRN.Framework.Hosting.Endpoints;
            [assembly: AccessorController(typeof(TestProgram), typeof(Referenced.IncludedController))]
            public class Consumer { public DRN.Framework.Hosting.Endpoints.ApiEndpoint Read => new Helpers.EndpointFor.TestEndpointFor().Included.Read; }
            """ + ProgramSource;
        var result = Generate(source, references: [reference]);

        result.Diagnostics.Should().BeEmpty();
        Generated(result, "IncludedFor.g.cs").Should().Contain("typeof(global::System.Guid)")
            .And.Contain("global::DRN.Framework.Hosting.Endpoints.ApiEndpoint Read");
        result.GeneratedTrees.Should().NotContain(tree => tree.FilePath.EndsWith("OtherFor.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Generator_Should_Invalidate_Changed_And_Removed_Inputs_On_The_Same_Driver()
    {
        const string declarations = """
            using DRN.Framework.Hosting.Endpoints;
            public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase { public void Read(int id) {} }
            """;
        var page = new Page("Pages/Home.cshtml", "@page");
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new HostedAccessorGenerator().AsSourceGenerator()],
            additionalTexts: [page], optionsProvider: new PageOptions(true));
        driver = driver.RunGeneratorsAndUpdateCompilation(CreateCompilation(declarations + ProgramSource), out var first, out _);
        driver.GetRunResult().Diagnostics.Should().BeEmpty();
        first.AssertNoCompilerErrors();
        Generated(driver.GetRunResult(), "ItemsFor.g.cs").Should().Contain("typeof(int)");
        Generated(driver.GetRunResult(), "RootPageFor.g.cs").Should().Contain("string Home");

        driver = driver.ReplaceAdditionalText(page, new Page("Pages/Home.cshtml", "<p>Not a page</p>"));
        driver = driver.RunGeneratorsAndUpdateCompilation(CreateCompilation(declarations.Replace("int id", "System.Guid id", StringComparison.Ordinal) + ProgramSource), out var second, out _);
        driver.GetRunResult().Diagnostics.Should().BeEmpty();
        second.AssertNoCompilerErrors();
        Generated(driver.GetRunResult(), "ItemsFor.g.cs").Should().Contain("typeof(global::System.Guid)").And.NotContain("typeof(int)");
        Generated(driver.GetRunResult(), "TestPageFor.g.cs").Should().Contain("PageCollectionBase<TestPageFor>");
        driver.GetRunResult().GeneratedTrees.Should().NotContain(tree => tree.FilePath.EndsWith("RootPageFor.g.cs", StringComparison.Ordinal));

        const string removed = "using DRN.Framework.Hosting.Endpoints;";
        driver = driver.RunGeneratorsAndUpdateCompilation(CreateCompilation(removed + ProgramSource), out var third, out _);
        driver.GetRunResult().Diagnostics.Should().BeEmpty();
        third.AssertNoCompilerErrors();
        driver.GetRunResult().GeneratedTrees.Should().NotContain(tree => tree.FilePath.EndsWith("ItemsFor.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Generator_Should_Infer_Only_Unambiguous_Standard_Controller_Templates()
    {
        var source = """
            using DRN.Framework.Hosting.Endpoints;
            using Microsoft.AspNetCore.Mvc;
            [assembly: AccessorController(typeof(TestProgram), typeof(OverrideController), "Override", RouteTemplate = "/legacy")]
            [assembly: AccessorController(typeof(TestProgram), typeof(EmptyController), "Empty", RouteTemplate = "")]
            public static class Routes { public const string Template = "/api/[controller]"; }
            [Route(Routes.Template)] public class DirectController : ControllerBase { }
            [Route(Routes.Template)] public abstract class RoutedBase : ControllerBase { }
            public class InheritedController : RoutedBase { }
            [Route(Routes.Template)] public class RepeatedController : RoutedBase { }
            [Route("/second")] public class MultipleController : RoutedBase { }
            public class PlainController : ControllerBase { }
            public class CustomRouteAttribute : RouteAttribute { public CustomRouteAttribute() : base("/custom") { } }
            [Route(Routes.Template), CustomRoute] public class CustomController : ControllerBase { }
            [Route(Routes.Template)] public class OverrideController : ControllerBase { }
            [Route(Routes.Template)] public class EmptyController : ControllerBase { }
            """ + ProgramSource;

        // Generate also checks compilation of the resulting consumer and generated APIs.
        var result = Generate(source);
        result.Diagnostics.Should().BeEmpty();
        foreach (var name in new[] { "Direct", "Inherited", "Repeated" })
            Generated(result, name + "For.g.cs").Should().Contain("base(\"/api/[controller]\")");
        foreach (var name in new[] { "Multiple", "Plain", "Custom", "Empty" })
            Generated(result, name + "For.g.cs").Should().Contain("base(\"\")");
        Generated(result, "OverrideFor.g.cs").Should().Contain("base(\"/legacy\")");
    }

    [Fact]
    public void Generator_Should_Resolve_Inherited_SourceKnown_Actions_And_Overrides()
    {
        var source = """
            using DRN.Framework.Hosting.Endpoints;
            using DRN.Framework.Hosting.Controllers;
            using DRN.Framework.SharedKernel.Domain;
            using Microsoft.AspNetCore.Mvc;
            [assembly: AccessorController(typeof(TestProgram), typeof(TagController), "QA.Tag")]
            [assembly: AccessorMember(typeof(TestProgram), "Endpoint.QA.Tag.GetAsync", Name = "Read")]
            public class Entity : AggregateRoot { public override Dto ToDto() => new(this); }
            public class Model { }
            public record CreateModel;
            public class TagController(ISourceKnownService<Entity, Model, CreateModel> service) : SourceKnownController<Entity, Model, CreateModel>(service)
            {
                [NonAction] public override System.Threading.Tasks.Task<long> CountAsync(System.Threading.CancellationToken token = default)
                    => base.CountAsync(token);
                [ApiExplorerSettings(IgnoreApi = true)] public override System.Threading.Tasks.Task<bool> AnyAsync(System.Threading.CancellationToken token = default)
                    => base.AnyAsync(token);
            }
            public class WriteController(ISourceKnownService<Entity, Model, CreateModel> service) : SourceKnownWriteOnlyController<Entity, Model, CreateModel>(service);
            public class ReadController(ISourceKnownService<Entity, Model> service) : SourceKnownReadOnlyController<Entity, Model>(service);
            """ + ProgramSource;
        var result = Generate(source);
        result.Diagnostics.Should().BeEmpty();
        var tag = Generated(result, "TagFor.g.cs");
        tag.Should().Contain("ApiEndpoint Read").And.Contain("EndpointAction(\"GetAsync\"")
            .And.Contain("ApiEndpoint AnyAsync").And.NotContain("ApiEndpoint CountAsync")
            .And.Contain("EndpointAction(\"PostAsync\", typeof(global::CreateModel), typeof(global::System.Threading.CancellationToken))")
            .And.Contain("EndpointAction(\"GetAsync\", typeof(global::System.Guid), typeof(global::System.Threading.CancellationToken))");
        var write = Generated(result, "WriteFor.g.cs");
        write.Should().Contain("ApiEndpoint PostAsync").And.Contain("ApiEndpoint DeleteAsync")
            .And.NotContain("ApiEndpoint GetAsync").And.NotContain("ApiEndpoint PaginateAsync");
        Members(tag).Should().Equal("AnyAsync", "DeleteAsync", "GetManyAsync", "PaginateAsync", "PaginateWithBodyAsync", "PaginateWithOptionsAsync", "PostAsync", "Read");
        Members(write).Should().Equal("DeleteAsync", "PostAsync");
        Members(Generated(result, "ReadFor.g.cs")).Should().Equal("AnyAsync", "CountAsync", "GetAsync", "GetManyAsync",
            "PaginateAsync", "PaginateWithBodyAsync", "PaginateWithOptionsAsync");
        Generated(result, "TestPageFor.g.cs").Should().Contain("PageCollectionBase<TestPageFor>");
    }

    [Fact]
    public void Generator_Should_Preserve_Page_Identity_Area_And_Partial_Customization_Deterministically()
    {
        var source = """
            using DRN.Framework.Hosting.Endpoints;
            [assembly: AccessorMember(typeof(TestProgram), "Page.Root.Manual", Exclude = true)]
            namespace Helpers.PageFor
            {
                public partial class RootPageFor { public string Manual { get; init; } = "/Manual"; }
            }
            """ + ProgramSource;
        AdditionalText[] pages = [new Page("Pages/Home.cshtml", "@page \"/\""),
            new Page("Pages/Manual.cshtml", "@page"), new Page("Areas/Admin/Pages/User/Edit.cshtml", "@page \"{id:guid}\""),
            new Page("Pages/Shared/_Layout.cshtml", "@*\n@page\n*@\n<html></html>")];
        var first = Generate(source, pages);
        var second = Generate(source, pages.Reverse().ToArray());
        first.Diagnostics.Should().BeEmpty();
        var home = Generated(first, "RootPageFor.g.cs");
        home.Should().Contain("= \"/Home\"").And.NotContain("string Manual").And.NotContain("Layout");
        Generated(first, "AreasAdminUserPageFor.g.cs").Should().Contain("const string Area = \"Admin\"")
            .And.Contain("= \"/User/Edit\"");
        first.GeneratedTrees.Select(tree => tree.ToString()).Should().Equal(second.GeneratedTrees.Select(tree => tree.ToString()));
        Generate(source, [pages[0], pages[1]]).GeneratedTrees.Should().NotContain(tree => tree.FilePath.Contains("AreasAdmin"));
    }

    [Theory]
    [DataInlineUnit("public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase { public void Read(int id) {} public void Read(string id) {} }", "overloads")]
    [DataInlineUnit("namespace Helpers.EndpointFor { public class TestEndpointFor {} }", "partial")]
    [DataInlineUnit("namespace Other { public class TestEndpointFor : DRN.Framework.Hosting.Endpoints.EndpointCollectionBase<TestProgram, TestEndpointFor> {} }", "another endpoint collection")]
    [DataInlineUnit("namespace Helpers.EndpointFor { public partial class TestEndpointFor { public static object EndpointCollection => new(); } }", "reserved for the inherited typed singleton")]
    public void Generator_Should_Diagnose_Ambiguous_Or_Conflicting_Declarations(string declarations, string reason)
    {
        var result = Generate("using DRN.Framework.Hosting.Endpoints;" + declarations + ProgramSource,
            checkCompilation: false);
        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "DRNHOSTING001" && diagnostic.GetMessage().Contains(reason));
    }

    [Theory]
    [DataInlineUnit("public class Outer { public class Catalog : EndpointCollectionBase<TestProgram, Catalog> {} }")]
    [DataInlineUnit("public class Outer { public class Inner { public class Catalog : EndpointCollectionBase<TestProgram, Catalog> {} } }")]
    public void Generator_Should_Reject_Nested_Endpoint_Collections(string declarations)
    {
        var result = Generate("using DRN.Framework.Hosting.Endpoints;" + declarations + ProgramSource,
            checkCompilation: false);

        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "DRNHOSTING001" &&
            diagnostic.GetMessage().Contains("another endpoint collection"));
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void Generator_Should_Reject_Stale_Customization_In_Ordinal_Order(bool includeMembers)
    {
        var declarations = """
            using DRN.Framework.Hosting.Endpoints;
            [assembly: AccessorGroup(typeof(TestProgram), "ZMissing", "ZFor")]
            [assembly: AccessorGroup(typeof(TestProgram), "AMissing", "AFor")]
            """;
        if (includeMembers)
            declarations += """
                [assembly: AccessorMember(typeof(TestProgram), "Endpoint.ZMissing.Action", Exclude = true)]
                [assembly: AccessorMember(typeof(TestProgram), "Endpoint.AMissing.Action", Exclude = true)]
                """;
        var stale = Generate(declarations + ProgramSource, checkCompilation: false);

        var diagnostic = stale.Diagnostics.Should().ContainSingle().Which;
        diagnostic.Id.Should().Be("DRNHOSTING001");
        diagnostic.GetMessage().Should().Be(includeMembers
            ? "AccessorMember 'Endpoint.AMissing.Action' does not identify an eligible action/page. Remove stale configuration."
            : "AccessorGroup 'AMissing' does not identify a generated group.");
        stale.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_Should_Skip_Controllers_Outside_The_Accessor_Contract()
    {
        var source = """
            using Microsoft.AspNetCore.Mvc;
            public class PlainController { public void Read() { } }
            [Controller] public class AttributedEndpoint { public void Read() { } }
            public class SupportedController : ControllerBase { public void Read() { } }
            """ + ProgramSource;

        var result = Generate(source);

        result.Diagnostics.Should().BeEmpty();
        Generated(result, "TestEndpointFor.g.cs").Should().Contain("SupportedFor Supported").And.NotContain("Plain").And.NotContain("Attributed");
        Generated(result, "TestPageFor.g.cs").Should().Contain("TestPageFor");
        Members(Generated(result, "SupportedFor.g.cs")).Should().Equal("Read");

        var mapped = Generate("""
            [assembly: DRN.Framework.Hosting.Endpoints.AccessorController(typeof(TestProgram), typeof(PlainController))]
            public class PlainController { public void Read() { } }
            """ + ProgramSource, checkCompilation: false);

        mapped.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNHOSTING001");
        mapped.GeneratedTrees.Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("void System.IDisposable.Dispose() { }", true)]
    [DataInlineUnit("public void Dispose() { }", false)]
    public void Generator_Should_Exclude_Only_The_Disposable_Interface_Implementation(string implementation, bool separateAction)
    {
        var action = separateAction ? "public void Dispose() { }" : "";
        var source = $$"""
            public abstract class DisposableBase : Microsoft.AspNetCore.Mvc.ControllerBase, System.IDisposable
            {
                {{implementation}}
            }
            public class ProbeController : DisposableBase
            {
                {{action}}
                public void Read() { }
            }
            """ + ProgramSource;

        var result = Generate(source);

        result.Diagnostics.Should().BeEmpty();
        string[] expected = separateAction ? ["Dispose", "Read"] : ["Read"];
        Members(Generated(result, "ProbeFor.g.cs")).Should().Equal(expected);
    }

    [Theory]
    [DataInlineUnit("public override void Dispose() { }", false)]
    [DataInlineUnit("void System.IDisposable.Dispose() { }", false)]
    [DataInlineUnit("public new void Dispose() { }", true)]
    public void Generator_Should_Check_Dispose_At_Its_Base_Definition(string implementation, bool separateAction)
    {
        var interfaces = separateAction ? "" : ", System.IDisposable";
        var source = $$"""
            public abstract class DisposableBase : Microsoft.AspNetCore.Mvc.ControllerBase, System.IDisposable
            {
                public virtual void Dispose() { }
            }
            public class ProbeController : DisposableBase{{interfaces}}
            {
                {{implementation}}
                public void Read() { }
            }
            """ + ProgramSource;

        var result = Generate(source);

        result.Diagnostics.Should().BeEmpty();
        string[] expected = separateAction ? ["Dispose", "Read"] : ["Read"];
        Members(Generated(result, "ProbeFor.g.cs")).Should().Equal(expected);
    }

    [Theory]
    [DataInlineUnit("public System.Threading.Tasks.ValueTask DisposeAsync() => default;", false)]
    [DataInlineUnit("System.Threading.Tasks.ValueTask System.IAsyncDisposable.DisposeAsync() => default;", false)]
    [DataInlineUnit("[Microsoft.AspNetCore.Mvc.NonAction] public System.Threading.Tasks.ValueTask DisposeAsync() => default;", false)]
    [DataInlineUnit("System.Threading.Tasks.ValueTask System.IAsyncDisposable.DisposeAsync() => default; public System.Threading.Tasks.ValueTask DisposeAsync() => default;", true)]
    public void Generator_Should_Exclude_Only_Async_Disposal_Implementations(string implementation, bool isAction)
    {
        var source = $$"""
            public class ProbeController : Microsoft.AspNetCore.Mvc.ControllerBase, System.IAsyncDisposable
            {
                {{implementation}}
                public void Read() { }
            }
            """ + ProgramSource;

        var result = Generate(source);

        result.Diagnostics.Should().BeEmpty();
        string[] expected = isAction ? ["DisposeAsync", "Read"] : ["Read"];
        Members(Generated(result, "ProbeFor.g.cs")).Should().Equal(expected);
    }

    [Theory]
    [DataInlineUnit("", false)]
    [DataInlineUnit("public override System.Threading.Tasks.ValueTask DisposeAsync() => default;", false)]
    [DataInlineUnit("System.Threading.Tasks.ValueTask System.IAsyncDisposable.DisposeAsync() => default;", false)]
    [DataInlineUnit("public new System.Threading.Tasks.ValueTask DisposeAsync() => default;", true)]
    public void Generator_Should_Check_Async_Dispose_At_Its_Base_Definition(string implementation, bool separateAction)
    {
        var interfaces = separateAction ? "" : ", System.IAsyncDisposable";
        var source = $$"""
            public abstract class DisposableBase : Microsoft.AspNetCore.Mvc.ControllerBase, System.IAsyncDisposable
            {
                public virtual System.Threading.Tasks.ValueTask DisposeAsync() => default;
            }
            public class ProbeController : DisposableBase{{interfaces}}
            {
                {{implementation}}
                public void Read() { }
            }
            """ + ProgramSource;

        var result = Generate(source);

        result.Diagnostics.Should().BeEmpty();
        string[] expected = separateAction ? ["DisposeAsync", "Read"] : ["Read"];
        Members(Generated(result, "ProbeFor.g.cs")).Should().Equal(expected);
    }

    [Fact]
    public void Generator_Should_Not_Restore_Base_Actions_Hidden_By_Ineligible_Methods()
    {
        var source = """
            using Microsoft.AspNetCore.Mvc;
            public abstract class ProbeBase : ControllerBase
            {
                public void Hidden() { }
                public virtual void Disabled() { }
                public void Read() { }
            }
            public class ProbeController : ProbeBase, System.IDisposable
            {
                public new static void Hidden() { }
                [NonAction] public override void Disabled() { }
                public void Generic<T>() { }
                public void Dispose() { }
                public override string ToString() => "probe";
            }
            """ + ProgramSource;

        var result = Generate(source);

        result.Diagnostics.Should().BeEmpty();
        Members(Generated(result, "ProbeFor.g.cs")).Should().Equal("Read");
    }

    [Fact]
    public void Generator_Should_Discover_Mvc_Actions_Across_Route_And_Http_Attribute_Forms()
    {
        var source = """
            using Microsoft.AspNetCore.Mvc;
            using Microsoft.AspNetCore.Mvc.Routing;
            public sealed class CustomVerbAttribute() : HttpMethodAttribute(["CUSTOM"]);
            public sealed class CustomMethodsAttribute : System.Attribute, IActionHttpMethodProvider
            {
                public System.Collections.Generic.IEnumerable<string> HttpMethods => ["SEARCH"];
            }
            public sealed class CustomRouteAttribute : System.Attribute, IRouteTemplateProvider
            {
                public string Template => "custom";
                public int? Order => null;
                public string Name => "CustomRoute";
            }
            public abstract class ProbeBase : ControllerBase
            {
                [HttpGet("inherited")] public void InheritedAction() { }
                [HttpPost("override")] public virtual void OverriddenAction() { }
                [NonAction] public virtual void DisabledAction() { }
            }
            public interface IProbeActions
            {
                void InterfaceAction();
                void ExplicitAction();
            }
            [Route("api/[controller]")]
            public class ProbeController : ProbeBase, IProbeActions
            {
                [HttpGet] public void GetAction() { }
                [HttpPost("post")] public void PostAction() { }
                [HttpPut("put")] public void PutAction() { }
                [HttpPatch("patch")] public void PatchAction() { }
                [HttpDelete("delete")] public void DeleteAction() { }
                [HttpHead("head")] public void HeadAction() { }
                [HttpOptions("options")] public void OptionsAction() { }
                [AcceptVerbs("GET", "POST", Route = "accepted")] public void AcceptedAction() { }
                [Route("route")] public void RouteAction() { }
                [HttpGet("first"), HttpPost("second"), Route("third")] public void MultipleRoutesAction() { }
                [CustomVerb, Route("verb")] public void CustomVerbAction() { }
                [CustomMethods, Route("methods")] public void CustomMethodsAction() { }
                [CustomRoute] public void CustomRouteAction() { }
                [ActionName("Renamed"), HttpGet("renamed")] public void NamedActionAsync() { }
                [ApiExplorerSettings(IgnoreApi = true), HttpGet("hidden-docs")] public void UndocumentedAction() { }
                public override void OverriddenAction() { }
                public override void DisabledAction() { }
                public void InterfaceAction() { }
                void IProbeActions.ExplicitAction() { }
                [HttpGet("non-action"), NonAction] public void ExcludedAction() { }
                [HttpGet("private")] private void PrivateAction() { }
                [HttpGet("protected")] protected void ProtectedAction() { }
                [HttpGet("internal")] internal void InternalAction() { }
                [HttpGet("static")] public static void StaticAction() { }
                [HttpGet("generic")] public void GenericAction<T>() { }
                public string Property => "not an action";
                public override string ToString() => "not an action";
            }
            public class ConventionalController : ControllerBase
            {
                public void ConventionalAction() { }
            }
            """ + ProgramSource;

        var result = Generate(source);

        result.Diagnostics.Should().BeEmpty();
        Members(Generated(result, "ProbeFor.g.cs")).Should().Equal(ExpectedProbeActions.Order(StringComparer.Ordinal));
        Members(Generated(result, "ConventionalFor.g.cs")).Should().Equal("ConventionalAction");
        Generated(result, "ProbeFor.g.cs").Should().Contain("EndpointAction(\"NamedActionAsync\")")
            .And.NotContain("EndpointAction(\"Renamed\")");
    }

    [Fact]
    public void Generator_Should_Keep_Program_Aliases_And_Exclusions_Separate_When_Sharing_Controller_Actions()
    {
        var source = """
            using DRN.Framework.Hosting.Endpoints;
            [assembly: AccessorMember(typeof(First.TestProgram), "Endpoint.Items.Read", Name = "Fetch")]
            [assembly: AccessorMember(typeof(First.TestProgram), "Endpoint.Items.Write", Exclude = true)]
            [assembly: AccessorMember(typeof(Second.OtherProgram), "Endpoint.Items.Write", Name = "Save")]
            public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase
            {
                [Microsoft.AspNetCore.Mvc.HttpGet("items")] public void Read() { }
                [Microsoft.AspNetCore.Mvc.HttpPost("items")] public void Write() { }
            }
            """ + "namespace First {" + ProgramSource + "} namespace Second {" +
            ProgramSource.Replace("TestProgram", "OtherProgram", StringComparison.Ordinal) + "}";

        var result = Generate(source);

        result.Diagnostics.Should().BeEmpty();
        var first = Generated(result, "First.Helpers.EndpointFor.ItemsFor.g.cs");
        var second = Generated(result, "Second.Helpers.EndpointFor.ItemsFor.g.cs");
        Members(first).Should().Equal("Fetch");
        Members(second).Should().Equal("Read", "Save");
        first.Should().Contain("EndpointAction(\"Read\")");
        second.Should().Contain("EndpointAction(\"Write\")");
    }

    [Fact]
    public void Generator_Should_Isolate_Programs_And_Reject_Manual_Member_Collisions()
    {
        var declarations = """
            using DRN.Framework.Hosting.Endpoints;
            public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase
            {
                [Microsoft.AspNetCore.Mvc.ActionName("Renamed")]
                public void ReadAsync() { }
                [Microsoft.AspNetCore.Mvc.NonAction]
                public void InternalOperation() { }
            }
            """ + "namespace First {" + ProgramSource + "} namespace Second {" +
            ProgramSource.Replace("TestProgram", "OtherProgram", StringComparison.Ordinal) + "}";
        var result = Generate(declarations);
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedTrees.Should().Contain(tree => tree.FilePath.Contains("First.Helpers.EndpointFor.ItemsFor"));
        result.GeneratedTrees.Should().Contain(tree => tree.FilePath.Contains("Second.Helpers.EndpointFor.ItemsFor"));
        foreach (var leaf in result.GeneratedTrees.Where(tree => tree.FilePath.EndsWith("ItemsFor.g.cs", StringComparison.Ordinal)))
            Members(leaf.ToString()).Should().Equal("ReadAsync");

        var collision = Generate(declarations + "namespace First.Helpers.EndpointFor { public partial class ItemsFor { public int ReadAsync { get; } } }",
            checkCompilation: false);
        collision.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "DRNHOSTING001" && diagnostic.GetMessage().Contains("collides"));
    }

    [Theory]
    [DataInlineUnit("A.B.C.D.E.Items", false)]
    [DataInlineUnit("A.B.C.D.E.F.Items", true)]
    public void Generator_Should_Respect_Runtime_Discovery_Depth(string path, bool rejected)
    {
        var source = $$"""
            using DRN.Framework.Hosting.Endpoints;
            [assembly: AccessorController(typeof(TestProgram), typeof(ItemsController), "{{path}}")]
            public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase { public void Read() {} }
            """ + ProgramSource;
        var result = Generate(source);
        if (rejected)
        {
            result.GeneratedTrees.Should().BeEmpty();
            result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "DRNHOSTING001" &&
                diagnostic.GetMessage().Contains(path) && diagnostic.GetMessage().Contains("shorter controller alias"));
        }
        else
        {
            result.Diagnostics.Should().BeEmpty();
            Members(Generated(result, "ItemsFor.g.cs")).Should().Equal("Read");
        }
    }

    [Fact]
    public void Generator_Should_Exclude_Pages_Before_Validating_Folder_Identifiers()
    {
        var source = """
            using DRN.Framework.Hosting.Endpoints;
            [assembly: AccessorMember(typeof(TestProgram), "Page.user-profile.Edit", Exclude = true)]
            [assembly: AccessorMember(typeof(TestProgram), "Page.Areas.admin-tools.Root.Edit", Exclude = true)]
            [assembly: AccessorMember(typeof(TestProgram), "Page.Manual.Only", Exclude = true)]
            namespace Helpers.PageFor
            {
                public partial class RootPageFor { public string ProfileEdit { get; init; } = "/user-profile/Edit"; }
                public partial class ManualPageFor { public string Only { get; init; } = "/Manual/Only"; }
            }
            """ + ProgramSource;
        var result = Generate(source, [new Page("Pages/Home.cshtml", "@page"),
            new Page("Pages/user-profile/Edit.cshtml", "@page"), new Page("Areas/admin-tools/Pages/Edit.cshtml", "@page"),
            new Page("Pages/Manual/Only.cshtml", "@page")]);
        result.Diagnostics.Should().BeEmpty();
        Generated(result, "RootPageFor.g.cs").Should().Contain("string Home");
        Generated(result, "TestPageFor.g.cs").Should().Contain("ManualPageFor Manual");
        Generated(result, "ManualPageFor.g.cs").Should().Contain("PageForBase").And.NotContain("string Only");
        result.GeneratedTrees.Should().NotContain(tree => tree.ToString().Contains("user-profile") || tree.ToString().Contains("admin-tools"));
    }

    private static string Generated(GeneratorDriverRunResult result, string suffix) =>
        result.GeneratedTrees.Single(tree => tree.FilePath.EndsWith(suffix, StringComparison.Ordinal)).ToString();

    [Fact]
    public void Generator_Should_Compile_A_Larger_Consumer_Without_Timing_Assumptions()
    {
        var source = new StringBuilder("""
            using DRN.Framework.Hosting.Endpoints;
            """);
        for (var controller = 0; controller < 40; controller++)
        {
            source.Append($"public class Items{controller}Controller : Microsoft.AspNetCore.Mvc.ControllerBase {{");
            for (var action = 0; action < 4; action++)
                source.Append($"public void Operation{action}(System.Guid id) {{}}");
            source.Append('}');
        }
        source.Append("public class Consumer { public object[] Read() => [");
        for (var controller = 0; controller < 40; controller++)
            for (var action = 0; action < 4; action++)
                source.Append($"new Helpers.EndpointFor.TestEndpointFor().Items{controller}.Operation{action},");
        for (var page = 0; page < 24; page++)
            source.Append($"new Helpers.PageFor.TestPageFor().Catalog.Entry{page},");
        source.Append("]; }").Append(ProgramSource);
        var pages = Enumerable.Range(0, 24)
            .Select(index => (AdditionalText)new Page($"Pages/Catalog/Entry{index}.cshtml", "@page\n<p>Content</p>"))
            .ToArray();

        var result = Generate(source.ToString(), pages);

        result.Diagnostics.Should().BeEmpty();
        Members(Generated(result, "Items39For.g.cs")).Should().Equal("Operation0", "Operation1", "Operation2", "Operation3");
    }

    [Fact]
    public void Generator_Should_Cache_Unchanged_Inputs_And_Reevaluate_Program_Changes()
    {
        var compilation = CreateCompilation(ProgramSource);
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new HostedAccessorGenerator().AsSourceGenerator()],
            optionsProvider: new PageOptions(true),
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
        Run(compilation);
        var original = driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()).ToArray();

        Run(compilation);
        driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()).Should().Equal(original);
        driver.GetRunResult().Results.Single().TrackedSteps["HostedAccessorInputs"].SelectMany(step => step.Outputs)
            .Should().OnlyContain(output => output.Reason == IncrementalStepRunReason.Cached);

        Run(CreateCompilation(ProgramSource.Replace("TestProgram", "RenamedProgram", StringComparison.Ordinal)));
        Generated(driver.GetRunResult(), "RenamedEndpointFor.g.cs").Should().Contain("global::RenamedProgram");
        driver.GetRunResult().GeneratedTrees.Should().NotContain(tree => tree.FilePath.EndsWith("TestEndpointFor.g.cs", StringComparison.Ordinal));

        Run(CreateCompilation("public class RenamedProgram {}"));
        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();

        Run(CreateCompilation("public class Ordinary {}" + ProgramSource));
        driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()).Should().Equal(original);

        Run(CreateCompilation("public class Ordinary {}"));
        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();

        void Run(CSharpCompilation input)
        {
            driver = driver.RunGeneratorsAndUpdateCompilation(input, out var updated, out _);
            driver.GetRunResult().Diagnostics.Should().BeEmpty();
            updated.AssertNoCompilerErrors();
        }
    }

    [Fact]
    public void Generator_Should_Cache_Rendering_After_CSharp_Body_Edits_And_Invalidate_Changed_Accessors()
    {
        const string controllers = """
            public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase { public void Read(int id) { } }
            public class OtherController : Microsoft.AspNetCore.Mvc.ControllerBase { public void List() { } }
            """;
        var compilation = CreateCompilation(controllers + ProgramSource);
        var declaration = compilation.SyntaxTrees.Single();
        var consumer = CSharpSyntaxTree.ParseText("public class Consumer { public int Read() => 1; }");
        compilation = compilation.AddSyntaxTrees(consumer);
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new HostedAccessorGenerator().AsSourceGenerator()],
            optionsProvider: new PageOptions(true),
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
        Run();
        var original = driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()).ToArray();
        var originalItems = Generated(driver.GetRunResult(), "ItemsFor.g.cs");

        var changedConsumer = CSharpSyntaxTree.ParseText("public class Consumer { public int Read() => 2; }");
        compilation = compilation.ReplaceSyntaxTree(consumer, changedConsumer);
        Run();
        AssertCached();

        var changedDeclaration = CSharpSyntaxTree.ParseText(controllers.Replace("Read(int id) { }", "Read(int id) { var value = id; }", StringComparison.Ordinal) + ProgramSource);
        compilation = compilation.ReplaceSyntaxTree(declaration, changedDeclaration);
        Run();
        AssertCached();

        var changedSignature = CSharpSyntaxTree.ParseText(controllers.Replace("int id", "string id", StringComparison.Ordinal) + ProgramSource);
        compilation = compilation.ReplaceSyntaxTree(changedDeclaration, changedSignature);
        Run();
        Generated(driver.GetRunResult(), "ItemsFor.g.cs").Should().NotBe(originalItems);
        driver.GetRunResult().Results.Single().TrackedSteps["HostedAccessorSources"].SelectMany(step => step.Outputs)
            .Should().ContainSingle(output => output.Reason == IncrementalStepRunReason.Modified);

        void AssertCached()
        {
            driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()).Should().Equal(original);
            driver.GetRunResult().Results.Single().TrackedSteps["HostedAccessorSources"].SelectMany(step => step.Outputs)
                .Should().NotBeEmpty().And.OnlyContain(output => output.Reason == IncrementalStepRunReason.Cached);
        }

        void Run()
        {
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
            driver.GetRunResult().Diagnostics.Should().BeEmpty();
            updated.AssertNoCompilerErrors();
        }
    }

    [Fact]
    public void Generator_Should_Reevaluate_Unchanged_Program_Syntax_When_Its_Base_Changes()
    {
        const string source = "public class IndirectProgram : BaseProgram {}";
        var tree = CSharpSyntaxTree.ParseText(source);
        var baseSource = ProgramSource.Replace("TestProgram", "IndirectProgram", StringComparison.Ordinal)
            .Replace("public class IndirectProgram", "public abstract class BaseProgram", StringComparison.Ordinal);
        var compilation = CreateCompilation(baseSource).AddSyntaxTrees(tree);
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new HostedAccessorGenerator().AsSourceGenerator()],
            optionsProvider: new PageOptions(true));
        Run(compilation);
        driver.GetRunResult().GeneratedTrees.Should().HaveCount(2);

        var changedBase = CSharpSyntaxTree.ParseText("public abstract class BaseProgram {}");
        Run(compilation.ReplaceSyntaxTree(compilation.SyntaxTrees.First(), changedBase));
        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();

        Run(compilation);
        Generated(driver.GetRunResult(), "IndirectEndpointFor.g.cs").Should().Contain("global::IndirectProgram");

        void Run(CSharpCompilation input)
        {
            driver = driver.RunGeneratorsAndUpdateCompilation(input, out var updated, out _);
            driver.GetRunResult().Diagnostics.Should().BeEmpty();
            updated.AssertNoCompilerErrors();
        }
    }

    [Fact]
    public void Generator_Should_Cache_Razor_Body_Edits_And_Invalidate_Page_Identity_Changes()
    {
        const string declarations = """
            using DRN.Framework.Hosting.Endpoints;
            """;
        var compilation = CreateCompilation(declarations + ProgramSource);
        var page = new Page("Pages/Home.cshtml", "@page\n<p>Before</p>");
        var anchor = new Page("Pages/Anchor.cshtml", "@page");
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new HostedAccessorGenerator().AsSourceGenerator()],
            additionalTexts: [page, anchor], optionsProvider: new PageOptions(true),
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
        Run();
        var original = driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()).ToArray();

        var body = new Page(page.Path, "@page\n<section>After</section>");
        driver = driver.ReplaceAdditionalText(page, body);
        Run();
        driver.GetRunResult().GeneratedTrees.Select(tree => tree.ToString()).Should().Equal(original);
        var steps = driver.GetRunResult().Results.Single().TrackedSteps;
        steps["HostedAccessorPageIdentity"].SelectMany(step => step.Outputs)
            .Should().Contain(output => output.Reason == IncrementalStepRunReason.Unchanged);
        steps["HostedAccessorInputs"].SelectMany(step => step.Outputs)
            .Should().OnlyContain(output => output.Reason == IncrementalStepRunReason.Cached);

        var renamed = new Page("Pages/Dashboard.cshtml", "@page\n<section>After</section>");
        driver = driver.ReplaceAdditionalText(body, renamed);
        Run();
        Generated(driver.GetRunResult(), "RootPageFor.g.cs").Should().Contain("string Dashboard").And.NotContain("string Home");

        var partial = new Page(renamed.Path, "<section>Not a page</section>");
        driver = driver.ReplaceAdditionalText(renamed, partial);
        Run();
        Generated(driver.GetRunResult(), "RootPageFor.g.cs").Should().Contain("string Anchor").And.NotContain("string Dashboard");

        var movedLayout = new Page("Layouts/Moved.cshtml", "@* @page *@");
        driver = driver.ReplaceAdditionalText(partial, movedLayout);
        Run();
        driver.GetRunResult().Results.Single().TrackedSteps["HostedAccessorInputs"].SelectMany(step => step.Outputs)
            .Should().NotBeEmpty().And.OnlyContain(output => output.Reason == IncrementalStepRunReason.Cached);

        driver = driver.ReplaceAdditionalText(movedLayout, renamed);
        Run();
        Generated(driver.GetRunResult(), "RootPageFor.g.cs").Should().Contain("string Dashboard");
        driver = driver.RemoveAdditionalTexts([renamed]);
        Run();
        Generated(driver.GetRunResult(), "RootPageFor.g.cs").Should().Contain("string Anchor").And.NotContain("string Dashboard");

        driver = driver.RemoveAdditionalTexts([anchor]);
        Run();
        Generated(driver.GetRunResult(), "TestPageFor.g.cs").Should().Contain("PageCollectionBase<TestPageFor>");
        driver.GetRunResult().GeneratedTrees.Should().NotContain(tree => tree.FilePath.EndsWith("RootPageFor.g.cs", StringComparison.Ordinal));

        driver = driver.AddAdditionalTexts([page]);
        Run();
        Generated(driver.GetRunResult(), "RootPageFor.g.cs").Should().Contain("string Home");

        void Run()
        {
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
            driver.GetRunResult().Diagnostics.Should().BeEmpty();
            updated.AssertNoCompilerErrors();
        }
    }

    [Fact]
    public void Generator_Should_Reevaluate_Global_Using_Shadows_On_A_Reused_Driver()
    {
        var referenced = CreateCompilation("namespace Shadows { public class ApiEndpoint {} }", "GlobalUsingShadow");
        var reference = AnalyzerTestCompilation.EmitReference(referenced);
        const string declarations = """
            using DRN.Framework.Hosting.Endpoints;
            public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase { public void Read() {} }
            public class Consumer
            {
                public DRN.Framework.Hosting.Endpoints.ApiEndpoint Read => new Helpers.EndpointFor.TestEndpointFor().Items.Read;
            }
            """;
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new HostedAccessorGenerator().AsSourceGenerator()],
            optionsProvider: new PageOptions(true));
        foreach (var globalUsing in new[] { "", "global using Shadows;", "", "global using ApiEndpoint = Shadows.ApiEndpoint;", "" })
        {
            var compilation = CreateCompilation(globalUsing + declarations + ProgramSource, additionalReferences: [reference]);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
            driver.GetRunResult().Diagnostics.Should().BeEmpty();
            updated.AssertNoCompilerErrors();
            var leaf = Generated(driver.GetRunResult(), "ItemsFor.g.cs");
            if (globalUsing.Length == 0)
                leaf.Should().Contain("public ApiEndpoint Read").And.NotContain("public global::DRN.Framework.Hosting.Endpoints.ApiEndpoint Read");
            else
                leaf.Should().Contain("public global::DRN.Framework.Hosting.Endpoints.ApiEndpoint Read");
        }
    }

    [Fact]
    public void Generator_Should_Share_Identical_Leaf_Types_Between_Separate_Program_Roots()
    {
        var source = "public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase { public void Read() {} }" +
            ProgramSource + ProgramSource.Replace("TestProgram", "OtherProgram", StringComparison.Ordinal);

        var result = Generate(source, [new Page("Pages/Home.cshtml", "@page")]);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedTrees.Should().HaveCount(6);
        result.GeneratedTrees.Should().ContainSingle(tree => tree.FilePath.EndsWith(".ItemsFor.g.cs", StringComparison.Ordinal));
        result.GeneratedTrees.Should().ContainSingle(tree => tree.FilePath.EndsWith(".RootPageFor.g.cs", StringComparison.Ordinal));
        Generated(result, "TestEndpointFor.g.cs").Should().Contain("EndpointCollectionBase<global::TestProgram, TestEndpointFor>")
            .And.Contain("public ItemsFor Items { get; } = new();");
        Generated(result, "OtherEndpointFor.g.cs").Should().Contain("EndpointCollectionBase<global::OtherProgram, OtherEndpointFor>")
            .And.Contain("public ItemsFor Items { get; } = new();");
        Generated(result, "TestPageFor.g.cs").Should().Contain("PageCollectionBase<TestPageFor>")
            .And.Contain("public RootPageFor Root { get; } = new();");
        Generated(result, "OtherPageFor.g.cs").Should().Contain("PageCollectionBase<OtherPageFor>")
            .And.Contain("public RootPageFor Root { get; } = new();");
    }

    [Fact]
    public void Generator_Should_Reject_Differently_Customized_Leaf_Types_Before_Emitting_Their_Roots()
    {
        var source = """
            using DRN.Framework.Hosting.Endpoints;
            [assembly: AccessorMember(typeof(TestProgram), "Endpoint.Items.Read", Name = "Fetch")]
            public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase { public void Read() {} }
            """ + ProgramSource + ProgramSource.Replace("TestProgram", "OtherProgram", StringComparison.Ordinal);

        var result = Generate(source);

        result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "DRNHOSTING001" && diagnostic.GetMessage().Contains("collides"));
        Generated(result, "ItemsFor.g.cs").Should().Contain("ApiEndpoint Read").And.NotContain("ApiEndpoint Fetch");
        Generated(result, "OtherEndpointFor.g.cs").Should().Contain("public ItemsFor Items { get; } = new();");
        result.GeneratedTrees.Should().NotContain(tree => tree.FilePath.EndsWith(".TestEndpointFor.g.cs", StringComparison.Ordinal) ||
            tree.FilePath.EndsWith(".TestPageFor.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Generator_Should_Account_For_Sibling_Host_Aliases_In_A_Shared_Output_Namespace()
    {
        var source = """
            using DRN.Framework.Hosting.Endpoints;
            [assembly: AccessorController(typeof(TestProgram), typeof(ItemsController), "First.Items", TypeName = "FirstItemsFor")]
            [assembly: AccessorController(typeof(ZOtherProgram), typeof(ItemsController), "Second.Items", TypeName = "SecondItemsFor")]
            [assembly: AccessorGroup(typeof(ZOtherProgram), "Second", "ApiEndpoint")]
            public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase { public void Read() {} }
            public class Consumer
            {
                public DRN.Framework.Hosting.Endpoints.ApiEndpoint First => new Helpers.EndpointFor.TestEndpointFor().First.Items.Read;
                public DRN.Framework.Hosting.Endpoints.ApiEndpoint Second => new Helpers.EndpointFor.ZOtherEndpointFor().Second.Items.Read;
            }
            """ + ProgramSource + ProgramSource.Replace("TestProgram", "ZOtherProgram", StringComparison.Ordinal);

        var result = Generate(source);

        result.Diagnostics.Should().BeEmpty();
        Generated(result, "FirstItemsFor.g.cs").Should().Contain("public global::DRN.Framework.Hosting.Endpoints.ApiEndpoint Read");
        Generated(result, "ApiEndpoint.g.cs").Should().Contain("SecondItemsFor Items");
    }

    private static IEnumerable<string> Members(string source) => CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes()
        .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax>()
        .Where(property => property.Type.ToString().EndsWith("ApiEndpoint", StringComparison.Ordinal))
        .Select(property => property.Identifier.ValueText).Order(StringComparer.Ordinal);

    private static CSharpCompilation CreateCompilation(string source, string name = "AccessorFixture", MetadataReference[]? additionalReferences = null)
    {
        return CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source)], AnalyzerTestCompilation.HostingReferences.Concat(additionalReferences ?? []),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static GeneratorDriverRunResult Generate(string source, AdditionalText[]? pages = null, bool checkCompilation = true, bool hosted = true,
        MetadataReference[]? references = null)
    {
        var compilation = CreateCompilation(source, additionalReferences: references);
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new HostedAccessorGenerator().AsSourceGenerator()],
            additionalTexts: pages ?? [], optionsProvider: new PageOptions(hosted));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
        if (checkCompilation)
            updated.AssertNoCompilerErrors();
        return driver.GetRunResult();
    }

    private sealed class Page(string path, string content) : AdditionalText
    {
        public override string Path => path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(content, Encoding.UTF8);
    }

    private sealed class UnreadablePage : AdditionalText
    {
        public override string Path => "Pages/Unused.cshtml";
        public override SourceText GetText(CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Disabled page generation must not read Razor text.");
    }

    private sealed class PageOptions(bool hosted) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new Options(null, hosted);
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new Options(textFile.Path, hosted);
    }

    private sealed class Options(string? path, bool hosted) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value)
        {
            if (key == "build_property.UsingMicrosoftNETSdkWeb") { value = hosted ? "true" : "false"; return true; }
            value = path ?? "";
            return path != null && key == "build_metadata.AdditionalFiles.TargetPath";
        }
    }
}
