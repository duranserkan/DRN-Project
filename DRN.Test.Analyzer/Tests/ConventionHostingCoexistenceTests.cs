using DRN.Framework.Hosting.Generators;
using DRN.Framework.SharedKernel.Analyzers.Conventions;
using Microsoft.CodeAnalysis.Text;
using static DRN.Test.Analyzer.Tests.ProjectDiagnosticRulesTests;

namespace DRN.Test.Analyzer.Tests;

public sealed class ConventionHostingCoexistenceTests
{
    private const string Program = """
        namespace Sample.Hosted;
        public class SampleProgram : DRN.Framework.Hosting.DrnProgram.DrnProgramBase<SampleProgram>, DRN.Framework.Hosting.DrnProgram.IDrnProgram
        {
            public static System.Threading.Tasks.Task Main(string[] args) => System.Threading.Tasks.Task.CompletedTask;
            protected override System.Threading.Tasks.Task AddServicesAsync(Microsoft.AspNetCore.Builder.WebApplicationBuilder builder,
                DRN.Framework.Utils.Settings.IAppSettings settings, DRN.Framework.Utils.Logging.IScopedLog log)
                => System.Threading.Tasks.Task.CompletedTask;
        }
        """;

    private const string Controller = """
        namespace Sample.Hosted.QA;
        public class Order : DRN.Framework.SharedKernel.Domain.AggregateRoot;
        public class OrderDto : DRN.Framework.SharedKernel.Domain.Dto;
        public abstract class ControllerParent : DRN.Framework.Hosting.Controllers.SourceKnownControllerBase<Order, OrderDto>
        {
            protected ControllerParent(DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<Order> repository) : base(repository) { }
            protected override OrderDto ToDto(Order entity) => new();
            public void Unnamed() { }
            [Microsoft.AspNetCore.Authorization.AllowAnonymous]
            [Microsoft.AspNetCore.Authorization.Authorize("Readers")]
            public void Anonymous() { }
            [Microsoft.AspNetCore.Mvc.NonAction] public void InternalHelper() { }
            [Microsoft.AspNetCore.Authorization.Authorize("Readers")] public void Read() { }
        }
        public class OrdersController : ControllerParent
        {
            public OrdersController(DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<Order> repository) : base(repository) { }
        }
        """;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Convention_Severity_Should_Not_Weaken_Inherited_Action_Authorization(bool enabled)
    {
        Compilation compilation = Compile(Controller);
        if (!enabled) compilation = ConventionTestDependencies.SuppressConventions(compilation);
        var diagnostics = await compilation.WithAnalyzers(
            [new SourceKnownControllerAuthorizationAnalyzer(), new SourceKnownConventionAnalyzer()], Inputs(true)).GetAnalyzerDiagnosticsAsync();
        diagnostics.Should().NotContain(item => item.Id == "AD0001");
        var security = diagnostics.Where(item => item.Id == "DRN0026").ToArray();
        security.Should().HaveCount(2);
        security.Select(item => item.GetMessage()).Should().BeEquivalentTo(
            "Source-Known controller 'OrdersController' action 'Unnamed' requires an explicit named authorization policy: no explicit nonblank named policy is declared",
            "Source-Known controller 'OrdersController' action 'Anonymous' requires an explicit named authorization policy: anonymous access bypasses authorization");
        security.Should().OnlyContain(item => item.Severity == DiagnosticSeverity.Error);
        security.Select(LocationText).Should().OnlyContain(text => text == "OrdersController");
        diagnostics.Should().NotContain(item => item.Id == "DRNCV016" && item.GetMessage().Contains("controller", StringComparison.OrdinalIgnoreCase));
        if (enabled) diagnostics.Should().Contain(item => item.Id == "DRNCV016" && item.GetMessage().Contains("OrderDto"));
        else diagnostics.Should().NotContain(item => item.Id.StartsWith("DRNCV", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Program_Name_Guard_Should_Remain_An_Error_Independently_Of_Convention_Severity(bool enabled)
    {
        Compilation compilation = Compile(Program.Replace("SampleProgram", "SampleHost", StringComparison.Ordinal));
        if (!enabled) compilation = ConventionTestDependencies.SuppressConventions(compilation);
        var diagnostics = await compilation.WithAnalyzers([new DrnProgramNameAnalyzer(), new SourceKnownConventionAnalyzer()],
            Inputs(true)).GetAnalyzerDiagnosticsAsync();
        diagnostics.Should().NotContain(item => item.Id == "AD0001");
        var diagnostic = diagnostics.Where(item => item.Id == "DRNHG002").Should().ContainSingle().Which;
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Be("Class 'SampleHost' inherits DrnProgramBase<TProgram> and must have a name ending with 'Program'");
        LocationText(diagnostic).Should().Be("SampleHost");
    }

    [Fact]
    public async Task Reused_Accessor_Driver_Should_Preserve_Outputs_Across_Project_Classification_And_Irrelevant_Edits_Then_Remove_Deleted_Actions()
    {
        var compilation = Compile(Program, """
            namespace Sample.Hosted.QA;
            public class ItemsController : Microsoft.AspNetCore.Mvc.ControllerBase
            {
                [Microsoft.AspNetCore.Mvc.HttpGet] public void Read() { }
                [Microsoft.AspNetCore.Mvc.NonAction] public void InternalHelper() { }
            }
            """);
        var page = new RuleFile("Pages/Index.cshtml", "@page");
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new HostedAccessorGenerator().AsSourceGenerator()],
            additionalTexts: [page], optionsProvider: new Options(false));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var initial, out var generatorDiagnostics);
        generatorDiagnostics.Should().BeEmpty();
        initial.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        var baseline = Outputs(driver);
        baseline.Should().Contain(text => text.Contains("ApiEndpoint Read", StringComparison.Ordinal));
        baseline.Should().Contain(text => text.Contains("Index", StringComparison.Ordinal) && text.Contains("Page", StringComparison.Ordinal));
        baseline.Should().NotContain(text => text.Contains("ApiEndpoint InternalHelper", StringComparison.Ordinal));

        var firstTree = compilation.SyntaxTrees.First();
        var edited = compilation.ReplaceSyntaxTree(firstTree, CSharpSyntaxTree.ParseText(firstTree.GetText() + "\n// unrelated edit", path: firstTree.FilePath));
        driver = driver.WithUpdatedAnalyzerConfigOptions(new Options(true));
        driver = driver.RunGeneratorsAndUpdateCompilation(edited, out var enabled, out generatorDiagnostics);
        generatorDiagnostics.Should().BeEmpty();
        Outputs(driver).Should().Equal(baseline);
        enabled.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        var diagnostics = await enabled.WithAnalyzers([new SourceKnownConventionAnalyzer(), new DrnProgramNameAnalyzer()], Inputs(true))
            .GetAnalyzerDiagnosticsAsync();
        diagnostics.Should().NotContain(item => item.Id == "AD0001" || item.Id == "DRNHG002");

        driver = driver.WithUpdatedAnalyzerConfigOptions(new Options(true, "Application"));
        driver = driver.RunGeneratorsAndUpdateCompilation(edited, out var movedRole, out generatorDiagnostics);
        generatorDiagnostics.Should().BeEmpty();
        Outputs(driver).Should().Equal(baseline);
        var movedDiagnostics = await movedRole.WithAnalyzers([new SourceKnownConventionAnalyzer()], Inputs(true, "Application"))
            .GetAnalyzerDiagnosticsAsync();
        movedDiagnostics.Should().Contain(item => item.Id == "DRNCV016" && item.GetMessage() ==
            "Implementation 'ItemsController' (transport) belongs to role 'Hosted'.");

        var removed = edited.RemoveSyntaxTrees(edited.SyntaxTrees.Last());
        driver = driver.RunGeneratorsAndUpdateCompilation(removed, out var final, out generatorDiagnostics);
        generatorDiagnostics.Should().BeEmpty();
        Outputs(driver).Should().NotContain(text => text.Contains("ApiEndpoint Read", StringComparison.Ordinal));
        final.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void Canceled_Accessor_Run_Should_Not_Poison_A_Later_Convention_Enabled_Run()
    {
        var compilation = Compile(Program);
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new HostedAccessorGenerator().AsSourceGenerator()], optionsProvider: new Options(true));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Action canceled = () => driver.RunGenerators(compilation, cancellation.Token);
        canceled.Should().Throw<OperationCanceledException>();
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);
        diagnostics.Should().BeEmpty();
        output.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        driver.GetRunResult().GeneratedTrees.Should().HaveCount(2);
    }

    private static string[] Outputs(GeneratorDriver driver) => driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources)
        .OrderBy(source => source.HintName, StringComparer.Ordinal).Select(source => source.HintName + "\n" + source.SourceText).ToArray();

    private static CSharpCompilation Compile(params string[] sources)
    {
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(DRN.Framework.Hosting.Endpoints.AccessorControllerAttribute).Assembly.Location)
            .Append(typeof(DRN.Framework.Utils.Settings.IAppSettings).Assembly.Location)
            .Append(typeof(DRN.Framework.SharedKernel.Domain.Dto).Assembly.Location)
            .Distinct(StringComparer.Ordinal).Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("Sample.Hosted", sources.Select((source, index) =>
            CSharpSyntaxTree.ParseText(source, path: "Fixture" + index + ".cs")), references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        compilation.GetDiagnostics().Should().NotContain(item => item.Severity == DiagnosticSeverity.Error);
        return compilation;
    }

    private static AnalyzerOptions Inputs(bool enabled, string role = "Hosted") => new([new RuleFile(ConventionInputs.FileName, """
        [assembly: DRN.Conventions.Version("1")]
        [assembly: DRN.Conventions.Reference("Inventory", Stage = "Complete")]
        """)], new Options(enabled, role));

    private static string LocationText(Diagnostic diagnostic) => diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);

    private sealed class Options(bool enabled, string role = "Hosted") : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new ConventionInputTests.Config(new()
        {
            ["build_property.MSBuildProjectName"] = enabled ? "Sample." + role : "Sample.Tests." + role,
            ["build_property.RootNamespace"] = "Sample.Hosted",
            ["build_property.UsingMicrosoftNETSdkWeb"] = "true"
        });
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => GlobalOptions;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new ConventionInputTests.Config(new()
        {
            ["build_metadata.AdditionalFiles.TargetPath"] = textFile.Path
        });
    }
}
