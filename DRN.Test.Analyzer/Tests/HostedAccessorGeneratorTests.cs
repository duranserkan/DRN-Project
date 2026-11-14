using System.IO;
using System.Text;
using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Generators;
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
            .And.Contain("ApiEndpoint AnyAsync").And.NotContain("ApiEndpoint CountAsync");
        var write = Generated(result, "WriteFor.g.cs");
        write.Should().Contain("ApiEndpoint PostAsync").And.Contain("ApiEndpoint DeleteAsync")
            .And.NotContain("ApiEndpoint GetAsync").And.NotContain("ApiEndpoint PaginateAsync");
        Members(tag).Should().Equal("AnyAsync", "DeleteAsync", "GetManyAsync", "PaginateAsync", "PaginateWithBodyAsync", "PaginateWithOptionsAsync", "PostAsync", "Read");
        Members(write).Should().Equal("DeleteAsync", "PostAsync");
        Members(Generated(result, "ReadFor.g.cs")).Should().Equal("AnyAsync", "CountAsync", "GetAsync", "GetManyAsync",
            "PaginateAsync", "PaginateWithBodyAsync", "PaginateWithOptionsAsync");
        result.GeneratedTrees.Should().NotContain(tree => tree.FilePath.Contains(".PageFor."));
    }

    [Fact]
    public void Generator_Should_Preserve_Page_Identity_Area_And_Partial_Customization_Deterministically()
    {
        var source = """
            using DRN.Framework.Hosting.Endpoints;
            [assembly: HostedAccessors(typeof(TestProgram), Namespace = "Test.Helpers", Pages = true)]
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
    [InlineData("public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase { public void Read(int id) {} public void Read(string id) {} }", "overloads")]
    [InlineData("namespace Test.Helpers.EndpointFor { public class TestEndpointFor {} }", "partial")]
    [InlineData("namespace Other { public class TestEndpointFor : DRN.Framework.Hosting.Endpoints.EndpointCollectionBase<TestProgram> {} }", "another endpoint collection")]
    public void Generator_Should_Diagnose_Ambiguous_Or_Conflicting_Declarations(string declarations, string reason)
    {
        var result = Generate("using DRN.Framework.Hosting.Endpoints; [assembly: HostedAccessors(typeof(TestProgram), Namespace = \"Test.Helpers\")]" + declarations + ProgramSource,
            checkCompilation: false);
        result.Diagnostics.Should().Contain(diagnostic => diagnostic.Id == "DRNHG001" && diagnostic.GetMessage().Contains(reason));
    }

    [Fact]
    public void Generator_Should_Reject_Missing_Page_Inputs_And_Stale_Customization()
    {
        var missing = Generate("using DRN.Framework.Hosting.Endpoints; [assembly: HostedAccessors(typeof(TestProgram), Pages = true)]" + ProgramSource,
            checkCompilation: false);
        missing.Diagnostics.Should().Contain(diagnostic => diagnostic.GetMessage().Contains("AdditionalFiles"));
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
    [InlineData("A.B.C.D.E.Items", false)]
    [InlineData("A.B.C.D.E.F.Items", true)]
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
            [assembly: HostedAccessors(typeof(TestProgram), Namespace = "Test.Helpers", Pages = true)]
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

    private static IEnumerable<string> Members(string source) => CSharpSyntaxTree.ParseText(source).GetRoot().DescendantNodes()
        .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax>()
        .Where(property => property.Type.ToString().EndsWith("ApiEndpoint", StringComparison.Ordinal))
        .Select(property => property.Identifier.ValueText).Order(StringComparer.Ordinal);

    private static GeneratorDriverRunResult Generate(string source, AdditionalText[]? pages = null, bool checkCompilation = true, bool hosted = true)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(HostedAccessorsAttribute).Assembly.Location)
            .Distinct(StringComparer.Ordinal).Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("AccessorFixture", [CSharpSyntaxTree.ParseText(source)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
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
