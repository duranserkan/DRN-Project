using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;

namespace DRN.Framework.SharedKernel.Analyzers;

internal static class ReferencedEntityDiscovery
{
    internal static IEnumerable<(INamedTypeSymbol Type, Compilation? Producer)> Find(Compilation compilation, INamedTypeSymbol entity,
        EntityAnalyzerHelper.EntityTypeFacts facts, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        var projects = new ProjectReferences(compilation, entity, cancellation);
        var entityAssemblies = projects.EntityAssemblies(entity.ContainingAssembly);
        var candidates = AssembliesReachingEntity(compilation.Assembly, entityAssemblies, projects, cancellation);
        candidates.Remove(compilation.Assembly);
        ExcludeFrameworkDependencies(candidates, compilation.Assembly, projects, cancellation);
        var pending = new Stack<INamespaceOrTypeSymbol>();
        foreach (var assembly in candidates)
        {
            cancellation.ThrowIfCancellationRequested();
            if (assembly.Name.StartsWith("System.", StringComparison.OrdinalIgnoreCase) ||
                assembly.Name.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase)) continue;

            var (inspectionAssembly, alternate) = projects.InspectionScope(assembly);
            if (alternate is not null && projects.HasConsumerMetadata(assembly)) continue;
            var inspectionFacts = alternate is { } producer
                ? EntityAnalyzerHelper.EntityTypeFacts.For(producer.Compilation, producer.Entity)
                : facts;
            foreach (var type in FindInAssembly(inspectionAssembly, inspectionFacts, pending, cancellation))
                yield return (type, alternate?.Compilation);
        }
    }

    private static IEnumerable<INamedTypeSymbol> FindInAssembly(IAssemblySymbol assembly, EntityAnalyzerHelper.EntityTypeFacts facts,
        Stack<INamespaceOrTypeSymbol> pending, CancellationToken cancellation)
    {
        pending.Push(assembly.GlobalNamespace);
        while (pending.TryPop(out var container))
        {
            cancellation.ThrowIfCancellationRequested();
            if (container is INamespaceSymbol ns)
                foreach (var child in ns.GetNamespaceMembers()) pending.Push(child);
            foreach (var type in container.GetTypeMembers())
            {
                cancellation.ThrowIfCancellationRequested();
                if (type.DeclaredAccessibility == Accessibility.Private) continue;
                pending.Push(type);
                if (type is { TypeKind: TypeKind.Class, IsAbstract: false } && facts.IsDerived(type, cancellation))
                    yield return type;
            }
        }
    }

    private sealed class ProjectReferences
    {
        // One source assembly identifies each project, including in diamonds. Its symbols may need retargeting for inspection.
        private readonly Dictionary<IAssemblySymbol, IAssemblySymbol> _sources = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<IAssemblySymbol, IAssemblySymbol> _retargeted = new(SymbolEqualityComparer.Default);
        private readonly Dictionary<IAssemblySymbol, (Compilation Compilation, INamedTypeSymbol Entity)> _compatibleProducers =
            new(SymbolEqualityComparer.Default);
        private readonly HashSet<AssemblyIdentity>? _consumerMetadata;

        internal ProjectReferences(Compilation root, INamedTypeSymbol entity, CancellationToken cancellation)
        {
            DiscoverProjects(root, entity, cancellation);

            if (_compatibleProducers.Count == 0) return;
            _consumerMetadata = [];
            foreach (var reference in root.References)
            {
                cancellation.ThrowIfCancellationRequested();
                if (reference is PortableExecutableReference && root.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly)
                    _consumerMetadata.Add(assembly.Identity);
            }
        }

        private void DiscoverProjects(Compilation root, INamedTypeSymbol entity, CancellationToken cancellation)
        {
            var pending = new Queue<Compilation>();
            var seen = new HashSet<Compilation> { root };
            pending.Enqueue(root);
            while (pending.TryDequeue(out var compilation))
            {
                var sharesEntity = SharesEntity(compilation, entity);
                foreach (var reference in compilation.References)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (reference is not CompilationReference project) continue;
                    var producer = project.Compilation;
                    if (seen.Add(producer))
                    {
                        pending.Enqueue(producer);
                        RecordCompatibleProducer(root, producer, entity);
                    }
                    if (compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol bound)
                        RecordBinding(bound, producer, entity, sharesEntity);
                }
            }
        }

        private void RecordCompatibleProducer(Compilation root, Compilation producer, INamedTypeSymbol entity)
        {
            var producerEntity = producer.GetTypeByMetadataName(EntityAnalyzerHelper.SourceKnownEntityMetadataName);
            if (producerEntity is not null && !SymbolEqualityComparer.Default.Equals(producerEntity, entity) &&
                root.Options.AssemblyIdentityComparer.ReferenceMatchesDefinition(producerEntity.ContainingAssembly.Identity, entity.ContainingAssembly.Identity))
                _compatibleProducers.Add(producer.Assembly, (producer, producerEntity));
        }

        private void RecordBinding(IAssemblySymbol bound, Compilation producer, INamedTypeSymbol entity, bool sharesEntity)
        {
            _sources[bound] = producer.Assembly;
            if (sharesEntity && !SymbolEqualityComparer.Default.Equals(bound, producer.Assembly) && !SharesEntity(producer, entity))
                _retargeted.TryAdd(producer.Assembly, bound);
        }

        private static bool SharesEntity(Compilation compilation, INamedTypeSymbol entity) =>
            SymbolEqualityComparer.Default.Equals(compilation.GetTypeByMetadataName(EntityAnalyzerHelper.SourceKnownEntityMetadataName), entity);

        internal IAssemblySymbol Source(IAssemblySymbol assembly) => _sources.GetValueOrDefault(assembly, assembly);

        // The consumer's resolved image already contributes the project when both representations are reachable.
        internal bool HasConsumerMetadata(IAssemblySymbol source) => _consumerMetadata?.Contains(source.Identity) == true;

        internal IEnumerable<IAssemblySymbol> EntityAssemblies(IAssemblySymbol consumerEntityAssembly)
        {
            yield return Source(consumerEntityAssembly);
            foreach (var (_, entity) in _compatibleProducers.Values)
                yield return Source(entity.ContainingAssembly);
        }

        internal (IAssemblySymbol Assembly, (Compilation Compilation, INamedTypeSymbol Entity)? Alternate) InspectionScope(IAssemblySymbol source)
        {
            if (_retargeted.TryGetValue(source, out var bound)) return (bound, null);
            // A project reached only through another producer can retain that producer's compatible SharedKernel symbols.
            return _compatibleProducers.TryGetValue(source, out var producer)
                ? (source, producer)
                : (source, null);
        }

        internal IEnumerable<IAssemblySymbol> Dependencies(IAssemblySymbol source)
        {
            // Producer edges include project-only dependencies. Bound edges reach the consumer's compatible framework image.
            foreach (var module in source.Modules)
                foreach (var dependency in module.ReferencedAssemblySymbols)
                    yield return Source(dependency);
            if (!_retargeted.TryGetValue(source, out var bound)) yield break;
            foreach (var module in bound.Modules)
                foreach (var dependency in module.ReferencedAssemblySymbols)
                    yield return Source(dependency);
        }
    }

    [SuppressMessage("SonarQube", "S3267", Justification = "Filtering on visited.Add would hide a traversal state change inside a LINQ predicate.")]
    private static HashSet<IAssemblySymbol> AssembliesReachingEntity(IAssemblySymbol consumer,
        IEnumerable<IAssemblySymbol> targets, ProjectReferences projects, CancellationToken cancellation)
    {
        // Keep edges through skipped assemblies because an application entity can inherit a base type through one.
        var dependents = new Dictionary<IAssemblySymbol, List<IAssemblySymbol>>(SymbolEqualityComparer.Default);
        var visited = new HashSet<IAssemblySymbol>(projects.Dependencies(consumer), SymbolEqualityComparer.Default);
        var pending = new Stack<IAssemblySymbol>(visited);
        IndexDependents(projects, dependents, visited, pending, cancellation);
        visited.Clear();
        foreach (var target in targets)
        {
            cancellation.ThrowIfCancellationRequested();
            if (visited.Add(target)) pending.Push(target);
        }
        while (pending.TryPop(out var assembly))
        {
            cancellation.ThrowIfCancellationRequested();
            if (!dependents.TryGetValue(assembly, out var parents)) continue;
            foreach (var parent in parents)
                if (visited.Add(parent)) pending.Push(parent);
        }
        return visited;
    }

    private static void IndexDependents(ProjectReferences projects, Dictionary<IAssemblySymbol, List<IAssemblySymbol>> dependents,
        HashSet<IAssemblySymbol> visited, Stack<IAssemblySymbol> pending, CancellationToken cancellation)
    {
        while (pending.TryPop(out var assembly))
        {
            cancellation.ThrowIfCancellationRequested();
            foreach (var dependency in projects.Dependencies(assembly))
            {
                cancellation.ThrowIfCancellationRequested();
                if (!dependents.TryGetValue(dependency, out var parents))
                {
                    parents = [];
                    dependents.Add(dependency, parents);
                }
                parents.Add(assembly);
                if (visited.Add(dependency)) pending.Push(dependency);
            }
        }
    }

    private static void ExcludeFrameworkDependencies(HashSet<IAssemblySymbol> candidates, IAssemblySymbol consumer,
        ProjectReferences projects, CancellationToken cancellation)
    {
        // Every intermediate assembly on a framework path to a candidate also reaches the entity assembly.
        // Seed all framework assemblies before removing their non-framework dependencies from the candidate set.
        var pending = new Stack<IAssemblySymbol>(candidates.Where(IsDrnFramework));
        if (IsDrnFramework(consumer)) pending.Push(consumer);
        while (pending.TryPop(out var assembly))
        {
            cancellation.ThrowIfCancellationRequested();
            foreach (var dependency in projects.Dependencies(assembly))
            {
                cancellation.ThrowIfCancellationRequested();
                if (!IsDrnFramework(dependency) && candidates.Remove(dependency)) pending.Push(dependency);
            }
        }
    }

    private static bool IsDrnFramework(IAssemblySymbol assembly) =>
        Conventions.ConventionReferenceBoundaryAnalysis.FrameworkPackage(assembly.Name) is not null;
}
