using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Framework.SharedKernel.CodeFixes;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(EntityConfigurationRefactoringProvider)), Shared]
public sealed class EntityConfigurationRefactoringProvider : CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var entity = await SelectedEntity(context.Document, context.Span.Start, context.CancellationToken);
        if (entity is null) return;
        var solution = context.Document.Project.Solution;
        if (!solution.Workspace.CanApplyChange(ApplyChangesKind.AddDocument)) return;
        var entityName = MetadataName(entity);
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
                    "DrnEntityConfiguration:" + project.Id + ":" + candidate.ContextName + ":" + entityName));
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

    internal static async Task<INamedTypeSymbol?> SelectedEntity(Document document, int position, CancellationToken token)
    {
        var root = await ClassificationEdit.FindEditableRoot(document, position, token);
        var model = await document.GetSemanticModelAsync(token);
        if (root is null || model is null) return null;
        foreach (var node in root.FindToken(position).Parent?.AncestorsAndSelf() ?? [])
        {
            var symbol = node is TypeDeclarationSyntax declaration ? model.GetDeclaredSymbol(declaration, token) :
                node is TypeSyntax syntax ? model.GetTypeInfo(syntax, token).Type as INamedTypeSymbol : null;
            if (symbol is INamedTypeSymbol type && IsEntity(type, model.Compilation) && !IsOpen(type)) return type;
        }
        return null;
    }

    internal static async Task<List<ConfigurationCandidate>> ConfigurationCandidates(Project project, string entityName, AssemblyIdentity entityAssembly, CancellationToken token)
    {
        var result = new List<ConfigurationCandidate>();
        var role = ConventionGenerationPreflight.Identity(project).Role;
        if (role != "Infra" || project.FilePath is null || !Path.IsPathFullyQualified(project.FilePath) ||
            await HasRoleOverrides(project, token)) return result;
        var compilation = await project.GetCompilationAsync(token);
        if (compilation?.GetTypeByMetadataName(entityName) is not { } entity || !entity.ContainingAssembly.Identity.Equals(entityAssembly) || IsOpen(entity) ||
            !IsEntity(entity, compilation) || !compilation.IsSymbolAccessibleWithin(entity, compilation.Assembly)) return result;
        // The generated public Configure method exposes the entity in its parameter type.
        for (var containing = entity; containing is not null; containing = containing.ContainingType)
            if (containing.DeclaredAccessibility != Accessibility.Public) return result;
        var configuration = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.IEntityTypeConfiguration`1");
        var contextContract = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Context.IDrnContext");
        var configurationAttribute = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.EntityTypeConfigurationAttribute");
        if (configuration is null || contextContract is null ||
            entity.GetAttributes().Any(attribute => Same(attribute.AttributeClass, configurationAttribute))) return result;
        var types = Types(compilation.Assembly.GlobalNamespace, token).ToArray();
        if (types.Any(type => type.AllInterfaces.Any(contract => Same(contract, configuration) &&
            SymbolEqualityComparer.Default.Equals(contract.TypeArguments[0], entity)))) return result;
        var contexts = types.Where(type => !type.IsAbstract && !IsOpen(type) &&
            type.AllInterfaces.Any(contract => Same(contract, contextContract))).ToArray();
        if (await HasUnknownDependentDiscovery(project, token)) return result;
        if (!TryFullPath(project.FilePath, out var projectPath)) return result;
        var projectDirectory = Path.GetDirectoryName(projectPath)!;
        var discovery = contexts.Select(context => (Context: context, Scopes: ConfigurationDiscovery.Read(context, compilation, token))).ToArray();
        foreach (var context in contexts)
        {
            token.ThrowIfCancellationRequested();
            if (!HasMembership(context, entity, compilation) ||
                context.DeclaringSyntaxReferences.Length == 0) continue;
            var contextNamespace = context.ContainingNamespace.ToDisplayString();
            var configurationNamespace = contextNamespace + ".Configurations";
            // Unknown discovery could include this new class. Shared scans require an explicit all-context preview.
            if (discovery.Any(item => item.Scopes is null)) continue;
            var affected = discovery.Where(item => item.Scopes!.Any(scope => scope.Contains(configurationNamespace)))
                .Select(item => item.Context).ToArray();
            if (!affected.Any(item => SymbolEqualityComparer.Default.Equals(item, context)) ||
                affected.Any(item => !HasMembership(item, entity, compilation))) continue;
            Document? contextDocument = null;
            var editable = true;
            foreach (var reference in context.DeclaringSyntaxReferences)
            {
                var part = project.GetDocument(reference.SyntaxTree);
                if (part is null || part.FilePath is null || !TryFullPath(part.FilePath, out _) || part.GetLinkedDocumentIds().Length != 0 ||
                    await ClassificationEdit.FindEditableRoot(part, reference.Span.Start, token) is null)
                {
                    editable = false;
                    break;
                }
                if (contextDocument is not null && Path.GetDirectoryName(contextDocument.FilePath) != Path.GetDirectoryName(part.FilePath))
                {
                    editable = false;
                    break;
                }
                contextDocument = part;
            }
            if (!editable || contextDocument?.FilePath is null) continue;
            var className = entity.Name + "Configuration";
            if (ConventionGenerationPreflight.SymbolCollision(compilation, configurationNamespace, className, token)) continue;
            if (!TryFullPath(Path.Combine(Path.GetDirectoryName(contextDocument.FilePath)!, "Configurations", className + ".cs"), out var destination)) continue;
            if (!SafeDestination(projectDirectory, destination) || WorkspaceDestinationConflict(project.Solution, destination, token)) continue;
            var relative = Path.GetRelativePath(projectDirectory, destination).Replace('\\', '/');
            var candidate = new ConfigurationCandidate(string.Join(", ", affected.Select(MetadataName).OrderBy(name => name, StringComparer.Ordinal)),
                configurationNamespace, className, entity.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), relative, destination,
                affected.Length == 1 ? MetadataName(affected[0]) : null);
            if (!result.Contains(candidate)) result.Add(candidate);
        }
        return result;
    }

    private static async Task<bool> HasUnknownDependentDiscovery(Project project, CancellationToken token)
    {
        foreach (var id in project.Solution.GetProjectDependencyGraph().GetProjectsThatTransitivelyDependOnThisProject(project.Id))
        {
            token.ThrowIfCancellationRequested();
            var dependent = project.Solution.GetProject(id);
            if (dependent is null || dependent.Language != LanguageNames.CSharp) return true;
            var compilation = await dependent.GetCompilationAsync(token);
            if (compilation is null) return true;
            var contract = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Context.IDrnContext");
            foreach (var context in Types(compilation.Assembly.GlobalNamespace, token))
            {
                if (context.IsAbstract || IsOpen(context) || !context.AllInterfaces.Any(type => Same(type, contract))) continue;
                // Recognized scans are limited to the dependent's own assembly. Unknown scans may include this project.
                if (ConfigurationDiscovery.Read(context, compilation, token) is null) return true;
            }
        }
        return false;
    }

    internal static bool SafeDestination(string root, string destination)
    {
        try
        {
            if (!destination.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) || File.Exists(destination) ||
                Directory.Exists(destination)) return false;
            if (new FileInfo(destination).LinkTarget is not null) return false;
            var destinationDirectory = Path.GetDirectoryName(destination)!;
            if (Directory.Exists(destinationDirectory) && Directory.EnumerateFileSystemEntries(destinationDirectory)
                .Any(entry => string.Equals(Path.GetFileName(entry), Path.GetFileName(destination), StringComparison.OrdinalIgnoreCase))) return false;
            for (var directory = new DirectoryInfo(Path.GetDirectoryName(destination)!); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(directory.FullName) || directory.LinkTarget is not null) return false;
                if (directory.Parent is { Exists: true } parent && Directory.EnumerateFileSystemEntries(parent.FullName)
                    .Any(entry => string.Equals(Path.GetFileName(entry), directory.Name, StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(Path.GetFileName(entry), directory.Name, StringComparison.Ordinal))) return false;
                if (directory.FullName == root) return true;
            }
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
        return false;
    }

    internal static bool TryFullPath(string path, out string fullPath)
    {
        fullPath = "";
        try
        {
            if (!Path.IsPathFullyQualified(path)) return false;
            fullPath = Path.GetFullPath(path);
            return true;
        }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
        catch (IOException) { return false; }
    }

    internal static bool WorkspaceDestinationConflict(Solution solution, string destination, CancellationToken token = default)
    {
        foreach (var project in solution.Projects)
        foreach (var document in project.Documents.Cast<TextDocument>().Concat(project.AdditionalDocuments).Concat(project.AnalyzerConfigDocuments))
        {
            token.ThrowIfCancellationRequested();
            if (document.FilePath is { } path ? WorkspacePathConflict(path, destination) :
                string.Equals(document.Name, Path.GetFileName(destination), StringComparison.OrdinalIgnoreCase)) return true;
        }
        return false;
    }

    internal static bool WorkspacePathConflict(string path, string destination)
    {
        if (!TryFullPath(path, out var existing)) return true;
        if (string.Equals(existing, destination, StringComparison.OrdinalIgnoreCase)) return true;
        if (destination.StartsWith(existing + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            existing.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return true;
        var existingParts = existing.Split(Path.DirectorySeparatorChar);
        var destinationParts = destination.Split(Path.DirectorySeparatorChar);
        for (var index = 0; index < Math.Min(existingParts.Length, destinationParts.Length) - 1; index++)
        {
            if (!string.Equals(existingParts[index], destinationParts[index], StringComparison.OrdinalIgnoreCase)) return false;
            if (existingParts[index] != destinationParts[index]) return true;
        }
        return false;
    }

    internal static bool DefaultDiscovery(INamedTypeSymbol context, Compilation compilation, CancellationToken token = default)
    {
        var drn = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Context.DrnContext`1");
        var identity = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Context.Identity.DrnContextIdentity`2");
        for (var current = context; current is not null; current = current.BaseType)
        {
            token.ThrowIfCancellationRequested();
            if (Same(current, drn) || Same(current, identity)) return true;
            foreach (var method in current.GetMembers("OnModelCreating").OfType<IMethodSymbol>())
            {
                if (method.OverriddenMethod is null || method.Parameters.Length != 1 || method.DeclaringSyntaxReferences.Length != 1 ||
                    method.DeclaringSyntaxReferences[0].GetSyntax(token) is not MethodDeclarationSyntax declaration ||
                    !compilation.ContainsSyntaxTree(declaration.SyntaxTree)) return false;
                var expression = declaration.ExpressionBody?.Expression ??
                    (declaration.Body?.Statements is { Count: 1 } statements && statements[0] is ExpressionStatementSyntax statement
                        ? statement.Expression : null);
                if (expression is not InvocationExpressionSyntax
                    {
                        Expression: MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax },
                        ArgumentList.Arguments.Count: 1
                    } invocation) return false;
                var model = compilation.GetSemanticModel(declaration.SyntaxTree);
                if (!SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(invocation, token).Symbol, method.OverriddenMethod) ||
                    !SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(invocation.ArgumentList.Arguments[0].Expression, token).Symbol,
                        method.Parameters[0])) return false;
            }
        }
        return false;
    }

    internal static async Task<bool> HasRoleOverrides(Project project, CancellationToken token)
    {
        var root = await ConventionGenerationPreflight.ReadInputs(project, required: false, token);
        return root is null || root.AttributeLists.SelectMany(list => list.Attributes).Any(attribute =>
            attribute.Name.ToString() is "DRN.Conventions.Role" or "global::DRN.Conventions.Role");
    }

    internal static bool HasMembership(INamedTypeSymbol context, INamedTypeSymbol entity, Compilation compilation)
    {
        var dbSet = compilation.GetTypeByMetadataName("Microsoft.EntityFrameworkCore.DbSet`1");
        var membership = compilation.GetTypeByMetadataName("DRN.Framework.EntityFramework.Attributes.SourceKnownPersistenceAttribute");
        var explicitlyIncluded = false;
        foreach (var attribute in context.GetAttributes())
        {
            if (!Same(attribute.AttributeClass, membership) || attribute.ConstructorArguments is not [{ Value: INamedTypeSymbol mapped }] ||
                !SymbolEqualityComparer.Default.Equals(mapped, entity)) continue;
            if (attribute.NamedArguments.FirstOrDefault(pair => pair.Key == "Inclusion").Value.Value is int inclusion && inclusion != 0) return false;
            explicitlyIncluded = true;
        }
        if (explicitlyIncluded) return true;
        for (var current = context; current is not null; current = current.BaseType)
        foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            if (!property.IsStatic && !property.IsIndexer && property.Type is INamedTypeSymbol set && Same(set, dbSet) &&
                SymbolEqualityComparer.Default.Equals(set.TypeArguments[0], entity)) return true;
        return false;
    }

    private static bool IsEntity(INamedTypeSymbol type, Compilation compilation)
    {
        var entity = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.SourceKnownEntity");
        var identity = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Identity.IdentityUser`1");
        for (var current = type; current is not null; current = current.BaseType)
            if (Same(current, entity) || Same(current, identity)) return true;
        return false;
    }

    internal static bool IsOpen(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.ContainingType)
            if (current.TypeParameters.Length != 0) return true;
        return false;
    }

    internal static bool Same(INamedTypeSymbol? left, INamedTypeSymbol? right) => left is not null && right is not null &&
        SymbolEqualityComparer.Default.Equals(left.OriginalDefinition, right.OriginalDefinition);

    internal static IEnumerable<INamedTypeSymbol> Types(INamespaceOrTypeSymbol container, CancellationToken token)
    {
        var pending = new Stack<INamespaceOrTypeSymbol>();
        pending.Push(container);
        while (pending.TryPop(out var current))
        {
            token.ThrowIfCancellationRequested();
            foreach (var member in current.GetMembers())
            {
                token.ThrowIfCancellationRequested();
                if (member is INamedTypeSymbol type) yield return type;
                if (member is INamespaceOrTypeSymbol nested) pending.Push(nested);
            }
        }
    }

    internal static string MetadataName(INamedTypeSymbol type) => type.ContainingType is { } parent
        ? MetadataName(parent) + "+" + type.MetadataName
        : (type.ContainingNamespace.IsGlobalNamespace ? "" : type.ContainingNamespace.ToDisplayString() + ".") + type.MetadataName;

    internal sealed record ConfigurationCandidate(string ContextName, string Namespace, string ClassName, string EntityType,
        string RelativePath, string FullPath, string? SingleContextName);
}
