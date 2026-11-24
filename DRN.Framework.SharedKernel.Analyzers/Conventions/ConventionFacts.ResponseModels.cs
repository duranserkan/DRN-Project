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
        var pending = new Stack<ITypeSymbol>();
        foreach (var type in Types) AddResponseRoot(type, pending);
        foreach (var assembly in Compilation.SourceModule.ReferencedAssemblySymbols)
        {
            Cancellation.ThrowIfCancellationRequested();
            var metadata = AssemblyMetadata(assembly);
            var identity = ConventionNames.ParseReferenceMetadata(assembly.Name, metadata);
            if (!identity.Valid || (ConventionNames.Read(identity.ProjectName).Role != ConventionArchitecture.Contract &&
                !metadata.Any(entry => entry.Key == "DRN.Conventions.Role.Contract"))) continue;
            var containers = new Stack<INamespaceOrTypeSymbol>();
            containers.Push(assembly.GlobalNamespace);
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

        var models = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        while (pending.TryPop(out var current))
        {
            Cancellation.ThrowIfCancellationRequested();
            if (!visited.Add(current)) continue;
            if (current is IArrayTypeSymbol array) pending.Push(array.ElementType);
            if (current is not INamedTypeSymbol named || named.TypeKind == TypeKind.Error) continue;
            foreach (var argument in named.TypeArguments) pending.Push(argument);
            var definition = named.OriginalDefinition;
            if (Role(definition).Role != ConventionArchitecture.Contract || Symbols.IsKnown(definition, Symbols.Dto, Cancellation) ||
                !models.Add(definition)) continue;
            // Expand declarations once, including recursive generic models. The concrete arguments are visited above.
            if (definition.BaseType is { } parent) pending.Push(parent);
            foreach (var contract in definition.Interfaces) pending.Push(contract);
            foreach (var member in definition.GetMembers())
            {
                Cancellation.ThrowIfCancellationRequested();
                if (member.IsStatic || member.DeclaredAccessibility != Accessibility.Public) continue;
                if (member is IPropertySymbol { IsIndexer: false, GetMethod.DeclaredAccessibility: Accessibility.Public } property)
                    pending.Push(property.Type);
                if (member is IFieldSymbol field) pending.Push(field.Type);
            }
        }
        return _responseModels = new(models, models.Select(type => type.Name).ToHashSet(StringComparer.Ordinal));
    }

    private void AddResponseRoot(INamedTypeSymbol type, Stack<ITypeSymbol> pending)
    {
        Cancellation.ThrowIfCancellationRequested();
        if (type.Name.EndsWith("Response", StringComparison.Ordinal) && Role(type).Role == ConventionArchitecture.Contract &&
            !Symbols.IsKnown(type, Symbols.Dto, Cancellation))
            pending.Push(type);
    }
}
