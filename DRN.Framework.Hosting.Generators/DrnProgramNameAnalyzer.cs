using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DRN.Framework.Hosting.Generators;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DrnProgramNameAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor ProgramSuffixRequired = new("DRNHG002", "DRN program name must end with Program",
        "Class '{0}' inherits DrnProgramBase<TProgram> and must have a name ending with 'Program'", "Hosting", DiagnosticSeverity.Error, true);
    private static readonly ImmutableArray<DiagnosticDescriptor> Diagnostics = [ProgramSuffixRequired];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => Diagnostics;

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            var programBase = start.Compilation.GetTypeByMetadataName(DrnProgramConvention.BaseMetadataName);
            if (programBase is null) return;
            start.RegisterSymbolAction(symbol => AnalyzeType(symbol, programBase), SymbolKind.NamedType);
        });
    }

    private static void AnalyzeType(SymbolAnalysisContext context, INamedTypeSymbol programBase)
    {
        var type = (INamedTypeSymbol)context.Symbol;
        if (type.TypeKind != TypeKind.Class || type.Name.EndsWith("Program", StringComparison.Ordinal) ||
            DrnProgramConvention.FindBase(type, programBase) is null)
            return;

        foreach (var location in type.Locations)
        {
            if (!location.IsInSource) continue;
            context.ReportDiagnostic(Diagnostic.Create(ProgramSuffixRequired, location, type.Name));
            return;
        }
    }
}
