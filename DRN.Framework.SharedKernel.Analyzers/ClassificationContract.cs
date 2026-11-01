using Microsoft.CodeAnalysis;

namespace DRN.Framework.SharedKernel.Analyzers;

internal sealed class ClassificationContract(INamedTypeSymbol sensitive, INamedTypeSymbol personal)
{
    internal INamedTypeSymbol Sensitive { get; } = sensitive;

    internal static bool Eligible(IPropertySymbol property) => !property.IsStatic && !property.IsIndexer &&
        property.DeclaredAccessibility == Accessibility.Public && property.GetMethod?.DeclaredAccessibility == Accessibility.Public;

    internal bool Accepted(IPropertySymbol property) => Positive(property) || Positive(property.ContainingType) ||
        Positive(property.Type) || Exempt(property);

    internal bool Exempt(IPropertySymbol property) => property.GetAttributes().Any(attribute =>
            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, Sensitive) && IsFalse(attribute));

    internal bool Positive(ISymbol symbol)
    {
        if (symbol is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            symbol = nullable.TypeArguments[0];
        for (ISymbol? current = symbol; current != null; current = current switch
             { IPropertySymbol property => property.OverriddenProperty, INamedTypeSymbol type => type.BaseType, _ => null })
            foreach (var attribute in current.GetAttributes())
                if ((SymbolEqualityComparer.Default.Equals(current, symbol) || Inherited(attribute)) &&
                    (Derives(attribute.AttributeClass, personal) || Derives(attribute.AttributeClass, Sensitive) &&
                    (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, Sensitive) || !IsFalse(attribute)))) return true;
        return false;
    }

    internal static bool HasUnknown(IPropertySymbol property) => property.GetAttributes().Concat(property.Type.GetAttributes())
        .Any(attribute => Ancestors(attribute.AttributeClass).Any(type =>
            type.ToDisplayString() == "Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute"));

    private static bool IsFalse(AttributeData attribute) => attribute.ConstructorArguments is [{ Value: false }];

    private static bool Inherited(AttributeData attribute)
    {
        foreach (var type in Ancestors(attribute.AttributeClass))
            foreach (var usage in type.GetAttributes())
                if (usage.AttributeClass?.ToDisplayString() == "System.AttributeUsageAttribute")
                    return !usage.NamedArguments.Any(pair => pair.Key == "Inherited" && pair.Value.Value is false);
        return true;
    }

    private static bool Derives(INamedTypeSymbol? type, INamedTypeSymbol parent) =>
        Ancestors(type).Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, parent));

    private static IEnumerable<INamedTypeSymbol> Ancestors(INamedTypeSymbol? type)
    {
        for (; type != null; type = type.BaseType) yield return type;
    }
}
