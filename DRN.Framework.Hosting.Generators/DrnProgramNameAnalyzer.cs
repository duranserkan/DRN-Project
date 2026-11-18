using System.Collections.Immutable;
using DRN.Framework.CompilerTools;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace DRN.Framework.Hosting.Generators;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DrnProgramNameAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor ProgramSuffixRequired = new("DRNHOSTING002", "DRN program name must end with Program",
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

        if (GeneratedSource.FirstUserLocation(type, context) is { } location)
            context.ReportDiagnostic(Diagnostic.Create(ProgramSuffixRequired, location, type.Name));
    }
}
