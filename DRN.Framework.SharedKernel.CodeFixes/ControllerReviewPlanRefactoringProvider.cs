using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis.CodeRefactorings;
using static DRN.Framework.SharedKernel.CodeFixes.CodeFixDestinationGuard;
using static DRN.Framework.SharedKernel.CodeFixes.CodeFixSymbolFacts;

namespace DRN.Framework.SharedKernel.CodeFixes;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(ControllerReviewPlanRefactoringProvider)), Shared]
public sealed class ControllerReviewPlanRefactoringProvider : CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var snapshot = context.Document.Project.Solution;
        if (!snapshot.Workspace.CanApplyChange(ApplyChangesKind.AddAdditionalDocument)) return;
        var plan = await CreatePlan(context.Document, context.Span, context.CancellationToken);
        if (plan is null || !ReferenceEquals(snapshot.Workspace.CurrentSolution, snapshot)) return;
        context.RegisterRefactoring(CodeAction.Create("Create controller review plan only: " + plan.Name,
            async token =>
            {
                token.ThrowIfCancellationRequested();
                if (!ReferenceEquals(snapshot.Workspace.CurrentSolution, snapshot)) return snapshot.Workspace.CurrentSolution;
                var current = await CreatePlan(context.Document, context.Span, token);
                token.ThrowIfCancellationRequested();
                if (current != plan || !ReferenceEquals(snapshot.Workspace.CurrentSolution, snapshot)) return snapshot.Workspace.CurrentSolution;
                return snapshot.AddAdditionalDocument(DocumentId.CreateNewId(context.Document.Project.Id), plan.Name,
                    SourceText.From(plan.Content), folders: plan.RelativePath.Split('/')[..^1], filePath: plan.Path);
            }, nameof(ControllerReviewPlanRefactoringProvider)));
    }

    private static async Task<Plan?> CreatePlan(Document document, TextSpan span, CancellationToken token)
    {
        var target = await ResolveControllerTargetAsync(document, span, token);
        if (target is null) return null;
        var text = new StringBuilder("# Controller review plan\n\nPlan only. Adds this Markdown AdditionalFiles review plan without modifying controller code, project metadata, references, identity or runtime behavior.\n\n");
        text.AppendLine("Selected symbol: " + Cell(target.Type.ToDisplayString()));
        text.AppendLine("Project: " + Cell(target.ProjectPath));
        text.AppendLine("Plan destination: " + Cell(target.Path));
        text.AppendLine("Source preimages (SHA-256 of UTF-8 workspace text):");
        if (!await TryAppendSourcePreimagesAsync(document.Project, target.Type, target.Directory, text, token)) return null;
        await AppendInheritanceMetadataAsync(document.Project.Solution, target.Type, text, token);
        AppendDirectProjectReferences(document.Project, text, token);
        if (!TryAppendPublicMethods(target.Model.Compilation, target.Type, text, token)) return null;
        text.AppendLine("\n## Required decisions before implementation\n\n- Select enabled operations and disable unwanted overridable actions with NonAction.\n- Confirm exact route and verb composition, including inherited metadata and application MVC conventions.\n- Supply named policies, authentication transport and per-resource authorization for each enabled action. AllowAnonymous is not a fallback.\n- Supply actual request/response fields, classification, validation and entity-to-DTO mapping.\n- Confirm creation invariants and deletion semantics. Inherited DeleteAsync performs direct deletion without entity lifecycle hooks.\n- Confirm repository visibility filters and cancellation propagation.\n- Select exact source destinations and any required project metadata changes. No executable output destinations have been selected by this plan.\n- Review all source/project/reference preimages again. This plan is evidence of the current loaded workspace, not an executable transaction.\n- Use a host capable of previewing and atomically applying every required source and project change. Unsupported project edits remain manual reviewed template work.\n\nNo C# files, IDs, policies, business logic, runnable endpoints, project references or project items were generated. Compilation, model compatibility and runtime authorization remain unverified.\n");
        return text.Length >= 12000 ? null : new(target.Name, target.Path, target.RelativePath, text.ToString());
    }

    private static async Task<ControllerTarget?> ResolveControllerTargetAsync(Document document, TextSpan span, CancellationToken token)
    {
        if (document.FilePath is null || document.Project.FilePath is null || document.GetLinkedDocumentIds().Length != 0 ||
            !TryFullPath(document.FilePath, out var sourcePath) || !TryFullPath(document.Project.FilePath, out var projectPath)) return null;
        var directory = Path.GetDirectoryName(projectPath)!;
        if (!ConventionGenerationPreflight.IsUnderDirectory(sourcePath, directory)) return null;
        var syntaxRoot = await document.GetSyntaxRootAsync(token);
        if (syntaxRoot is null || span.End > syntaxRoot.FullSpan.End) return null;
        var declaration = syntaxRoot.FindToken(span.Start).Parent?.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (declaration is null || !declaration.Identifier.Span.IntersectsWith(span)) return null;
        var root = await ClassificationEdit.FindEditableRoot(document, span.Start, token);
        var model = await document.GetSemanticModelAsync(token);
        if (root is null || model is null ||
            model.GetDeclaredSymbol(declaration, token) is not INamedTypeSymbol type || IsOpen(type) ||
            model.GetDiagnostics(declaration.Span, token).Any(item => item.Severity == DiagnosticSeverity.Error)) return null;
        var known = model.Compilation.GetTypeByMetadataName("DRN.Framework.Hosting.Controllers.SourceKnownControllerBase`2");
        if (known is null || !NonObjectBases(type).Any(item => Same(item, known))) return null;
        var name = type.Name + ".controller-review.md";
        var path = Path.Combine(Path.GetDirectoryName(sourcePath)!, name);
        if (!SafeDocumentDestination(document.Project, directory, path, token))
            return null;
        return new(type, model, projectPath, directory, name, path, Path.GetRelativePath(directory, path).Replace('\\', '/'));
    }

    private static async Task<bool> TryAppendSourcePreimagesAsync(Project project, INamedTypeSymbol type,
        string directory, StringBuilder text, CancellationToken token)
    {
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            token.ThrowIfCancellationRequested();
            var part = project.GetDocument(reference.SyntaxTree);
            if (part?.FilePath is null || part.GetLinkedDocumentIds().Length != 0 ||
                await ClassificationEdit.FindEditableRoot(part, reference.Span.Start, token) is null ||
                !TryFullPath(part.FilePath, out var partPath) || !ConventionGenerationPreflight.IsUnderDirectory(partPath, directory))
                return false;
            var content = await part.GetTextAsync(token);
            text.AppendLine("- " + Cell(partPath) + ": " + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content.ToString()))));
        }
        return true;
    }

    private static async Task AppendInheritanceMetadataAsync(Solution solution, INamedTypeSymbol type,
        StringBuilder text, CancellationToken token)
    {
        text.AppendLine("\n## Inheritance and declared metadata\n");
        foreach (var ancestor in NonObjectBases(type))
        {
            token.ThrowIfCancellationRequested();
            text.AppendLine("- " + Cell(ancestor.ToDisplayString()) + ": " + Attributes(ancestor));
            text.AppendLine("  - Assembly: " + Cell(ancestor.ContainingAssembly.Identity.ToString()));
            foreach (var location in ancestor.Locations.Where(item => item.IsInSource))
            {
                var inheritedDocument = location.SourceTree is { } tree ? solution.GetDocument(tree) : null;
                var fingerprint = inheritedDocument is null ? " (source preimage unavailable)" : "; SHA-256 " +
                    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes((await inheritedDocument.GetTextAsync(token)).ToString())));
                text.AppendLine("  - Declaration: " + Cell(location.GetLineSpan().Path) + fingerprint);
            }
        }
    }

    private static void AppendDirectProjectReferences(Project project, StringBuilder text, CancellationToken token)
    {
        text.AppendLine("\nExisting direct project references (no reference changes requested):");
        foreach (var reference in project.ProjectReferences)
        {
            token.ThrowIfCancellationRequested();
            var target = project.Solution.GetProject(reference.ProjectId);
            text.AppendLine("- " + Cell(target?.FilePath ?? target?.Name ?? "Unavailable loaded project") +
                            "; aliases: " + Cell(string.Join(", ", reference.Aliases)));
        }
    }

    private static bool TryAppendPublicMethods(Compilation compilation, INamedTypeSymbol type, StringBuilder text, CancellationToken token)
    {
        text.AppendLine("\n## Public instance method inventory\n\nRows are candidate actions, not a runtime route or authorization proof. Attribute declarations on overrides are retained separately. NonAction disables routing. API visibility alone does not.\n");
        var overridden = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var mvcBase = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ControllerBase");
        var nonAction = compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.NonActionAttribute");
        var excludedHelpers = 0;
        var count = 0;
        foreach (var ancestor in NonObjectBases(type))
        {
            foreach (var method in ancestor.GetMembers().OfType<IMethodSymbol>())
            {
                token.ThrowIfCancellationRequested();
                if (!IsCandidateActionMethod(method, overridden)) continue;
                if (IsExcludedMvcHelper(method, mvcBase, nonAction)) { excludedHelpers++; continue; }
                if (++count > 128) return false;
                AppendMethodInventory(method, overridden, text, token);
            }
        }
        text.AppendLine("\nExcluded exact MVC ControllerBase helpers declared NonAction: " + excludedHelpers +
                        ". User overrides and SourceKnown methods remain listed. Object methods are excluded. Custom MVC conventions require separate review.");
        return true;
    }

    private static bool IsCandidateActionMethod(IMethodSymbol method, HashSet<ISymbol> overridden) =>
        method.MethodKind == MethodKind.Ordinary && !method.IsStatic &&
        method.DeclaredAccessibility == Accessibility.Public && !method.IsImplicitlyDeclared &&
        !overridden.Contains(method.OriginalDefinition);

    private static bool IsExcludedMvcHelper(IMethodSymbol method, INamedTypeSymbol? mvcBase, INamedTypeSymbol? nonAction) =>
        Same(method.ContainingType, mvcBase) && method.GetAttributes().Any(attribute => Same(attribute.AttributeClass, nonAction));

    private static void AppendMethodInventory(IMethodSymbol method, HashSet<ISymbol> overridden, StringBuilder text, CancellationToken token)
    {
        text.AppendLine("- " + Cell(method.ToDisplayString()) + (method.IsGenericMethod ? " (generic, not an MVC action)" : ""));
        text.AppendLine("  - Override eligibility: " + ((method.IsVirtual || method.IsOverride || method.IsAbstract) && !method.IsSealed
            ? "overridable" : "not overridable") + (method.IsSealed ? " (sealed override)" : "") +
            (method.IsAbstract ? "; abstract declaration" : ""));
        for (var current = method; current is not null; current = current.OverriddenMethod)
        {
            token.ThrowIfCancellationRequested();
            overridden.Add(current.OriginalDefinition);
            text.AppendLine("  - Declared on " + Cell(current.ContainingType.ToDisplayString()) + ": " + Attributes(current));
        }
    }

    private sealed record ControllerTarget(INamedTypeSymbol Type, SemanticModel Model, string ProjectPath,
        string Directory, string Name, string Path, string RelativePath);

    private static string Attributes(ISymbol symbol) => string.Join("; ", symbol.GetAttributes().Select(attribute => Cell(attribute.ToString())));
    private static string Cell(string? value) => EscapeMarkdown(value);
    private sealed record Plan(string Name, string Path, string RelativePath, string Content);
}
