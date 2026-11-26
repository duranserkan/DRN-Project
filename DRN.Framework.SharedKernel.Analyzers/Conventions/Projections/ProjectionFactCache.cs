namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal readonly record struct GetterRead(
    IPropertySymbol Property,
    INamedTypeSymbol? Receiver,
    ImmutableArray<IPropertySymbol> Forwarded,
    bool MayReadSibling)
{
    internal bool Matches(IPropertySymbol written, CancellationToken cancellation, INamedTypeSymbol? staticReceiver = null)
    {
        var dispatched = PropertyAccessFacts.DispatchedProperty(written, Receiver ?? staticReceiver, cancellation);
        if (MatchesTarget(written, dispatched, Property)) return true;
        if (!Forwarded.IsDefault)
        {
            for (var i = 0; i < Forwarded.Length; i++)
            {
                if (MatchesTarget(written, dispatched, Forwarded[i]))
                    return true;
            }
        }
        return false;
    }

    private bool MatchesTarget(IPropertySymbol written, IPropertySymbol dispatched, IPropertySymbol target)
    {
        if (PropertyAccessFacts.SameOrMatchingProperty(dispatched, target))
            return true;
        // An unsealed class property can satisfy an interface cast on a derived runtime type.
        return Receiver is null &&
               written.ContainingType.TypeKind == TypeKind.Interface &&
               !target.ContainingType.IsSealed &&
               target.DeclaredAccessibility == Accessibility.Public &&
               written.IsIndexer == target.IsIndexer &&
               string.Equals(written.Name, target.Name, StringComparison.Ordinal) &&
               SymbolEqualityComparer.Default.Equals(written.Type, target.Type);
    }
}

internal readonly record struct PropertyReceiverKey(
    IPropertySymbol Declared,
    INamedTypeSymbol? Receiver);

internal readonly record struct TypePropertyNameKey(
    INamedTypeSymbol Type,
    string Name);

internal sealed class PropertyReceiverKeyComparer : IEqualityComparer<PropertyReceiverKey>
{
    internal static readonly PropertyReceiverKeyComparer Instance = new();

    public bool Equals(PropertyReceiverKey x, PropertyReceiverKey y) =>
        SymbolEqualityComparer.Default.Equals(x.Declared, y.Declared) &&
        SymbolEqualityComparer.Default.Equals(x.Receiver, y.Receiver);

    public int GetHashCode(PropertyReceiverKey obj) =>
        HashCode.Combine(
            SymbolEqualityComparer.Default.GetHashCode(obj.Declared),
            obj.Receiver is null ? 0 : SymbolEqualityComparer.Default.GetHashCode(obj.Receiver));
}

internal sealed class TypePropertyNameKeyComparer : IEqualityComparer<TypePropertyNameKey>
{
    internal static readonly TypePropertyNameKeyComparer Instance = new();

    public bool Equals(TypePropertyNameKey x, TypePropertyNameKey y) =>
        StringComparer.Ordinal.Equals(x.Name, y.Name) &&
        SymbolEqualityComparer.Default.Equals(x.Type, y.Type);

    public int GetHashCode(TypePropertyNameKey obj) =>
        HashCode.Combine(
            SymbolEqualityComparer.Default.GetHashCode(obj.Type),
            StringComparer.Ordinal.GetHashCode(obj.Name));
}

internal sealed class ProjectionFactCache(ConventionFacts facts)
{
    private readonly Dictionary<PropertyReceiverKey, GetterRead> _getterReads =
        new(PropertyReceiverKeyComparer.Instance);
    private readonly Dictionary<IPropertySymbol, bool> _autoSetters = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<TypePropertyNameKey, IPropertySymbol?> _destinationProperties =
        new(TypePropertyNameKeyComparer.Instance);

    internal GetterRead ResolveGetterRead(IPropertySymbol declared, INamedTypeSymbol? receiver)
    {
        facts.Cancellation.ThrowIfCancellationRequested();
        var key = new PropertyReceiverKey(declared, receiver);
        if (_getterReads.TryGetValue(key, out var cached)) return cached;
        var resolved = PropertyAccessFacts.ComputeGetterRead(declared, receiver, facts);
        facts.Cancellation.ThrowIfCancellationRequested();
        _getterReads.Add(key, resolved);
        return resolved;
    }

    internal bool HasAutoPropertySetter(IPropertySymbol property)
    {
        facts.Cancellation.ThrowIfCancellationRequested();
        if (_autoSetters.TryGetValue(property, out var cached)) return cached;
        var result = PropertyAccessFacts.ComputeHasAutoPropertySetter(property, facts);
        facts.Cancellation.ThrowIfCancellationRequested();
        _autoSetters.Add(property, result);
        return result;
    }

    internal bool CanGuaranteeDirectSetter(IPropertySymbol property, INamedTypeSymbol? receiver) =>
        !RoslynSymbolHelpers.IsDynamicDispatchProperty(property, receiver) &&
        HasAutoPropertySetter(property);

    internal IPropertySymbol? FindDestinationProperty(INamedTypeSymbol destinationType, IParameterSymbol? parameter)
    {
        if (parameter is null) return null;
        facts.Cancellation.ThrowIfCancellationRequested();
        var key = new TypePropertyNameKey(destinationType, parameter.Name);
        if (_destinationProperties.TryGetValue(key, out var cached)) return cached;
        var found = PropertyAccessFacts.ComputeDestinationProperty(destinationType, parameter.Name);
        facts.Cancellation.ThrowIfCancellationRequested();
        _destinationProperties.Add(key, found);
        return found;
    }
}
