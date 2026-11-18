using DRN.Framework.SharedKernel.Analyzers.Conventions;
using static DRN.Test.Analyzer.Tests.ClassificationTestFixture;

namespace DRN.Test.Analyzer.Tests;

// These fixtures isolate other diagnostics while supplying the architecture's required direct layers.
internal static class ConventionTestDependencies
{
    private static readonly SyntaxTree ModuleTree = CSharpSyntaxTree.ParseText("""
        public static class ConventionFixtureModule
        {
            public static Microsoft.Extensions.DependencyInjection.IServiceCollection Register(
                Microsoft.Extensions.DependencyInjection.IServiceCollection services)
            {
                DRN.Framework.Utils.DependencyInjection.ServiceCollectionExtensions.AddServicesWithAttributes(services);
                return services;
            }
        }
        """, path: "ConventionFixtureModule.cs");

    internal static CSharpCompilation WithModule(CSharpCompilation compilation) => compilation.AddSyntaxTrees(ModuleTree);

    private static readonly Lazy<Layer> Application = new(() => CreateLayer("Application"));
    private static readonly Lazy<Layer> Infra = new(() => CreateLayer("Infra"));
    private static readonly Lazy<Layer> Contract = new(() => CreateLayer("Contract"));
    private static readonly Lazy<Layer> Domain = new(() => CreateLayer("Domain"));

    internal static (Compilation Compilation, ImmutableArray<ConventionInput> Records) WithRequiredLayers(
        Compilation compilation, string role)
    {
        return role switch
        {
            "Hosted" => (compilation.AddReferences(Application.Value.Reference, Infra.Value.Reference),
                Application.Value.Records.AddRange(Infra.Value.Records)),
            "Domain" => (compilation.AddReferences(Contract.Value.Reference), Contract.Value.Records),
            "Infra" => (compilation.AddReferences(Domain.Value.Reference), Domain.Value.Records),
            _ => (compilation, [])
        };
    }

    private static Layer CreateLayer(string layer)
    {
        var project = "Sample." + layer;
        var path = "/convention-fixtures/" + project;
        var dependency = Compilation($$"""
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.ProjectName", "{{project}}")]
            [assembly: System.Reflection.AssemblyMetadata("DRN.Conventions.RootNamespace", "{{project}}")]
            namespace {{project}}.QA;
            public class RequiredLayerMarker;
            """).WithAssemblyName(project);
        return new(AnalyzerTestCompilation.EmitReference(dependency, path + ".dll"),
        [
            new("Reference", path + ".csproj", ImmutableDictionary<string, string>.Empty
                .Add("Stage", "Project").Add("Source", path + ".csproj"), Location.None),
            new("Reference", path + ".dll", ImmutableDictionary<string, string>.Empty
                .Add("Stage", "Resolved").Add("Project", path + ".csproj"), Location.None)
        ]);
    }

    private sealed record Layer(MetadataReference Reference, ImmutableArray<ConventionInput> Records);

    internal static string Serialize(IEnumerable<ConventionInput> records) => string.Join("\n", records.Select(record =>
        "[assembly: DRN.Conventions." + record.Kind + "(" + SymbolDisplay.FormatLiteral(record.Identity, true) +
        string.Concat(record.Fields.Select(field => ", " + field.Key + " = " + SymbolDisplay.FormatLiteral(field.Value, true))) + ")]"));

    internal static Compilation SuppressConventions(Compilation compilation) => compilation.WithOptions(compilation.Options
        .WithSpecificDiagnosticOptions(new SourceKnownConventionAnalyzer().SupportedDiagnostics.ToDictionary(
            descriptor => descriptor.Id, _ => ReportDiagnostic.Suppress)));
}
