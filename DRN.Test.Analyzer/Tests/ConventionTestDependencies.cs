using DRN.Framework.SharedKernel.Analyzers.Conventions;
using static DRN.Test.Analyzer.Tests.DiagnosticClassificationAnalyzerTests;

namespace DRN.Test.Analyzer.Tests;

// These fixtures isolate other diagnostics while supplying the architecture's required direct layers.
internal static class ConventionTestDependencies
{
    internal static (Compilation Compilation, ImmutableArray<ConventionInput> Records) WithRequiredLayers(
        Compilation compilation, string role)
    {
        string[] layers = role switch
        {
            "Hosted" => ["Application", "Infra"],
            "Domain" => ["Contract"],
            "Infra" => ["Domain"],
            _ => []
        };
        var records = ImmutableArray.CreateBuilder<ConventionInput>();
        foreach (var layer in layers)
        {
            var project = "Sample." + layer;
            var path = "/convention-fixtures/" + project;
            var dependency = Compilation($$"""
                [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "{{project}}")]
                [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "{{project}}")]
                namespace {{project}}.QA;
                public class RequiredLayerMarker;
                """).WithAssemblyName(project);
            using var image = new MemoryStream();
            dependency.Emit(image).Success.Should().BeTrue();
            compilation = compilation.AddReferences(MetadataReference.CreateFromImage(image.ToArray(), filePath: path + ".dll"));
            records.Add(new("Reference", path + ".csproj", ImmutableDictionary<string, string>.Empty
                .Add("Stage", "Project").Add("Source", path + ".csproj"), Location.None));
            records.Add(new("Reference", path + ".dll", ImmutableDictionary<string, string>.Empty
                .Add("Stage", "Resolved").Add("Project", path + ".csproj"), Location.None));
        }
        return (compilation, records.ToImmutable());
    }

    internal static string Serialize(IEnumerable<ConventionInput> records) => string.Join("\n", records.Select(record =>
        "[assembly: DRN.Conventions." + record.Kind + "(" + SymbolDisplay.FormatLiteral(record.Identity, true) +
        string.Concat(record.Fields.Select(field => ", " + field.Key + " = " + SymbolDisplay.FormatLiteral(field.Value, true))) + ")]"));

    internal static Compilation SuppressConventions(Compilation compilation) => compilation.WithOptions(compilation.Options
        .WithSpecificDiagnosticOptions(new SourceKnownConventionAnalyzer().SupportedDiagnostics.ToDictionary(
            descriptor => descriptor.Id, _ => ReportDiagnostic.Suppress)));
}
