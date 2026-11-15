using System.Composition;
using System.Security.Cryptography;
using System.Text;
using DRN.Framework.SharedKernel.Analyzers.Conventions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static DRN.Framework.SharedKernel.CodeFixes.EntityConfigurationRefactoringProvider;

namespace DRN.Framework.SharedKernel.CodeFixes;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(AggregateReviewPlanRefactoringProvider)), Shared]
public sealed class AggregateReviewPlanRefactoringProvider : CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var snapshot = context.Document.Project.Solution;
        if (!snapshot.Workspace.CanApplyChange(ApplyChangesKind.AddAdditionalDocument)) return;
        foreach (var plan in await Plans(context.Document, context.Span, context.CancellationToken))
        {
            if (!ReferenceEquals(snapshot.Workspace.CurrentSolution, snapshot)) return;
            context.RegisterRefactoring(CodeAction.Create("Create aggregate review plan only: " + plan.Context + " in " + plan.ProjectName,
                async token =>
                {
                    token.ThrowIfCancellationRequested();
                    if (!ReferenceEquals(snapshot.Workspace.CurrentSolution, snapshot)) return snapshot.Workspace.CurrentSolution;
                    if (!(await Plans(context.Document, context.Span, token)).Contains(plan)) return snapshot.Workspace.CurrentSolution;
                    token.ThrowIfCancellationRequested();
                    if (!ReferenceEquals(snapshot.Workspace.CurrentSolution, snapshot)) return snapshot.Workspace.CurrentSolution;
                    return snapshot.AddAdditionalDocument(DocumentId.CreateNewId(plan.Project), plan.Name, SourceText.From(plan.Content), filePath: plan.Path);
                }, nameof(AggregateReviewPlanRefactoringProvider) + ":" + plan.Project + ":" + plan.Context));
        }
    }

    private static async Task<List<Plan>> Plans(Document document, TextSpan span, CancellationToken token)
    {
        var plans = new List<Plan>();
        var domain = document.Project;
        var domainNamespace = RoleNamespace(domain, "Domain");
        if (domainNamespace is null || document.FilePath is null || domain.FilePath is null || document.GetLinkedDocumentIds().Length != 0 ||
            !TryFullPath(document.FilePath, out var entityPath) || !TryFullPath(domain.FilePath, out var domainPath) ||
            !entityPath.StartsWith(Path.GetDirectoryName(domainPath)! + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            await HasRoleOverrides(domain, token)) return plans;
        var syntax = await ClassificationEdit.FindEditableRoot(document, span.Start, token);
        var model = await document.GetSemanticModelAsync(token);
        if (syntax is null || span.End > syntax.FullSpan.End) return plans;
        var declaration = syntax?.FindToken(span.Start).Parent?.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (model is null || declaration is null || !declaration.Identifier.Span.IntersectsWith(span) ||
            model.GetDeclaredSymbol(declaration, token) is not INamedTypeSymbol entity || entity.IsAbstract || IsOpen(entity) ||
            !Bases(entity).Any(type => Same(type, model.Compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.AggregateRoot"))) ||
            model.GetDiagnostics(declaration.Span, token).Any(item => item.Severity == DiagnosticSeverity.Error)) return plans;
        var namespacePrefix = domainNamespace + ".";
        var entityNamespace = entity.ContainingNamespace.ToDisplayString();
        if (!entityNamespace.StartsWith(namespacePrefix, StringComparison.Ordinal)) return plans;
        var owner = entityNamespace[namespacePrefix.Length..].Split('.')[0];
        if (!SyntaxFacts.IsValidIdentifier(owner)) return plans;
        var ownership = model.Compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomainAttribute");
        if (entity.GetAttributes().Any(attribute => Same(attribute.AttributeClass, ownership)) ||
            entity.ContainingAssembly.GetAttributes().Any(attribute => Same(attribute.AttributeClass, ownership) &&
                attribute.ConstructorArguments.FirstOrDefault().Value is INamedTypeSymbol target &&
                SymbolEqualityComparer.Default.Equals(target, entity))) return plans;
        var evidence = new StringBuilder();
        foreach (var reference in entity.DeclaringSyntaxReferences)
        {
            var part = domain.GetDocument(reference.SyntaxTree);
            if (part?.FilePath is null || part.GetLinkedDocumentIds().Length != 0 ||
                await ClassificationEdit.FindEditableRoot(part, reference.Span.Start, token) is null ||
                !TryFullPath(part.FilePath, out var partPath) ||
                !partPath.StartsWith(Path.GetDirectoryName(domainPath)! + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return plans;
            evidence.AppendLine("- Entity source: " + Escape(part.FilePath) + "; SHA-256 " + Fingerprint(await part.GetTextAsync(token)));
        }
        var identity = model.Compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.EntityTypeAttribute");
        var declarations = entity.GetAttributes().Where(attribute => attribute.AttributeClass is { } attributeType && Bases(attributeType).Any(type => Same(type, identity)));
        evidence.AppendLine("- Existing identity declarations (not evaluated or allocated): " + string.Join("; ", declarations.Select(attribute => Escape(attribute.ToString()))));
        foreach (var infra in domain.Solution.Projects)
        {
            token.ThrowIfCancellationRequested();
            var infraNamespace = RoleNamespace(infra, "Infra");
            if (infraNamespace is null || !infra.ProjectReferences.Any(reference => reference.ProjectId == domain.Id &&
                    (reference.Aliases.IsEmpty || reference.Aliases.Contains("global", StringComparer.Ordinal))) ||
                infra.FilePath is null || !TryFullPath(infra.FilePath, out var infraPath) ||
                await HasRoleOverrides(infra, token)) continue;
            var compilation = await infra.GetCompilationAsync(token);
            var contract = compilation?.GetTypeByMetadataName("DRN.Framework.EntityFramework.Context.IDrnContext");
            if (compilation is null || contract is null) continue;
            var types = Types(compilation.Assembly.GlobalNamespace, token).ToArray();
            var contexts = types.Where(type => !type.IsAbstract && !IsOpen(type) &&
                type.AllInterfaces.Any(item => Same(item, contract)) &&
                (type.ContainingNamespace.ToDisplayString() == infraNamespace + "." + owner ||
                 type.ContainingNamespace.ToDisplayString().StartsWith(infraNamespace + "." + owner + ".", StringComparison.Ordinal))).ToArray();
            var inputEvidence = new StringBuilder();
            var contextOwners = new Dictionary<string, (string Owner, string Reason, string Declaration)>(StringComparer.Ordinal);
            var unsupportedOwnership = new[] { domain, infra }.Any(project =>
                project.AdditionalDocuments.Count(item => item.Name == "DrnConventionInputs.g.cs") > 1);
            foreach (var project in new[] { domain, infra })
            foreach (var input in project.AdditionalDocuments.Where(item => item.Name == "DrnConventionInputs.g.cs"))
            {
                var text = await input.GetTextAsync(token);
                var inputRoot = ConventionInputSyntax.Parse(text, input.FilePath ?? input.Name, token, out _);
                if (inputRoot is null) { unsupportedOwnership = true; break; }
                var attributes = inputRoot.AttributeLists.SelectMany(list => list.Attributes).ToArray();
                if (attributes.Any(attribute =>
                        attribute.Name.ToString() is "DRN.Conventions.Ownership" or "global::DRN.Conventions.Ownership"))
                { unsupportedOwnership = true; break; }
                foreach (var attribute in inputRoot.DescendantNodes().OfType<AttributeSyntax>())
                {
                    var arguments = attribute.ArgumentList?.Arguments;
                    if (attribute.Name.ToString() is not ("DRN.Conventions.Context" or "global::DRN.Conventions.Context")) continue;
                    if (arguments is null || arguments.Value.Count < 1) { unsupportedOwnership = true; break; }
                    var name = ConventionInputSyntax.String(arguments.Value[0].Expression, token);
                    if (arguments.Value[0].NameEquals is not null || arguments.Value[0].NameColon is not null ||
                        arguments.Value.Where(argument => argument.NameEquals is not null)
                            .GroupBy(argument => argument.NameEquals!.Name.Identifier.ValueText).Any(group => group.Count() > 1))
                    { unsupportedOwnership = true; break; }
                    var ownerArgument = arguments.Value.FirstOrDefault(argument => argument.NameEquals?.Name.Identifier.ValueText == "Subdomain");
                    var reasonArgument = arguments.Value.FirstOrDefault(argument => argument.NameEquals?.Name.Identifier.ValueText == "Reason");
                    var declaredOwner = ownerArgument is null ? "" : ConventionInputSyntax.String(ownerArgument.Expression, token);
                    var reason = reasonArgument is null ? "" : ConventionInputSyntax.String(reasonArgument.Expression, token);
                    if (name is null || declaredOwner is null || reason is null ||
                        contextOwners.TryGetValue(name, out var previous) && (previous.Owner != declaredOwner || previous.Reason != reason))
                    { unsupportedOwnership = true; break; }
                    contextOwners[name] = (declaredOwner, reason, attribute.ToString());
                }
                inputEvidence.AppendLine("- Supplied inputs for " + Escape(project.Name) + "; SHA-256 " + Fingerprint(text) +
                    "; records: " + attributes.Length + ". Full inventory retained in DrnConventionInputs.g.cs.");
            }
            if (unsupportedOwnership) continue;
            var selections = contexts.Length == 0 ? new INamedTypeSymbol?[] { null } : contexts.Cast<INamedTypeSymbol?>();
            foreach (var selected in selections)
            {
                var infraDirectory = Path.GetDirectoryName(infraPath)!;
                var contextName = selected?.Name ?? owner + "Context";
                var contextNamespace = selected?.ContainingNamespace.ToDisplayString() ?? infraNamespace + "." + owner;
                var contextSymbol = selected?.ToDisplayString() ?? contextNamespace + "." + contextName;
                if (selected is null && ConventionGenerationPreflight.SymbolCollision(compilation, contextNamespace, contextName, token)) continue;
                if (selected is not null)
                {
                    var ownerAttribute = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomainAttribute");
                    if (compilation.Assembly.GetAttributes().Any(attribute => Same(attribute.AttributeClass, ownerAttribute) &&
                        attribute.ConstructorArguments.FirstOrDefault().Value is INamedTypeSymbol target &&
                        SymbolEqualityComparer.Default.Equals(target, selected))) continue;
                    var declaredOwners = selected.GetAttributes().Where(attribute => Same(attribute.AttributeClass, ownerAttribute)).ToArray();
                    if (declaredOwners.Length > 1 || declaredOwners.Any(attribute => attribute.ConstructorArguments.Length != 1 ||
                            attribute.ConstructorArguments[0].Value as string != owner)) continue;
                    var reasonedOwner = declaredOwners.Any(attribute => attribute.NamedArguments.Any(argument => argument.Key == "Reason" &&
                        argument.Value.Value is string reason && !string.IsNullOrWhiteSpace(reason)));
                    if (contextOwners.TryGetValue(MetadataName(selected), out var declared))
                    {
                        if (declared.Owner.Length > 0 && declared.Owner != owner) continue;
                        reasonedOwner |= declared.Owner == owner && !string.IsNullOrWhiteSpace(declared.Reason);
                    }
                    if (contextName != owner + "Context" && !reasonedOwner) continue;
                }
                var contextTree = selected?.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree;
                var contextDocument = contextTree is null ? null : infra.GetDocument(contextTree);
                if (selected is not null && (contextDocument?.FilePath is null || contextDocument.GetLinkedDocumentIds().Length != 0)) continue;
                if (contextDocument is not null && await ClassificationEdit.FindEditableRoot(contextDocument,
                        selected!.DeclaringSyntaxReferences[0].Span.Start, token) is null) continue;
                var contextEvidence = new StringBuilder();
                if (selected is not null && contextOwners.TryGetValue(MetadataName(selected), out var selectedOwnership))
                    contextEvidence.AppendLine("Selected context ownership input: " + Escape(selectedOwnership.Owner) +
                        "; reason: " + Escape(selectedOwnership.Reason));
                var editable = true;
                foreach (var reference in selected?.DeclaringSyntaxReferences ?? [])
                {
                    var part = infra.GetDocument(reference.SyntaxTree);
                    if (part?.FilePath is null || part.GetLinkedDocumentIds().Length != 0 ||
                        !TryFullPath(part.FilePath, out var partPath) ||
                        !partPath.StartsWith(infraDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
                        await ClassificationEdit.FindEditableRoot(part, reference.Span.Start, token) is null)
                    { editable = false; break; }
                    contextEvidence.AppendLine("Context source SHA-256: " + Fingerprint(await part.GetTextAsync(token)) + "; " + Escape(partPath));
                }
                if (!editable) continue;
                if (selected is not null)
                {
                    contextEvidence.AppendLine("Context declared attributes: " + string.Join("; ", selected.GetAttributes().Select(attribute => Escape(attribute.ToString()))));
                    if (contextOwners.TryGetValue(MetadataName(selected), out var selectedInput))
                        contextEvidence.AppendLine("Selected context input: <pre>" + Escape(selectedInput.Declaration) + "</pre>");
                }
                var contextPath = contextDocument?.FilePath ?? Path.Combine(infraDirectory, owner, contextName + ".cs");
                if (!TryFullPath(contextPath, out contextPath) || !contextPath.StartsWith(infraDirectory + Path.DirectorySeparatorChar, StringComparison.Ordinal)) continue;
                if (selected is null && (types.Any(type => string.Equals(MetadataName(type), contextSymbol, StringComparison.OrdinalIgnoreCase)) ||
                    !SafeDestination(infraDirectory, contextPath) || WorkspaceDestinationConflict(domain.Solution, contextPath, token))) continue;
                var planName = entity.Name + "." + contextName + ".aggregate-review.md";
                var planPath = Path.Combine(Path.GetDirectoryName(contextPath)!, planName);
                if (!SafeDestination(infraDirectory, planPath) || WorkspaceDestinationConflict(domain.Solution, planPath, token)) continue;
                var relative = Path.GetRelativePath(infraDirectory, Path.GetDirectoryName(contextPath)!).Replace('\\', '/');
                var body = new StringBuilder("# Aggregate review plan\n\nPlan only. No source, project metadata, membership or identity is changed.\n\n");
                body.AppendLine("Aggregate: " + Escape(entity.ToDisplayString()) + "\n\nDomain project: " + Escape(domainPath));
                body.AppendLine("Infra project: " + Escape(infraPath) + "\n\nPlan destination: " + Escape(planPath));
                body.AppendLine("Context: " + Escape(contextSymbol) + (selected is null ? " (proposed new context, not created)" : " (existing, preserve identity and configuration)"));
                body.AppendLine("Context source destination: " + Escape(contextPath));
                body.AppendLine("Proposed Repositories destination: " + Escape(relative + "/Repositories"));
                body.AppendLine("Proposed Migrations destination: " + Escape(relative + "/Migrations"));
                body.AppendLine("Conventional destinations need no Context item when namespace and logical source homes agree. Declare alternate or ambiguous destinations explicitly, with a Reason for alternatives.");
                body.AppendLine("\n## Existing evidence\n" + evidence + inputEvidence.ToString());
                body.Append(contextEvidence);
                body.AppendLine("\n## Required review\n\n- Confirm namespace-derived ownership and every supplied input. Multiple contexts are valid. No membership is inferred from selection.\n- Reconcile proposed destinations with existing Context items. Reuse existing metadata; do not duplicate or replace it.\n- Select optional configuration, repository, DTO, service and transport artifacts independently. Attribute-only mapping remains valid.\n- Confirm entity inclusion and all shared configuration discovery paths. Cross-owner use requires a separate narrow reasoned exception.\n- Preserve existing entity ID declarations, context CLR name, connection keys, schema, migrations and runtime safeguards. Missing identity is unresolved, never allocated here.\n- For a new context, review the SCAFFOLDING.md DrnContext template and conventional options/parameterless constructors. No provider settings are inferred.\n- Review exact source and project preimages, path/case/symbol collisions and references before implementation. No required project reference is inferred or added.\n- Project-system atomic application is unsupported by this plan. Keep source and any explicit destination overrides together in a separately reviewed host transaction.\n- EF tooling alone owns generated migrations. No database operation, executable scaffold or empty directory placeholder is produced.\n\nCompilation, model compatibility and host atomic application remain unverified.\n");
                if (body.Length < 12000) plans.Add(new(infra.Id, infra.Name, contextSymbol, planName, planPath, body.ToString()));
            }
        }
        return plans;
    }

    private static string? RoleNamespace(Project project, string role)
    {
        var identity = ConventionGenerationPreflight.Identity(project);
        return identity.Role == role && identity.Namespace.Length > 0 && identity.Namespace.Split('.').All(SyntaxFacts.IsValidIdentifier)
            ? identity.Namespace : null;
    }

    private static IEnumerable<INamedTypeSymbol> Bases(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType) yield return current;
    }

    private static string Fingerprint(SourceText text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    private static string Escape(string? value) => (value ?? "<unavailable metadata>").Replace("&", "&amp;", StringComparison.Ordinal).Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal).Replace("\r", "&#13;", StringComparison.Ordinal).Replace("\n", "&#10;", StringComparison.Ordinal)
        .Replace("\\", "&#92;", StringComparison.Ordinal).Replace("`", "&#96;", StringComparison.Ordinal).Replace("[", "&#91;", StringComparison.Ordinal).Replace("]", "&#93;", StringComparison.Ordinal)
        .Replace("*", "&#42;", StringComparison.Ordinal).Replace("_", "&#95;", StringComparison.Ordinal);
    private sealed record Plan(ProjectId Project, string ProjectName, string Context, string Name, string Path, string Content);
}
