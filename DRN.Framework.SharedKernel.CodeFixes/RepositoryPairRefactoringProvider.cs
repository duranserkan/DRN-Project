using DRN.Framework.SharedKernel.Analyzers.Conventions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using static DRN.Framework.SharedKernel.CodeFixes.CodeFixDestinationGuard;
using static DRN.Framework.SharedKernel.CodeFixes.CodeFixSymbolFacts;

namespace DRN.Framework.SharedKernel.CodeFixes;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(RepositoryPairRefactoringProvider)), Shared]
public sealed class RepositoryPairRefactoringProvider : CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var snapshot = context.Document.Project.Solution;
        if (!snapshot.Workspace.CanApplyChange(ApplyChangesKind.AddDocument) ||
            !ReferenceEquals(snapshot, snapshot.Workspace.CurrentSolution) ||
            ConventionGenerationPreflight.ReadProject(context.Document.Project).Role != "Domain") return;
        var entity = await EntityConfigurationRefactoringProvider.SelectedEntity(context.Document, context.Span, context.CancellationToken);
        if (entity is null) return;
        var entityName = MetadataName(entity);
        var candidates = await Candidates(context.Document.Project, entityName, context.CancellationToken);
        var configurations = new Dictionary<ProjectId, List<EntityConfigurationRefactoringProvider.ConfigurationCandidate>>();
        foreach (var candidate in candidates)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(snapshot, snapshot.Workspace.CurrentSolution)) return;
            context.RegisterRefactoring(CodeAction.Create(
                "Add scoped repository pair: " + candidate.InterfaceName + " + " + candidate.ClassName +
                " for " + candidate.ContextName + " (" + candidate.DomainPath + ", " + candidate.InfraPath + ")",
                token => Apply(snapshot, context.Document.Project.Id, entityName, candidate, entity.ContainingAssembly.Identity, null, token),
                "DrnRepositoryPair:" + candidate.InfraProject + ":" + candidate.ContextName + ":" + entityName));
            if (!configurations.TryGetValue(candidate.InfraProject, out var options))
            {
                options = await EntityConfigurationRefactoringProvider.ConfigurationCandidates(snapshot.GetProject(candidate.InfraProject)!, entityName,
                    entity.ContainingAssembly.Identity, context.CancellationToken);
                configurations.Add(candidate.InfraProject, options);
            }
            foreach (var configuration in options.Where(option => option.SingleContextName == candidate.ContextName &&
                         CompatibleDestinations(candidate, option)))
            {
                if (!ReferenceEquals(snapshot, snapshot.Workspace.CurrentSolution)) return;
                context.RegisterRefactoring(CodeAction.Create(
                    "Add scoped repository pair and entity configuration: " + candidate.InterfaceName + " + " + candidate.ClassName +
                    " + " + configuration.ClassName + " for " + candidate.ContextName + " (" + candidate.DomainPath + ", " +
                    candidate.InfraPath + ", " + configuration.RelativePath + ")",
                    token => Apply(snapshot, context.Document.Project.Id, entityName, candidate, entity.ContainingAssembly.Identity, configuration, token),
                    "DrnPersistenceBundle:" + candidate.InfraProject + ":" + candidate.ContextName + ":" + entityName));
            }
        }
    }

    private static async Task<Solution> Apply(Solution snapshot, ProjectId domainId, string entityName, RepositoryCandidate candidate,
        AssemblyIdentity entityAssembly, EntityConfigurationRefactoringProvider.ConfigurationCandidate? configuration, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var current = snapshot.Workspace.CurrentSolution;
        if (!ReferenceEquals(current, snapshot) || snapshot.GetProject(domainId) is not { } domain) return current;
        if (!(await Candidates(domain, entityName, token)).Contains(candidate)) return snapshot.Workspace.CurrentSolution;
        if (configuration is not null && (snapshot.GetProject(candidate.InfraProject) is not { } infra ||
            configuration.SingleContextName != candidate.ContextName || !CompatibleDestinations(candidate, configuration) ||
            !(await EntityConfigurationRefactoringProvider.ConfigurationCandidates(infra, entityName, entityAssembly, token)).Contains(configuration)))
            return snapshot.Workspace.CurrentSolution;
        var contract = $$"""
            namespace {{candidate.DomainNamespace}};

            public interface {{candidate.InterfaceName}} : global::DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository<{{candidate.EntityType}}>
            {
            }

            """;
        var implementation = $$"""
            namespace {{candidate.InfraNamespace}};

            [global::DRN.Framework.Utils.DependencyInjection.Attributes.Scoped<global::{{candidate.DomainNamespace}}.{{candidate.InterfaceName}}>]
            public sealed class {{candidate.ClassName}}
                : global::DRN.Framework.EntityFramework.Domain.SourceKnownRepository<{{candidate.ContextType}}, {{candidate.EntityType}}>,
                  global::{{candidate.DomainNamespace}}.{{candidate.InterfaceName}}
            {
                public {{candidate.ClassName}}({{candidate.ContextType}} context, global::DRN.Framework.Utils.Entity.IEntityUtils utils)
                    : base(context, utils)
                {
                }
            }

            """;
        var changed = snapshot.AddDocument(DocumentId.CreateNewId(domainId), candidate.InterfaceName + ".cs", SourceText.From(contract),
                candidate.DomainPath.Split('/')[..^1], candidate.DomainFullPath)
            .AddDocument(DocumentId.CreateNewId(candidate.InfraProject), candidate.ClassName + ".cs", SourceText.From(implementation),
                candidate.InfraPath.Split('/')[..^1], candidate.InfraFullPath);
        if (configuration is not null) changed = EntityConfigurationRefactoringProvider.AddConfigurationDocument(changed, candidate.InfraProject, configuration);
        token.ThrowIfCancellationRequested();
        return ReferenceEquals(snapshot.Workspace.CurrentSolution, snapshot) ? changed : snapshot.Workspace.CurrentSolution;
    }

    private static bool CompatibleDestinations(RepositoryCandidate repository, EntityConfigurationRefactoringProvider.ConfigurationCandidate configuration) =>
        !WorkspacePathConflict(repository.DomainFullPath, configuration.FullPath) &&
        !WorkspacePathConflict(repository.InfraFullPath, configuration.FullPath);

    private static async Task<List<RepositoryCandidate>> Candidates(Project domain, string entityName, CancellationToken token)
    {
        var result = new List<RepositoryCandidate>();
        var domainAggregate = await ResolveDomainAggregateAsync(domain, entityName, token);
        if (domainAggregate is null) return result;
        foreach (var infra in domain.Solution.Projects.Where(project => project.Language == LanguageNames.CSharp &&
                     project.ProjectReferences.Any(reference => reference.ProjectId == domain.Id &&
                         (reference.Aliases.IsEmpty || reference.Aliases.Contains("global", StringComparer.Ordinal)))))
        {
            token.ThrowIfCancellationRequested();
            await CollectInfraProjectCandidatesAsync(domainAggregate, infra, result, token);
        }
        return result;
    }

    private static async Task<DomainAggregateInfo?> ResolveDomainAggregateAsync(Project domain, string entityName, CancellationToken token)
    {
        var domainInfo = ConventionGenerationPreflight.ReadProject(domain);
        if (domainInfo.Role != "Domain" || !domainInfo.Layout.Valid || !ValidNamespace(domainInfo.Namespace) || domain.FilePath is null ||
            !TryFullPath(domain.FilePath, out var domainProjectPath)) return null;
        var domainInputs = await ReadInputsWithoutRoleOverrideAsync(domain, required: false, token);
        if (domainInputs is null) return null;
        var domainCompilation = await domain.GetCompilationAsync(token);
        if (domainCompilation?.GetTypeByMetadataName(entityName) is not { IsAbstract: false } entity || IsOpen(entity) || !IsPublicType(entity) ||
            !SymbolEqualityComparer.Default.Equals(entity.ContainingAssembly, domainCompilation.Assembly) ||
            !Inherits(entity, domainCompilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.AggregateRoot"))) return null;
        return await ResolveDomainDestinationAsync(domain, (domainInfo.Namespace, domainProjectPath, domainInfo.Layout), domainInputs,
            domainCompilation, entity, entityName, token);
    }

    private static async Task<DomainAggregateInfo?> ResolveDomainDestinationAsync(Project domain,
        (string Namespace, string ProjectPath, ConventionGenerationPreflight.ProjectLayout Layout) home,
        CompilationUnitSyntax domainInputs, Compilation domainCompilation,
        INamedTypeSymbol entity, string entityName, CancellationToken token)
    {
        var domainDocument = await ResolveEditableHomeAsync(domain, entity, rejectSpanErrors: true, token);
        var conventionalOwner = ResolveNamespaceOwnerKey(entity, home.Namespace, home.Layout);
        var ownerKey = conventionalOwner is null ? null : DeclaredOwner(entity, conventionalOwner, domainCompilation);
        if (domainDocument?.FilePath is null || !TryFullPath(domainDocument.FilePath, out var domainDocumentPath) ||
            conventionalOwner is null || ownerKey is null || entity.ContainingNamespace.IsGlobalNamespace ||
            !ownerKey.SameOwner(conventionalOwner) ||
            !OwnershipCompatible(domainInputs, entityName, ownerKey.Subdomain, token, out var domainExplicitDomain, ownerKey.DomainFilter)) return null;
        ownerKey = ownerKey.WithExplicitDomain(domainExplicitDomain);
        var domainNamespace = entity.ContainingNamespace.ToDisplayString();
        var domainRoot = Path.GetDirectoryName(home.ProjectPath)!;
        var expectedDomainRelative = domainNamespace[(home.Namespace.Length + 1)..].Replace('.', '/');
        if (!ValidNamespace(domainNamespace) ||
            Path.GetRelativePath(domainRoot, Path.GetDirectoryName(domainDocumentPath)!).Replace('\\', '/') != expectedDomainRelative)
            return null;
        var repositoryContract = domainCompilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository`1");
        if (repositoryContract is null) return null;
        var stem = ConventionGenerationPreflight.EntityStem(entity.Name);
        var interfaceName = "I" + stem + "Repository";
        var className = stem + "Repository";
        if (!SyntaxFacts.IsValidIdentifier(interfaceName) || !SyntaxFacts.IsValidIdentifier(className)) return null;
        var domainTypes = Types(domainCompilation.Assembly.GlobalNamespace, token).ToArray();
        if (domainTypes.Any(type => RepositoryFor(type, repositoryContract, entity)) ||
            ConventionGenerationPreflight.SymbolCollision(domainCompilation, domainNamespace, interfaceName, token)) return null;
        if (!TryFullPath(Path.Combine(Path.GetDirectoryName(domainDocumentPath)!, interfaceName + ".cs"), out var domainDestination) ||
            !SafeDocumentDestination(domain, domainRoot, domainDestination, token)) return null;
        return new(entity, entityName, ownerKey, domainNamespace, domainRoot, domainDestination, interfaceName, className);
    }

    private static async Task CollectInfraProjectCandidatesAsync(DomainAggregateInfo domainAggregate, Project infra,
        List<RepositoryCandidate> result, CancellationToken token)
    {
        var infraInfo = ConventionGenerationPreflight.ReadProject(infra);
        if (infraInfo.Role != "Infra" || !infraInfo.Layout.Valid || !ValidNamespace(infraInfo.Namespace) || infra.FilePath is null ||
            !TryFullPath(infra.FilePath, out var infraProjectPath)) return;
        var infraInputs = await ReadInputsWithoutRoleOverrideAsync(infra, required: false, token);
        if (infraInputs is null) return;
        var compilation = await infra.GetCompilationAsync(token);
        var importedEntity = compilation?.GetTypeByMetadataName(domainAggregate.EntityName);
        if (compilation is null || importedEntity is null ||
            !importedEntity.ContainingAssembly.Identity.Equals(domainAggregate.Entity.ContainingAssembly.Identity) ||
            !compilation.IsSymbolAccessibleWithin(importedEntity, compilation.Assembly) ||
            !Inherits(importedEntity, compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.AggregateRoot")) ||
            !OwnershipCompatible(infraInputs, domainAggregate.EntityName, domainAggregate.Owner.Subdomain, token,
                out var infraEntityExplicitDomain, domainAggregate.Owner.DomainFilter)) return;
        var effectiveAggregate = infraEntityExplicitDomain
            ? domainAggregate with { Owner = domainAggregate.Owner.WithExplicitDomain(true) }
            : domainAggregate;
        if (InfraSymbols.TryCreate(compilation) is not { } symbols) return;
        var types = Types(compilation.Assembly.GlobalNamespace, token).ToArray();
        if (types.Any(type => RepositoryFor(type, symbols.ImportedContract, importedEntity))) return;
        var project = new InfraProject(infra, infraInfo.Namespace, Path.GetDirectoryName(infraProjectPath)!, infraInfo.Layout, infraInputs);
        foreach (var context in types.Where(type => !type.IsAbstract && !IsOpen(type) &&
                     IsPublicType(type) && type.AllInterfaces.Any(contractType => Same(contractType, symbols.ContextContract))))
        {
            token.ThrowIfCancellationRequested();
            if (await TryCreateRepositoryCandidateAsync(effectiveAggregate, project, compilation, importedEntity, symbols, context, token) is { } candidate)
                result.Add(candidate);
        }
    }

    private static async Task<RepositoryCandidate?> TryCreateRepositoryCandidateAsync(DomainAggregateInfo domainAggregate,
        InfraProject project,
        Compilation compilation, INamedTypeSymbol importedEntity, InfraSymbols symbols,
        INamedTypeSymbol context, CancellationToken token)
    {
        var conventionalContextOwner = ResolveNamespaceOwnerKey(context, project.Namespace, project.Layout);
        var contextOwner = conventionalContextOwner is null ? null : DeclaredOwner(context, conventionalContextOwner, compilation);
        if (!DefaultDiscovery(context, compilation, token) || !EntityConfigurationRefactoringProvider.HasMembership(context, importedEntity, compilation) ||
            conventionalContextOwner is null || contextOwner is null || !contextOwner.SameOwner(conventionalContextOwner) ||
            !context.Name.EndsWith("Context", StringComparison.Ordinal) || context.Name.Length <= 7 ||
            !OwnershipCompatible(project.Inputs, MetadataName(context), contextOwner.Subdomain, token,
                out var contextOwnershipExplicitDomain, contextOwner.DomainFilter)) return null;
        var matchesContextName = ConventionNames.IsConventionalContextName(context.Name, contextOwner.Domain,
            contextOwner.Subdomain, contextOwner.HasFolderDomain);
        var aliasRequired = !matchesContextName && !ReasonedSymbolOwner(context, contextOwner, compilation);
        var conventionalDestination = context.ContainingNamespace.ToDisplayString()[(project.Namespace.Length + 1)..]
            .Replace('.', '/') + "/Repositories";
        var declaredDestination = RepositoryDestination(project.Inputs, (MetadataName(context), contextOwner, aliasRequired),
            conventionalDestination, token, out var contextAttributeExplicitDomain);
        if (declaredDestination is null) return null;
        var effectiveContextOwner = contextOwner.WithExplicitDomain(contextOwnershipExplicitDomain || contextAttributeExplicitDomain);
        if (!effectiveContextOwner.MatchesAcrossLayers(domainAggregate.Owner)) return null;
        var contextDocument = await ResolveEditableHomeAsync(project.Project, context, rejectSpanErrors: true, token);
        if (contextDocument?.FilePath is null) return null;
        var repositoryNamespace = context.ContainingNamespace.ToDisplayString() + ".Repositories";
        if (!ValidNamespace(repositoryNamespace) ||
            ConventionGenerationPreflight.SymbolCollision(compilation, repositoryNamespace, domainAggregate.ClassName, token)) return null;
        if (!TryFullPath(Path.Combine(Path.GetDirectoryName(contextDocument.FilePath)!, "Repositories",
                domainAggregate.ClassName + ".cs"), out var destination) ||
            !SafeDocumentDestination(project.Project, project.Directory, destination, token)) return null;
        var relative = Path.GetRelativePath(project.Directory, destination).Replace('\\', '/');
        if (declaredDestination != relative[..relative.LastIndexOf('/')] ||
            !SupportsRepositoryConstructor(symbols, context, importedEntity)) return null;
        return new(project.Project.Id, MetadataName(context), context.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            importedEntity.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), domainAggregate.InterfaceName,
            domainAggregate.ClassName, domainAggregate.DomainNamespace, repositoryNamespace,
            Path.GetRelativePath(domainAggregate.DomainRoot, domainAggregate.DomainDestination).Replace('\\', '/'),
            relative, domainAggregate.DomainDestination, destination);
    }

    private static bool SupportsRepositoryConstructor(InfraSymbols symbols, INamedTypeSymbol context, INamedTypeSymbol importedEntity)
    {
        var constructed = symbols.Repository.Construct(context, importedEntity);
        return constructed.InstanceConstructors.Any(constructor => constructor.Parameters.Length == 2 &&
            constructor.Parameters.All(parameter => parameter.RefKind == RefKind.None) &&
            SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, context) &&
            SymbolEqualityComparer.Default.Equals(constructor.Parameters[1].Type, symbols.Utils) &&
            constructor.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal);
    }

    private sealed record InfraProject(Project Project, string Namespace, string Directory,
        ConventionGenerationPreflight.ProjectLayout Layout, CompilationUnitSyntax Inputs);

    private sealed record DomainAggregateInfo(INamedTypeSymbol Entity, string EntityName, OwnerKey Owner,
        string DomainNamespace, string DomainRoot, string DomainDestination, string InterfaceName, string ClassName);

    private sealed record InfraSymbols(INamedTypeSymbol ContextContract, INamedTypeSymbol Repository,
        INamedTypeSymbol Utils, INamedTypeSymbol ImportedContract)
    {
        internal static InfraSymbols? TryCreate(Compilation compilation)
        {
            var contextContract = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Context.IDrnContext");
            var repository = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Domain.SourceKnownRepository`2");
            var utils = compilation.GetTypeByMetadataName("DRN.Framework.Utils.Entity.IEntityUtils");
            var scoped = compilation.GetTypeByMetadataName("DRN.Framework.Utils.DependencyInjection.Attributes.ScopedAttribute`1");
            var importedContract = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository`1");
            return contextContract is null || repository is null || utils is null || scoped is null || importedContract is null
                ? null
                : new(contextContract, repository, utils, importedContract);
        }
    }

    private static bool ReasonedSymbolOwner(INamedTypeSymbol type, OwnerKey owner, Compilation compilation)
    {
        var ownership = compilation.GetTypeByMetadataName(OwnershipAttributeMetadataName);
        return type.GetAttributes().Any(attribute => Same(attribute.AttributeClass, ownership) &&
            MatchesSubdomainAttribute(attribute, owner) && HasReason(attribute));
    }

    private static bool RepositoryFor(INamedTypeSymbol type, INamedTypeSymbol contract, INamedTypeSymbol entity) =>
        type.AllInterfaces.Any(binding => Same(binding, contract) && SymbolEqualityComparer.Default.Equals(binding.TypeArguments[0], entity));

    private static string? RepositoryDestination(CompilationUnitSyntax root, (string Name, OwnerKey Owner, bool AliasRequired) context,
        string conventionalDestination, CancellationToken token, out bool hasExplicitDomain)
    {
        hasExplicitDomain = false;
        string? destination = null;
        var declaredContext = false;
        foreach (var list in root.AttributeLists)
        {
            foreach (var attribute in list.Attributes)
            {
                token.ThrowIfCancellationRequested();
                if (!TryReadContextRepositoryAttribute(list, attribute, context, conventionalDestination,
                        ref destination, ref declaredContext, ref hasExplicitDomain, token))
                    return null;
            }
        }
        return context.AliasRequired && !declaredContext ? null : destination ?? conventionalDestination;
    }

    private static bool TryReadContextRepositoryAttribute(AttributeListSyntax list, AttributeSyntax attribute,
        (string Name, OwnerKey Owner, bool AliasRequired) context, string conventionalDestination,
        ref string? destination, ref bool declaredContext, ref bool hasExplicitDomain, CancellationToken token)
    {
        if (list.Target?.Identifier.ValueText != "assembly" ||
            ConventionInputSyntax.Kind(attribute) != ConventionInputSyntax.ContextKind ||
            attribute.ArgumentList is not { Arguments.Count: > 0 } arguments ||
            ConventionInputSyntax.String(arguments.Arguments[0].Expression, token) != context.Name) return true;
        declaredContext = true;
        if (arguments.Arguments[0].NameEquals is not null || arguments.Arguments[0].NameColon is not null) return false;
        if (!ValidateContextSubdomainAndAlias(arguments.Arguments, context.Owner, context.AliasRequired, token, out var attrExplicitDomain)) return false;
        hasExplicitDomain |= attrExplicitDomain;
        var resolved = TryResolveContextRepositoryPath(arguments.Arguments, conventionalDestination, token);
        if (resolved is null || destination is not null && destination != resolved) return false;
        destination = resolved;
        return true;
    }

    private static bool ValidateContextSubdomainAndAlias(SeparatedSyntaxList<AttributeArgumentSyntax> arguments,
        OwnerKey owner, bool aliasRequired, CancellationToken token, out bool hasExplicitDomain) =>
        ContextOwnerCompatible(arguments, owner, token, out var hasDeclaredSubdomain, out hasExplicitDomain) &&
        (!aliasRequired || hasDeclaredSubdomain && HasSingleNonBlankArgument(arguments, ConventionInputSyntax.ReasonField, token));

    private static string? TryResolveContextRepositoryPath(SeparatedSyntaxList<AttributeArgumentSyntax> arguments,
        string conventionalDestination, CancellationToken token)
    {
        if (!TryReadSingleOptionalField(arguments, ConventionInputSyntax.RepositoriesField, token, out var value)) return null;
        var resolved = value.Length == 0 ? conventionalDestination : value.Replace('\\', '/');
        if (resolved != conventionalDestination && !HasSingleNonBlankArgument(arguments, ConventionInputSyntax.ReasonField, token)) return null;
        return resolved;
    }

    private static bool HasSingleNonBlankArgument(SeparatedSyntaxList<AttributeArgumentSyntax> arguments,
        string name, CancellationToken token) =>
        TryReadSingleOptionalField(arguments, name, token, out var value) && !string.IsNullOrWhiteSpace(value);

    private sealed record RepositoryCandidate(ProjectId InfraProject, string ContextName, string ContextType, string EntityType,
        string InterfaceName, string ClassName, string DomainNamespace, string InfraNamespace, string DomainPath, string InfraPath,
        string DomainFullPath, string InfraFullPath);
}
