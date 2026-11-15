using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionDependencyAnalysis
{
    internal static void Analyze(ConventionFacts facts)
    {
        if (facts.Diagnostics.Any(diagnostic => diagnostic.Id == "DRNCV001")) return;
        var references = facts.Records.Where(record => record.Kind == "Reference").ToArray();
        var inventories = references.Where(record => record.Identity == "Inventory" && record.Get("Stage") == "Complete").ToArray();
        if (inventories is not [var inventory]) return;
        var direct = ReadDirectProjects(facts, references);
        // An unresolved declared edge is incomplete evidence, not proof that a layer is absent.
        if (direct is null || facts.Diagnostics.Any(diagnostic => diagnostic.Id == "DRNCV001")) return;
        var roles = facts.Records.Where(record => record.Kind == "Role").Select(record => record.Identity)
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
                else if (role == "Infra") AnalyzeSubdomains(facts, matches, inventory.Location);
            }
        }
    }

    private static List<(IAssemblySymbol Assembly, ConventionProject Project)>? ReadDirectProjects(
        ConventionFacts facts, ConventionInput[] references)
    {
        var result = new List<(IAssemblySymbol, ConventionProject)>();
        var metadata = facts.Compilation.References.OfType<PortableExecutableReference>()
            .Where(reference => reference.FilePath is not null)
            .GroupBy(reference => ConventionProject.NormalizeSeparators(reference.FilePath!), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var declared in references.Where(record => record.Get("Stage") == "Project"))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var resolved = references.Where(record => record.Get("Stage") == "Resolved" &&
                (ConventionProject.NormalizeSeparators(record.Get("Project")) == ConventionProject.NormalizeSeparators(declared.Identity) ||
                 declared.Get("Source").Length > 0 && ConventionProject.NormalizeSeparators(record.Get("OriginalProject")) ==
                 ConventionProject.NormalizeSeparators(declared.Get("Source")))).ToArray();
            if (resolved.Length == 0) return null;
            foreach (var record in resolved)
            {
                if (!metadata.TryGetValue(ConventionProject.NormalizeSeparators(record.Identity), out var reference) ||
                    facts.Compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly) return null;
                if (facts.ReferenceProject(assembly) is { } project) result.Add((assembly, project));
            }
        }
        return result;
    }

    private static void AnalyzeSubdomains(ConventionFacts facts,
        (IAssemblySymbol Assembly, ConventionProject Project)[] domains, Location inventoryLocation)
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
        if (subdomains.Count == 0)
        {
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.DomainSubdomain, inventoryLocation,
                ImmutableDictionary<string, string?>.Empty.Add("Target", facts.Project.AssemblyName + ":Domain"),
                facts.Project.Name, "<any>"));
            return;
        }
        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (type.TypeKind != TypeKind.Class || type.IsAbstract || ConventionSymbols.IsOpen(type, facts.Cancellation) ||
                facts.Role(type).Role != "Infra" || facts.HasInvalidOwner(type) ||
                !(facts.Symbols.IsContext(type, facts.Cancellation) ||
                  ConventionSymbols.Is(type, facts.Symbols.RepositoryContract, facts.Cancellation))) continue;
            var owner = facts.Owner(type)?.Subdomain;
            var location = facts.Location(type);
            if (location == Location.None || owner is not null && subdomains.Contains(owner)) continue;
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.DomainSubdomain, location,
                ImmutableDictionary<string, string?>.Empty.Add("Target", type.GetDocumentationCommentId()),
                type.Name, owner ?? "<unowned>"));
        }
    }
}
