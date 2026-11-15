using System.Collections.Immutable;
using DRN.Framework.CompilerTools;
using Microsoft.CodeAnalysis;

namespace DRN.Framework.SharedKernel.Analyzers;

internal sealed class ClassificationContract
{
    private readonly INamedTypeSymbol _personal;
    private readonly SymbolFactCache<ISymbol, bool> _positive = new();
    private readonly SymbolFactCache<INamedTypeSymbol, ImmutableArray<IPropertySymbol>> _properties = new();
    private readonly Func<ISymbol, CancellationToken, bool> _calculatePositive;

    internal ClassificationContract(INamedTypeSymbol sensitive, INamedTypeSymbol personal)
    {
        Sensitive = sensitive;
        _personal = personal;
        _calculatePositive = CalculatePositive;
    }

    internal INamedTypeSymbol Sensitive { get; }

    internal static bool Eligible(IPropertySymbol property) => !property.IsStatic && !property.IsIndexer &&
        property.DeclaredAccessibility == Accessibility.Public && property.GetMethod?.DeclaredAccessibility == Accessibility.Public;

    internal bool Accepted(IPropertySymbol property, CancellationToken cancellation) => Positive(property, cancellation) ||
        Positive(property.ContainingType, cancellation) || Positive(property.Type, cancellation) || Exempt(property);

    internal bool Exempt(IPropertySymbol property) => property.GetAttributes().Any(attribute =>
            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, Sensitive) && IsFalse(attribute));

    internal bool Positive(ISymbol symbol, CancellationToken cancellation)
    {
        if (symbol is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            symbol = nullable.TypeArguments[0];
        return _positive.GetOrAdd(symbol, _calculatePositive, cancellation);
    }

    private bool CalculatePositive(ISymbol symbol, CancellationToken cancellation)
    {
        for (ISymbol? current = symbol; current != null; current = current switch
             { IPropertySymbol property => property.OverriddenProperty, INamedTypeSymbol type => type.BaseType, _ => null })
        {
            cancellation.ThrowIfCancellationRequested();
            foreach (var attribute in current.GetAttributes())
                if ((SymbolEqualityComparer.Default.Equals(current, symbol) || Inherited(attribute)) &&
                    (Derives(attribute.AttributeClass, _personal) || Derives(attribute.AttributeClass, Sensitive) &&
                    (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, Sensitive) || !IsFalse(attribute)))) return true;
        }
        return false;
    }

    internal ImmutableArray<IPropertySymbol> DeclaredProperties(INamedTypeSymbol type, CancellationToken cancellation) =>
        _properties.GetOrAdd(type, CollectProperties, cancellation);

    private static ImmutableArray<IPropertySymbol> CollectProperties(INamedTypeSymbol type, CancellationToken cancellation)
    {
        var properties = ImmutableArray.CreateBuilder<IPropertySymbol>();
        foreach (var member in type.GetMembers())
        {
            cancellation.ThrowIfCancellationRequested();
            if (member is not IPropertySymbol property || !Eligible(property)) continue;
            properties.Add(property);
            // A visit consumes work before properties. This prefix still reaches the same limit diagnostic.
            if (properties.Count == ModelCaptureAnalysis.MaxModelWork) break;
        }
        return properties.ToImmutable();
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
