using DRN.Framework.SharedKernel.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers;

internal sealed class ModelCaptureAnalysis(OperationAnalysisContext context, ClassificationContract contract,
    DiagnosticRedactionRules rules, IInvocationOperation call, bool flat)
{
    private const int MaxModelDepth = 16;
    private readonly HashSet<ITypeSymbol> _ancestors = new(SymbolEqualityComparer.Default);

    internal void Analyze(ITypeSymbol type, string path)
    {
        if (contract.Positive(type)) return;
        if (call.TargetMethod.Name == "Add" && type.ToDisplayString() is "System.TimeSpan" or "System.TimeSpan?")
            path += "_Seconds";
        if (rules.MatchesPath(path) || !IsModelType(type) && rules.MatchesField(path))
        {
            // Untyped roots still require an explicit capture decision.
            context.ReportDiagnostic(Diagnostic.Create(ClassificationDiagnosticDescriptors.PathClassification, call.Syntax.GetLocation(), path, type.Name));
            return;
        }
        Visit(type, path, IsModelType(type) ? "" : path, 0);
    }

    private void Visit(ITypeSymbol current, string currentPath, string namePath, int depth)
    {
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
        depth > MaxModelDepth || current.SpecialType != SpecialType.None || current.TypeKind is TypeKind.Enum or TypeKind.TypeParameter ||
        current.ToDisplayString() is "System.TimeSpan" or "System.DateTimeOffset" or "System.DateOnly" or "System.TimeOnly" or
            "System.Uri" or "System.Guid" or "DRN.Framework.Utils.Logging.ScopeEvent" || contract.Positive(current);

    private void VisitNamedType(INamedTypeSymbol named, string currentPath, string namePath, int depth)
    {
        if (IsUri(named)) return;
        if (named.AllInterfaces.Any(item => item.ToDisplayString() == "System.Collections.IDictionary" ||
                item.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>")) return;
        if (named.ToDisplayString() == "System.Text.Json.JsonElement") return;
        var list = named.AllInterfaces.FirstOrDefault(item => item.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IList<T>");
        if (list != null) { Visit(list.TypeArguments[0], currentPath, namePath, depth + 1); return; }
        if (named.AllInterfaces.Any(item => item.ToDisplayString() == "System.Collections.IEnumerable")) return;
        VisitProperties(named, currentPath, namePath, depth);
    }

    private void VisitProperties(INamedTypeSymbol named, string currentPath, string namePath, int depth)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        for (var level = named; level != null; level = level.BaseType)
            foreach (var property in level.GetMembers().OfType<IPropertySymbol>().Where(ClassificationContract.Eligible))
            {
                if (!names.Add(property.Name) || contract.Positive(property) || contract.Positive(property.Type) ||
                    contract.Positive(property.ContainingType)) continue;
                var propertyNamePath = IsModelType(named) ? property.Name : namePath + "." + property.Name;
                VisitProperty(property, currentPath + "." + property.Name, propertyNamePath, depth);
            }
    }

    private void VisitProperty(IPropertySymbol property, string full, string propertyNamePath, int depth)
    {
        // Flat property capture converts TimeSpan to seconds and checks the resulting key again.
        if (flat && depth == 0 && property.Type.ToDisplayString() is "System.TimeSpan" or "System.TimeSpan?")
        {
            full += "_Seconds";
            propertyNamePath += "_Seconds";
        }
        // Source own-name diagnostics are emitted once at declaration, independent of logging calls.
        if (rules.MatchesPath(full))
        {
            ReportProperty(property, full);
            return;
        }
        if (!rules.MatchesField(propertyNamePath) || contract.Exempt(property))
        {
            Visit(property.Type, full, propertyNamePath, depth + 1);
            return;
        }
        if (!rules.MatchesField(property.Name) || !property.Locations.Any(location => location.IsInSource))
            ReportProperty(property, full);
    }

    private void ReportProperty(IPropertySymbol property, string path) =>
        context.ReportDiagnostic(Diagnostic.Create(ClassificationDiagnosticDescriptors.PathClassification, call.Syntax.GetLocation(), path, property.Name));

    private static bool IsModelType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            type = nullable.TypeArguments[0];
        return type is INamedTypeSymbol { IsAnonymousType: false } named && type.SpecialType == SpecialType.None &&
            type.TypeKind != TypeKind.Enum &&
            type.ToDisplayString() is not ("System.TimeSpan" or "System.DateTimeOffset" or "System.DateOnly" or "System.TimeOnly" or "System.Guid" or
                "System.Text.Json.JsonElement" or "DRN.Framework.Utils.Logging.ScopeEvent") &&
            !IsUri(named) &&
            type.ToDisplayString() != "System.Collections.IEnumerable" &&
            !named.AllInterfaces.Any(item => item.ToDisplayString() == "System.Collections.IEnumerable");
    }

    private static bool IsUri(INamedTypeSymbol type)
    {
        for (var current = type; current != null; current = current.BaseType)
            if (current.ToDisplayString() == "System.Uri") return true;
        return false;
    }
}
