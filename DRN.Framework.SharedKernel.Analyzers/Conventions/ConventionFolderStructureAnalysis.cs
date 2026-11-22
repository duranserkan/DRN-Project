namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionFolderStructureAnalysis
{
    private const string SharedFolder = "_Shared";

    internal static void Analyze(ConventionFacts facts)
    {
        if (facts.Project.DomainLayout.Length == 0) return;
        if (!facts.HasDomainRole()) return;

        var foldersLayout = facts.Project.DomainLayout == ConventionProject.FoldersLayout;
        var minTypeDepth = foldersLayout ? 3 : 2;
        var populatedSubdomains = new HashSet<(string Domain, string Subdomain)>();
        var hasAnyLogicalPaths = false;
        var rootIdentityEnums = CollectRootIdentityEnums(facts);

        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (facts.Role(type).Role != ConventionArchitecture.Domain) continue;
            var role = facts.Role(type);
            InspectDomainType(facts, type, role.PathPrefix, foldersLayout, minTypeDepth, rootIdentityEnums, populatedSubdomains, ref hasAnyLogicalPaths);
        }

        InspectGroupingFolderFilesWithoutTypes(facts, minTypeDepth);

        if (populatedSubdomains.Count == 0 && (!hasAnyLogicalPaths && HasFallbackPopulatedSubdomain(facts, rootIdentityEnums)))
            return;

        if (populatedSubdomains.Count == 0 && !facts.HasInvalidFolderStructureInputs())
        {
            var anchor = facts.Records.FirstOrDefault(record =>
                record.Kind == ConventionInputSyntax.ReferenceKind &&
                record.Identity == ConventionInputSyntax.InventoryIdentity &&
                record.Get(ConventionInputSyntax.StageField) == ConventionInputSyntax.CompleteStage)?.Location ?? Location.None;
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.DomainSubdomainRequired, anchor,
                ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField,
                    facts.Project.AssemblyName + ":" + ConventionArchitecture.Domain),
                facts.Project.Name));
        }
    }

    private static void InspectDomainType(ConventionFacts facts, INamedTypeSymbol type, string pathPrefix,
        bool foldersLayout, int minTypeDepth, HashSet<INamedTypeSymbol> rootIdentityEnums,
        HashSet<(string Domain, string Subdomain)> populatedSubdomains, ref bool hasAnyLogicalPaths)
    {
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var tree = reference.SyntaxTree;
            if (facts.IsGenerated(tree) || facts.HasInvalidPath(tree)) continue;
            var placement = facts.LogicalDirectory(tree);
            if (placement is null) continue;
            hasAnyLogicalPaths = true;
            var relativeDirectory = StripPrefix(placement.Value.Directory, pathPrefix);
            var segments = relativeDirectory.Length == 0
                ? Array.Empty<string>()
                : relativeDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var partLocation = PartLocation(type, reference, facts.Cancellation);

            if (segments.Length == 0)
            {
                if (IsAllowedRootIdentityType(facts, type, rootIdentityEnums)) continue;
                Report(facts, type, partLocation, foldersLayout
                    ? "place Domain types under <Domain>/<Subdomain>/<Folder>/; only root zero-type files (such as Usings.cs) and EntityTypeAttribute<TApp>/IAppId/[DomainOnly] byte identity enums referenced by a local EntityTypeAttribute subclass may live at the project root"
                    : "place Domain types under <Subdomain>/<Folder>/; only root zero-type files (such as Usings.cs) and EntityTypeAttribute<TApp>/IAppId/[DomainOnly] byte identity enums referenced by a local EntityTypeAttribute subclass may live at the project root");
                continue;
            }

            if (segments.Length < minTypeDepth)
            {
                Report(facts, type, partLocation, foldersLayout
                    ? $"grouping folder '{relativeDirectory}' may contain only folders; place types under <Domain>/<Subdomain>/<Folder>/"
                    : $"grouping folder '{relativeDirectory}' may contain only folders; place types under <Subdomain>/<Folder>/");
                continue;
            }

            if (ValidateSharedPlacement(facts, type, segments, foldersLayout, partLocation))
                continue;

            var domain = foldersLayout ? segments[0] : facts.Project.DefaultDomain;
            var subdomain = foldersLayout ? segments[1] : segments[0];
            populatedSubdomains.Add((domain, subdomain));
        }
    }

    private static bool ValidateSharedPlacement(ConventionFacts facts, INamedTypeSymbol type, string[] segments,
        bool foldersLayout, Location location)
    {
        var expectedSharedIndex = foldersLayout ? 2 : 1;
        var hasShared = false;
        for (var index = 0; index < segments.Length; index++)
        {
            if (!string.Equals(segments[index], SharedFolder, StringComparison.OrdinalIgnoreCase))
                continue;
            if (index != expectedSharedIndex || !string.Equals(segments[index], SharedFolder, StringComparison.Ordinal))
            {
                Report(facts, type, location, foldersLayout
                    ? "'_Shared' is reserved only at subdomain scope '<Domain>/<Subdomain>/_Shared/'"
                    : "'_Shared' is reserved only at subdomain scope '<Subdomain>/_Shared/'");
                return true;
            }
            hasShared = true;
        }
        if (!hasShared) return false;
        if (facts.IsEntity(type) ||
            type.IsStatic ||
            type.TypeKind is TypeKind.Enum or TypeKind.Interface or TypeKind.Delegate ||
            type.Name.EndsWith("Service", StringComparison.Ordinal) ||
            facts.Symbols.IsKnown(type, facts.Symbols.ServiceContract, facts.Cancellation) ||
            facts.Symbols.IsKnown(type, facts.Symbols.RepositoryContract, facts.Cancellation) ||
            facts.Symbols.IsPersistenceType(type, facts.Cancellation))
        {
            Report(facts, type, location, "'_Shared' may contain only subdomain-scoped value objects (non-static classes, records, or structs), never entities, repositories, services, enums, interfaces, delegates, static classes, or persistence types");
            return true;
        }
        return false;
    }

    private static void InspectGroupingFolderFilesWithoutTypes(ConventionFacts facts, int minTypeDepth)
    {
        foreach (var tree in facts.Compilation.SyntaxTrees)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (facts.IsGenerated(tree) || facts.HasInvalidPath(tree)) continue;
            var placement = facts.LogicalDirectory(tree);
            if (placement is null) continue;
            if (!facts.TryGetDomainRelativeDirectory(placement.Value.Directory, out var relativeDirectory) ||
                relativeDirectory.Length == 0) continue;
            if (tree.GetRoot(facts.Cancellation) is not CompilationUnitSyntax root) continue;
            if (HasAnyDeclaredType(root)) continue;
            var segments = relativeDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= minTypeDepth) continue;
            var fileName = ConventionProject.NormalizeSeparators(facts.LogicalPath(tree) ?? tree.FilePath);
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.DomainFolderStructure,
                Location.Create(tree, root.Span),
                ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField, fileName),
                fileName,
                $"grouping folder '{placement.Value.Directory}' may contain only folders; keep zero-type files such as Usings.cs at the project root"));
        }
    }

    private static bool HasAnyDeclaredType(CompilationUnitSyntax root) =>
        root.DescendantNodes(static node => node is CompilationUnitSyntax or BaseNamespaceDeclarationSyntax)
            .Any(static node => node is BaseTypeDeclarationSyntax or DelegateDeclarationSyntax);

    private static bool HasFallbackPopulatedSubdomain(ConventionFacts facts, HashSet<INamedTypeSymbol> rootIdentityEnums)
    {
        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (facts.Role(type).Role != ConventionArchitecture.Domain || IsAllowedRootIdentityType(facts, type, rootIdentityEnums)) continue;
            if (facts.Owner(type) is not null) return true;
        }
        return false;
    }

    private static HashSet<INamedTypeSymbol> CollectRootIdentityEnums(ConventionFacts facts)
    {
        var enums = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (facts.Role(type).Role != ConventionArchitecture.Domain ||
                !facts.Symbols.IsKnown(type, facts.Symbols.EntityTypeAttribute, facts.Cancellation))
                continue;
            foreach (var ctor in type.InstanceConstructors)
            {
                foreach (var parameter in ctor.Parameters)
                {
                    if (parameter.Type is INamedTypeSymbol { TypeKind: TypeKind.Enum, EnumUnderlyingType.SpecialType: SpecialType.System_Byte } enumType &&
                        facts.IsLocal(enumType))
                        enums.Add(enumType);
                }
            }
        }
        return enums;
    }

    private static bool IsAllowedRootIdentityType(ConventionFacts facts, INamedTypeSymbol type,
        HashSet<INamedTypeSymbol> rootIdentityEnums)
    {
        if (facts.Symbols.IsKnown(type, facts.Symbols.EntityTypeAttribute, facts.Cancellation) ||
            facts.Symbols.IsKnown(type, facts.Symbols.AppId, facts.Cancellation))
            return true;
        return type.TypeKind == TypeKind.Enum &&
               type.EnumUnderlyingType?.SpecialType == SpecialType.System_Byte &&
               rootIdentityEnums.Contains(type) &&
               type.GetAttributes().Any(attribute => ConventionSymbols.Same(attribute.AttributeClass, facts.Symbols.DomainOnlyAttribute));
    }

    private static string StripPrefix(string directory, string pathPrefix)
    {
        if (pathPrefix.Length == 0) return directory;
        if (directory == pathPrefix) return "";
        return directory.StartsWith(pathPrefix + "/", StringComparison.Ordinal)
            ? directory[(pathPrefix.Length + 1)..]
            : directory;
    }

    private static Location PartLocation(INamedTypeSymbol type, SyntaxReference reference, CancellationToken cancellation) =>
        type.Locations.FirstOrDefault(candidate => candidate.SourceTree == reference.SyntaxTree &&
            reference.Span.Contains(candidate.SourceSpan)) ?? reference.GetSyntax(cancellation).GetLocation();

    private static void Report(ConventionFacts facts, INamedTypeSymbol type, Location location, string detail) =>
        facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.DomainFolderStructure, location,
            ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField, type.GetDocumentationCommentId()),
            type.Name, detail));
}
