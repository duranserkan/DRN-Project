using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static DRN.Framework.SharedKernel.CodeFixes.EntityConfigurationRefactoringProvider;

namespace DRN.Framework.SharedKernel.CodeFixes;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(RepositoryPairRefactoringProvider)), Shared]
public sealed class RepositoryPairRefactoringProvider : CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        if (ConventionGenerationPreflight.Identity(context.Document.Project).Role != "Domain") return;
        var entity = await SelectedEntity(context.Document, context.Span, context.CancellationToken);
        if (entity is null) return;
        var snapshot = context.Document.Project.Solution;
        if (!ReferenceEquals(snapshot, snapshot.Workspace.CurrentSolution) ||
            !snapshot.Workspace.CanApplyChange(ApplyChangesKind.AddDocument)) return;
        var candidates = await Candidates(context.Document.Project, MetadataName(entity), context.CancellationToken);
        var configurations = new Dictionary<ProjectId, List<ConfigurationCandidate>>();
        foreach (var candidate in candidates)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            if (!ReferenceEquals(snapshot, snapshot.Workspace.CurrentSolution)) return;
            context.RegisterRefactoring(CodeAction.Create(
                "Add scoped repository pair: " + candidate.InterfaceName + " + " + candidate.ClassName +
                " for " + candidate.ContextName + " (" + candidate.DomainPath + ", " + candidate.InfraPath + ")",
                token => Apply(snapshot, context.Document.Project.Id, MetadataName(entity), candidate, entity.ContainingAssembly.Identity, null, token),
                "DrnRepositoryPair:" + candidate.InfraProject + ":" + candidate.ContextName + ":" + MetadataName(entity)));
            if (!configurations.TryGetValue(candidate.InfraProject, out var options))
            {
                options = await ConfigurationCandidates(snapshot.GetProject(candidate.InfraProject)!, MetadataName(entity),
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
                    token => Apply(snapshot, context.Document.Project.Id, MetadataName(entity), candidate, entity.ContainingAssembly.Identity, configuration, token),
                    "DrnPersistenceBundle:" + candidate.InfraProject + ":" + candidate.ContextName + ":" + MetadataName(entity)));
            }
        }
    }

    private static async Task<Solution> Apply(Solution snapshot, ProjectId domainId, string entityName, RepositoryCandidate candidate,
        AssemblyIdentity entityAssembly, ConfigurationCandidate? configuration, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var current = snapshot.Workspace.CurrentSolution;
        if (!ReferenceEquals(current, snapshot) || snapshot.GetProject(domainId) is not { } domain) return current;
        if (!(await Candidates(domain, entityName, token)).Contains(candidate)) return snapshot.Workspace.CurrentSolution;
        if (configuration is not null && (snapshot.GetProject(candidate.InfraProject) is not { } infra ||
            configuration.SingleContextName != candidate.ContextName || !CompatibleDestinations(candidate, configuration) ||
            !(await ConfigurationCandidates(infra, entityName, entityAssembly, token)).Contains(configuration)))
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
        if (configuration is not null) changed = AddConfigurationDocument(changed, candidate.InfraProject, configuration);
        token.ThrowIfCancellationRequested();
        return ReferenceEquals(snapshot.Workspace.CurrentSolution, snapshot) ? changed : snapshot.Workspace.CurrentSolution;
    }

    private static bool CompatibleDestinations(RepositoryCandidate repository, ConfigurationCandidate configuration) =>
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
        var domainIdentity = ProjectIdentity(domain);
        if (domainIdentity.Role != "Domain" || !ValidNamespace(domainIdentity.Namespace) || domain.FilePath is null ||
            !TryFullPath(domain.FilePath, out var domainProjectPath)) return null;
        var domainInputs = await ReadInputs(domain, required: false, token);
        if (domainInputs is null) return null;
        var domainCompilation = await domain.GetCompilationAsync(token);
        if (domainCompilation?.GetTypeByMetadataName(entityName) is not { IsAbstract: false } entity || IsOpen(entity) || !PublicType(entity) ||
            !SymbolEqualityComparer.Default.Equals(entity.ContainingAssembly, domainCompilation.Assembly) ||
            !Inherits(entity, domainCompilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.AggregateRoot"))) return null;
        return await ResolveDomainDestinationAsync(domain, domainIdentity.Namespace, domainProjectPath, domainInputs,
            domainCompilation, entity, entityName, token);
    }

    private static async Task<DomainAggregateInfo?> ResolveDomainDestinationAsync(Project domain, string domainRoleNamespace,
        string domainProjectPath, CompilationUnitSyntax domainInputs, Compilation domainCompilation,
        INamedTypeSymbol entity, string entityName, CancellationToken token)
    {
        var domainDocument = await EditableHome(domain, entity, token);
        var owner = Owner(entity, domainRoleNamespace, domainCompilation);
        if (domainDocument?.FilePath is null || !TryFullPath(domainDocument.FilePath, out var domainDocumentPath) ||
            owner is null || entity.ContainingNamespace.IsGlobalNamespace ||
            NamespaceOwner(entity, domainRoleNamespace) != owner ||
            !OwnershipCompatible(domainInputs, entityName, owner, token)) return null;
        var domainNamespace = entity.ContainingNamespace.ToDisplayString();
        var domainRoot = Path.GetDirectoryName(domainProjectPath)!;
        var expectedDomainRelative = domainNamespace[(domainRoleNamespace.Length + 1)..].Replace('.', '/');
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
        return new(entity, entityName, owner, domainNamespace, domainRoot, domainDestination, interfaceName, className);
    }

    private static async Task CollectInfraProjectCandidatesAsync(DomainAggregateInfo domainAggregate, Project infra,
        List<RepositoryCandidate> result, CancellationToken token)
    {
        var infraIdentity = ProjectIdentity(infra);
        if (infraIdentity.Role != "Infra" || !ValidNamespace(infraIdentity.Namespace) || infra.FilePath is null ||
            !TryFullPath(infra.FilePath, out var infraProjectPath)) return;
        var infraInputs = await ReadInputs(infra, required: false, token);
        if (infraInputs is null) return;
        var compilation = await infra.GetCompilationAsync(token);
        var importedEntity = compilation?.GetTypeByMetadataName(domainAggregate.EntityName);
        if (compilation is null || importedEntity is null ||
            !importedEntity.ContainingAssembly.Identity.Equals(domainAggregate.Entity.ContainingAssembly.Identity) ||
            !compilation.IsSymbolAccessibleWithin(importedEntity, compilation.Assembly) ||
            !Inherits(importedEntity, compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.AggregateRoot")) ||
            !OwnershipCompatible(infraInputs, domainAggregate.EntityName, domainAggregate.Owner, token)) return;
        if (InfraSymbols.TryCreate(compilation) is not { } symbols) return;
        var types = Types(compilation.Assembly.GlobalNamespace, token).ToArray();
        if (types.Any(type => RepositoryFor(type, symbols.ImportedContract, importedEntity))) return;
        var infraRoot = Path.GetDirectoryName(infraProjectPath)!;
        foreach (var context in types.Where(type => !type.IsAbstract && !IsOpen(type) &&
                     PublicType(type) && type.AllInterfaces.Any(contractType => Same(contractType, symbols.ContextContract))))
        {
            token.ThrowIfCancellationRequested();
            if (await TryCreateRepositoryCandidateAsync(domainAggregate, infra, infraIdentity.Namespace, infraRoot,
                    infraInputs, compilation, importedEntity, symbols, context, token) is { } candidate)
                result.Add(candidate);
        }
    }

    private static async Task<RepositoryCandidate?> TryCreateRepositoryCandidateAsync(DomainAggregateInfo domainAggregate,
        Project infra, string infraRoleNamespace, string infraRoot, CompilationUnitSyntax infraInputs,
        Compilation compilation, INamedTypeSymbol importedEntity, InfraSymbols symbols,
        INamedTypeSymbol context, CancellationToken token)
    {
        if (!DefaultDiscovery(context, compilation, token) || !HasMembership(context, importedEntity, compilation) ||
            Owner(context, infraRoleNamespace, compilation) != domainAggregate.Owner ||
            NamespaceOwner(context, infraRoleNamespace) != domainAggregate.Owner ||
            !context.Name.EndsWith("Context", StringComparison.Ordinal) || context.Name.Length <= 7 ||
            !OwnershipCompatible(infraInputs, MetadataName(context), domainAggregate.Owner, token)) return null;
        var contextDocument = await EditableHome(infra, context, token);
        if (contextDocument?.FilePath is null) return null;
        var repositoryNamespace = context.ContainingNamespace.ToDisplayString() + ".Repositories";
        if (!ValidNamespace(repositoryNamespace) ||
            ConventionGenerationPreflight.SymbolCollision(compilation, repositoryNamespace, domainAggregate.ClassName, token)) return null;
        if (!TryFullPath(Path.Combine(Path.GetDirectoryName(contextDocument.FilePath)!, "Repositories",
                domainAggregate.ClassName + ".cs"), out var destination) ||
            !SafeDocumentDestination(infra, infraRoot, destination, token)) return null;
        var relative = Path.GetRelativePath(infraRoot, destination).Replace('\\', '/');
        var aliasRequired = context.Name[..^7] != domainAggregate.Owner && !ReasonedSymbolOwner(context, domainAggregate.Owner, compilation);
        var conventionalDestination = context.ContainingNamespace.ToDisplayString()[(infraRoleNamespace.Length + 1)..]
            .Replace('.', '/') + "/Repositories";
        var declaredDestination = RepositoryDestination(infraInputs, MetadataName(context), domainAggregate.Owner,
            aliasRequired, conventionalDestination, token);
        if (declaredDestination != relative[..relative.LastIndexOf('/')] ||
            !SupportsRepositoryConstructor(symbols, context, importedEntity)) return null;
        return new(infra.Id, MetadataName(context), context.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
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

    private sealed record DomainAggregateInfo(INamedTypeSymbol Entity, string EntityName, string Owner,
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

    private static (string Role, string Namespace) ProjectIdentity(Project project) =>
        ConventionGenerationPreflight.Identity(project);

    private static string? Owner(INamedTypeSymbol type, string rootNamespace, Compilation compilation) =>
        DeclaredSubdomain(type, NamespaceOwner(type, rootNamespace), compilation);

    private static bool ReasonedSymbolOwner(INamedTypeSymbol type, string owner, Compilation compilation)
    {
        var ownership = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomainAttribute");
        return type.GetAttributes().Any(attribute => Same(attribute.AttributeClass, ownership) &&
            attribute.ConstructorArguments is [{ Value: string value }] && value == owner &&
            attribute.NamedArguments.Any(argument => argument is { Key: "Reason", Value.Value: string reason } &&
                                                     !string.IsNullOrWhiteSpace(reason)));
    }

    private static async Task<Document?> EditableHome(Project project, INamedTypeSymbol type, CancellationToken token)
    {
        Document? home = null;
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            token.ThrowIfCancellationRequested();
            var document = project.GetDocument(reference.SyntaxTree);
            if (document?.FilePath is null || !TryFullPath(document.FilePath, out _) || document.GetLinkedDocumentIds().Length != 0 ||
                await ClassificationEdit.FindEditableRoot(document, reference.Span.Start, token) is null) return null;
            var model = await document.GetSemanticModelAsync(token);
            if (model is null || model.GetDiagnostics(reference.Span, token).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)) return null;
            if (home is not null && Path.GetDirectoryName(home.FilePath) != Path.GetDirectoryName(document.FilePath)) return null;
            home = document;
        }
        return home;
    }

    private static bool SafeDocumentDestination(Project project, string root, string destination, CancellationToken token) => SafeDestination(root, destination) &&
        !WorkspaceDestinationConflict(project.Solution, destination, project.Id, token);

    private static bool RepositoryFor(INamedTypeSymbol type, INamedTypeSymbol contract, INamedTypeSymbol entity) =>
        type.AllInterfaces.Any(binding => Same(binding, contract) && SymbolEqualityComparer.Default.Equals(binding.TypeArguments[0], entity));

    private static bool Inherits(INamedTypeSymbol type, INamedTypeSymbol? target)
    {
        for (var current = type; current is not null; current = current.BaseType)
            if (Same(current, target)) return true;
        return false;
    }

    private static bool PublicType(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
            if (current.DeclaredAccessibility != Accessibility.Public) return false;
        return true;
    }

    private static string? RepositoryDestination(CompilationUnitSyntax root, string contextName, string owner, bool aliasRequired,
        string conventionalDestination, CancellationToken token)
    {
        string? destination = null;
        var declaredContext = false;
        foreach (var list in root.AttributeLists)
        {
            foreach (var attribute in list.Attributes)
            {
                token.ThrowIfCancellationRequested();
                if (!TryReadContextRepositoryAttribute(list, attribute, contextName, owner, aliasRequired,
                        conventionalDestination, ref destination, ref declaredContext, token)) return null;
            }
        }
        return aliasRequired && !declaredContext ? null : destination ?? conventionalDestination;
    }

    private static bool TryReadContextRepositoryAttribute(AttributeListSyntax list, AttributeSyntax attribute,
        string contextName, string owner, bool aliasRequired, string conventionalDestination,
        ref string? destination, ref bool declaredContext, CancellationToken token)
    {
        if (list.Target?.Identifier.ValueText != "assembly" ||
            attribute.Name.ToString().Replace("global::", "") != "DRN.Conventions.Context" ||
            attribute.ArgumentList is not { Arguments.Count: > 0 } arguments ||
            Literal(arguments.Arguments[0].Expression, token) != contextName) return true;
        declaredContext = true;
        if (arguments.Arguments[0].NameEquals is not null || arguments.Arguments[0].NameColon is not null) return false;
        if (!ValidateContextSubdomainAndAlias(arguments.Arguments, owner, aliasRequired, token)) return false;
        var resolved = TryResolveContextRepositoryPath(arguments.Arguments, conventionalDestination, token);
        if (resolved is null || destination is not null && destination != resolved) return false;
        destination = resolved;
        return true;
    }

    private static bool ValidateContextSubdomainAndAlias(SeparatedSyntaxList<AttributeArgumentSyntax> arguments,
        string owner, bool aliasRequired, CancellationToken token)
    {
        var owners = arguments.Where(argument => argument.NameEquals?.Name.Identifier.ValueText == "Subdomain").ToArray();
        if (owners.Length > 1) return false;
        var declaredOwner = owners.Length == 1 ? Literal(owners[0].Expression, token) : "";
        if (declaredOwner is null || declaredOwner.Length > 0 && declaredOwner != owner) return false;
        if (!aliasRequired) return true;
        return owners.Length == 1 && declaredOwner == owner && HasSingleNonBlankArgument(arguments, "Reason", token);
    }

    private static string? TryResolveContextRepositoryPath(SeparatedSyntaxList<AttributeArgumentSyntax> arguments,
        string conventionalDestination, CancellationToken token)
    {
        var values = arguments.Where(argument => argument.NameEquals?.Name.Identifier.ValueText == "Repositories").ToArray();
        if (values.Length > 1) return null;
        var value = values.Length == 0 ? "" : Literal(values[0].Expression, token);
        if (value is null) return null;
        var resolved = value.Length == 0 ? conventionalDestination : value.Replace('\\', '/');
        if (resolved != conventionalDestination && !HasSingleNonBlankArgument(arguments, "Reason", token)) return null;
        return resolved;
    }

    private static bool HasSingleNonBlankArgument(SeparatedSyntaxList<AttributeArgumentSyntax> arguments,
        string name, CancellationToken token)
    {
        var matches = arguments.Where(argument => argument.NameEquals?.Name.Identifier.ValueText == name).ToArray();
        return matches.Length == 1 && !string.IsNullOrWhiteSpace(Literal(matches[0].Expression, token));
    }

    private static async Task<CompilationUnitSyntax?> ReadInputs(Project project, bool required, CancellationToken token)
    {
        var root = await ConventionGenerationPreflight.ReadInputs(project, required, token);
        return root is null || HasRoleOverride(root) ? null : root;
    }

    private static string? Literal(ExpressionSyntax expression, CancellationToken token) =>
        Analyzers.Conventions.ConventionInputSyntax.String(expression, token);

    private sealed record RepositoryCandidate(ProjectId InfraProject, string ContextName, string ContextType, string EntityType,
        string InterfaceName, string ClassName, string DomainNamespace, string InfraNamespace, string DomainPath, string InfraPath,
        string DomainFullPath, string InfraFullPath);
}
