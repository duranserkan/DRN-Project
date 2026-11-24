namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionOwnershipAnalysis
{
    internal static void Analyze(ConventionFacts facts)
    {
        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            AnalyzeTypeOwnership(facts, type);
        }
    }

    private static void AnalyzeTypeOwnership(ConventionFacts facts, INamedTypeSymbol type)
    {
        var location = facts.Location(type);
        if (location == Location.None) return;
        var role = facts.Role(type);
        if (facts.Symbols.IsOpen(type, facts.Cancellation) &&
            !(role.Role == ConventionArchitecture.Contract && facts.Symbols.IsKnown(type, facts.Symbols.Dto, facts.Cancellation))) return;
        if (role.Role == ConventionArchitecture.SharedKernel && (type.TypeKind == TypeKind.Interface || type.IsAbstract)) return;
        if (!TryClassifyOwnedType(facts, type, role.Role, out var entity, out var configuration, out var repository, out var service))
            return;

        var owner = facts.Owner(type);
        if (facts.HasInvalidOwner(type)) return;
        if (owner is null)
        {
            if (entity && !type.IsAbstract && role.Role == ConventionArchitecture.Domain || service || repository)
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.MissingOwnership, location,
                    ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField, type.GetDocumentationCommentId()),
                    type.Name));
            return;
        }
        if (role.Namespace.Length == 0) return;

        var (ns, expectedNamespace, expectedDirectory) = ComputeExpectedPlacement(
            type, role, owner, ExpectedStructuralFolder(configuration, repository, role.Role));
        AnalyzeTypePlacement(facts, type, location, ns, expectedNamespace, expectedDirectory);
    }

    private static bool TryClassifyOwnedType(ConventionFacts facts, INamedTypeSymbol type, string role,
        out bool entity, out bool configuration, out bool repository, out bool service)
    {
        var symbols = facts.Symbols;
        entity = facts.IsEntity(type);
        var context = symbols.IsContext(type, facts.Cancellation);
        configuration = symbols.IsKnown(type, symbols.Configuration, facts.Cancellation);
        repository = symbols.IsKnown(type, symbols.RepositoryContract, facts.Cancellation);
        service = symbols.IsKnown(type, symbols.ServiceContract, facts.Cancellation) ||
                  role == ConventionArchitecture.Application && HasEntityBinding(facts, type);
        var dto = symbols.IsKnown(type, symbols.Dto, facts.Cancellation);
        var transport = symbols.IsKnown(type, symbols.SourceKnownReadOnlyController, facts.Cancellation);
        return entity || context || configuration || repository || service || dto || transport;
    }

    private static string? ExpectedStructuralFolder(bool configuration, bool repository, string role)
    {
        if (configuration) return "Configurations";
        return repository && role == ConventionArchitecture.Infra ? "Repositories" : null;
    }

    private static (string CurrentNamespace, string ExpectedNamespace, string ExpectedDirectory) ComputeExpectedPlacement(
        INamedTypeSymbol type, (string Role, string Namespace, string PathPrefix) role, ConventionOwner owner, string? structural)
    {
        var ns = type.ContainingNamespace.ToDisplayString();
        var suffix = ns.StartsWith(role.Namespace + ".", StringComparison.Ordinal)
            ? ns[(role.Namespace.Length + 1)..] : "";
        var parts = suffix.Length == 0 ? [] : suffix.Split('.');
        var index = ConventionFacts.OwnerSegmentOffset(role.Role, parts);
        var expectedParts = parts.ToList();
        var afterOwner = ApplyOwnerSegments(expectedParts, index, owner);
        if (structural is not null && !expectedParts.Skip(afterOwner).Contains(structural, StringComparer.Ordinal))
            expectedParts.Insert(afterOwner, structural);
        var expectedSuffix = string.Join(".", expectedParts);
        var expectedNamespace = role.Namespace + "." + expectedSuffix;
        var expectedDirectory = (role.PathPrefix.Length == 0 ? "" : role.PathPrefix + "/") + expectedSuffix.Replace('.', '/');
        return (ns, expectedNamespace, expectedDirectory);
    }

    private static int ApplyOwnerSegments(List<string> expectedParts, int index, ConventionOwner owner)
    {
        if (owner.HasFolderDomain)
        {
            SetOrAppendSegment(expectedParts, index, owner.Domain);
            SetOrAppendSegment(expectedParts, index + 1, owner.Subdomain);
            return index + 2;
        }
        SetOrAppendSegment(expectedParts, index, owner.Subdomain);
        return index + 1;
    }

    private static void SetOrAppendSegment(List<string> segments, int index, string value)
    {
        if (index >= segments.Count) segments.Add(value);
        else segments[index] = value;
    }

    internal static void AnalyzeTypePlacement(ConventionFacts facts, INamedTypeSymbol type, Location location,
        string ns, string expectedNamespace, string? expectedDirectory, DiagnosticDescriptor? descriptor = null)
    {
        var namespaceConflict = ns != expectedNamespace;
        var (conflictingParts, missing) = FindConflictingParts(facts, type, namespaceConflict, expectedDirectory);
        if (missing)
            facts.Diagnostics.Add(ConventionProject.InputIssue(ConventionSymbols.MetadataName(type),
                "missing logical source placement", "Compile or Link path", location));
        if (namespaceConflict || conflictingParts.Count != 0)
        {
            conflictingParts.Remove(location);
            facts.Diagnostics.Add(Diagnostic.Create(descriptor ?? ConventionDiagnosticDescriptors.Placement, location,
                conflictingParts.OrderBy(part => facts.LogicalPath(part.SourceTree!) ?? part.SourceTree!.FilePath, StringComparer.Ordinal)
                    .ThenBy(part => part.SourceSpan.Start),
                properties: descriptor is null ? null : ImmutableDictionary<string, string?>.Empty.Add("Target", type.GetDocumentationCommentId()),
                messageArgs: [type.Name, expectedDirectory ?? "<unknown>", expectedNamespace]));
        }
    }

    private static (HashSet<Location> Conflicts, bool MissingPlacement) FindConflictingParts(ConventionFacts facts,
        INamedTypeSymbol type, bool namespaceConflict, string? expectedDirectory)
    {
        var conflicts = new HashSet<Location>();
        var missingPlacement = false;
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (facts.IsGenerated(reference.SyntaxTree) || facts.HasInvalidPath(reference.SyntaxTree)) continue;
            var placement = facts.LogicalDirectory(reference.SyntaxTree);
            if (placement is null)
            {
                missingPlacement = true;
                continue;
            }
            var directoryConflict = expectedDirectory is not null && placement.Value.Directory != expectedDirectory;
            if (!directoryConflict && !namespaceConflict) continue;
            conflicts.Add(ConventionBoundaryAnalysis.PartLocation(type, reference, facts.Cancellation));
        }
        return (conflicts, missingPlacement);
    }

    internal static bool HasEntityBinding(ConventionFacts facts, INamedTypeSymbol type)
    {
        if (facts.Symbols.IsTransport(type, facts.Cancellation)) return false;
        if (HasDeclaredEntityBinding(facts, type)) return true;
        if (type.TypeKind != TypeKind.Interface) return false;
        foreach (var contract in type.AllInterfaces)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (HasDeclaredEntityBinding(facts, contract)) return true;
        }
        return false;
    }

    private static bool HasDeclaredEntityBinding(ConventionFacts facts, INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers())
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (member is not IMethodSymbol method) continue;
            if (method.MethodKind == MethodKind.Constructor && HasRepositoryParameter(facts, method)) return true;
            if (type.TypeKind == TypeKind.Interface && method.MethodKind == MethodKind.Ordinary &&
                MethodSignatureReferencesEntity(facts, method)) return true;
        }
        return false;
    }

    private static bool HasRepositoryParameter(ConventionFacts facts, IMethodSymbol method) =>
        method.Parameters.Any(parameter => parameter.Type is INamedTypeSymbol dependency &&
            ConventionSymbols.Is(dependency, facts.Symbols.RepositoryContract, facts.Cancellation));

    private static bool MethodSignatureReferencesEntity(ConventionFacts facts, IMethodSymbol method)
    {
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
        return false;
    }
}
