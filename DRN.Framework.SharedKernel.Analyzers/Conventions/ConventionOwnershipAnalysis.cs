using Microsoft.CodeAnalysis;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionOwnershipAnalysis
{
    internal static void Analyze(ConventionFacts facts)
    {
        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var location = facts.Location(type);
            if (location == Location.None || ConventionSymbols.IsOpen(type, facts.Cancellation)) continue;
            var symbols = facts.Symbols;
            var role = facts.Role(type);
            if (role.Role == "SharedKernel" && (type.TypeKind == TypeKind.Interface || type.IsAbstract)) continue;
            var entity = facts.IsEntity(type);
            var context = symbols.IsContext(type, facts.Cancellation);
            var configuration = ConventionSymbols.Is(type, symbols.Configuration, facts.Cancellation);
            var repository = ConventionSymbols.Is(type, symbols.RepositoryContract, facts.Cancellation);
            var service = ConventionSymbols.Is(type, symbols.ServiceContract, facts.Cancellation) ||
                          role.Role == "Application" && HasEntityBinding(facts, type);
            var dto = ConventionSymbols.Is(type, symbols.Dto, facts.Cancellation);
            var transport = symbols.IsTransport(type, facts.Cancellation);
            if (!(entity || context || configuration || repository || service || dto || transport)) continue;
            var owner = facts.Owner(type);
            if (facts.HasInvalidOwner(type)) continue;
            if (owner is null)
            {
                if (entity && !type.IsAbstract && role.Role == "Domain" || service || repository)
                    facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.MissingOwnership, location, type.Name));
                continue;
            }

            if (role.Namespace.Length == 0) continue;
            var ns = type.ContainingNamespace.ToDisplayString();
            var suffix = ns.StartsWith(role.Namespace + ".", StringComparison.Ordinal)
                ? ns[(role.Namespace.Length + 1)..] : "";
            var parts = suffix.Split('.');
            var index = role.Role == "Hosted" && parts[0] is "Controllers" or "Pages" ||
                        role.Role == "Application" && parts[0] == "Services" ? 1 : 0;
            var expectedParts = parts.ToList();
            if (index >= expectedParts.Count) expectedParts.Add(owner.Subdomain);
            else expectedParts[index] = owner.Subdomain;
            var structural = configuration ? "Configurations" : repository && role.Role == "Infra" ? "Repositories" : null;
            if (structural is not null && !expectedParts.Skip(index + 1).Contains(structural, StringComparer.Ordinal))
                expectedParts.Insert(index + 1, structural);
            var expectedSuffix = string.Join(".", expectedParts);
            var expectedNamespace = role.Namespace + "." + expectedSuffix;
            var expectedDirectory = (role.PathPrefix.Length == 0 ? "" : role.PathPrefix + "/") + expectedSuffix.Replace('.', '/');
            var conflict = ns != expectedNamespace;
            var conflictingParts = new HashSet<Location>();
            var missing = false;
            foreach (var reference in type.DeclaringSyntaxReferences)
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (facts.IsGenerated(reference.SyntaxTree) || facts.HasInvalidPath(reference.SyntaxTree)) continue;
                var path = facts.LogicalPath(reference.SyntaxTree);
                if (path is null) { missing = true; continue; }
                path = ConventionProject.NormalizeSeparators(path);
                var separator = path.LastIndexOf('/');
                var directory = separator < 0 ? "" : path[..separator];
                if (directory != expectedDirectory || ns != expectedNamespace)
                {
                    conflict = true;
                    var part = type.Locations.FirstOrDefault(candidate => candidate.SourceTree == reference.SyntaxTree &&
                        reference.Span.Contains(candidate.SourceSpan)) ?? reference.GetSyntax(facts.Cancellation).GetLocation();
                    if (!part.Equals(location)) conflictingParts.Add(part);
                }
            }
            if (missing)
                facts.Diagnostics.Add(ConventionProject.InputIssue(ConventionSymbols.MetadataName(type),
                    "missing logical source placement", "Compile or Link path", location));
            if (conflict)
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Placement, location,
                    conflictingParts.OrderBy(part => facts.LogicalPath(part.SourceTree!) ?? part.SourceTree!.FilePath, StringComparer.Ordinal)
                        .ThenBy(part => part.SourceSpan.Start),
                    properties: null, messageArgs: [type.Name, expectedDirectory, expectedNamespace]));
        }
    }

    private static bool HasEntityBinding(ConventionFacts facts, INamedTypeSymbol type)
    {
        if (facts.Symbols.IsTransport(type, facts.Cancellation)) return false;
        foreach (var member in type.GetMembers())
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (member is not IMethodSymbol method) continue;
            if (method.MethodKind == MethodKind.Constructor)
            {
                if (method.Parameters.Any(parameter => parameter.Type is INamedTypeSymbol dependency &&
                        ConventionSymbols.Is(dependency, facts.Symbols.RepositoryContract, facts.Cancellation))) return true;
                continue;
            }
            if (type.TypeKind != TypeKind.Interface || method.MethodKind != MethodKind.Ordinary) continue;
            var pending = new Stack<ITypeSymbol>();
            var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            pending.Push(method.ReturnType);
            foreach (var parameter in method.Parameters) pending.Push(parameter.Type);
            while (pending.TryPop(out var current))
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (!visited.Add(current)) continue;
                if (current is IArrayTypeSymbol array) pending.Push(array.ElementType);
                if (current is not INamedTypeSymbol named) continue;
                if (facts.IsEntity(named)) return true;
                foreach (var argument in named.TypeArguments) pending.Push(argument);
            }
        }
        return false;
    }
}
