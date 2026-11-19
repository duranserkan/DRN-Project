namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SourceKnownConventionAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ConventionDiagnosticDescriptors.All;

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze);
        context.RegisterCompilationAction(Analyze);
    }

    private static void Analyze(CompilationAnalysisContext context)
    {
        var project = ConventionProject.Read(context.Compilation, context.Options.AnalyzerConfigOptionsProvider.GlobalOptions);
        if (!project.Enabled)
        {
            foreach (var diagnostic in project.Diagnostics) context.ReportDiagnostic(diagnostic);
            return;
        }

        var inputs = ConventionInputs.Read(context.Options, context.CancellationToken);
        var facts = new ConventionFacts(context.Compilation, project, inputs,
            context.Options.AnalyzerConfigOptionsProvider, context.CancellationToken);
        if (inputs.Diagnostics.IsEmpty && facts.Records.Count(record =>
                record.Kind == "Reference" && record.Identity == "Inventory" && record.Get("Stage") == "Complete") != 1)
            facts.Diagnostics.Add(ConventionProject.InputIssue("direct references", "missing or duplicated inventory provenance",
                "complete build-supplied reference inventory"));
        var exceptions = new ConventionExceptions(facts);
        ConventionOwnershipAnalysis.Analyze(facts);
        var registrations = ConventionRegistrationAnalysis.ReadInputs(facts, requireModule: true);
        var calls = new ConventionCallSites(facts, registrations.NeedsComposition);
        ConventionPersistenceAnalysis.AnalyzeWithContexts(facts, calls);
        ConventionRegistrationAnalysis.AnalyzeAll(facts, registrations, calls);
        ConventionBoundaryAnalysis.Analyze(facts);
        ConventionNamingAnalysis.Analyze(facts);
        ConventionDtoAnalysis.Analyze(facts);
        ConventionAggregateRepositoryAnalysis.Analyze(facts);
        ConventionInfrastructureAnalysis.Analyze(facts);
        foreach (var diagnostic in facts.Diagnostics)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (!exceptions.Applies(diagnostic)) context.ReportDiagnostic(diagnostic);
        }
    }
}
