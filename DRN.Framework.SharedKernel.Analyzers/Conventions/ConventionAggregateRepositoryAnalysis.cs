using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionAggregateRepositoryAnalysis
{
    internal static void Analyze(ConventionFacts facts)
    {
        var contracts = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
        var indexed = new HashSet<IAssemblySymbol>(SymbolEqualityComparer.Default);
        var implementations = new Dictionary<INamedTypeSymbol, List<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (type.TypeKind != TypeKind.Class || type.IsAbstract || ConventionSymbols.IsOpen(type, facts.Cancellation) ||
                facts.Role(type).Role != "Infra") continue;
            foreach (var binding in ConventionSymbols.Bindings(type, facts.Symbols.Repository, facts.Cancellation))
            {
                if (binding.TypeArguments is not [_, INamedTypeSymbol entity]) continue;
                if (!implementations.TryGetValue(entity, out var list)) implementations.Add(entity, list = []);
                list.Add(type);
            }
        }

        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (type.IsAbstract || ConventionSymbols.IsOpen(type, facts.Cancellation)) continue;
            var location = facts.Location(type);
            if (location == Location.None) continue;
            if (facts.Role(type).Role == "Domain" && IsAggregate(type) && facts.Owner(type) is { } owner &&
                FindContracts(type).Count == 0)
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.AggregateContract, location,
                    Properties(type, type), type.Name, owner.Subdomain));
        }

        var reported = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var repositoryMatches = new Dictionary<INamedTypeSymbol, bool>(SymbolEqualityComparer.Default);
        var configurationEntities = new List<List<INamedTypeSymbol>>();
        var defaultConfigurations = new Dictionary<INamespaceSymbol, List<int>>(SymbolEqualityComparer.Default);
        foreach (var configuration in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!facts.Symbols.IsKnown(configuration, facts.Symbols.Configuration, facts.Cancellation)) continue;
            var index = configurationEntities.Count;
            var entities = new List<INamedTypeSymbol>();
            foreach (var binding in ConventionSymbols.Bindings(configuration, facts.Symbols.Configuration, facts.Cancellation))
                if (binding.TypeArguments is [INamedTypeSymbol entity]) entities.Add(entity);
            configurationEntities.Add(entities);
            if (!ConventionPersistenceAnalysis.DefaultDiscoverable(facts, configuration, configuration)) continue;
            // Preserve source order for default configuration discovery.
            for (var ns = configuration.ContainingNamespace; ns is not null; ns = ns.ContainingNamespace)
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                // IsNamespaceWithin treats the empty namespace as an exact match only.
                if (ns.IsGlobalNamespace && !configuration.ContainingNamespace.IsGlobalNamespace) break;
                if (!defaultConfigurations.TryGetValue(ns, out var list)) defaultConfigurations.Add(ns, list = []);
                list.Add(index);
            }
        }
        var selectedConfigurations = new List<int>();
        var assigned = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var context in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (context.IsAbstract || ConventionSymbols.IsOpen(context, facts.Cancellation) ||
                facts.Role(context).Role != "Infra" || !facts.Symbols.IsContext(context, facts.Cancellation)) continue;
            var location = facts.Location(context);
            if (location == Location.None || facts.HasInvalidContext(context)) continue;
            var model = ConventionPersistenceAnalysis.GetModel(facts, context);
            assigned.Clear();
            foreach (var entity in model.Candidates)
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                assigned.Add(entity);
            }
            selectedConfigurations.Clear();
            if (model.DefaultScan && defaultConfigurations.TryGetValue(context.ContainingNamespace, out var defaults))
                foreach (var index in defaults)
                {
                    facts.Cancellation.ThrowIfCancellationRequested();
                    selectedConfigurations.Add(index);
                }
            foreach (var configuration in model.Applied)
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                foreach (var binding in ConventionSymbols.Bindings(configuration, facts.Symbols.Configuration, facts.Cancellation))
                    if (binding.TypeArguments is [INamedTypeSymbol entity]) assigned.Add(entity);
            }
            selectedConfigurations.Sort();
            var previousIndex = -1;
            foreach (var index in selectedConfigurations)
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (index == previousIndex) continue;
                previousIndex = index;
                foreach (var entity in configurationEntities[index]) assigned.Add(entity);
            }
            foreach (var entity in assigned)
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (!IsAggregate(entity) || entity.IsAbstract || ConventionSymbols.IsOpen(entity, facts.Cancellation) ||
                    model.Excluded.Contains(entity) || model.Ignored.Contains(entity) ||
                    facts.Role(entity).Role != "Domain" || facts.Owner(entity) is not { } owner ||
                    !facts.UsesOwnSubdomain(context, entity)) continue;
                if (HasRepository(entity)) continue;
                if (!reported.Add(entity)) continue;
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.AggregateRepository, location,
                    Properties(context, entity), entity.Name, owner.Subdomain));
            }
        }

        bool IsAggregate(INamedTypeSymbol type) => facts.Symbols.IsKnown(type, facts.Symbols.Aggregate, facts.Cancellation);

        bool HasRepository(INamedTypeSymbol entity)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (repositoryMatches.TryGetValue(entity, out var cached)) return cached;
            var required = FindContracts(entity);
            var matches = false;
            if (implementations.TryGetValue(entity, out var candidates))
                foreach (var repository in candidates)
                {
                    facts.Cancellation.ThrowIfCancellationRequested();
                    if (!facts.UsesOwnSubdomain(repository, entity)) continue;
                    foreach (var contract in repository.AllInterfaces)
                    {
                        facts.Cancellation.ThrowIfCancellationRequested();
                        if (!required.Contains(contract, SymbolEqualityComparer.Default)) continue;
                        matches = true;
                        break;
                    }
                    if (matches) break;
                }
            facts.Cancellation.ThrowIfCancellationRequested();
            repositoryMatches.Add(entity, matches);
            return matches;
        }

        IReadOnlyList<INamedTypeSymbol> FindContracts(INamedTypeSymbol entity)
        {
            if (indexed.Add(entity.ContainingAssembly))
            {
                var pending = new Stack<INamespaceOrTypeSymbol>();
                pending.Push(entity.ContainingAssembly.GlobalNamespace);
                while (pending.TryPop(out var container))
                {
                    facts.Cancellation.ThrowIfCancellationRequested();
                    foreach (var member in container.GetMembers())
                    {
                        if (member is INamespaceOrTypeSymbol child) pending.Push(child);
                        if (member is not INamedTypeSymbol { TypeKind: TypeKind.Interface } contract ||
                            ConventionSymbols.IsOpen(contract, facts.Cancellation) || facts.Role(contract).Role != "Domain" ||
                            facts.Owner(contract) is not { } contractOwner) continue;
                        foreach (var binding in ConventionSymbols.Bindings(contract, facts.Symbols.RepositoryContract, facts.Cancellation))
                        {
                            if (binding.TypeArguments is not [INamedTypeSymbol aggregate] ||
                                !SymbolEqualityComparer.Default.Equals(aggregate.ContainingAssembly, contract.ContainingAssembly) ||
                                facts.Owner(aggregate)?.Subdomain != contractOwner.Subdomain ||
                                !SymbolEqualityComparer.Default.Equals(aggregate.ContainingNamespace, contract.ContainingNamespace) ||
                                !SharesLogicalDirectory(facts, aggregate, contract)) continue;
                            if (!contracts.TryGetValue(aggregate, out var list)) contracts.Add(aggregate, list = []);
                            list.Add(contract);
                        }
                    }
                }
            }
            return contracts.TryGetValue(entity, out var result) ? result : Array.Empty<INamedTypeSymbol>();
        }
    }

    private static bool SharesLogicalDirectory(ConventionFacts facts, INamedTypeSymbol aggregate, INamedTypeSymbol contract)
    {
        // Referenced assemblies expose namespaces, not their producer's Compile/Link paths.
        if (!facts.IsLocal(aggregate)) return true;
        string? directory = null;
        return Matches(aggregate) && Matches(contract);

        bool Matches(INamedTypeSymbol type)
        {
            foreach (var reference in type.DeclaringSyntaxReferences)
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                var tree = reference.SyntaxTree;
                if (facts.IsGenerated(tree) || facts.HasInvalidPath(tree)) continue;
                var placement = facts.LogicalDirectory(tree);
                if (placement is null) continue; // Ownership analysis reports missing logical paths.
                var current = placement.Value.Directory;
                if (directory is not null && !string.Equals(directory, current, StringComparison.Ordinal)) return false;
                directory = current;
            }
            return true;
        }
    }

    private static ImmutableDictionary<string, string?> Properties(ISymbol target, INamedTypeSymbol entity) =>
        ImmutableDictionary<string, string?>.Empty.Add("Target", target.GetDocumentationCommentId())
            .Add("Entity", ConventionSymbols.MetadataName(entity));
}
