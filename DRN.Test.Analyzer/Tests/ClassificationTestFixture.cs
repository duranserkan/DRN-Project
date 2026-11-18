namespace DRN.Test.Analyzer.Tests;

internal static class ClassificationTestFixture
{
    private static readonly Lazy<ImmutableArray<MetadataReference>> CachedReferences = new(() =>
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
        .Split(Path.PathSeparator).Where(path => !Path.GetFileName(path).StartsWith("DRN.", StringComparison.Ordinal))
        .Append(typeof(Framework.SharedKernel.Attributes.SensitiveAttribute).Assembly.Location)
        .Append(typeof(NSubstitute.SubstituteExtensions).Assembly.Location)
        .Append(typeof(Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder).Assembly.Location)
        .Append(typeof(Microsoft.EntityFrameworkCore.DbContext).Assembly.Location)
        .Append(typeof(Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute).Assembly.Location)
        .Append(typeof(Microsoft.Extensions.Logging.ILogger).Assembly.Location)
        .Distinct(StringComparer.Ordinal).Select(AnalyzerTestCompilation.FileReference)
    ]);

    internal static ImmutableArray<MetadataReference> References => CachedReferences.Value;

    internal static CSharpCompilation Compilation(string source, string path = "Models.cs") => CSharpCompilation.Create(
        "DiagnosticModelConsumer", [CSharpSyntaxTree.ParseText(source, path: path)], References,
        new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    internal static Task<ImmutableArray<Diagnostic>> Analyze(Compilation compilation, params AdditionalText[] files) =>
        AnalyzerTestCompilation.Analyze(compilation, new DiagnosticClassificationAnalyzer(), new AnalyzerOptions([.. files]));
}
