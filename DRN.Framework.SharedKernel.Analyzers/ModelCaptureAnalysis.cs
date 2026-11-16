using DRN.Framework.SharedKernel.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers;

internal sealed class ModelCaptureAnalysis(OperationAnalysisContext context, ClassificationContract contract,
    DiagnosticRedactionRules rules, IInvocationOperation call, bool flat, DiagnosticNameExemption exemptions,
    bool namesAcknowledged = false)
{
    private const int MaxModelDepth = 16;
    internal const int MaxModelWork = 16384;
    private const string SecondsSuffix = "_Seconds";
    private readonly HashSet<ITypeSymbol> _ancestors = new(SymbolEqualityComparer.Default);
    // Shared types must be revisited under different paths, so depth alone cannot bound work.
    private int _remainingWork = MaxModelWork;
    private bool _workLimitReported;

    private bool TryVisit()
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        if (_remainingWork == 0)
        {
            if (!_workLimitReported)
            {
                _workLimitReported = true;
                context.ReportDiagnostic(Diagnostic.Create(ClassificationDiagnosticDescriptors.IncompleteModelAnalysis,
                    call.Syntax.GetLocation()));
            }
            return false;
        }
        _remainingWork--;
        return true;
    }

    internal void Analyze(ITypeSymbol type, string path)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        if (contract.Positive(type, context.CancellationToken)) return;
        if (call.TargetMethod.Name == "Add" && contract.ModelType(type, context.CancellationToken).TimeSpan)
            path += SecondsSuffix;
        if (rules.MatchesPath(path) || !namesAcknowledged && !IsModelType(type) && rules.MatchesField(path) &&
            !exemptions.Allows(call.Syntax, path))
        {
            // Untyped roots still require an explicit capture decision.
            context.ReportDiagnostic(Diagnostic.Create(ClassificationDiagnosticDescriptors.PathClassification, call.Syntax.GetLocation(), path, type.Name));
            return;
        }
        Visit(type, path, IsModelType(type) ? "" : path, 0);
    }

    private void Visit(ITypeSymbol current, string currentPath, string namePath, int depth)
    {
        if (!TryVisit()) return;
        if (current is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            current = nullable.TypeArguments[0];
        if (ShouldSkipType(current, depth) || !_ancestors.Add(current)) return;
        try
        {
            if (current is IArrayTypeSymbol array) { Visit(array.ElementType, currentPath, namePath, depth + 1); return; }
            if (current is not INamedTypeSymbol named) return;
            VisitNamedType(named, currentPath, namePath, depth);
        }
        finally { _ancestors.Remove(current); }
    }

    private bool ShouldSkipType(ITypeSymbol current, int depth) =>
        depth > MaxModelDepth || contract.ModelType(current, context.CancellationToken).Scalar ||
        contract.Positive(current, context.CancellationToken);

    private void VisitNamedType(INamedTypeSymbol named, string currentPath, string namePath, int depth)
    {
        var shape = contract.ModelType(named, context.CancellationToken);
        if (shape.SkipProperties) return;
        if (shape.ListElement is { } element) { Visit(element, currentPath, namePath, depth + 1); return; }
        VisitProperties(named, currentPath, namePath, depth);
    }

    private void VisitProperties(INamedTypeSymbol named, string currentPath, string namePath, int depth)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var isModelType = IsModelType(named);
        for (var level = named; level != null; level = level.BaseType)
            foreach (var property in contract.DeclaredProperties(level, context.CancellationToken))
            {
                if (!TryVisit()) return;
                if (!names.Add(property.Name) || contract.Positive(property, context.CancellationToken) ||
                    contract.Positive(property.Type, context.CancellationToken) ||
                    contract.Positive(property.ContainingType, context.CancellationToken)) continue;
                var propertyNamePath = isModelType ? property.Name : namePath + "." + property.Name;
                VisitProperty(property, currentPath + "." + property.Name, propertyNamePath, depth);
            }
    }

    private void VisitProperty(IPropertySymbol property, string full, string propertyNamePath, int depth)
    {
        // Flat property capture converts TimeSpan to seconds and checks the resulting key again.
        if (flat && depth == 0 && contract.ModelType(property.Type, context.CancellationToken).TimeSpan)
        {
            full += SecondsSuffix;
            propertyNamePath += SecondsSuffix;
        }
        // Source own-name diagnostics are emitted once at declaration, independent of logging calls.
        if (rules.MatchesPath(full))
        {
            ReportProperty(property, full);
            return;
        }
        if (namesAcknowledged || !rules.MatchesField(propertyNamePath) || contract.Exempt(property, context.CancellationToken))
        {
            Visit(property.Type, full, propertyNamePath, depth + 1);
            return;
        }
        if (!rules.MatchesField(property.Name) || !property.Locations.Any(location => location.IsInSource))
        {
            if (exemptions.Allows(call.Syntax, full)) Visit(property.Type, full, propertyNamePath, depth + 1);
            else ReportProperty(property, full);
        }
        else Visit(property.Type, full, propertyNamePath, depth + 1);
    }

    private void ReportProperty(IPropertySymbol property, string path) =>
        context.ReportDiagnostic(Diagnostic.Create(ClassificationDiagnosticDescriptors.PathClassification, call.Syntax.GetLocation(), path, property.Name));

    private bool IsModelType(ITypeSymbol type) => contract.ModelType(type, context.CancellationToken).Model;
}
