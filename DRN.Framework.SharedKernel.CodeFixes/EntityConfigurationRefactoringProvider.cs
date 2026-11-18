using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static DRN.Framework.SharedKernel.CodeFixes.CodeFixDestinationGuard;
using static DRN.Framework.SharedKernel.CodeFixes.CodeFixSymbolFacts;

namespace DRN.Framework.SharedKernel.CodeFixes;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(EntityConfigurationRefactoringProvider)), Shared]
public sealed class EntityConfigurationRefactoringProvider : CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var solution = context.Document.Project.Solution;
        if (!solution.Workspace.CanApplyChange(ApplyChangesKind.AddDocument)) return;
        var entity = await SelectedEntity(context.Document, context.Span, context.CancellationToken);
        if (entity is null) return;
        var entityName = CodeFixSymbolFacts.MetadataName(entity);
        var entityAssembly = entity.ContainingAssembly.Identity;
        foreach (var project in solution.Projects.Where(project => project.Language == LanguageNames.CSharp))
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var candidates = await ConfigurationCandidates(project, entityName, entityAssembly, context.CancellationToken);
            foreach (var candidate in candidates)
            {
                var snapshot = solution.Workspace.CurrentSolution;
                if (snapshot != solution) return;
                context.RegisterRefactoring(CodeAction.Create(
                    "Add entity configuration: " + candidate.ContextName + " / " + candidate.RelativePath,
                    token => Apply(solution, project.Id, entityName, entityAssembly, candidate, snapshot, token),
                    "DrnEntityConfiguration:" + project.Id + ":" + candidate.ContextName + ":" + candidate.RelativePath + ":" + entityName));
            }
        }
    }

    private static async Task<Solution> Apply(Solution solution, ProjectId projectId, string entityName, AssemblyIdentity entityAssembly,
        ConfigurationCandidate candidate, Solution snapshot, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var current = snapshot.Workspace.CurrentSolution;
        if (current != snapshot) return current;
        var project = solution.GetProject(projectId);
        if (project is null) return current;
        var candidates = await ConfigurationCandidates(project, entityName, entityAssembly, token);
        if (!candidates.Contains(candidate)) return current;
        token.ThrowIfCancellationRequested();
        if (snapshot.Workspace.CurrentSolution != snapshot) return snapshot.Workspace.CurrentSolution;
        return AddConfigurationDocument(solution, projectId, candidate);
    }

    internal static Solution AddConfigurationDocument(Solution solution, ProjectId projectId, ConfigurationCandidate candidate)
    {
        var text = $$"""
            namespace {{candidate.Namespace}};

            public sealed class {{candidate.ClassName}} : global::Microsoft.EntityFrameworkCore.IEntityTypeConfiguration<{{candidate.EntityType}}>
            {
                public void Configure(global::Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<{{candidate.EntityType}}> builder)
                {
                }
            }

            """;
        return solution.AddDocument(DocumentId.CreateNewId(projectId), candidate.ClassName + ".cs", SourceText.From(text),
            candidate.RelativePath.Split('/')[..^1], candidate.FullPath);
    }

    internal static async Task<INamedTypeSymbol?> SelectedEntity(Document document, TextSpan span, CancellationToken token)
    {
        var syntaxRoot = await document.GetSyntaxRootAsync(token);
        if (syntaxRoot is null || span.End > syntaxRoot.FullSpan.End ||
            !HasIntersectingTypeNode(syntaxRoot, span)) return null;
        var root = await ClassificationEdit.FindEditableRoot(document, span.Start, token);
        var model = await document.GetSemanticModelAsync(token);
        if (root is null || model is null) return null;
        foreach (var node in root.FindToken(span.Start).Parent?.AncestorsAndSelf() ?? [])
        {
            if (ResolveSelectedNodeSymbol(node, span, model, token) is INamedTypeSymbol type &&
                !type.IsAbstract && IsEntity(type, model.Compilation) && !IsOpen(type))
                return type;
        }
        return null;
    }

    private static bool HasIntersectingTypeNode(SyntaxNode syntaxRoot, TextSpan span)
    {
        foreach (var node in syntaxRoot.FindToken(span.Start).Parent?.AncestorsAndSelf() ?? [])
        {
            if (node is TypeDeclarationSyntax declaration && declaration.Identifier.Span.IntersectsWith(span) ||
                node is TypeSyntax syntax && syntax.Span.IntersectsWith(span))
                return true;
        }
        return false;
    }

    private static ISymbol? ResolveSelectedNodeSymbol(SyntaxNode node, TextSpan span, SemanticModel model, CancellationToken token) => node switch
    {
        TypeDeclarationSyntax declaration when declaration.Identifier.Span.IntersectsWith(span) => model.GetDeclaredSymbol(declaration, token),
        TypeSyntax syntax when syntax.Span.IntersectsWith(span) => model.GetTypeInfo(syntax, token).Type as INamedTypeSymbol,
        _ => null
    };

    internal static async Task<List<ConfigurationCandidate>> ConfigurationCandidates(Project project, string entityName, AssemblyIdentity entityAssembly, CancellationToken token)
    {
        var result = new List<ConfigurationCandidate>();
        var infraIdentity = ConventionGenerationPreflight.Identity(project);
        if (infraIdentity.Role != "Infra" || !ValidNamespace(infraIdentity.Namespace) ||
            project.FilePath is null || !TryFullPath(project.FilePath, out var projectPath)) return result;
        var infraInputs = await ConventionGenerationPreflight.ReadInputs(project, required: false, token);
        if (infraInputs is null || HasRoleOverride(infraInputs)) return result;
        if (await ResolveAccessibleEntityAsync(project, entityName, entityAssembly, infraIdentity.Namespace, infraInputs, token)
            is not var (compilation, entity, entityOwner)) return result;
        if (HasConfigurationForEntity(compilation, entity, token, out var types, out var contextContract) ||
            await HasUnknownDependentDiscovery(project, token)) return result;
        var contexts = types.Where(type => !type.IsAbstract && !IsOpen(type) &&
            type.AllInterfaces.Any(contract => Same(contract, contextContract))).ToArray();
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var discovery = contexts.Select(context => (Context: context, Scopes: ConfigurationDiscovery.Read(context, compilation, token))).ToArray();
        // Unknown discovery could include this new class. Shared scans require an explicit all-context preview.
        if (discovery.Any(item => item.Scopes is null)) return result;
        foreach (var context in contexts)
        {
            token.ThrowIfCancellationRequested();
            if (await TryBuildContextCandidateAsync(project, compilation, entity, entityOwner, infraIdentity.Namespace,
                    infraInputs, projectDirectory, discovery, context, token) is { } candidate &&
                !result.Contains(candidate))
                result.Add(candidate);
        }
        return result;
    }

    private static async Task<(Compilation Compilation, INamedTypeSymbol Entity, string EntityOwner)?> ResolveAccessibleEntityAsync(
        Project project, string entityName, AssemblyIdentity entityAssembly, string infraNamespace,
        CompilationUnitSyntax infraInputs, CancellationToken token)
    {
        var compilation = await project.GetCompilationAsync(token);
        if (compilation?.GetTypeByMetadataName(entityName) is not { IsAbstract: false } entity ||
            !entity.ContainingAssembly.Identity.Equals(entityAssembly) || IsOpen(entity) ||
            !IsEntity(entity, compilation) || !compilation.IsSymbolAccessibleWithin(entity, compilation.Assembly)) return null;
        if (!SymbolEqualityComparer.Default.Equals(entity.ContainingAssembly, compilation.Assembly))
        {
            var reference = compilation.GetMetadataReference(entity.ContainingAssembly);
            if (reference is null || reference.Properties.Aliases.Length != 0 &&
                !reference.Properties.Aliases.Contains("global", StringComparer.Ordinal))
                return null;
        }
        // The generated public Configure method exposes the entity in its parameter type.
        if (!IsPublicType(entity)) return null;
        var entityOwner = await EntityOwner(project, compilation, entity, entityName, infraNamespace, infraInputs, token);
        return entityOwner is null ? null : (compilation, entity, entityOwner);
    }

    private static bool HasConfigurationForEntity(Compilation compilation, INamedTypeSymbol entity,
        CancellationToken token, out INamedTypeSymbol[] types, out INamedTypeSymbol? contextContract)
    {
        types = [];
        contextContract = null;
        var configuration = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.IEntityTypeConfiguration`1");
        contextContract = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Context.IDrnContext");
        var configurationAttribute = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.EntityTypeConfigurationAttribute");
        if (configuration is null || contextContract is null ||
            entity.GetAttributes().Any(attribute => Same(attribute.AttributeClass, configurationAttribute))) return true;
        types = Types(compilation.Assembly.GlobalNamespace, token).ToArray();
        return types.Any(type => type.AllInterfaces.Any(contract => Same(contract, configuration) &&
            SymbolEqualityComparer.Default.Equals(contract.TypeArguments[0], entity)));
    }

    private static async Task<ConfigurationCandidate?> TryBuildContextCandidateAsync(Project project, Compilation compilation,
        INamedTypeSymbol entity, string entityOwner, string infraNamespace, CompilationUnitSyntax infraInputs,
        string projectDirectory, (INamedTypeSymbol Context, List<ConfigurationDiscovery.Scope>? Scopes)[] discovery,
        INamedTypeSymbol context, CancellationToken token)
    {
        if (context.ContainingNamespace.IsGlobalNamespace || !HasMembership(context, entity, compilation) ||
            context.DeclaringSyntaxReferences.Length == 0) return null;
        var contextNamespace = context.ContainingNamespace.ToDisplayString();
        if (!ValidNamespace(contextNamespace) ||
            NamespaceOwner(context, infraNamespace) != entityOwner ||
            !ContextOwnershipCompatible(context, infraNamespace, entityOwner, infraInputs, compilation, token)) return null;
        var configurationNamespace = contextNamespace + ".Configurations";
        var affected = discovery.Where(item => item.Scopes!.Any(scope => scope.Contains(configurationNamespace)))
            .Select(item => item.Context).ToArray();
        if (!AreAffectedContextsCompatible(affected, context, entity, infraNamespace, entityOwner, infraInputs, compilation, token))
            return null;
        var contextDocument = await ResolveEditableHomeAsync(project, context, rejectSpanErrors: false, token);
        if (contextDocument?.FilePath is null) return null;
        var className = ConventionGenerationPreflight.EntityStem(entity.Name) + "Config";
        if (!SyntaxFacts.IsValidIdentifier(className) ||
            ConventionGenerationPreflight.SymbolCollision(compilation, configurationNamespace, className, token)) return null;
        if (!TryFullPath(Path.Combine(Path.GetDirectoryName(contextDocument.FilePath)!, "Configurations", className + ".cs"), out var destination))
            return null;
        if (!SafeDocumentDestination(project, projectDirectory, destination, token)) return null;
        var relative = Path.GetRelativePath(projectDirectory, destination).Replace('\\', '/');
        return new(string.Join(", ", affected.Select(CodeFixSymbolFacts.MetadataName).OrderBy(name => name, StringComparer.Ordinal)),
            configurationNamespace, className, entity.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), relative, destination,
            affected.Length == 1 ? CodeFixSymbolFacts.MetadataName(affected[0]) : null);
    }

    private static bool AreAffectedContextsCompatible(INamedTypeSymbol[] affected, INamedTypeSymbol context,
        INamedTypeSymbol entity, string infraNamespace, string entityOwner, CompilationUnitSyntax infraInputs,
        Compilation compilation, CancellationToken token) =>
        affected.Any(item => SymbolEqualityComparer.Default.Equals(item, context)) &&
        !affected.Any(item => !HasMembership(item, entity, compilation) ||
            NamespaceOwner(item, infraNamespace) != entityOwner ||
            !ContextOwnershipCompatible(item, infraNamespace, entityOwner, infraInputs, compilation, token));

    private static async Task<bool> HasUnknownDependentDiscovery(Project project, CancellationToken token)
    {
        foreach (var id in project.Solution.GetProjectDependencyGraph().GetProjectsThatTransitivelyDependOnThisProject(project.Id))
        {
            token.ThrowIfCancellationRequested();
            var dependent = project.Solution.GetProject(id);
            if (dependent is null || dependent.Language != LanguageNames.CSharp) return true;
            var compilation = await dependent.GetCompilationAsync(token);
            if (compilation is null || HasUnknownContextDiscovery(compilation, token)) return true;
        }
        return false;
    }

    private static bool HasUnknownContextDiscovery(Compilation compilation, CancellationToken token)
    {
        var contract = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Context.IDrnContext");
        foreach (var context in Types(compilation.Assembly.GlobalNamespace, token))
        {
            if (context.IsAbstract || IsOpen(context) || !context.AllInterfaces.Any(type => Same(type, contract))) continue;
            // Recognized scans are limited to the dependent's own assembly. Unknown scans may include this project.
            if (ConfigurationDiscovery.Read(context, compilation, token) is null) return true;
        }
        return false;
    }

    private static async Task<string?> EntityOwner(Project project, Compilation compilation, INamedTypeSymbol entity,
        string entityName, string infraNamespace, CompilationUnitSyntax infraInputs, CancellationToken token)
    {
        var (conventional, domainInputs, valid) = await ResolveConventionalOwnerAsync(project, compilation, entity, infraNamespace, token);
        if (!valid) return null;
        var owner = DeclaredSubdomain(entity, conventional, compilation);
        if (owner is null || !ConventionGenerationPreflight.ValidNamespaceSegment(owner)) return null;
        if (!OwnershipCompatible(infraInputs, entityName, owner, token)) return null;
        if (domainInputs is not null && !OwnershipCompatible(domainInputs, entityName, owner, token)) return null;
        return owner;
    }

    private static async Task<(string? Conventional, CompilationUnitSyntax? DomainInputs, bool Valid)> ResolveConventionalOwnerAsync(
        Project project, Compilation compilation, INamedTypeSymbol entity, string infraNamespace, CancellationToken token)
    {
        var declaringProject = await DeclaringProject(project, compilation, entity, token);
        if (declaringProject is null || declaringProject.Id == project.Id)
        {
            var localConventional = EntityDomainOwner(entity) ??
                (declaringProject?.Id == project.Id ? NamespaceOwner(entity, infraNamespace) : null);
            return (localConventional, null, true);
        }
        var declaringIdentity = ConventionGenerationPreflight.Identity(declaringProject);
        if (declaringIdentity.Role != "Domain") return (EntityDomainOwner(entity), null, true);
        if (!ValidNamespace(declaringIdentity.Namespace)) return (null, null, false);
        var domainInputs = await ConventionGenerationPreflight.ReadInputs(declaringProject, required: false, token);
        if (domainInputs is null || HasRoleOverride(domainInputs)) return (null, null, false);
        return (NamespaceOwner(entity, declaringIdentity.Namespace), domainInputs, true);
    }

    private static async Task<Project?> DeclaringProject(Project project, Compilation compilation, INamedTypeSymbol entity, CancellationToken token)
    {
        if (SymbolEqualityComparer.Default.Equals(entity.ContainingAssembly, compilation.Assembly)) return project;
        var pending = new Queue<ProjectReference>(project.ProjectReferences);
        var seen = new HashSet<ProjectId> { project.Id };
        while (pending.Count > 0)
        {
            token.ThrowIfCancellationRequested();
            var current = project.Solution.GetProject(pending.Dequeue().ProjectId);
            if (current is null || !seen.Add(current.Id) || current.Language != LanguageNames.CSharp) continue;
            foreach (var reference in current.ProjectReferences) pending.Enqueue(reference);
            if (!string.Equals(current.AssemblyName, entity.ContainingAssembly.Name, StringComparison.OrdinalIgnoreCase)) continue;
            var currentCompilation = await current.GetCompilationAsync(token);
            if (currentCompilation is not null && currentCompilation.Assembly.Identity.Equals(entity.ContainingAssembly.Identity))
                return current;
        }
        return null;
    }

    private static string? EntityDomainOwner(INamedTypeSymbol entity)
    {
        if (entity.ContainingNamespace.IsGlobalNamespace) return null;
        var ns = entity.ContainingNamespace.ToDisplayString();
        var marker = ns.IndexOf(".Domain.", StringComparison.Ordinal);
        if (marker >= 0) return ns[(marker + ".Domain.".Length)..].Split('.')[0];
        return ns.StartsWith("Domain.", StringComparison.Ordinal) ? ns["Domain.".Length..].Split('.')[0] : null;
    }

    private static bool ContextOwnershipCompatible(INamedTypeSymbol context, string infraNamespace, string owner,
        CompilationUnitSyntax inputs, Compilation compilation, CancellationToken token)
    {
        var contextName = CodeFixSymbolFacts.MetadataName(context);
        var conventional = NamespaceOwner(context, infraNamespace);
        if (DeclaredSubdomain(context, conventional, compilation) != owner) return false;
        if (!OwnershipCompatible(inputs, contextName, owner, token)) return false;
        foreach (var list in inputs.AttributeLists)
        {
            foreach (var attribute in list.Attributes)
            {
                token.ThrowIfCancellationRequested();
                if (!IsCompatibleContextAttribute(list, attribute, contextName, owner, token)) return false;
            }
        }
        return true;
    }

    private static bool IsCompatibleContextAttribute(AttributeListSyntax list, AttributeSyntax attribute,
        string contextName, string owner, CancellationToken token)
    {
        if (list.Target?.Identifier.ValueText != "assembly" ||
            attribute.Name.ToString() is not ("DRN.Conventions.Context" or "global::DRN.Conventions.Context") ||
            attribute.ArgumentList is not { Arguments.Count: > 0 } arguments ||
            DRN.Framework.SharedKernel.Analyzers.Conventions.ConventionInputSyntax.String(arguments.Arguments[0].Expression, token) != contextName)
            return true;
        if (arguments.Arguments[0].NameEquals is not null || arguments.Arguments[0].NameColon is not null) return false;
        var owners = arguments.Arguments.Where(argument => argument.NameEquals?.Name.Identifier.ValueText == "Subdomain").ToArray();
        if (owners.Length > 1) return false;
        if (owners.Length == 0) return true;
        var declared = DRN.Framework.SharedKernel.Analyzers.Conventions.ConventionInputSyntax.String(owners[0].Expression, token);
        return declared is not null && (declared.Length == 0 || declared == owner);
    }

    internal static bool HasMembership(INamedTypeSymbol context, INamedTypeSymbol entity, Compilation compilation)
    {
        var dbSet = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbSet`1");
        var membership = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Attributes.SourceKnownPersistenceAttribute");
        if (!TryCheckExplicitInclusion(context, entity, membership, out var explicitlyIncluded)) return false;
        if (explicitlyIncluded) return true;
        for (var current = context; current is not null; current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
                if (!property.IsStatic && !property.IsIndexer && property.Type is INamedTypeSymbol set &&
                    Same(set, dbSet) && set.TypeArguments.Length == 1 &&
                    SymbolEqualityComparer.Default.Equals(set.TypeArguments[0], entity)) return true;
        }
        return false;
    }

    private static bool TryCheckExplicitInclusion(INamedTypeSymbol context, INamedTypeSymbol entity,
        INamedTypeSymbol? membership, out bool explicitlyIncluded)
    {
        explicitlyIncluded = false;
        foreach (var attribute in context.GetAttributes())
        {
            if (!Same(attribute.AttributeClass, membership) || attribute.ConstructorArguments is not [{ Value: INamedTypeSymbol mapped }] ||
                !SymbolEqualityComparer.Default.Equals(mapped, entity)) continue;
            var inclusion = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == "Inclusion");
            if (inclusion.Key is not null &&
                (inclusion.Value.Kind == TypedConstantKind.Error || inclusion.Value.Value is not int value || value != 0))
                return false;
            explicitlyIncluded = true;
        }
        return true;
    }

    private static bool IsEntity(INamedTypeSymbol type, Compilation compilation)
    {
        var entity = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.SourceKnownEntity");
        var identity = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Identity.IdentityUser`1");
        return Inherits(type, entity) || Inherits(type, identity);
    }

    internal sealed record ConfigurationCandidate(string ContextName, string Namespace, string ClassName, string EntityType,
        string RelativePath, string FullPath, string? SingleContextName);
}
