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

    private static readonly Lazy<ImmutableArray<MetadataReference>> Classification = new(() =>
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator).Where(path => !Path.GetFileName(path).StartsWith("DRN.", StringComparison.Ordinal))
        .Append(typeof(Framework.SharedKernel.Attributes.SensitiveAttribute).Assembly.Location)
        .Append(typeof(NSubstitute.SubstituteExtensions).Assembly.Location)
        .Append(typeof(Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder).Assembly.Location)
        .Append(typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly.Location)
        .Append(typeof(Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute).Assembly.Location)
        .Append(typeof(Microsoft.Extensions.Logging.ILogger).Assembly.Location)
        .Distinct(StringComparer.Ordinal).Select(FileReference)
    ]);

    internal static MetadataReference ObjectReference => FileReference(typeof(object).Assembly.Location);
    internal static MetadataReference ConsoleReference => FileReference(typeof(Console).Assembly.Location);
    internal static MetadataReference SharedKernelReference => FileReference(typeof(Framework.SharedKernel.Domain.Dto).Assembly.Location);
    internal static MetadataReference UtilsReference => FileReference(typeof(Framework.Utils.Settings.IAppSettings).Assembly.Location);
    internal static MetadataReference EntityFrameworkReference => FileReference(typeof(Framework.EntityFramework.Context.DrnContext<>).Assembly.Location);
    internal static MetadataReference HostingReference => FileReference(typeof(Framework.Hosting.Endpoints.AccessorControllerAttribute).Assembly.Location);

    internal static ImmutableArray<MetadataReference> CoreReferences => Core.Value;
    internal static ImmutableArray<MetadataReference> HostingReferences => Hosted.Value;
    internal static ImmutableArray<MetadataReference> ClassificationReferences => Classification.Value;

    internal static MetadataReference FileReference(string path) =>
        FileReferences.GetOrAdd(path, static filePath => MetadataReference.CreateFromFile(filePath));

    internal static Task<ImmutableArray<Diagnostic>> Analyze(CSharpCompilation compilation, DiagnosticAnalyzer analyzer, AnalyzerOptions? options = null) =>
        Analyze(compilation, analyzer, options, CancellationToken.None);

    internal static Task<ImmutableArray<Diagnostic>> Analyze(CSharpCompilation compilation, DiagnosticAnalyzer analyzer,
        AnalyzerOptions? options, CancellationToken cancellationToken)
    {
        compilation.GetDiagnostics(cancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        return compilation.WithAnalyzers([analyzer], options).GetAnalyzerDiagnosticsAsync(cancellationToken);
    }

    internal static PortableExecutableReference EmitReference(CSharpCompilation compilation) =>
        EmitReference(compilation, filePath: null);

    internal static PortableExecutableReference EmitReference(CSharpCompilation compilation, string? filePath, bool metadataOnly = false)
    {
        using var stream = new MemoryStream();
        var options = metadataOnly ? new EmitOptions(metadataOnly: true, includePrivateMembers: false) : null;
        var result = compilation.Emit(stream, options: options);
        result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).Should().BeEmpty();
        result.Success.Should().BeTrue();
        return MetadataReference.CreateFromImage(stream.ToArray(), filePath: filePath);
    }

    internal static AnalyzerOptions OptionsWithProperties(IReadOnlyDictionary<string, string> properties, params AdditionalText[] files) =>
        new([.. files], new ConfigOptionsProvider(new ConfigOptions(properties)));

    internal static AnalyzerOptions ConventionOptions(string projectName, string? rootNamespace = null, params AdditionalText[] files) =>
        OptionsWithProperties(new Dictionary<string, string>
        {
            ["build_property.MSBuildProjectName"] = projectName,
            ["build_property.RootNamespace"] = rootNamespace ?? projectName
        }, files);

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

    internal sealed class ConfigOptionsProvider(AnalyzerConfigOptions global) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions => global;
        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => global;
        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => global;
    }
}
