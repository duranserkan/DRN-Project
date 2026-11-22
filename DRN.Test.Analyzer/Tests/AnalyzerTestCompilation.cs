using System.Collections.Concurrent;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Test.Analyzer.Tests;

internal static class AnalyzerTestCompilation
{
    private static readonly ConcurrentDictionary<string, MetadataReference> FileReferences = new(StringComparer.Ordinal);

    private static readonly Lazy<ImmutableArray<MetadataReference>> Core = new(() =>
    [
        ObjectReference,
        FileReference(Path.Combine(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "System.Runtime.dll"))
    ]);

    private static readonly Lazy<ImmutableArray<MetadataReference>> Hosted = new(() =>
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Append(typeof(Framework.Hosting.Endpoints.AccessorControllerAttribute).Assembly.Location)
        .Append(typeof(Framework.Utils.Settings.IAppSettings).Assembly.Location)
        .Append(typeof(Framework.SharedKernel.Domain.Dto).Assembly.Location)
        .Distinct(StringComparer.Ordinal).Select(FileReference).ToImmutableArray());

    internal static MetadataReference ObjectReference => FileReference(typeof(object).Assembly.Location);
    internal static MetadataReference ConsoleReference => FileReference(typeof(Console).Assembly.Location);
    internal static MetadataReference SharedKernelReference => FileReference(typeof(Framework.SharedKernel.Domain.Dto).Assembly.Location);
    internal static MetadataReference UtilsReference => FileReference(typeof(Framework.Utils.Settings.IAppSettings).Assembly.Location);
    internal static MetadataReference EntityFrameworkReference => FileReference(typeof(Framework.EntityFramework.Context.DrnContext<>).Assembly.Location);
    internal static MetadataReference HostingReference => FileReference(typeof(Framework.Hosting.Endpoints.AccessorControllerAttribute).Assembly.Location);

    internal static ImmutableArray<MetadataReference> CoreReferences => Core.Value;
    internal static ImmutableArray<MetadataReference> HostingReferences => Hosted.Value;

    internal static MetadataReference FileReference(string path) =>
        FileReferences.GetOrAdd(path, static filePath => MetadataReference.CreateFromFile(filePath));

    internal static void AssertNoCompilerErrors(this Compilation compilation) =>
        compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should().BeEmpty("fixture {0} must compile", compilation.AssemblyName ?? "<unnamed>");

    internal static Task<ImmutableArray<Diagnostic>> Analyze(Compilation compilation, DiagnosticAnalyzer analyzer, AnalyzerOptions? options = null)
    {
        compilation.AssertNoCompilerErrors();
        return compilation.WithAnalyzers([analyzer], options).GetAnalyzerDiagnosticsAsync();
    }

    internal static EmitOptions ReferenceAssemblyOptions { get; } = new(metadataOnly: true, includePrivateMembers: false);

    internal static PortableExecutableReference EmitReference(Compilation compilation, string? filePath = null, EmitOptions? options = null)
    {
        using var stream = new MemoryStream();
        var result = compilation.Emit(stream, options: options);
        result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        result.Success.Should().BeTrue();
        return MetadataReference.CreateFromImage(stream.ToArray(), filePath: filePath);
    }

    internal static AnalyzerOptions OptionsWithProperties(IReadOnlyDictionary<string, string> properties, params AdditionalText[] files) =>
        new([.. files], new ConfigOptionsProvider(new ConfigOptions(properties)));

    internal static AnalyzerConfigOptionsProvider EmptyOptionsProvider { get; } =
        new ConfigOptionsProvider(new ConfigOptions(ImmutableDictionary<string, string>.Empty));

    internal static AnalyzerOptions ConventionOptions(string projectName, string? rootNamespace = null, params AdditionalText[] files) =>
        new([.. files], ConventionOptionsProvider(projectName, rootNamespace));

    internal static AnalyzerConfigOptionsProvider ConventionOptionsProvider(string projectName, string? rootNamespace = null) =>
        new ConfigOptionsProvider(new ConfigOptions(new Dictionary<string, string>
        {
            ["build_property.MSBuildProjectName"] = projectName,
            ["build_property.RootNamespace"] = rootNamespace ?? projectName
        }));

    internal static AnalyzerConfigOptionsProvider ConventionOptionsWithProperties(
        string projectName, params (string Key, string Value)[] extra)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["build_property.MSBuildProjectName"] = projectName,
            ["build_property.RootNamespace"] = projectName
        };
        foreach (var (key, value) in extra) values["build_property." + key] = value;
        return new ConfigOptionsProvider(new ConfigOptions(values));
    }

    internal static Solution WithFoldersLayout(this Solution solution)
    {
        var layoutConfig = SourceText.From("is_global = true\nbuild_property.DrnDomainLayout = Folders\n");
        foreach (var project in solution.Projects.ToArray())
        {
            var configPath = Path.Combine(Path.GetDirectoryName(project.FilePath!)!, "layout.globalconfig");
            solution = solution.GetProject(project.Id)!
                .AddAnalyzerConfigDocument("layout.globalconfig", layoutConfig, filePath: configPath).Project.Solution;
        }
        return solution;
    }

    internal static SyntaxTree WideModelTree(string ns) => CSharpSyntaxTree.ParseText(
        $"namespace {ns}; public class Wide {{ " +
        string.Join("\n", Enumerable.Range(0, 4097).Select(index => $"public int Field{index};")) + " }",
        path: "Wide.cs");

    internal static string LocationText(Diagnostic diagnostic) =>
        diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan);

    internal class InMemoryAdditionalText(string path, string? text) : AdditionalText
    {
        public override string Path => path;
        public override SourceText? GetText(CancellationToken cancellationToken = default) => text is null ? null : SourceText.From(text);
    }

    internal sealed class ConfigOptions(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, out string value) => values.TryGetValue(key, out value!);
    }

    private sealed class ConfigOptionsProvider(AnalyzerConfigOptions global) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions => global;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => global;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => global;
    }
}
