namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal sealed partial class ConventionFacts
{
    private sealed record ResponseModels(HashSet<INamedTypeSymbol> Types, HashSet<string> Names);
    private ResponseModels? _responseModels;

    internal bool MayBeResponseModel(string name) =>
        name.EndsWith("Response", StringComparison.Ordinal) || ResponseModelIndex().Names.Contains(name);

    internal bool IsResponseModel(INamedTypeSymbol type) =>
        type.TypeKind != TypeKind.Error && Role(type).Role == ConventionArchitecture.Contract &&
        !Symbols.IsKnown(type, Symbols.Dto, Cancellation) &&
        (type.Name.EndsWith("Response", StringComparison.Ordinal) || ResponseModelIndex().Types.Contains(type.OriginalDefinition));

    private ResponseModels ResponseModelIndex()
    {
        Cancellation.ThrowIfCancellationRequested();
        if (_responseModels is not null) return _responseModels;
        var pending = CollectResponseRoots();
        var models = ExpandResponseModels(pending);
        Cancellation.ThrowIfCancellationRequested();
        var names = new HashSet<string>(models.Count, StringComparer.Ordinal);
        foreach (var model in models) names.Add(model.Name);
        return _responseModels = new(models, names);
    }

    private Stack<ITypeSymbol> CollectResponseRoots()
    {
        var pending = new Stack<ITypeSymbol>();
        foreach (var type in Types) AddResponseRoot(type, pending);
        CollectReferencedResponseRoots(pending);
        return pending;
    }

    private void CollectReferencedResponseRoots(Stack<ITypeSymbol> pending)
    {
        Stack<INamespaceOrTypeSymbol>? containers = null;
        foreach (var assembly in Compilation.SourceModule.ReferencedAssemblySymbols)
        {
            Cancellation.ThrowIfCancellationRequested();
            if (!MayContainContractResponses(assembly)) continue;
            containers ??= new();
            CollectAssemblyResponseRoots(assembly.GlobalNamespace, containers, pending);
        }
    }

    private bool MayContainContractResponses(IAssemblySymbol assembly)
    {
        var metadata = AssemblyMetadata(assembly);
        var identity = ConventionNames.ParseReferenceMetadata(assembly.Name, metadata);
        if (!identity.Valid) return false;
        if (ConventionNames.Read(identity.ProjectName).Role == ConventionArchitecture.Contract)
            return true;
        foreach (var entry in metadata)
        {
            if (entry.Key == ConventionNames.ContractRoleMetadataKey)
                return true;
        }
        return false;
    }

    private void CollectAssemblyResponseRoots(
        INamespaceSymbol rootNamespace, Stack<INamespaceOrTypeSymbol> containers, Stack<ITypeSymbol> pending)
    {
        containers.Clear();
        containers.Push(rootNamespace);
        while (containers.TryPop(out var container))
        {
            Cancellation.ThrowIfCancellationRequested();
            if (container is INamespaceSymbol ns)
                foreach (var child in ns.GetNamespaceMembers()) containers.Push(child);
            foreach (var child in container.GetTypeMembers())
            {
                AddResponseRoot(child, pending);
                containers.Push(child);
            }
        }
    }

    private HashSet<INamedTypeSymbol> ExpandResponseModels(Stack<ITypeSymbol> pending)
    {
        var models = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        while (pending.TryPop(out var current))
        {
            Cancellation.ThrowIfCancellationRequested();
            if (!visited.Add(current)) continue;
            ExpandResponseType(current, pending, models);
        }
        return models;
    }

    private void ExpandResponseType(ITypeSymbol current, Stack<ITypeSymbol> pending, HashSet<INamedTypeSymbol> models)
    {
        if (ConventionBoundaryAnalysis.UnwrapNamedType(current, pending) is not { } named) return;
        ConventionSymbols.PushTypeArguments(named, pending);
        var definition = named.OriginalDefinition;
        var isExpandableContractModel = Role(definition).Role == ConventionArchitecture.Contract &&
            !Symbols.IsKnown(definition, Symbols.Dto, Cancellation) &&
            models.Add(definition);
        if (!isExpandableContractModel) return;
        // Expand declarations once, including recursive generic models. The concrete arguments are visited above.
        PushResponseModelDependencies(definition, pending);
    }

    private void PushResponseModelDependencies(INamedTypeSymbol definition, Stack<ITypeSymbol> pending)
    {
        if (definition.BaseType is { } parent) pending.Push(parent);
        foreach (var contract in definition.Interfaces) pending.Push(contract);
        foreach (var member in definition.GetMembers())
        {
            Cancellation.ThrowIfCancellationRequested();
            if (TryGetPublicModelMemberType(member) is { } memberType) pending.Push(memberType);
        }
    }

    private static ITypeSymbol? TryGetPublicModelMemberType(ISymbol member)
    {
        if (member.IsStatic || member.DeclaredAccessibility != Accessibility.Public) return null;
        if (member is IPropertySymbol { IsIndexer: false, GetMethod.DeclaredAccessibility: Accessibility.Public } property)
            return property.Type;
        return member is IFieldSymbol field ? field.Type : null;
    }

    private void AddResponseRoot(INamedTypeSymbol type, Stack<ITypeSymbol> pending)
    {
        Cancellation.ThrowIfCancellationRequested();
        var isContractResponse = type.Name.EndsWith("Response", StringComparison.Ordinal) &&
            Role(type).Role == ConventionArchitecture.Contract &&
            !Symbols.IsKnown(type, Symbols.Dto, Cancellation);
        if (isContractResponse) pending.Push(type);
    }
}
