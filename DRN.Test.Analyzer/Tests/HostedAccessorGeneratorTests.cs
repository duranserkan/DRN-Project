using System.IO;
using System.Text;
using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Generators;
using DRN.Framework.Testing.DataAttributes;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Test.Analyzer.Tests;

public class HostedAccessorGeneratorTests
{
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
    public void Generator_Should_Require_Explicit_OptIn()
    {
        var result = Generate(ProgramSource);
        result.GeneratedTrees.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Generator_Should_Inherit_The_Endpoint_Singleton_With_The_Generated_Root_Type()
    {
        var source = """
            using DRN.Framework.Hosting.Endpoints;
            using Test.Helpers.EndpointFor;
            [assembly: HostedAccessors(typeof(TestProgram), Namespace = "Test.Helpers")]
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
    public void Generator_Should_Not_Read_Razor_Text_Unless_A_Web_Host_Opts_In(bool optedIn, bool hosted)
    {
        var declaration = optedIn ? "[assembly: HostedAccessors(typeof(TestProgram))]" : "";
        var result = Generate("using DRN.Framework.Hosting.Endpoints;" + declaration + ProgramSource,
            [new UnreadablePage()], hosted: hosted);

        result.GeneratedTrees.Should().NotContain(tree => tree.FilePath.Contains(".PageFor.", StringComparison.Ordinal));
        if (hosted)
            result.Diagnostics.Should().BeEmpty();
        else
        {
            result.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("DRNHG001");
            result.Diagnostics[0].GetMessage().Should().Contain("SDK Web");
        }
    }

    [Fact]
    public void Generator_Should_Not_Index_Nested_Lookalike_Customization_Attributes()
    {
        var source = """
            using DRN.Framework.Hosting.Endpoints;
            [assembly: HostedAccessors(typeof(TestProgram), Namespace = "Test.Helpers")]
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
            [assembly: HostedAccessors(typeof(TestProgram), Namespace = "Test.Helpers")]
            public class Consumer
            {
                public Test.Helpers.PageFor.TestPageFor Pages => Test.Helpers.PageFor.TestPageFor.PageCollection;
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
            [assembly: HostedAccessors(typeof(TestProgram), Namespace = "Test.Helpers")]
            [assembly: AccessorController(typeof(TestProgram), typeof(ItemsController), TypeName = "LegacyItemsFor", RouteTemplate = "")]
            [Route("/items")]
            public class ItemsController : ControllerBase { public void Read() {} }
            public class Consumer { public ApiEndpoint Read => new Test.Helpers.EndpointFor.TestEndpointFor().Items.Read; }
            """ + ProgramSource;

        var result = Generate(source);
        result.Diagnostics.Should().BeEmpty();
        Generated(result, "LegacyItemsFor.g.cs").Should().Contain("base(\"\")")
            .And.Contain("[EndpointAction(\"Read\")]").And.Contain("public ApiEndpoint Read")
            .And.NotContain("Sensitive");
        Generated(result, "TestEndpointFor.g.cs").Should().Contain("LegacyItemsFor Items");

        var invalid = Generate(source.Replace("typeof(ItemsController), TypeName", "typeof(ItemsController), \"\", TypeName", StringComparison.Ordinal), checkCompilation: false);
        invalid.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "DRNHG001" && diagnostic.GetMessage().Contains("nonempty"));
    }

    [Fact]
    public void Generator_Should_Keep_Framework_Bindings_When_Authored_And_Generated_Names_Collide()
    {
        var source = """
            using DRN.Framework.Hosting.Endpoints;
            [assembly: HostedAccessors(typeof(TestProgram), Namespace = "Test.Helpers")]
            [assembly: AccessorController(typeof(TestProgram), typeof(ItemsController), "Group.Items")]
            [assembly: AccessorGroup(typeof(TestProgram), "Group", "ApiEndpoint")]
            public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase { public void Read() {} }
            namespace Test
            {
                public class ControllerForBase<T> {}
                public class EndpointActionAttribute : System.Attribute {}
            }
            public class Consumer
            {
                public DRN.Framework.Hosting.Endpoints.ApiEndpoint Read => new Test.Helpers.EndpointFor.TestEndpointFor().Group.Items.Read;
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
            [assembly: HostedAccessors(typeof(TestProgram), Namespace = "Test.Helpers")]
            public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase { public void Password() {} }
            public class Consumer
            {
                public ApiEndpoint Get() => new Test.Helpers.EndpointFor.TestEndpointFor().Items.Password;
                public string Read() => new Test.Helpers.PageFor.TestPageFor().Root.Token;
            }
            """ + ProgramSource;
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new HostedAccessorGenerator().AsSourceGenerator()],
            additionalTexts: [new Page("Pages/Token.cshtml", "@page")], optionsProvider: new PageOptions(true));
        driver = driver.RunGeneratorsAndUpdateCompilation(CreateCompilation(source), out var updated, out _);

        driver.GetRunResult().Diagnostics.Should().BeEmpty();
        updated.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        driver.GetRunResult().GeneratedTrees.Should().NotContain(tree => tree.ToString().Contains("Sensitive", StringComparison.Ordinal));
        var diagnostics = await updated.WithAnalyzers([new DiagnosticClassificationAnalyzer()]).GetAnalyzerDiagnosticsAsync();
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
        using var assembly = new MemoryStream();
        var emitted = referenced.Emit(assembly);
        emitted.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        var reference = MetadataReference.CreateFromImage(assembly.ToArray());
        var source = """
            global using Shadows;
            using DRN.Framework.Hosting.Endpoints;
            [assembly: HostedAccessors(typeof(TestProgram), Namespace = "Test.Helpers")]
            [assembly: AccessorController(typeof(TestProgram), typeof(Referenced.IncludedController))]
            public class Consumer { public DRN.Framework.Hosting.Endpoints.ApiEndpoint Read => new Test.Helpers.EndpointFor.TestEndpointFor().Included.Read; }
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
            [assembly: HostedAccessors(typeof(TestProgram), Namespace = "Test.Helpers")]
            public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase { public void Read(int id) {} }
            """;
        var page = new Page("Pages/Home.cshtml", "@page");
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new HostedAccessorGenerator().AsSourceGenerator()],
            additionalTexts: [page], optionsProvider: new PageOptions(true));
        driver = driver.RunGeneratorsAndUpdateCompilation(CreateCompilation(declarations + ProgramSource), out var first, out _);
        driver.GetRunResult().Diagnostics.Should().BeEmpty();
        first.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        Generated(driver.GetRunResult(), "ItemsFor.g.cs").Should().Contain("typeof(int)");
        Generated(driver.GetRunResult(), "RootPageFor.g.cs").Should().Contain("string Home");

        driver = driver.ReplaceAdditionalText(page, new Page("Pages/Home.cshtml", "<p>Not a page</p>"));
        driver = driver.RunGeneratorsAndUpdateCompilation(CreateCompilation(declarations.Replace("int id", "System.Guid id", StringComparison.Ordinal) + ProgramSource), out var second, out _);
        driver.GetRunResult().Diagnostics.Should().BeEmpty();
        second.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        Generated(driver.GetRunResult(), "ItemsFor.g.cs").Should().Contain("typeof(global::System.Guid)").And.NotContain("typeof(int)");
        Generated(driver.GetRunResult(), "TestPageFor.g.cs").Should().Contain("PageCollectionBase<TestPageFor>");
        driver.GetRunResult().GeneratedTrees.Should().NotContain(tree => tree.FilePath.EndsWith("RootPageFor.g.cs", StringComparison.Ordinal));

        const string removed = "using DRN.Framework.Hosting.Endpoints; [assembly: HostedAccessors(typeof(TestProgram), Namespace = \"Test.Helpers\")]";
        driver = driver.RunGeneratorsAndUpdateCompilation(CreateCompilation(removed + ProgramSource), out var third, out _);
        driver.GetRunResult().Diagnostics.Should().BeEmpty();
        third.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        driver.GetRunResult().GeneratedTrees.Should().NotContain(tree => tree.FilePath.EndsWith("ItemsFor.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Generator_Should_Infer_Only_Unambiguous_Standard_Controller_Templates()
    {
        var source = """
            using DRN.Framework.Hosting.Endpoints;
            using Microsoft.AspNetCore.Mvc;
            [assembly: HostedAccessors(typeof(TestProgram), Namespace = "Test.Helpers")]
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
    public void Generator_Should_Not_Emit_Helpers_In_Libraries_Even_When_Opted_In()
    {
        var result = Generate("using DRN.Framework.Hosting.Endpoints; [assembly: HostedAccessors(typeof(TestProgram))]" + ProgramSource,
            checkCompilation: false, hosted: false);
        result.GeneratedTrees.Should().BeEmpty();
        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "DRNHG001" && diagnostic.GetMessage().Contains("SDK Web"));
    }

    [Fact]
    public void Generator_Should_Resolve_Inherited_SourceKnown_Actions_And_Overrides()
    {
        var source = """
            using DRN.Framework.Hosting.Endpoints;
            using DRN.Framework.Hosting.Controllers;
            using DRN.Framework.SharedKernel.Domain;
            using DRN.Framework.SharedKernel.Domain.Repository;
            using Microsoft.AspNetCore.Mvc;
            [assembly: HostedAccessors(typeof(TestProgram), Namespace = "Test.Helpers")]
            [assembly: AccessorController(typeof(TestProgram), typeof(TagController), "QA.Tag")]
            [assembly: AccessorMember(typeof(TestProgram), "Endpoint.QA.Tag.GetAsync", Name = "Read")]
            public class Entity : AggregateRoot { }
            public class Model : Dto { }
            public record CreateModel;
            public class TagController(ISourceKnownRepository<Entity> repository) : SourceKnownController<Entity, Model, CreateModel>(repository)
            {
                protected override Model ToDto(Entity entity) => new();
                protected override System.Threading.Tasks.Task<Entity> CreateEntityAsync(CreateModel request, System.Threading.CancellationToken token)
                    => System.Threading.Tasks.Task.FromResult(new Entity());
                [NonAction] public override System.Threading.Tasks.Task<long> CountAsync(System.Threading.CancellationToken token = default)
                    => base.CountAsync(token);
                [ApiExplorerSettings(IgnoreApi = true)] public override System.Threading.Tasks.Task<bool> AnyAsync(System.Threading.CancellationToken token = default)
                    => base.AnyAsync(token);
            }
            public class WriteController(ISourceKnownRepository<Entity> repository) : SourceKnownWriteOnlyController<Entity, Model, CreateModel>(repository)
            {
                protected override Model ToDto(Entity entity) => new();
                protected override System.Threading.Tasks.Task<Entity> CreateEntityAsync(CreateModel request, System.Threading.CancellationToken token)
                    => System.Threading.Tasks.Task.FromResult(new Entity());
            }
            public class ReadController(ISourceKnownRepository<Entity> repository) : SourceKnownReadOnlyController<Entity, Model>(repository)
            {
                protected override Model ToDto(Entity entity) => new();
            }
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
            [assembly: HostedAccessors(typeof(TestProgram), Namespace = "Test.Helpers")]
            [assembly: AccessorMember(typeof(TestProgram), "Page.Root.Manual", Exclude = true)]
            namespace Test.Helpers.PageFor
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
    [DataInlineUnit("namespace Test.Helpers.EndpointFor { public class TestEndpointFor {} }", "partial")]
    [DataInlineUnit("namespace Other { public class TestEndpointFor : DRN.Framework.Hosting.Endpoints.EndpointCollectionBase<TestProgram, TestEndpointFor> {} }", "another endpoint collection")]
    [DataInlineUnit("namespace Test.Helpers.EndpointFor { public partial class TestEndpointFor { public static object EndpointCollection => new(); } }", "reserved for the inherited typed singleton")]
    public void Generator_Should_Diagnose_Ambiguous_Or_Conflicting_Declarations(string declarations, string reason)
    {
        var result = Generate("using DRN.Framework.Hosting.Endpoints; [assembly: HostedAccessors(typeof(TestProgram), Namespace = \"Test.Helpers\")]" + declarations + ProgramSource,
            checkCompilation: false);
        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "DRNHG001" && diagnostic.GetMessage().Contains(reason));
    }

    [Theory]
    [DataInlineUnit("public class Outer { public class Catalog : EndpointCollectionBase<TestProgram, Catalog> {} }")]
    [DataInlineUnit("public class Outer { public class Inner { public class Catalog : EndpointCollectionBase<TestProgram, Catalog> {} } }")]
    public void Generator_Should_Reject_Nested_Endpoint_Collections(string declarations)
    {
        var result = Generate("using DRN.Framework.Hosting.Endpoints; [assembly: HostedAccessors(typeof(TestProgram), Namespace = \"Test.Helpers\")]" + declarations + ProgramSource,
            checkCompilation: false);

        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "DRNHG001" &&
            diagnostic.GetMessage().Contains("another endpoint collection"));
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void Generator_Should_Reject_Stale_Customization()
    {
        var stale = Generate("using DRN.Framework.Hosting.Endpoints; [assembly: HostedAccessors(typeof(TestProgram), Namespace = \"Test.Helpers\")]" +
            "[assembly: AccessorMember(typeof(TestProgram), \"Endpoint.Missing.Action\", Exclude = true)]" + ProgramSource, checkCompilation: false);
        stale.Diagnostics.Should().Contain(diagnostic => diagnostic.GetMessage().Contains("stale"));
    }

    [Fact]
    public void Generator_Should_Isolate_Programs_And_Reject_Manual_Member_Collisions()
    {
        var declarations = """
            using DRN.Framework.Hosting.Endpoints;
            [assembly: HostedAccessors(typeof(TestProgram), Namespace = "First.Helpers")]
            [assembly: HostedAccessors(typeof(OtherProgram), Namespace = "Second.Helpers")]
            public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase
            {
                [Microsoft.AspNetCore.Mvc.ActionName("Renamed")]
                public void ReadAsync() { }
                [Microsoft.AspNetCore.Mvc.NonAction]
                public void InternalOperation() { }
            }
            """ + ProgramSource + ProgramSource.Replace("TestProgram", "OtherProgram", StringComparison.Ordinal);
        var result = Generate(declarations);
        result.Diagnostics.Should().BeEmpty();
        result.GeneratedTrees.Should().Contain(tree => tree.FilePath.Contains("First.Helpers.EndpointFor.ItemsFor"));
        result.GeneratedTrees.Should().Contain(tree => tree.FilePath.Contains("Second.Helpers.EndpointFor.ItemsFor"));
        foreach (var leaf in result.GeneratedTrees.Where(tree => tree.FilePath.EndsWith("ItemsFor.g.cs", StringComparison.Ordinal)))
            Members(leaf.ToString()).Should().Equal("ReadAsync");

        var collision = Generate(declarations + "namespace First.Helpers.EndpointFor { public partial class ItemsFor { public int ReadAsync { get; } } }",
            checkCompilation: false);
        collision.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "DRNHG001" && diagnostic.GetMessage().Contains("collides"));
    }

    [Theory]
    [DataInlineUnit("A.B.C.D.E.Items", false)]
    [DataInlineUnit("A.B.C.D.E.F.Items", true)]
    public void Generator_Should_Respect_Runtime_Discovery_Depth(string path, bool rejected)
    {
        var source = $$"""
            using DRN.Framework.Hosting.Endpoints;
            [assembly: HostedAccessors(typeof(TestProgram), Namespace = "Test.Helpers")]
            [assembly: AccessorController(typeof(TestProgram), typeof(ItemsController), "{{path}}")]
            public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase { public void Read() {} }
            """ + ProgramSource;
        var result = Generate(source);
        if (rejected)
        {
            result.GeneratedTrees.Should().BeEmpty();
            result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "DRNHG001" &&
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
            [assembly: HostedAccessors(typeof(TestProgram), Namespace = "Test.Helpers")]
            [assembly: AccessorMember(typeof(TestProgram), "Page.user-profile.Edit", Exclude = true)]
            [assembly: AccessorMember(typeof(TestProgram), "Page.Areas.admin-tools.Root.Edit", Exclude = true)]
            [assembly: AccessorMember(typeof(TestProgram), "Page.Manual.Only", Exclude = true)]
            namespace Test.Helpers.PageFor
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
            [assembly: HostedAccessors(typeof(TestProgram), Namespace = "Test.Helpers")]
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
                source.Append($"new Test.Helpers.EndpointFor.TestEndpointFor().Items{controller}.Operation{action},");
        for (var page = 0; page < 24; page++)
            source.Append($"new Test.Helpers.PageFor.TestPageFor().Catalog.Entry{page},");
        source.Append("]; }").Append(ProgramSource);
        var pages = Enumerable.Range(0, 24)
            .Select(index => (AdditionalText)new Page($"Pages/Catalog/Entry{index}.cshtml", "@page\n<p>Content</p>"))
            .ToArray();

        var result = Generate(source.ToString(), pages);

        result.Diagnostics.Should().BeEmpty();
        Members(Generated(result, "Items39For.g.cs")).Should().Equal("Operation0", "Operation1", "Operation2", "Operation3");
    }

    [Fact]
    public void Generator_Should_Cache_Razor_Body_Edits_And_Invalidate_Page_Identity_Changes()
    {
        const string declarations = """
            using DRN.Framework.Hosting.Endpoints;
            [assembly: HostedAccessors(typeof(TestProgram), Namespace = "Test.Helpers")]
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

        driver = driver.ReplaceAdditionalText(partial, renamed);
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
            updated.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        }
    }

    [Fact]
    public void Generator_Should_Reevaluate_Global_Using_Shadows_On_A_Reused_Driver()
    {
        var referenced = CreateCompilation("namespace Shadows { public class ApiEndpoint {} }", "GlobalUsingShadow");
        using var assembly = new MemoryStream();
        referenced.Emit(assembly).Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        var reference = MetadataReference.CreateFromImage(assembly.ToArray());
        const string declarations = """
            using DRN.Framework.Hosting.Endpoints;
            [assembly: HostedAccessors(typeof(TestProgram), Namespace = "Test.Helpers")]
            public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase { public void Read() {} }
            public class Consumer
            {
                public DRN.Framework.Hosting.Endpoints.ApiEndpoint Read => new Test.Helpers.EndpointFor.TestEndpointFor().Items.Read;
            }
            """;
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new HostedAccessorGenerator().AsSourceGenerator()],
            optionsProvider: new PageOptions(true));
        foreach (var globalUsing in new[] { "", "global using Shadows;", "", "global using ApiEndpoint = Shadows.ApiEndpoint;", "" })
        {
            var compilation = CreateCompilation(globalUsing + declarations + ProgramSource, additionalReferences: [reference]);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
            driver.GetRunResult().Diagnostics.Should().BeEmpty();
            updated.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
            var leaf = Generated(driver.GetRunResult(), "ItemsFor.g.cs");
            if (globalUsing.Length == 0)
                leaf.Should().Contain("public ApiEndpoint Read").And.NotContain("public global::DRN.Framework.Hosting.Endpoints.ApiEndpoint Read");
            else
                leaf.Should().Contain("public global::DRN.Framework.Hosting.Endpoints.ApiEndpoint Read");
        }
    }

    [Fact]
    public void Generator_Should_Account_For_Sibling_Host_Aliases_In_A_Shared_Output_Namespace()
    {
        var source = """
            using DRN.Framework.Hosting.Endpoints;
            [assembly: HostedAccessors(typeof(TestProgram), Namespace = "Test.Helpers")]
            [assembly: HostedAccessors(typeof(ZOtherProgram), Namespace = "Test.Helpers")]
            [assembly: AccessorController(typeof(TestProgram), typeof(ItemsController), "First.Items", TypeName = "FirstItemsFor")]
            [assembly: AccessorController(typeof(ZOtherProgram), typeof(ItemsController), "Second.Items", TypeName = "SecondItemsFor")]
            [assembly: AccessorGroup(typeof(ZOtherProgram), "Second", "ApiEndpoint")]
            public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase { public void Read() {} }
            public class Consumer
            {
                public DRN.Framework.Hosting.Endpoints.ApiEndpoint First => new Test.Helpers.EndpointFor.TestEndpointFor().First.Items.Read;
                public DRN.Framework.Hosting.Endpoints.ApiEndpoint Second => new Test.Helpers.EndpointFor.ZOtherEndpointFor().Second.Items.Read;
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
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(HostedAccessorsAttribute).Assembly.Location)
            .Distinct(StringComparer.Ordinal).Select(path => MetadataReference.CreateFromFile(path));
        return CSharpCompilation.Create(name, [CSharpSyntaxTree.ParseText(source)], references.Concat(additionalReferences ?? []),
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
            updated.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
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
