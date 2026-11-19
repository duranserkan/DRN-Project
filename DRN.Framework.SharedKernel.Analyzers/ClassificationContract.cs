using System.Collections.Concurrent;
using DRN.Framework.CompilerTools;

namespace DRN.Framework.SharedKernel.Analyzers;

internal sealed class ClassificationContract
{
    private readonly INamedTypeSymbol _personal;
    private readonly INamedTypeSymbol? _dataClassification;
    private readonly AttributeUsageFacts.Cache _attributeUsage;
    private readonly SymbolFactCache<ISymbol, bool> _positive = new();
    private readonly SymbolFactCache<ISymbol, PropertyClassification> _symbolKinds = new();
    private readonly SymbolFactCache<INamedTypeSymbol, ImmutableArray<IPropertySymbol>> _properties = new();
    private readonly SymbolFactCache<IPropertySymbol, bool> _exemptions = new();
    private readonly SymbolFactCache<INamedTypeSymbol, AttributeFacts> _attributes = new();
    private readonly SymbolFactCache<ITypeSymbol, ModelTypeFacts> _modelTypes = new();
    private readonly ConcurrentDictionary<INamedTypeSymbol, ReceiverFacts> _receivers = new(SymbolEqualityComparer.Default);
    private readonly Func<ISymbol, CancellationToken, bool> _calculatePositive;
    private readonly Func<ISymbol, CancellationToken, PropertyClassification> _calculateSymbolKinds;
    private readonly Func<IPropertySymbol, CancellationToken, bool> _calculateExempt;
    private readonly Func<INamedTypeSymbol, CancellationToken, AttributeFacts> _calculateAttribute;
    private readonly Func<INamedTypeSymbol, ReceiverFacts> _createReceiver;

    internal ClassificationContract(INamedTypeSymbol sensitive, INamedTypeSymbol personal, Compilation compilation)
    {
        Sensitive = sensitive;
        _personal = personal;
        _dataClassification = compilation.GetTypeByMetadataName("Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute");
        _attributeUsage = AttributeUsageFacts.Cache.For(compilation);
        _calculatePositive = CalculatePositive;
        _calculateSymbolKinds = CalculateSymbolKinds;
        _calculateExempt = CalculateExempt;
        _calculateAttribute = CalculateAttribute;
        _createReceiver = type => new ReceiverFacts(this, type);
    }

    internal INamedTypeSymbol Sensitive { get; }

    [Flags]
    internal enum PropertyClassification { None = 0, Sensitive = 1, Personal = 2 }

    internal (PropertyClassification Property, PropertyClassification Context) ProjectionClassification(
        IPropertySymbol property, INamedTypeSymbol receiver, ITypeSymbol? receiverView, CancellationToken cancellation)
    {
        var direct = SymbolKinds(property, cancellation);
        var context = SymbolKinds(receiver, cancellation) | SymbolKinds(property.Type, cancellation);
        if (!SymbolEqualityComparer.Default.Equals(receiver, property.ContainingType))
            context |= SymbolKinds(property.ContainingType, cancellation);
        if (receiverView is not null && !SymbolEqualityComparer.Default.Equals(receiver, receiverView))
            context |= SymbolKinds(receiverView, cancellation);
        var implementation = property;
        if (property.ContainingType.TypeKind == TypeKind.Interface &&
            receiver.FindImplementationForInterfaceMember(property) is IPropertySymbol implementationProperty)
        {
            implementation = implementationProperty;
            Include(implementationProperty);
        }
        if (FindReceiverOverride(implementation, receiver, cancellation) is { } overridingProperty)
        {
            implementation = overridingProperty;
            Include(overridingProperty);
        }
        foreach (var member in implementation.ExplicitInterfaceImplementations)
            Include(member);
        foreach (var contract in receiver.AllInterfaces)
        foreach (var member in contract.GetMembers(implementation.Name).OfType<IPropertySymbol>())
        {
            cancellation.ThrowIfCancellationRequested();
            if (receiver.FindImplementationForInterfaceMember(member) is IPropertySymbol interfaceImplementation &&
                OverridesOrEquals(implementation, interfaceImplementation))
                Include(member);
        }
        return (direct, context);

        void Include(IPropertySymbol member)
        {
            direct |= SymbolKinds(member, cancellation);
            context |= SymbolKinds(member.ContainingType, cancellation) | SymbolKinds(member.Type, cancellation);
        }
    }

    private static IPropertySymbol? FindReceiverOverride(IPropertySymbol property, INamedTypeSymbol receiver, CancellationToken cancellation)
    {
        if (property.ContainingType.TypeKind != TypeKind.Class || property.IsSealed ||
            !(property.IsVirtual || property.IsAbstract || property.IsOverride)) return null;
        for (var type = receiver; type is not null && !SymbolEqualityComparer.Default.Equals(type, property.ContainingType); type = type.BaseType)
        {
            cancellation.ThrowIfCancellationRequested();
            foreach (var member in type.GetMembers(property.Name))
                if (member is IPropertySymbol { OverriddenProperty: { } parent } candidate && OverridesOrEquals(parent, property))
                    return candidate;
        }
        return null;
    }

    private static bool OverridesOrEquals(IPropertySymbol property, IPropertySymbol ancestor)
    {
        for (var current = property; current is not null; current = current.OverriddenProperty)
            if (SymbolEqualityComparer.Default.Equals(current, ancestor)) return true;
        return false;
    }

    internal PropertyClassification SymbolKinds(ISymbol symbol, CancellationToken cancellation)
    {
        if (symbol is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable)
            symbol = nullable.TypeArguments[0];
        return _symbolKinds.GetOrAdd(symbol, _calculateSymbolKinds, cancellation);
    }

    private PropertyClassification CalculateSymbolKinds(ISymbol symbol, CancellationToken cancellation)
    {
        var result = PropertyClassification.None;
        for (ISymbol? current = symbol; current is not null; current = NextClassifiedSymbol(current))
        {
            cancellation.ThrowIfCancellationRequested();
            foreach (var attribute in current.GetAttributes())
            {
                if (attribute.AttributeClass is not { } type) continue;
                var facts = _attributes.GetOrAdd(type, _calculateAttribute, cancellation);
                if (!facts.IsInherited && !SymbolEqualityComparer.Default.Equals(current, symbol)) continue;
                if (facts.IsPersonal) result |= PropertyClassification.Personal;
                if (facts.IsSensitive && (!SymbolEqualityComparer.Default.Equals(type, Sensitive) || !IsFalse(attribute)))
                    result |= PropertyClassification.Sensitive;
            }
        }
        return result;
    }

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

    [SuppressMessage("SonarQube", "S3267", Justification = "Scanning the immutable attribute array directly avoids a captured predicate for each symbol in the inheritance chain.")]
    private bool CalculatePositive(ISymbol symbol, CancellationToken cancellation)
    {
        for (ISymbol? current = symbol; current != null; current = NextClassifiedSymbol(current))
        {
            cancellation.ThrowIfCancellationRequested();
            var directSymbol = SymbolEqualityComparer.Default.Equals(current, symbol);
            foreach (var attribute in current.GetAttributes())
            {
                if (IsPositiveAttribute(attribute, directSymbol, cancellation))
                    return true;
            }
        }
        return false;
    }

    private static ISymbol? NextClassifiedSymbol(ISymbol symbol) => symbol switch
    {
        IPropertySymbol property => property.OverriddenProperty,
        INamedTypeSymbol type => type.BaseType,
        _ => null
    };

    private bool IsPositiveAttribute(AttributeData attribute, bool directSymbol, CancellationToken cancellation)
    {
        if (attribute.AttributeClass is not { } attributeType) return false;
        var facts = _attributes.GetOrAdd(attributeType, _calculateAttribute, cancellation);
        if (!directSymbol && !facts.IsInherited) return false;
        if (facts.IsPersonal) return true;
        return facts.IsSensitive && (!SymbolEqualityComparer.Default.Equals(attributeType, Sensitive) || !IsFalse(attribute));
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

    [SuppressMessage("SonarQube", "S3267", Justification = "Both immutable attribute scans avoid predicates capturing the cancellation token and contract per property.")]
    internal bool HasUnknown(IPropertySymbol property, CancellationToken cancellation = default)
    {
        foreach (var attribute in property.GetAttributes())
            if (IsUnknownAttribute(attribute, cancellation)) return true;
        foreach (var attribute in property.Type.GetAttributes())
            if (IsUnknownAttribute(attribute, cancellation)) return true;
        return false;
    }

    private bool IsUnknownAttribute(AttributeData attribute, CancellationToken cancellation) =>
        attribute.AttributeClass is { } attributeType &&
        _attributes.GetOrAdd(attributeType, _calculateAttribute, cancellation).IsDataClassification;

    private static bool IsFalse(AttributeData attribute) => attribute.ConstructorArguments is [{ Value: false }];

    private readonly record struct AttributeFacts(bool IsPersonal, bool IsSensitive, bool IsDataClassification, bool IsInherited);

    private AttributeFacts CalculateAttribute(INamedTypeSymbol type, CancellationToken cancellation)
    {
        var personal = false;
        var sensitive = false;
        var dataClassification = false;
        foreach (var candidate in Ancestors(type))
        {
            cancellation.ThrowIfCancellationRequested();
            personal |= SymbolEqualityComparer.Default.Equals(candidate, _personal);
            sensitive |= SymbolEqualityComparer.Default.Equals(candidate, Sensitive);
            if (!dataClassification && IsDataClassificationAttribute(candidate))
                dataClassification = true;
            if (personal && sensitive && dataClassification) break;
        }
        return new(personal, sensitive, dataClassification,
            _attributeUsage.Get(type, cancellation).Inherited);
    }

    private bool IsDataClassificationAttribute(INamedTypeSymbol candidate) =>
        _dataClassification != null && SymbolEqualityComparer.Default.Equals(candidate, _dataClassification) ||
        candidate.Name == "DataClassificationAttribute" &&
        candidate.ToDisplayString() == "Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute";

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
            {
                foreach (var member in contract.GetMembers().OfType<IPropertySymbol>())
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (++inspected > LoggingCaptureAnalysis.MaxValues) return null;
                    if (IsPositiveInterfaceMember(property, member, cancellation)) return true;
                }
            }
            return false;
        }

        private bool IsPositiveInterfaceMember(IPropertySymbol property, IPropertySymbol member, CancellationToken cancellation) =>
            SymbolEqualityComparer.Default.Equals(_receiver.FindImplementationForInterfaceMember(member), property) &&
            (_contract.Positive(member, cancellation) || _contract.Positive(member.ContainingType, cancellation) ||
             _contract.Positive(member.Type, cancellation));
    }

    private static IEnumerable<INamedTypeSymbol> Ancestors(INamedTypeSymbol? type)
    {
        for (; type != null; type = type.BaseType) yield return type;
    }
}
