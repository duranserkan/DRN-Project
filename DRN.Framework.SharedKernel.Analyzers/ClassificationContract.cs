using System.Collections.Concurrent;
using System.Collections.Immutable;
using DRN.Framework.CompilerTools;
using Microsoft.CodeAnalysis;

namespace DRN.Framework.SharedKernel.Analyzers;

internal sealed class ClassificationContract
{
    private readonly INamedTypeSymbol _personal;
    private readonly SymbolFactCache<ISymbol, bool> _positive = new();
    private readonly SymbolFactCache<INamedTypeSymbol, ImmutableArray<IPropertySymbol>> _properties = new();
    private readonly SymbolFactCache<IPropertySymbol, bool> _exemptions = new();
    private readonly SymbolFactCache<INamedTypeSymbol, AttributeFacts> _attributes = new();
    private readonly SymbolFactCache<ITypeSymbol, ModelTypeFacts> _modelTypes = new();
    private readonly ConcurrentDictionary<INamedTypeSymbol, ReceiverFacts> _receivers = new(SymbolEqualityComparer.Default);
    private readonly Func<ISymbol, CancellationToken, bool> _calculatePositive;
    private readonly Func<IPropertySymbol, CancellationToken, bool> _calculateExempt;
    private readonly Func<INamedTypeSymbol, CancellationToken, AttributeFacts> _calculateAttribute;
    private readonly Func<INamedTypeSymbol, ReceiverFacts> _createReceiver;

    internal ClassificationContract(INamedTypeSymbol sensitive, INamedTypeSymbol personal)
    {
        Sensitive = sensitive;
        _personal = personal;
        _calculatePositive = CalculatePositive;
        _calculateExempt = CalculateExempt;
        _calculateAttribute = CalculateAttribute;
        _createReceiver = type => new ReceiverFacts(this, type);
    }

    internal INamedTypeSymbol Sensitive { get; }

    internal static bool Eligible(IPropertySymbol property) => !property.IsStatic && !property.IsIndexer &&
        property.DeclaredAccessibility == Accessibility.Public && property.GetMethod?.DeclaredAccessibility == Accessibility.Public;

    internal bool Accepted(IPropertySymbol property, CancellationToken cancellation) => Positive(property, cancellation) ||
        Positive(property.ContainingType, cancellation) || Positive(property.Type, cancellation) || Exempt(property, cancellation);

    internal bool Exempt(IPropertySymbol property, CancellationToken cancellation = default) =>
        _exemptions.GetOrAdd(property, _calculateExempt, cancellation);

    private bool CalculateExempt(IPropertySymbol property, CancellationToken cancellation)
    {
        foreach (var attribute in property.GetAttributes())
        {
            cancellation.ThrowIfCancellationRequested();
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, Sensitive) && IsFalse(attribute)) return true;
        }
        return false;
    }

    internal ModelTypeFacts ModelType(ITypeSymbol type, CancellationToken cancellation)
    {
        if (type.IsReferenceType && type.NullableAnnotation != NullableAnnotation.NotAnnotated)
            type = type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);
        return _modelTypes.GetOrAdd(type, ModelTypeFacts.Read, cancellation);
    }

    internal bool? PositiveInterfaceProperty(INamedTypeSymbol receiver, IPropertySymbol property, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        return _receivers.GetOrAdd(receiver, _createReceiver).Positive(property, cancellation);
    }

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
            {
                if (attribute.AttributeClass is not { } attributeType) continue;
                var facts = _attributes.GetOrAdd(attributeType, _calculateAttribute, cancellation);
                if ((SymbolEqualityComparer.Default.Equals(current, symbol) || facts.Inherited) &&
                    (facts.Personal || facts.Sensitive &&
                    (!SymbolEqualityComparer.Default.Equals(attributeType, Sensitive) || !IsFalse(attribute)))) return true;
            }
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

    private readonly record struct AttributeFacts(bool Personal, bool Sensitive, bool Inherited);

    private AttributeFacts CalculateAttribute(INamedTypeSymbol type, CancellationToken cancellation) =>
        new(Derives(type, _personal, cancellation), Derives(type, Sensitive, cancellation), Inherited(type, cancellation));

    private static bool Inherited(INamedTypeSymbol attributeType, CancellationToken cancellation)
    {
        foreach (var type in Ancestors(attributeType))
        {
            cancellation.ThrowIfCancellationRequested();
            foreach (var usage in type.GetAttributes())
                if (usage.AttributeClass?.ToDisplayString() == "System.AttributeUsageAttribute")
                    return !usage.NamedArguments.Any(pair => pair.Key == "Inherited" && pair.Value.Value is false);
        }
        return true;
    }

    private static bool Derives(INamedTypeSymbol type, INamedTypeSymbol parent, CancellationToken cancellation)
    {
        foreach (var candidate in Ancestors(type))
        {
            cancellation.ThrowIfCancellationRequested();
            if (SymbolEqualityComparer.Default.Equals(candidate, parent)) return true;
        }
        return false;
    }

    private sealed class ReceiverFacts
    {
        private readonly ClassificationContract _contract;
        private readonly INamedTypeSymbol _receiver;
        // Null retains an exhausted scan as incomplete, distinct from a proven negative.
        private readonly SymbolFactCache<IPropertySymbol, bool?> _properties = new(capacity: 4);
        private readonly Func<IPropertySymbol, CancellationToken, bool?> _calculate;

        internal ReceiverFacts(ClassificationContract contract, INamedTypeSymbol receiver)
        {
            _contract = contract;
            _receiver = receiver;
            _calculate = Calculate;
        }

        internal bool? Positive(IPropertySymbol property, CancellationToken cancellation) =>
            _properties.GetOrAdd(property, _calculate, cancellation);

        private bool? Calculate(IPropertySymbol property, CancellationToken cancellation)
        {
            var inspected = 0;
            foreach (var contract in _receiver.AllInterfaces)
            foreach (var member in contract.GetMembers().OfType<IPropertySymbol>())
            {
                cancellation.ThrowIfCancellationRequested();
                if (++inspected > LoggingCaptureAnalysis.MaxValues) return null;
                if (SymbolEqualityComparer.Default.Equals(_receiver.FindImplementationForInterfaceMember(member), property) &&
                    (_contract.Positive(member, cancellation) || _contract.Positive(member.ContainingType, cancellation) ||
                     _contract.Positive(member.Type, cancellation))) return true;
            }
            return false;
        }
    }

    private static IEnumerable<INamedTypeSymbol> Ancestors(INamedTypeSymbol? type)
    {
        for (; type != null; type = type.BaseType) yield return type;
    }
}
