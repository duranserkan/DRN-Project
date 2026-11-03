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
    private const string TimeSpanTypeName = "System.TimeSpan";
    private const string NullableTimeSpanTypeName = TimeSpanTypeName + "?";
    private const string DateTimeOffsetTypeName = "System.DateTimeOffset";
    private const string DateOnlyTypeName = "System.DateOnly";
    private const string TimeOnlyTypeName = "System.TimeOnly";
    private const string UriTypeName = "System.Uri";
    private const string GuidTypeName = "System.Guid";
    private const string ScopeEventTypeName = "DRN.Framework.Utils.Logging.ScopeEvent";
    private const string JsonElementTypeName = "System.Text.Json.JsonElement";
    private const string EnumerableTypeName = "System.Collections.IEnumerable";
    private const string SecondsSuffix = "_Seconds";
    private readonly HashSet<ITypeSymbol> _ancestors = new(SymbolEqualityComparer.Default);

    internal void Analyze(ITypeSymbol type, string path)
    {
        if (contract.Positive(type)) return;
        if (call.TargetMethod.Name == "Add" && type.ToDisplayString() is TimeSpanTypeName or NullableTimeSpanTypeName)
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
        current.ToDisplayString() is TimeSpanTypeName or DateTimeOffsetTypeName or DateOnlyTypeName or TimeOnlyTypeName or
            UriTypeName or GuidTypeName or ScopeEventTypeName || contract.Positive(current);

    private void VisitNamedType(INamedTypeSymbol named, string currentPath, string namePath, int depth)
    {
        if (IsUri(named)) return;
        if (named.AllInterfaces.Any(item => item.ToDisplayString() == "System.Collections.IDictionary" ||
                item.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>")) return;
        if (named.ToDisplayString() == JsonElementTypeName) return;
        var list = named.AllInterfaces.FirstOrDefault(item => item.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IList<T>");
        if (list != null) { Visit(list.TypeArguments[0], currentPath, namePath, depth + 1); return; }
        if (named.AllInterfaces.Any(item => item.ToDisplayString() == EnumerableTypeName)) return;
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
        if (flat && depth == 0 && property.Type.ToDisplayString() is TimeSpanTypeName or NullableTimeSpanTypeName)
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
        if (namesAcknowledged || !rules.MatchesField(propertyNamePath) || contract.Exempt(property))
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

    private static bool IsModelType(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            type = nullable.TypeArguments[0];
        return type is INamedTypeSymbol { IsAnonymousType: false } named && type.SpecialType == SpecialType.None &&
            type.TypeKind != TypeKind.Enum &&
            type.ToDisplayString() is not (TimeSpanTypeName or DateTimeOffsetTypeName or DateOnlyTypeName or TimeOnlyTypeName or GuidTypeName or
                JsonElementTypeName or ScopeEventTypeName) &&
            !IsUri(named) &&
            type.ToDisplayString() != EnumerableTypeName &&
            !named.AllInterfaces.Any(item => item.ToDisplayString() == EnumerableTypeName);
    }

    private static bool IsUri(INamedTypeSymbol type)
    {
        for (var current = type; current != null; current = current.BaseType)
            if (current.ToDisplayString() == UriTypeName) return true;
        return false;
    }
}
