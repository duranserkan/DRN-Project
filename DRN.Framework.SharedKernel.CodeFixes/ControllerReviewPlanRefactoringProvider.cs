using System.Composition;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static DRN.Framework.SharedKernel.CodeFixes.EntityConfigurationRefactoringProvider;

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
                    SourceText.From(plan.Content), filePath: plan.Path);
            }, nameof(ControllerReviewPlanRefactoringProvider)));
    }

    private static async Task<Plan?> CreatePlan(Document document, TextSpan span, CancellationToken token)
    {
        if (document.FilePath is null || document.Project.FilePath is null || document.GetLinkedDocumentIds().Length != 0 ||
            !TryFullPath(document.FilePath, out var sourcePath) || !TryFullPath(document.Project.FilePath, out var projectPath)) return null;
        var root = await ClassificationEdit.FindEditableRoot(document, span.Start, token);
        var model = await document.GetSemanticModelAsync(token);
        if (root is null || model is null || span.End > root.FullSpan.End) return null;
        var declaration = root.FindToken(span.Start).Parent?.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (declaration is null || !declaration.Identifier.Span.IntersectsWith(span) ||
            model.GetDeclaredSymbol(declaration, token) is not INamedTypeSymbol type || IsOpen(type) ||
            model.GetDiagnostics(declaration.Span, token).Any(item => item.Severity == DiagnosticSeverity.Error)) return null;
        var known = model.Compilation.GetTypeByMetadataName("DRN.Framework.Hosting.Controllers.SourceKnownControllerBase`2");
        if (known is null || !Chain(type).Any(item => Same(item, known))) return null;
        var directory = Path.GetDirectoryName(projectPath)!;
        if (!ConventionGenerationPreflight.IsUnderDirectory(sourcePath, directory)) return null;
        var name = type.Name + ".controller-review.md";
        var path = Path.Combine(Path.GetDirectoryName(sourcePath)!, name);
        if (!SafeDestination(directory, path) || WorkspaceDestinationConflict(document.Project.Solution, path, document.Project.Id, token)) return null;
        var text = new StringBuilder("# Controller review plan\n\nPlan only. Adds this Markdown AdditionalFiles review plan without modifying controller code, project metadata, references, identity or runtime behavior.\n\n");
        text.AppendLine("Selected symbol: " + Cell(type.ToDisplayString()));
        text.AppendLine("Project: " + Cell(projectPath));
        text.AppendLine("Plan destination: " + Cell(path));
        text.AppendLine("Source preimages (SHA-256 of UTF-8 workspace text):");
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            token.ThrowIfCancellationRequested();
            var part = document.Project.GetDocument(reference.SyntaxTree);
            if (part?.FilePath is null || part.GetLinkedDocumentIds().Length != 0 ||
                await ClassificationEdit.FindEditableRoot(part, reference.Span.Start, token) is null ||
                !TryFullPath(part.FilePath, out var partPath) || !ConventionGenerationPreflight.IsUnderDirectory(partPath, directory)) return null;
            var content = await part.GetTextAsync(token);
            text.AppendLine("- " + Cell(partPath) + ": " + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content.ToString()))));
        }
        text.AppendLine("\n## Inheritance and declared metadata\n");
        foreach (var ancestor in Chain(type))
        {
            token.ThrowIfCancellationRequested();
            text.AppendLine("- " + Cell(ancestor.ToDisplayString()) + ": " + Attributes(ancestor));
            text.AppendLine("  - Assembly: " + Cell(ancestor.ContainingAssembly.Identity.ToString()));
            foreach (var location in ancestor.Locations.Where(item => item.IsInSource))
            {
                var inheritedDocument = location.SourceTree is { } tree ? document.Project.Solution.GetDocument(tree) : null;
                var fingerprint = inheritedDocument is null ? " (source preimage unavailable)" : "; SHA-256 " +
                    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes((await inheritedDocument.GetTextAsync(token)).ToString())));
                text.AppendLine("  - Declaration: " + Cell(location.GetLineSpan().Path) + fingerprint);
            }
        }
        text.AppendLine("\nExisting direct project references (no reference changes requested):");
        foreach (var reference in document.Project.ProjectReferences)
        {
            token.ThrowIfCancellationRequested();
            var target = document.Project.Solution.GetProject(reference.ProjectId);
            text.AppendLine("- " + Cell(target?.FilePath ?? target?.Name ?? "Unavailable loaded project") +
                            "; aliases: " + Cell(string.Join(", ", reference.Aliases)));
        }
        text.AppendLine("\n## Public instance method inventory\n\nRows are candidate actions, not a runtime route or authorization proof. Attribute declarations on overrides are retained separately. NonAction disables routing. API visibility alone does not.\n");
        var overridden = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var mvcBase = model.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.ControllerBase");
        var nonAction = model.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.NonActionAttribute");
        var excludedHelpers = 0;
        var count = 0;
        foreach (var ancestor in Chain(type))
        foreach (var method in ancestor.GetMembers().OfType<IMethodSymbol>())
        {
            token.ThrowIfCancellationRequested();
            if (method.MethodKind != MethodKind.Ordinary || method.IsStatic || method.DeclaredAccessibility != Accessibility.Public ||
                method.IsImplicitlyDeclared || overridden.Contains(method.OriginalDefinition)) continue;
            if (Same(method.ContainingType, mvcBase) && method.GetAttributes().Any(attribute => Same(attribute.AttributeClass, nonAction)))
            {
                excludedHelpers++;
                continue;
            }
            if (++count > 128) return null;
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
        text.AppendLine("\nExcluded exact MVC ControllerBase helpers declared NonAction: " + excludedHelpers +
                        ". User overrides and SourceKnown methods remain listed. Object methods are excluded. Custom MVC conventions require separate review.");
        text.AppendLine("\n## Required decisions before implementation\n\n- Select enabled operations and disable unwanted overridable actions with NonAction.\n- Confirm exact route and verb composition, including inherited metadata and application MVC conventions.\n- Supply named policies, authentication transport and per-resource authorization for each enabled action. AllowAnonymous is not a fallback.\n- Supply actual request/response fields, classification, validation and entity-to-DTO mapping.\n- Confirm creation invariants and deletion semantics. Inherited DeleteAsync performs direct deletion without entity lifecycle hooks.\n- Confirm repository visibility filters and cancellation propagation.\n- Select exact source destinations and any required project metadata changes. No executable output destinations have been selected by this plan.\n- Review all source/project/reference preimages again. This plan is evidence of the current loaded workspace, not an executable transaction.\n- Use a host capable of previewing and atomically applying every required source and project change. Unsupported project edits remain manual reviewed template work.\n\nNo C# files, IDs, policies, business logic, runnable endpoints, project references or project items were generated. Compilation, model compatibility and runtime authorization remain unverified.\n");
        return text.Length >= 12000 ? null : new(name, path, text.ToString());
    }

    private static IEnumerable<INamedTypeSymbol> Chain(INamedTypeSymbol type)
    {
        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
            yield return current;
    }

    private static string Attributes(ISymbol symbol) => string.Join("; ", symbol.GetAttributes().Select(attribute => Cell(attribute.ToString())));
    private static string Cell(string? value) => (value ?? "<unavailable metadata>").Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\r", "&#13;", StringComparison.Ordinal).Replace("\n", "&#10;", StringComparison.Ordinal)
        .Replace("\\", "&#92;", StringComparison.Ordinal).Replace("`", "&#96;", StringComparison.Ordinal)
        .Replace("[", "&#91;", StringComparison.Ordinal).Replace("]", "&#93;", StringComparison.Ordinal)
        .Replace("*", "&#42;", StringComparison.Ordinal).Replace("_", "&#95;", StringComparison.Ordinal);
    private sealed record Plan(string Name, string Path, string Content);
}
