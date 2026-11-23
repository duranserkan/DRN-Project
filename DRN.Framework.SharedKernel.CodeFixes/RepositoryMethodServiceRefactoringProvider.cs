using DRN.Framework.SharedKernel.Analyzers.Conventions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using static DRN.Framework.SharedKernel.CodeFixes.CodeFixDestinationGuard;
using static DRN.Framework.SharedKernel.CodeFixes.CodeFixSymbolFacts;

namespace DRN.Framework.SharedKernel.CodeFixes;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(RepositoryMethodServiceRefactoringProvider)), Shared]
public sealed class RepositoryMethodServiceRefactoringProvider : CodeRefactoringProvider
{
    private static readonly bool[] ScopedOptions = [false, true];
    private static readonly string[] AsyncReturnTypeNames =
    [
        "System.Threading.Tasks.Task", "System.Threading.Tasks.Task`1",
        "System.Threading.Tasks.ValueTask", "System.Threading.Tasks.ValueTask`1"
    ];

    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var snapshot = context.Document.Project.Solution;
        if (context.Span.IsEmpty || !snapshot.Workspace.CanApplyChange(ApplyChangesKind.AddDocument)) return;
        var domainInfo = ConventionGenerationPreflight.ReadProject(context.Document.Project);
        if (domainInfo.Role != "Domain" || !domainInfo.Layout.Valid) return;
        var selected = await Select(context.Document, context.Span, context.CancellationToken);
        if (selected is null) return;
        var domainPreflight = await ResolveDomainPreflightAsync(context.Document.Project, domainInfo, selected, context.CancellationToken);
        if (domainPreflight is null) return;
        foreach (var target in snapshot.Projects)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var targetBase = await ResolveTargetPlanBaseAsync(domainPreflight, selected, target, context.CancellationToken);
            if (targetBase is null) continue;
            foreach (var scoped in ScopedOptions)
            {
                if (scoped && !targetBase.SupportsScoped) continue;
                var plan = RenderForwardingService(selected, targetBase, scoped);
                if (!ReferenceEquals(snapshot.Workspace.CurrentSolution, snapshot)) return;
                context.RegisterRefactoring(CodeAction.Create(
                    "Add " + (scoped ? "scoped" : "unregistered") + " forwarding service: " + target.Name + " / " + plan.RelativePath +
                    " for " + selected.Method.Name,
                    token => Apply(context.Document, context.Span, target.Id, scoped, plan, snapshot, token),
                    "DrnRepositoryMethodService:" + target.Id + ":" + selected.Method.GetDocumentationCommentId() + ":" + scoped));
            }
        }
    }

    private static async Task<Solution> Apply(Document document, TextSpan span, ProjectId targetId, bool scoped,
        ServicePlan expected, Solution snapshot, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!ReferenceEquals(snapshot.Workspace.CurrentSolution, snapshot)) return snapshot.Workspace.CurrentSolution;
        var target = snapshot.GetProject(targetId);
        var selected = await Select(document, span, token);
        if (target is null || selected is null) return snapshot.Workspace.CurrentSolution;
        var domainInfo = ConventionGenerationPreflight.ReadProject(document.Project);
        var domainPreflight = await ResolveDomainPreflightAsync(document.Project, domainInfo, selected, token);
        var targetBase = domainPreflight is null ? null : await ResolveTargetPlanBaseAsync(domainPreflight, selected, target, token);
        if (targetBase is null || scoped && !targetBase.SupportsScoped) return snapshot.Workspace.CurrentSolution;
        var plan = RenderForwardingService(selected, targetBase, scoped);
        if (plan != expected) return snapshot.Workspace.CurrentSolution;
        token.ThrowIfCancellationRequested();
        if (!ReferenceEquals(snapshot.Workspace.CurrentSolution, snapshot)) return snapshot.Workspace.CurrentSolution;
        return snapshot.AddDocument(DocumentId.CreateNewId(targetId), plan.Name + ".cs", SourceText.From(plan.Source),
            plan.RelativePath.Split('/')[..^1], plan.FullPath);
    }

    private static async Task<Selection?> Select(Document document, TextSpan span, CancellationToken token)
    {
        var found = await FindSelectedInterfaceMethodAsync(document, span, token);
        if (found is null) return null;
        var (declaration, model, method) = found.Value;
        if (!IsEligibleInterfaceMethod(method, model, declaration, token)) return null;
        var contract = model.Compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.Repository.ISourceKnownRepository`1");
        var bindings = method.ContainingType.AllInterfaces.Where(item => Same(item, contract)).ToArray();
        if (bindings is not [{ TypeArguments: [INamedTypeSymbol entity] }] || IsOpen(entity) || !IsPublicType(entity) ||
            !Inherits(entity, model.Compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.AggregateRoot")))
            return null;
        if (!HasOnlySupportedTypeAttributes(method.ContainingType) || !HasRequiredAsyncCancellation(method, model.Compilation))
            return null;
        return await ArePartsEditableAsync(document.Project, method.ContainingType, rejectSpanErrors: true, token)
            ? new(method, entity, declaration)
            : null;
    }

    private static async Task<(MethodDeclarationSyntax Declaration, SemanticModel Model, IMethodSymbol Method)?> FindSelectedInterfaceMethodAsync(
        Document document, TextSpan span, CancellationToken token)
    {
        if (document.GetLinkedDocumentIds().Length != 0) return null;
        var syntaxRoot = await document.GetSyntaxRootAsync(token);
        if (syntaxRoot is null || span.End > syntaxRoot.FullSpan.End) return null;
        var declarations = syntaxRoot.DescendantNodes(span).OfType<MethodDeclarationSyntax>()
            .Where(method => span.Contains(method.Span)).Take(2).ToArray();
        if (declarations is not [var declaration] || declaration.Body is not null || declaration.ExpressionBody is not null ||
            declaration.ContainsDirectives || declaration.Parent is not InterfaceDeclarationSyntax) return null;
        var root = await ClassificationEdit.FindEditableRoot(document, span.Start, token);
        var model = await document.GetSemanticModelAsync(token);
        if (root is null || model is null || model.GetDeclaredSymbol(declaration, token) is not IMethodSymbol method) return null;
        return (declaration, model, method);
    }

    private static bool IsEligibleInterfaceMethod(IMethodSymbol method, SemanticModel model,
        MethodDeclarationSyntax declaration, CancellationToken token)
    {
        if (method.ContainingType.TypeKind != TypeKind.Interface || method.IsStatic || method.IsGenericMethod ||
            method.DeclaredAccessibility != Accessibility.Public || method.ReturnsByRef || method.ReturnsByRefReadonly ||
            method.GetAttributes().Length != 0 || method.GetReturnTypeAttributes().Length != 0 ||
            IsOpen(method.ContainingType) || !IsPublicType(method.ContainingType) ||
            model.GetDiagnostics(declaration.Span, token).Any(item => item.Severity == DiagnosticSeverity.Error))
            return false;
        return AreParametersSupported(method, model, declaration, token);
    }

    private static bool AreParametersSupported(IMethodSymbol method, SemanticModel model,
        MethodDeclarationSyntax declaration, CancellationToken token)
    {
        var cancellation = model.Compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
        foreach (var parameter in method.Parameters)
        {
            token.ThrowIfCancellationRequested();
            if (parameter.RefKind != RefKind.None || parameter.IsParams || parameter.GetAttributes().Length != 0) return false;
            var syntax = declaration.ParameterList.Parameters[parameter.Ordinal];
            if (syntax.Modifiers.Count != 0) return false;
            if (syntax.Default is not null && (!Same(parameter.Type as INamedTypeSymbol, cancellation) ||
                syntax.Default.Value is not (DefaultExpressionSyntax or LiteralExpressionSyntax { RawKind: (int)SyntaxKind.DefaultLiteralExpression })))
                return false;
        }
        return true;
    }

    private static bool HasOnlySupportedTypeAttributes(INamedTypeSymbol interfaceType)
    {
        foreach (var type in interfaceType.AllInterfaces.Append(interfaceType))
        {
            foreach (var attribute in type.GetAttributes())
            {
                if (attribute.AttributeClass is not { } attributeType) return false;
                var name = MetadataName(attributeType);
                if (name is not ("DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomainAttribute" or
                    "System.Runtime.CompilerServices.NullableAttribute" or "System.Runtime.CompilerServices.NullableContextAttribute"))
                    return false;
            }
        }
        return true;
    }

    private static bool HasRequiredAsyncCancellation(IMethodSymbol method, Compilation compilation)
    {
        var cancellation = compilation.GetTypeByMetadataName("System.Threading.CancellationToken");
        var task = method.ReturnType as INamedTypeSymbol;
        var asyncReturn = task is not null && AsyncReturnTypeNames.Any(name => Same(task, compilation.GetTypeByMetadataName(name)));
        return !asyncReturn || method.Parameters.Any(parameter => Same(parameter.Type as INamedTypeSymbol, cancellation));
    }

    private static async Task<DomainServicePreflight?> ResolveDomainPreflightAsync(Project domain,
        ConventionGenerationPreflight.ProjectMetadata domainInfo, Selection selected, CancellationToken token)
    {
        if (domainInfo.Role != "Domain" || !domainInfo.Layout.Valid || !ValidNamespace(domainInfo.Namespace)) return null;
        var domainInputs = await ReadInputsWithoutRoleOverrideAsync(domain, required: false, token);
        if (domainInputs is null) return null;
        var source = await domain.GetCompilationAsync(token);
        if (source is null || !SymbolEqualityComparer.Default.Equals(selected.Method.ContainingType.ContainingAssembly, source.Assembly) ||
            source.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.SourceKnownService`1") is not { } sourceService)
            return null;
        var sourceNamespace = selected.Method.ContainingType.ContainingNamespace.ToDisplayString();
        if (!ValidNamespace(sourceNamespace) || !sourceNamespace.StartsWith(domainInfo.Namespace + ".", StringComparison.Ordinal))
            return null;
        var repoOwner = ResolveNamespaceOwnerKey(selected.Method.ContainingType, domainInfo.Namespace, domainInfo.Layout);
        var entityOwner = ResolveNamespaceOwnerKey(selected.Entity, domainInfo.Namespace, domainInfo.Layout);
        if (repoOwner is null || entityOwner is null || !entityOwner.SameOwner(repoOwner)) return null;
        var entityName = MetadataName(selected.Entity);
        var repositoryName = MetadataName(selected.Method.ContainingType);
        var repoMetadata = ConventionGenerationPreflight.MetadataFromAssembly(selected.Method.ContainingType.ContainingAssembly);
        var entityMetadata = SymbolEqualityComparer.Default.Equals(selected.Entity.ContainingAssembly, selected.Method.ContainingType.ContainingAssembly)
            ? repoMetadata
            : ConventionGenerationPreflight.MetadataFromAssembly(selected.Entity.ContainingAssembly);
        if (repoMetadata.HasRoleOverrides || entityMetadata.HasRoleOverrides ||
            !AttributesPreserveOwner(selected.Method.ContainingType, repoOwner, source, out var repoAttrExplicit) ||
            !AttributesPreserveOwner(selected.Entity, entityOwner, source, out var entityAttrExplicit) ||
            !OwnershipCompatible(domainInputs, entityName, repoOwner.Subdomain, token, out var entityInputExplicit, repoOwner.DomainFilter) ||
            !OwnershipCompatible(domainInputs, repositoryName, repoOwner.Subdomain, token, out var repoInputExplicit, repoOwner.DomainFilter) ||
            !entityMetadata.TryValidateProducerOwnership(entityName, entityOwner, out var entityMetadataExplicit) ||
            !repoMetadata.TryValidateProducerOwnership(repositoryName, repoOwner, out var repoMetadataExplicit))
            return null;
        var effectiveOwner = repoOwner.WithExplicitDomain(
            repoAttrExplicit || entityAttrExplicit || entityInputExplicit || repoInputExplicit ||
            entityMetadataExplicit || repoMetadataExplicit);
        var sourceSuffix = sourceNamespace[(domainInfo.Namespace.Length + 1)..];
        var ownerPrefix = repoOwner.NamespacePrefix;
        var namespaceTail = sourceSuffix.Length == ownerPrefix.Length ? "" : sourceSuffix[ownerPrefix.Length..];
        var operation = selected.Method.Name.EndsWith("Async", StringComparison.Ordinal) ? selected.Method.Name[..^5] : selected.Method.Name;
        var serviceName = ConventionGenerationPreflight.EntityStem(selected.Entity.Name) + operation + "Service";
        if (!SyntaxFacts.IsValidIdentifier(serviceName) || serviceName == selected.Method.Name || selected.Method.Name == "_repository")
            return null;
        return new(domain, sourceService, effectiveOwner, entityName, repositoryName, namespaceTail, serviceName);
    }

    private static async Task<TargetPlanBase?> ResolveTargetPlanBaseAsync(DomainServicePreflight domain, Selection selected,
        Project application, CancellationToken token)
    {
        if (application.Language != LanguageNames.CSharp) return null;
        var applicationInfo = ConventionGenerationPreflight.ReadProject(application);
        if (applicationInfo.Role != "Application" || !applicationInfo.Layout.Valid || !ValidNamespace(applicationInfo.Namespace) ||
            !application.ProjectReferences.Any(reference => reference.ProjectId == domain.Project.Id &&
                (reference.Aliases.IsEmpty || reference.Aliases.Contains("global", StringComparer.Ordinal))) ||
            application.FilePath is null || !TryFullPath(application.FilePath, out var projectPath)) return null;
        var applicationInputs = await ReadInputsWithoutRoleOverrideAsync(application, required: false, token);
        if (applicationInputs is null) return null;
        if (!OwnershipCompatible(applicationInputs, domain.EntityName, domain.Owner.Subdomain, token, out var appEntityExplicit, domain.Owner.DomainFilter) ||
            !OwnershipCompatible(applicationInputs, domain.RepositoryName, domain.Owner.Subdomain, token, out var appRepoExplicit, domain.Owner.DomainFilter))
            return null;
        var effectiveOwner = domain.Owner.WithExplicitDomain(appEntityExplicit || appRepoExplicit);
        if (!applicationInfo.Layout.TryResolveTargetOwner(effectiveOwner, out var targetOwner)) return null;
        var area = targetOwner.NamespacePrefix + domain.NamespaceTail;
        var ns = applicationInfo.Namespace + ".Services." + area;
        if (!ValidNamespace(ns)) return null;
        var compilation = await application.GetCompilationAsync(token);
        var repository = compilation?.GetTypeByMetadataName(domain.RepositoryName);
        var entity = compilation?.GetTypeByMetadataName(domain.EntityName);
        var service = compilation?.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.SourceKnownService`1");
        if (compilation is null || repository is null || entity is null || service is null ||
            !repository.ContainingAssembly.Identity.Equals(selected.Method.ContainingType.ContainingAssembly.Identity) ||
            !entity.ContainingAssembly.Identity.Equals(selected.Entity.ContainingAssembly.Identity) ||
            !domain.SourceService.ContainingAssembly.Identity.Equals(service.ContainingAssembly.Identity) ||
            !Bases(entity).Any(type => Same(type, compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.AggregateRoot"))) ||
            !compilation.IsSymbolAccessibleWithin(repository, compilation.Assembly) ||
            !compilation.IsSymbolAccessibleWithin(entity, compilation.Assembly) ||
            ConventionGenerationPreflight.SymbolCollision(compilation, ns, domain.ServiceName, token)) return null;
        var constructed = service.Construct(entity);
        if (!CanConstructForwardingServiceBase(constructed, repository, compilation, selected, token))
            return null;
        var root = Path.GetDirectoryName(projectPath)!;
        var relative = "Services/" + area.Replace('.', '/') + "/" + domain.ServiceName + ".cs";
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!SafeDocumentDestination(application, root, path, token))
            return null;
        var supportsScoped = SupportsScopedRegistration(compilation);
        return new(repository, constructed, ns, domain.ServiceName, relative, path, supportsScoped);
    }

    private static bool CanConstructForwardingServiceBase(INamedTypeSymbol constructed, INamedTypeSymbol repository,
        Compilation compilation, Selection selected, CancellationToken token)
    {
        if (Bases(constructed).Any(type => type.GetMembers(selected.Method.Name).Length != 0) ||
            !constructed.InstanceConstructors.Any(constructor => constructor.Parameters is [{ Type: { } parameter }] &&
                compilation.ClassifyCommonConversion(repository, parameter).IsImplicit &&
                constructor.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
            return false;
        return Available(selected.Method.ReturnType, compilation, token) &&
               selected.Method.Parameters.All(parameter => Available(parameter.Type, compilation, token));
    }

    private static bool SupportsScopedRegistration(Compilation compilation)
    {
        var attribute = compilation.GetTypeByMetadataName("DRN.Framework.Utils.DependencyInjection.Attributes.ScopedAttribute`1");
        return attribute is not null && compilation.IsSymbolAccessibleWithin(attribute, compilation.Assembly) &&
               attribute.InstanceConstructors.Any(constructor => constructor.DeclaredAccessibility == Accessibility.Public &&
                   constructor.Parameters.All(parameter => parameter.IsOptional));
    }

    private static ServicePlan RenderForwardingService(Selection selected, TargetPlanBase target, bool scoped)
    {
        var format = SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);
        var parameters = string.Join(", ", selected.Method.Parameters.Select(parameter => parameter.Type.ToDisplayString(format) + " " + EscapeIdentifier(parameter.Name) +
            (selected.Syntax.ParameterList.Parameters[parameter.Ordinal].Default is null ? "" : " = default")));
        var arguments = string.Join(", ", selected.Method.Parameters.Select(parameter => EscapeIdentifier(parameter.Name)));
        var registration = scoped ? "[global::DRN.Framework.Utils.DependencyInjection.Attributes.Scoped<" + target.Name + ">]\n" : "";
        var repositoryType = target.Repository.ToDisplayString(format);
        var code = $$"""
            #nullable enable
            namespace {{target.Namespace}};

            {{registration}}public sealed class {{target.Name}} : {{target.Constructed.ToDisplayString(format)}}
            {
                private readonly {{repositoryType}} _repository;

                public {{target.Name}}({{repositoryType}} repository) : base(repository)
                {
                    _repository = repository;
                }

                public {{(selected.Method.ReturnsVoid ? "void" : selected.Method.ReturnType.ToDisplayString(format))}} {{EscapeIdentifier(selected.Method.Name)}}({{parameters}})
                    => this._repository.{{EscapeIdentifier(selected.Method.Name)}}({{arguments}});
            }

            """;
        return new(target.Name, target.RelativePath, target.FullPath, code);
    }

    private static bool Available(ITypeSymbol type, Compilation compilation, CancellationToken token)
    {
        var pending = new Stack<ITypeSymbol>();
        var seen = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        pending.Push(type);
        while (pending.TryPop(out var current))
        {
            token.ThrowIfCancellationRequested();
            if (!seen.Add(current)) continue;
            if (seen.Count > 128) return false;
            if (current is IArrayTypeSymbol array) { pending.Push(array.ElementType); continue; }
            if (current is not INamedTypeSymbol named || named.TypeKind == TypeKind.Error ||
                !IsTypeAvailableInCompilation(named, compilation)) return false;
            if (named.ContainingType is { } containing) pending.Push(containing);
            foreach (var argument in named.TypeArguments) pending.Push(argument);
        }
        return true;
    }

    private static bool IsTypeAvailableInCompilation(INamedTypeSymbol named, Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName(MetadataName(named)) is not { } available ||
            !available.ContainingAssembly.Identity.Equals(named.ContainingAssembly.Identity) ||
            !compilation.IsSymbolAccessibleWithin(available, compilation.Assembly)) return false;
        return SymbolEqualityComparer.Default.Equals(available.ContainingAssembly, compilation.Assembly) ||
               compilation.References.Any(reference =>
                   (reference.Properties.Aliases.IsEmpty || reference.Properties.Aliases.Contains("global", StringComparer.Ordinal)) &&
                   compilation.GetAssemblyOrModuleSymbol(reference) is IAssemblySymbol assembly &&
                   assembly.Identity.Equals(available.ContainingAssembly.Identity));
    }

    private sealed record DomainServicePreflight(Project Project, INamedTypeSymbol SourceService, OwnerKey Owner,
        string EntityName, string RepositoryName, string NamespaceTail, string ServiceName);
    private sealed record TargetPlanBase(INamedTypeSymbol Repository, INamedTypeSymbol Constructed,
        string Namespace, string Name, string RelativePath, string FullPath, bool SupportsScoped);
    private sealed record Selection(IMethodSymbol Method, INamedTypeSymbol Entity, MethodDeclarationSyntax Syntax);
    private sealed record ServicePlan(string Name, string RelativePath, string FullPath, string Source);
}
