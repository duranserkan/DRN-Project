namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionDependencyAnalysis
{
    internal static void Analyze(ConventionFacts facts)
    {
        if (facts.Diagnostics.Any(diagnostic => diagnostic.Descriptor == ConventionDiagnosticDescriptors.InvalidInput)) return;
        var references = facts.Records.Where(record => record.Kind == ConventionInputSyntax.ReferenceKind).ToArray();
        var inventories = references.Where(record =>
            record.Identity == ConventionInputSyntax.InventoryIdentity &&
            record.Get(ConventionInputSyntax.StageField) == ConventionInputSyntax.CompleteStage).ToArray();
        if (inventories is not [var inventory]) return;
        var direct = ReadDirectProjects(facts, references);
        // An unresolved declared edge is incomplete evidence, not proof that a layer is absent.
        if (direct is null || facts.Diagnostics.Any(diagnostic => diagnostic.Descriptor == ConventionDiagnosticDescriptors.InvalidInput)) return;
        var roles = facts.Records.Where(record => record.Kind == ConventionInputSyntax.RoleKind).Select(record => record.Identity)
            .Append(facts.Project.Role).Distinct(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            foreach (var dependency in ConventionArchitecture.Required(role))
            {
                var matches = direct.Where(item => item.Project.Role == dependency).ToArray();
                if (matches.Length == 0)
                    facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.RequiredReference, inventory.Location,
                        ImmutableDictionary<string, string?>.Empty.Add("Target", facts.Project.AssemblyName + ":" + dependency),
                        facts.Project.Name, dependency));
                else if (role == ConventionArchitecture.Infra) AnalyzeSubdomains(facts, matches, inventory.Location);
            }
        }
    }

    internal static List<(IAssemblySymbol Assembly, ConventionProject Project)>? ReadDirectProjects(
        ConventionFacts facts, ConventionInput[] references)
    {
        var result = new List<(IAssemblySymbol, ConventionProject)>();
        var metadata = facts.Compilation.References.OfType<PortableExecutableReference>()
            .Where(reference => reference.FilePath is not null)
            .GroupBy(reference => ConventionProject.NormalizeSeparators(reference.FilePath!), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var (byProject, byOriginalProject) = IndexResolvedReferences(facts, references);
        var matched = new List<(int Index, ConventionInput Record)>();
        foreach (var declared in references.Where(record =>
                     record.Get(ConventionInputSyntax.StageField) == ConventionInputSyntax.ProjectField))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!TryAppendMatchedAssemblies(facts, declared, byProject, byOriginalProject, metadata, matched, result))
                return null;
        }
        return result;
    }

    private static (Dictionary<string, List<(int Index, ConventionInput Record)>> ByProject,
        Dictionary<string, List<(int Index, ConventionInput Record)>> ByOriginalProject) IndexResolvedReferences(
        ConventionFacts facts, ConventionInput[] references)
    {
        var byProject = new Dictionary<string, List<(int Index, ConventionInput Record)>>(StringComparer.Ordinal);
        var byOriginalProject = new Dictionary<string, List<(int Index, ConventionInput Record)>>(StringComparer.Ordinal);
        for (var index = 0; index < references.Length; index++)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var record = references[index];
            if (record.Get(ConventionInputSyntax.StageField) != "Resolved") continue;
            var projectPath = ConventionProject.NormalizeSeparators(record.Get(ConventionInputSyntax.ProjectField));
            if (!byProject.TryGetValue(projectPath, out var projectList))
            {
                projectList = [];
                byProject.Add(projectPath, projectList);
            }
            projectList.Add((index, record));
            var originalPath = ConventionProject.NormalizeSeparators(record.Get(ConventionInputSyntax.OriginalProjectField));
            if (!byOriginalProject.TryGetValue(originalPath, out var originalList))
            {
                originalList = [];
                byOriginalProject.Add(originalPath, originalList);
            }
            originalList.Add((index, record));
        }
        return (byProject, byOriginalProject);
    }

    private static bool TryAppendMatchedAssemblies(ConventionFacts facts, ConventionInput declared,
        Dictionary<string, List<(int Index, ConventionInput Record)>> byProject,
        Dictionary<string, List<(int Index, ConventionInput Record)>> byOriginalProject,
        Dictionary<string, PortableExecutableReference> metadata,
        List<(int Index, ConventionInput Record)> matched,
        List<(IAssemblySymbol Assembly, ConventionProject Project)> result)
    {
        matched.Clear();
        if (byProject.TryGetValue(ConventionProject.NormalizeSeparators(declared.Identity), out var projectMatches))
            matched.AddRange(projectMatches);
        var source = declared.Get(ConventionInputSyntax.SourceField);
        if (source.Length > 0 && byOriginalProject.TryGetValue(ConventionProject.NormalizeSeparators(source), out var originalMatches))
            matched.AddRange(originalMatches);
        if (matched.Count == 0) return false;
        matched.Sort(static (left, right) => left.Index.CompareTo(right.Index));
        var previousIndex = -1;
        foreach (var (index, record) in matched)
        {
            if (index == previousIndex) continue;
            previousIndex = index;
            if (!metadata.TryGetValue(ConventionProject.NormalizeSeparators(record.Identity), out var reference) ||
                facts.Compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly) return false;
            if (facts.ReferenceProject(assembly) is { } project) result.Add((assembly, project));
        }
        return true;
    }

    private static void AnalyzeSubdomains(ConventionFacts facts,
        (IAssemblySymbol Assembly, ConventionProject Project)[] domains, Location inventoryLocation)
    {
        var subdomains = CollectPopulatedSubdomains(facts, domains);
        if (subdomains.Count == 0)
        {
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.DomainSubdomain, inventoryLocation,
                ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField,
                    facts.Project.AssemblyName + ":" + ConventionArchitecture.Domain),
                facts.Project.Name, "<any>"));
            return;
        }
        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!IsMissingDomainSubdomain(facts, type, subdomains, out var owner)) continue;
            var location = facts.Location(type);
            if (location == Location.None) continue;
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.DomainSubdomain, location,
                ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField, type.GetDocumentationCommentId()),
                type.Name, owner ?? "<unowned>"));
        }
    }

    private static HashSet<string> CollectPopulatedSubdomains(ConventionFacts facts,
        (IAssemblySymbol Assembly, ConventionProject Project)[] domains)
    {
        var subdomains = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (assembly, project) in domains)
        {
            var pending = new Stack<INamespaceSymbol>();
            pending.Push(assembly.GlobalNamespace);
            while (pending.TryPop(out var ns))
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                foreach (var child in ns.GetNamespaceMembers()) pending.Push(child);
                var name = ns.ToDisplayString();
                if (ns.GetTypeMembers().Length == 0 || !name.StartsWith(project.RootNamespace + ".", StringComparison.Ordinal)) continue;
                subdomains.Add(name[(project.RootNamespace.Length + 1)..].Split('.')[0]);
            }
        }
        return subdomains;
    }

    private static bool IsMissingDomainSubdomain(ConventionFacts facts, INamedTypeSymbol type, HashSet<string> subdomains, out string? owner)
    {
        owner = null;
        if (type.TypeKind != TypeKind.Class || type.IsAbstract || facts.Symbols.IsOpen(type, facts.Cancellation) ||
            facts.Role(type).Role != ConventionArchitecture.Infra || facts.HasInvalidOwner(type)) return false;
        if (!facts.Symbols.IsContext(type, facts.Cancellation) &&
            !facts.Symbols.IsKnown(type, facts.Symbols.RepositoryContract, facts.Cancellation)) return false;
        owner = facts.Owner(type)?.Subdomain;
        return owner is null || !subdomains.Contains(owner);
    }
}
