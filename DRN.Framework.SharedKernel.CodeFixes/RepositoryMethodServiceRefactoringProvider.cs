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
        if (context.Span.IsEmpty || !snapshot.Workspace.CanApplyChange(ApplyChangesKind.AddDocument) ||
            ConventionGenerationPreflight.Identity(context.Document.Project).Role != "Domain") return;
        var selected = await Select(context.Document, context.Span, context.CancellationToken);
        if (selected is null) return;
        foreach (var target in snapshot.Projects)
        {
            foreach (var scoped in ScopedOptions)
            {
                var plan = await Plan(context.Document.Project, selected, target, scoped, context.CancellationToken);
                if (plan is null) continue;
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
        var plan = await Plan(document.Project, selected, target, scoped, token);
        if (plan is null || plan != expected) return snapshot.Workspace.CurrentSolution;
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
        if (await HasRoleOverrides(document.Project, token)) return null;
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

    private static async Task<ServicePlan?> Plan(Project domain, Selection selected, Project application, bool scoped, CancellationToken token)
    {
        var context = await ResolveServiceContextAsync(domain, application, selected, token);
        if (context is null) return null;
        if (!HasMatchingConventionalOwnership(selected, context.DomainNamespace, context.SourceCompilation,
                context.DomainInputs, context.ApplicationInputs, context.Owner, token)) return null;
        var ns = context.ApplicationNamespace + ".Services." + context.Area;
        if (!ValidNamespace(ns)) return null;
        var operation = selected.Method.Name.EndsWith("Async", StringComparison.Ordinal) ? selected.Method.Name[..^5] : selected.Method.Name;
        var name = ConventionGenerationPreflight.EntityStem(context.Entity.Name) + operation + "Service";
        if (!SyntaxFacts.IsValidIdentifier(name) || name == selected.Method.Name || selected.Method.Name == "_repository" ||
            ConventionGenerationPreflight.SymbolCollision(context.Compilation, ns, name, token)) return null;
        var constructed = context.Service.Construct(context.Entity);
        if (!CanConstructForwardingService(constructed, context.Repository, context.Compilation, selected, scoped, token))
            return null;
        var root = Path.GetDirectoryName(context.ProjectPath)!;
        var relative = "Services/" + context.Area.Replace('.', '/') + "/" + name + ".cs";
        var path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        if (!SafeDocumentDestination(application, root, path, token))
            return null;
        return RenderForwardingService(selected, context.Repository, constructed, ns, name, relative, path, scoped);
    }

    private static async Task<ServiceContext?> ResolveServiceContextAsync(Project domain, Project application,
        Selection selected, CancellationToken token)
    {
        var domainIdentity = ConventionGenerationPreflight.Identity(domain);
        var applicationIdentity = ConventionGenerationPreflight.Identity(application);
        if (domainIdentity.Role != "Domain" || applicationIdentity.Role != "Application" || application.Language != LanguageNames.CSharp ||
            !ValidNamespace(domainIdentity.Namespace) || !ValidNamespace(applicationIdentity.Namespace) ||
            !application.ProjectReferences.Any(reference => reference.ProjectId == domain.Id &&
                (reference.Aliases.IsEmpty || reference.Aliases.Contains("global", StringComparer.Ordinal))) ||
            application.FilePath is null || !TryFullPath(application.FilePath, out var projectPath)) return null;
        var domainInputs = await ReadInputsWithoutRoleOverrideAsync(domain, required: false, token);
        var applicationInputs = await ReadInputsWithoutRoleOverrideAsync(application, required: false, token);
        if (domainInputs is null || applicationInputs is null)
            return null;
        return await ResolveServiceSymbolsAsync(domain, application, selected, domainIdentity.Namespace,
            applicationIdentity.Namespace, projectPath, domainInputs, applicationInputs, token);
    }

    private static async Task<ServiceContext?> ResolveServiceSymbolsAsync(Project domain, Project application,
        Selection selected, string domainNamespace, string applicationNamespace, string projectPath,
        CompilationUnitSyntax domainInputs, CompilationUnitSyntax applicationInputs, CancellationToken token)
    {
        var compilation = await application.GetCompilationAsync(token);
        var repository = compilation?.GetTypeByMetadataName(MetadataName(selected.Method.ContainingType));
        var entity = compilation?.GetTypeByMetadataName(MetadataName(selected.Entity));
        var service = compilation?.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.SourceKnownService`1");
        if (compilation is null || repository is null || entity is null || service is null ||
            !repository.ContainingAssembly.Identity.Equals(selected.Method.ContainingType.ContainingAssembly.Identity) ||
            !entity.ContainingAssembly.Identity.Equals(selected.Entity.ContainingAssembly.Identity) ||
            !Bases(entity).Any(type => Same(type, compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.AggregateRoot"))) ||
            !compilation.IsSymbolAccessibleWithin(repository, compilation.Assembly) ||
            !compilation.IsSymbolAccessibleWithin(entity, compilation.Assembly)) return null;
        var source = await domain.GetCompilationAsync(token);
        if (source is null || !SymbolEqualityComparer.Default.Equals(selected.Method.ContainingType.ContainingAssembly, source.Assembly) ||
            source.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.SourceKnownService`1") is not { } sourceService ||
            !sourceService.ContainingAssembly.Identity.Equals(service.ContainingAssembly.Identity)) return null;
        var sourceNamespace = selected.Method.ContainingType.ContainingNamespace.ToDisplayString();
        if (!ValidNamespace(sourceNamespace) || !sourceNamespace.StartsWith(domainNamespace + ".", StringComparison.Ordinal))
            return null;
        var area = sourceNamespace[(domainNamespace.Length + 1)..];
        return new(compilation, source, repository, entity, service, domainInputs, applicationInputs,
            domainNamespace, applicationNamespace, projectPath, area, area.Split('.')[0]);
    }

    private static bool HasMatchingConventionalOwnership(Selection selected, string domainNamespace, Compilation source,
        CompilationUnitSyntax domainInputs, CompilationUnitSyntax applicationInputs, string owner, CancellationToken token)
    {
        var entityName = MetadataName(selected.Entity);
        var repositoryName = MetadataName(selected.Method.ContainingType);
        return ConventionalOwner(selected.Method.ContainingType, domainNamespace, source) &&
               ConventionalOwner(selected.Entity, domainNamespace, source) &&
               selected.Entity.ContainingNamespace.ToDisplayString().Split('.').Length > domainNamespace.Split('.').Length &&
               selected.Entity.ContainingNamespace.ToDisplayString()[(domainNamespace.Length + 1)..].Split('.')[0] == owner &&
               OwnershipCompatible(domainInputs, entityName, owner, token) &&
               OwnershipCompatible(domainInputs, repositoryName, owner, token) &&
               OwnershipCompatible(applicationInputs, entityName, owner, token) &&
               OwnershipCompatible(applicationInputs, repositoryName, owner, token);
    }

    private static bool CanConstructForwardingService(INamedTypeSymbol constructed, INamedTypeSymbol repository,
        Compilation compilation, Selection selected, bool scoped, CancellationToken token)
    {
        if (Bases(constructed).Any(type => type.GetMembers(selected.Method.Name).Length != 0) ||
            !constructed.InstanceConstructors.Any(constructor => constructor.Parameters is [{ Type: { } parameter }] &&
                compilation.ClassifyCommonConversion(repository, parameter).IsImplicit &&
                constructor.DeclaredAccessibility is Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
            return false;
        if (!Available(selected.Method.ReturnType, compilation, token) ||
            selected.Method.Parameters.Any(parameter => !Available(parameter.Type, compilation, token)))
            return false;
        if (!scoped) return true;
        var attribute = compilation.GetTypeByMetadataName("DRN.Framework.Utils.DependencyInjection.Attributes.ScopedAttribute`1");
        return attribute is not null && compilation.IsSymbolAccessibleWithin(attribute, compilation.Assembly) &&
               attribute.InstanceConstructors.Any(constructor => constructor.DeclaredAccessibility == Accessibility.Public &&
                   constructor.Parameters.All(parameter => parameter.IsOptional));
    }

    private static ServicePlan RenderForwardingService(Selection selected, INamedTypeSymbol repository,
        INamedTypeSymbol constructed, string ns, string name, string relative, string path, bool scoped)
    {
        var format = SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier | SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);
        var parameters = string.Join(", ", selected.Method.Parameters.Select(parameter => parameter.Type.ToDisplayString(format) + " " + EscapeIdentifier(parameter.Name) +
            (selected.Syntax.ParameterList.Parameters[parameter.Ordinal].Default is null ? "" : " = default")));
        var arguments = string.Join(", ", selected.Method.Parameters.Select(parameter => EscapeIdentifier(parameter.Name)));
        var registration = scoped ? "[global::DRN.Framework.Utils.DependencyInjection.Attributes.Scoped<" + name + ">]\n" : "";
        var repositoryType = repository.ToDisplayString(format);
        var code = $$"""
            #nullable enable
            namespace {{ns}};

            {{registration}}public sealed class {{name}} : {{constructed.ToDisplayString(format)}}
            {
                private readonly {{repositoryType}} _repository;

                public {{name}}({{repositoryType}} repository) : base(repository)
                {
                    _repository = repository;
                }

                public {{(selected.Method.ReturnsVoid ? "void" : selected.Method.ReturnType.ToDisplayString(format))}} {{EscapeIdentifier(selected.Method.Name)}}({{parameters}})
                    => this._repository.{{EscapeIdentifier(selected.Method.Name)}}({{arguments}});
            }

            """;
        return new(name, relative, path, code);
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

    private sealed record ServiceContext(Compilation Compilation, Compilation SourceCompilation, INamedTypeSymbol Repository,
        INamedTypeSymbol Entity, INamedTypeSymbol Service, CompilationUnitSyntax DomainInputs,
        CompilationUnitSyntax ApplicationInputs, string DomainNamespace, string ApplicationNamespace,
        string ProjectPath, string Area, string Owner);

    private static bool ConventionalOwner(INamedTypeSymbol type, string root, Compilation compilation)
    {
        var ns = type.ContainingNamespace.ToDisplayString();
        if (!ns.StartsWith(root + ".", StringComparison.Ordinal)) return false;
        var owner = ns[(root.Length + 1)..].Split('.')[0];
        var attributeType = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomainAttribute");
        return type.GetAttributes().Where(attribute => Same(attribute.AttributeClass, attributeType)).All(attribute =>
            attribute.ConstructorArguments is [{ Value: string declared }] && declared == owner);
    }

    private sealed record Selection(IMethodSymbol Method, INamedTypeSymbol Entity, MethodDeclarationSyntax Syntax);
    private sealed record ServicePlan(string Name, string RelativePath, string FullPath, string Source);
}
