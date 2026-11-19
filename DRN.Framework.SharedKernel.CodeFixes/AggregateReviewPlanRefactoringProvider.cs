using System.Security.Cryptography;
using System.Text;
using DRN.Framework.SharedKernel.Analyzers.Conventions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using static DRN.Framework.SharedKernel.CodeFixes.CodeFixDestinationGuard;
using static DRN.Framework.SharedKernel.CodeFixes.CodeFixSymbolFacts;

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
                    return snapshot.AddAdditionalDocument(DocumentId.CreateNewId(plan.Project), plan.Name, SourceText.From(plan.Content),
                        folders: plan.RelativePath.Split('/')[..^1], filePath: plan.Path);
                }, nameof(AggregateReviewPlanRefactoringProvider) + ":" + plan.Project + ":" + plan.Context));
        }
    }

    private static async Task<List<Plan>> Plans(Document document, TextSpan span, CancellationToken token)
    {
        var plans = new List<Plan>();
        var target = await ResolveAggregateTargetAsync(document, span, token);
        if (target is null) return plans;
        var evidence = await TryCollectEntityEvidenceAsync(target, token);
        if (evidence is null) return plans;
        foreach (var infra in target.Domain.Solution.Projects)
        {
            token.ThrowIfCancellationRequested();
            await CollectInfraPlansForProjectAsync(target, evidence, infra, plans, token);
        }
        return plans;
    }

    private static async Task<AggregateTarget?> ResolveAggregateTargetAsync(Document document, TextSpan span, CancellationToken token)
    {
        var domain = document.Project;
        var domainNamespace = RoleNamespace(domain, "Domain");
        if (domainNamespace is null || document.FilePath is null || domain.FilePath is null || document.GetLinkedDocumentIds().Length != 0 ||
            !TryFullPath(document.FilePath, out var entityPath) || !TryFullPath(domain.FilePath, out var domainPath) ||
            !ConventionGenerationPreflight.IsUnderDirectory(entityPath, Path.GetDirectoryName(domainPath)!)) return null;
        var syntaxRoot = await document.GetSyntaxRootAsync(token);
        if (syntaxRoot is null || span.End > syntaxRoot.FullSpan.End) return null;
        var declaration = syntaxRoot.FindToken(span.Start).Parent?.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (declaration is null || !declaration.Identifier.Span.IntersectsWith(span)) return null;
        var domainInputs = await ReadProjectInputs(domain, token);
        if (domainInputs is null) return null;
        var syntax = await ClassificationEdit.FindEditableRoot(document, span.Start, token);
        var model = await document.GetSemanticModelAsync(token);
        if (syntax is null || model is null ||
            model.GetDeclaredSymbol(declaration, token) is not INamedTypeSymbol entity || entity.IsAbstract || IsOpen(entity) ||
            !Bases(entity).Any(type => Same(type, model.Compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.AggregateRoot"))) ||
            model.GetDiagnostics(declaration.Span, token).Any(item => item.Severity == DiagnosticSeverity.Error)) return null;
        return ValidateAggregateOwnership(domain, model.Compilation, domainPath, domainNamespace, domainInputs, entity);
    }

    private static AggregateTarget? ValidateAggregateOwnership(Project domain, Compilation compilation, string domainPath,
        string domainNamespace, ParsedInputs domainInputs, INamedTypeSymbol entity)
    {
        var namespacePrefix = domainNamespace + ".";
        var entityNamespace = entity.ContainingNamespace.ToDisplayString();
        if (!entityNamespace.StartsWith(namespacePrefix, StringComparison.Ordinal)) return null;
        var owner = entityNamespace[namespacePrefix.Length..].Split('.')[0];
        if (!ConventionGenerationPreflight.ValidNamespaceSegment(owner)) return null;
        var ownership = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomainAttribute");
        if (entity.GetAttributes().Any(attribute => Same(attribute.AttributeClass, ownership)) ||
            entity.ContainingAssembly.GetAttributes().Any(attribute => Same(attribute.AttributeClass, ownership) &&
                attribute.ConstructorArguments.FirstOrDefault().Value is INamedTypeSymbol target &&
                SymbolEqualityComparer.Default.Equals(target, entity))) return null;
        var entityName = CodeFixSymbolFacts.MetadataName(entity);
        return domainInputs.OwnershipTargets.Contains(entityName)
            ? null
            : new(domain, compilation, domainPath, domainInputs, entity, entityName, owner);
    }

    private static async Task<string?> TryCollectEntityEvidenceAsync(AggregateTarget target, CancellationToken token)
    {
        var evidence = new StringBuilder();
        var domainDirectory = Path.GetDirectoryName(target.DomainPath)!;
        foreach (var reference in target.Entity.DeclaringSyntaxReferences)
        {
            var part = target.Domain.GetDocument(reference.SyntaxTree);
            if (part?.FilePath is null || part.GetLinkedDocumentIds().Length != 0 ||
                await ClassificationEdit.FindEditableRoot(part, reference.Span.Start, token) is null ||
                !TryFullPath(part.FilePath, out var partPath) ||
                !ConventionGenerationPreflight.IsUnderDirectory(partPath, domainDirectory)) return null;
            evidence.AppendLine("- Entity source: " + Escape(part.FilePath) + "; SHA-256 " + Fingerprint(await part.GetTextAsync(token)));
        }
        var identity = target.DomainCompilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.EntityTypeAttribute");
        var declarations = target.Entity.GetAttributes().Where(attribute =>
            attribute.AttributeClass is { } attributeType && Bases(attributeType).Any(type => Same(type, identity)));
        evidence.AppendLine("- Existing identity declarations (not evaluated or allocated): " +
                            string.Join("; ", declarations.Select(attribute => Escape(attribute.ToString()))));
        return evidence.ToString();
    }

    private static async Task CollectInfraPlansForProjectAsync(AggregateTarget target, string entityEvidence,
        Project infra, List<Plan> plans, CancellationToken token)
    {
        var infraNamespace = RoleNamespace(infra, "Infra");
        if (infraNamespace is null || !infra.ProjectReferences.Any(reference => reference.ProjectId == target.Domain.Id &&
                (reference.Aliases.IsEmpty || reference.Aliases.Contains("global", StringComparer.Ordinal))) ||
            infra.FilePath is null || !TryFullPath(infra.FilePath, out var infraPath)) return;
        var infraInputs = await ReadProjectInputs(infra, token);
        if (infraInputs is null || infraInputs.OwnershipTargets.Contains(target.EntityName)) return;
        var compilation = await infra.GetCompilationAsync(token);
        var contract = compilation?.GetTypeByMetadataName("DRN.Framework.EntityFramework.Context.IDrnContext");
        if (compilation is null || contract is null) return;
        var contextOwners = TryMergeContextOwners(target.DomainInputs.Contexts, infraInputs.Contexts);
        if (contextOwners is null) return;
        var evidence = entityEvidence + target.DomainInputs.Evidence + infraInputs.Evidence;
        var types = Types(compilation.Assembly.GlobalNamespace, token).ToArray();
        var contexts = types.Where(type => !type.IsAbstract && !IsOpen(type) &&
            type.AllInterfaces.Any(item => Same(item, contract)) &&
            (type.ContainingNamespace.ToDisplayString() == infraNamespace + "." + target.Owner ||
             type.ContainingNamespace.ToDisplayString().StartsWith(infraNamespace + "." + target.Owner + ".", StringComparison.Ordinal))).ToArray();
        var selections = contexts.Length == 0 ? new INamedTypeSymbol?[] { null } : contexts.Cast<INamedTypeSymbol?>();
        var project = new InfraPlanProject(infra, infraPath, infraNamespace, compilation, types, infraInputs, contextOwners);
        foreach (var selected in selections)
        {
            var plan = await TryCreatePlanForContextAsync(target, project, evidence, selected, token);
            if (plan is not null) plans.Add(plan);
        }
    }

    private static Dictionary<string, (string Owner, string Reason, string Declaration)>? TryMergeContextOwners(
        Dictionary<string, (string Owner, string Reason, string Declaration)> domainContexts,
        Dictionary<string, (string Owner, string Reason, string Declaration)> infraContexts)
    {
        var merged = new Dictionary<string, (string Owner, string Reason, string Declaration)>(domainContexts, StringComparer.Ordinal);
        foreach (var entry in infraContexts)
        {
            if (merged.TryGetValue(entry.Key, out var previous) &&
                (previous.Owner != entry.Value.Owner || previous.Reason != entry.Value.Reason))
                return null;
            merged[entry.Key] = entry.Value;
        }
        return merged;
    }

    private static async Task<Plan?> TryCreatePlanForContextAsync(AggregateTarget target, InfraPlanProject infra,
        string evidence, INamedTypeSymbol? selected, CancellationToken token)
    {
        if (selected is not null &&
            (target.DomainInputs.OwnershipTargets.Contains(CodeFixSymbolFacts.MetadataName(selected)) ||
             infra.Inputs.OwnershipTargets.Contains(CodeFixSymbolFacts.MetadataName(selected)))) return null;
        var contextName = selected?.Name ?? target.Owner + "Context";
        var contextNamespace = selected?.ContainingNamespace.ToDisplayString() ?? infra.Namespace + "." + target.Owner;
        var contextSymbol = selected?.ToDisplayString() ?? contextNamespace + "." + contextName;
        if (!IsContextSelectionCompatible(infra.Compilation, target.Owner, infra.ContextOwners, selected, contextNamespace, contextName, token))
            return null;
        var contextDocument = await ResolveEditableContextHomeAsync(infra.Project, selected, token);
        if (selected is not null && contextDocument is null) return null;
        var contextEvidence = await TryCollectContextEvidenceAsync(infra.Project, infra.Directory, infra.ContextOwners, selected, token);
        if (contextEvidence is null) return null;
        var contextPath = ResolveContextPlanSourcePath(target, infra, selected, contextDocument,
            contextName, contextSymbol, token);
        if (contextPath is null) return null;
        var planName = target.Entity.Name + "." + contextName + ".aggregate-review.md";
        var planPath = Path.Combine(Path.GetDirectoryName(contextPath)!, planName);
        if (!SafeDestination(infra.Directory, planPath) ||
            WorkspaceDestinationConflict(target.Domain.Solution, planPath, infra.Project.Id, token)) return null;
        return BuildAggregatePlan(target, infra, (contextSymbol, selected is null, contextPath), planPath, evidence, contextEvidence);
    }

    private static bool IsContextSelectionCompatible(Compilation compilation, string owner,
        Dictionary<string, (string Owner, string Reason, string Declaration)> contextOwners,
        INamedTypeSymbol? selected, string contextNamespace, string contextName, CancellationToken token)
    {
        if (selected is null)
            return !ConventionGenerationPreflight.SymbolCollision(compilation, contextNamespace, contextName, token);
        return IsSelectedContextCompatible(compilation, owner, contextOwners, selected, contextName);
    }

    private static string? ResolveContextPlanSourcePath(AggregateTarget target, InfraPlanProject infra,
        INamedTypeSymbol? selected, Document? contextDocument, string contextName,
        string contextSymbol, CancellationToken token)
    {
        var contextPath = contextDocument?.FilePath ?? Path.Combine(infra.Directory, target.Owner, contextName + ".cs");
        if (!TryFullPath(contextPath, out contextPath) ||
            !ConventionGenerationPreflight.IsUnderDirectory(contextPath, infra.Directory)) return null;
        if (selected is null && (infra.Types.Any(type => string.Equals(CodeFixSymbolFacts.MetadataName(type), contextSymbol, StringComparison.OrdinalIgnoreCase)) ||
                !SafeDocumentDestination(infra.Project, infra.Directory, contextPath, token))) return null;
        return contextPath;
    }

    private static bool IsSelectedContextCompatible(Compilation compilation, string owner,
        Dictionary<string, (string Owner, string Reason, string Declaration)> contextOwners,
        INamedTypeSymbol selected, string contextName)
    {
        var ownerAttribute = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomainAttribute");
        if (compilation.Assembly.GetAttributes().Any(attribute => Same(attribute.AttributeClass, ownerAttribute) &&
                attribute.ConstructorArguments.FirstOrDefault().Value is INamedTypeSymbol target &&
                SymbolEqualityComparer.Default.Equals(target, selected))) return false;
        var declaredOwners = selected.GetAttributes().Where(attribute => Same(attribute.AttributeClass, ownerAttribute)).ToArray();
        if (declaredOwners.Length > 1 || declaredOwners.Any(attribute => attribute.ConstructorArguments.Length != 1 ||
                attribute.ConstructorArguments[0].Value as string != owner)) return false;
        var reasonedOwner = declaredOwners.Any(attribute => attribute.NamedArguments.Any(argument =>
            argument.Key == "Reason" && argument.Value.Value is string reason && !string.IsNullOrWhiteSpace(reason)));
        if (contextOwners.TryGetValue(CodeFixSymbolFacts.MetadataName(selected), out var declared))
        {
            if (declared.Owner.Length > 0 && declared.Owner != owner) return false;
            reasonedOwner |= declared.Owner == owner && !string.IsNullOrWhiteSpace(declared.Reason);
        }
        return contextName == owner + "Context" || reasonedOwner;
    }

    private static async Task<Document?> ResolveEditableContextHomeAsync(Project infra, INamedTypeSymbol? selected,
        CancellationToken token)
    {
        var contextTree = selected?.DeclaringSyntaxReferences.FirstOrDefault()?.SyntaxTree;
        var contextDocument = contextTree is null ? null : infra.GetDocument(contextTree);
        if (selected is null) return null;
        if (contextDocument?.FilePath is null || contextDocument.GetLinkedDocumentIds().Length != 0) return null;
        return await ClassificationEdit.FindEditableRoot(contextDocument,
            selected.DeclaringSyntaxReferences[0].Span.Start, token) is null ? null : contextDocument;
    }

    private static async Task<string?> TryCollectContextEvidenceAsync(Project infra, string infraDirectory,
        Dictionary<string, (string Owner, string Reason, string Declaration)> contextOwners,
        INamedTypeSymbol? selected, CancellationToken token)
    {
        var contextEvidence = new StringBuilder();
        if (selected is null) return contextEvidence.ToString();
        if (contextOwners.TryGetValue(CodeFixSymbolFacts.MetadataName(selected), out var selectedOwnership))
            contextEvidence.AppendLine("Selected context ownership input: " + EscapeMarkdown(selectedOwnership.Owner) +
                "; reason: " + EscapeMarkdown(selectedOwnership.Reason));
        string? contextDirectory = null;
        foreach (var reference in selected.DeclaringSyntaxReferences)
        {
            var part = infra.GetDocument(reference.SyntaxTree);
            if (part?.FilePath is null || part.GetLinkedDocumentIds().Length != 0 ||
                !TryFullPath(part.FilePath, out var partPath) ||
                !ConventionGenerationPreflight.IsUnderDirectory(partPath, infraDirectory) ||
                await ClassificationEdit.FindEditableRoot(part, reference.Span.Start, token) is null)
                return null;
            var partDirectory = Path.GetDirectoryName(partPath);
            if (contextDirectory is null) contextDirectory = partDirectory;
            else if (!string.Equals(contextDirectory, partDirectory, ConventionGenerationPreflight.RootComparison))
                return null;
            contextEvidence.AppendLine("Context source SHA-256: " + Fingerprint(await part.GetTextAsync(token)) + "; " + EscapeMarkdown(partPath));
        }
        contextEvidence.AppendLine("Context declared attributes: " + string.Join("; ", selected.GetAttributes().Select(attribute => EscapeMarkdown(attribute.ToString()))));
        if (contextOwners.TryGetValue(CodeFixSymbolFacts.MetadataName(selected), out var selectedInput))
            contextEvidence.AppendLine("Selected context input: <pre>" + EscapeMarkdown(selectedInput.Declaration) + "</pre>");
        return contextEvidence.ToString();
    }

    private static Plan? BuildAggregatePlan(AggregateTarget target, InfraPlanProject infra,
        (string Symbol, bool IsNew, string Path) context, string planPath, string evidence, string contextEvidence)
    {
        var relativePlanPath = Path.GetRelativePath(infra.Directory, planPath).Replace('\\', '/');
        var relative = Path.GetRelativePath(infra.Directory, Path.GetDirectoryName(context.Path)!).Replace('\\', '/');
        var destinationPrefix = relative == "." ? "" : relative + "/";
        var body = new StringBuilder("# Aggregate review plan\n\nPlan only. Adds this Markdown AdditionalFiles review plan without modifying source, project metadata, references, membership or identity.\n\n");
        body.AppendLine("Aggregate: " + Escape(target.Entity.ToDisplayString()) + "\n\nDomain project: " + Escape(target.DomainPath));
        body.AppendLine("Infra project: " + Escape(infra.ProjectPath) + "\n\nPlan destination: " + Escape(planPath));
        body.AppendLine("Context: " + Escape(context.Symbol) + (context.IsNew ? " (proposed new context, not created)" : " (existing, preserve identity and configuration)"));
        body.AppendLine("Context source destination: " + Escape(context.Path));
        body.AppendLine("Proposed Repositories destination: " + Escape(destinationPrefix + "Repositories"));
        body.AppendLine("Proposed Migrations destination: " + Escape(destinationPrefix + "Migrations"));
        body.AppendLine("Conventional destinations need no Context item when namespace and logical source homes agree. Declare alternate or ambiguous destinations explicitly, with a Reason for alternatives.");
        body.AppendLine("\n## Existing evidence\n" + evidence);
        body.Append(contextEvidence);
        body.AppendLine("\n## Required review\n\n- Confirm namespace-derived ownership and every supplied input. Multiple contexts are valid. No membership is inferred from selection.\n- Reconcile proposed destinations with existing Context items. Reuse existing metadata; do not duplicate or replace it.\n- Select optional configuration, repository, DTO, service and transport artifacts independently. Attribute-only mapping remains valid.\n- Confirm entity inclusion and all shared configuration discovery paths. Cross-owner use requires a separate narrow reasoned exception.\n- Preserve existing entity ID declarations, context CLR name, connection keys, schema, migrations and runtime safeguards. Missing identity is unresolved, never allocated here.\n- For a new context, review the SCAFFOLDING.md DrnContext template and conventional options/parameterless constructors. No provider settings are inferred.\n- Review exact source and project preimages, path/case/symbol collisions and references before implementation. No required project reference is inferred or added.\n- Project-system atomic application is unsupported by this plan. Keep source and any explicit destination overrides together in a separately reviewed host transaction.\n- EF tooling alone owns generated migrations. No database operation, executable scaffold or empty directory placeholder is produced.\n\nCompilation, model compatibility and host atomic application remain unverified.\n");
        return body.Length < 12000 ? new(infra.Project.Id, infra.Project.Name, context.Symbol, Path.GetFileName(planPath), planPath, relativePlanPath, body.ToString()) : null;
    }

    private static async Task<ParsedInputs?> ReadProjectInputs(Project project, CancellationToken token)
    {
        var files = project.AdditionalDocuments.Where(ConventionGenerationPreflight.IsInputsDocument).Take(2).ToArray();
        if (files.Length > 1) return null;
        var contexts = new Dictionary<string, (string Owner, string Reason, string Declaration)>(StringComparer.Ordinal);
        var ownershipTargets = new HashSet<string>(StringComparer.Ordinal);
        var evidence = new StringBuilder();
        foreach (var input in files)
        {
            if (!await TryParseProjectInputFileAsync(project, input, contexts, ownershipTargets, evidence, token))
                return null;
        }
        return new(evidence.ToString(), contexts, ownershipTargets);
    }

    private static async Task<bool> TryParseProjectInputFileAsync(Project project, TextDocument input,
        Dictionary<string, (string Owner, string Reason, string Declaration)> contexts,
        HashSet<string> ownershipTargets, StringBuilder evidence, CancellationToken token)
    {
        var text = await input.GetTextAsync(token);
        var inputRoot = ConventionInputSyntax.Parse(text, input.FilePath ?? input.Name, token, out _);
        if (inputRoot is null || !ConventionInputSyntax.SupportsGeneration(inputRoot, token) || HasRoleOverride(inputRoot)) return false;
        var attributes = inputRoot.AttributeLists.SelectMany(list => list.Attributes).ToArray();
        if (!TryCollectOwnershipTargets(attributes, ownershipTargets, token) ||
            !TryCollectContextInputs(inputRoot, contexts, token)) return false;
        evidence.AppendLine("- Supplied inputs for " + Escape(project.Name) + "; SHA-256 " + Fingerprint(text) +
            "; records: " + attributes.Length + ". Full inventory retained in DrnConventionInputs.g.cs.");
        return true;
    }

    private static bool TryCollectOwnershipTargets(AttributeSyntax[] attributes, HashSet<string> ownershipTargets, CancellationToken token)
    {
        foreach (var attribute in attributes)
        {
            token.ThrowIfCancellationRequested();
            if (attribute.Name.ToString() is not ("DRN.Conventions.Ownership" or "global::DRN.Conventions.Ownership")) continue;
            if (attribute.ArgumentList is not { Arguments.Count: > 0 } arguments ||
                arguments.Arguments[0].NameEquals is not null || arguments.Arguments[0].NameColon is not null ||
                ConventionInputSyntax.String(arguments.Arguments[0].Expression, token) is not { } target) return false;
            ownershipTargets.Add(target);
        }
        return true;
    }

    private static bool TryCollectContextInputs(CompilationUnitSyntax inputRoot,
        Dictionary<string, (string Owner, string Reason, string Declaration)> contexts, CancellationToken token)
    {
        foreach (var attribute in inputRoot.DescendantNodes().OfType<AttributeSyntax>())
        {
            token.ThrowIfCancellationRequested();
            if (attribute.Name.ToString() is not ("DRN.Conventions.Context" or "global::DRN.Conventions.Context")) continue;
            if (!TryReadContextAttributeInput(attribute, contexts, token)) return false;
        }
        return true;
    }

    private static bool TryReadContextAttributeInput(AttributeSyntax attribute,
        Dictionary<string, (string Owner, string Reason, string Declaration)> contexts, CancellationToken token)
    {
        var arguments = attribute.ArgumentList?.Arguments;
        if (arguments is null || arguments.Value.Count < 1) return false;
        var name = ConventionInputSyntax.String(arguments.Value[0].Expression, token);
        if (arguments.Value[0].NameEquals is not null || arguments.Value[0].NameColon is not null ||
            arguments.Value.Where(argument => argument.NameEquals is not null)
                .GroupBy(argument => argument.NameEquals!.Name.Identifier.ValueText).Any(group => group.Count() > 1))
            return false;
        var ownerArgument = arguments.Value.FirstOrDefault(argument => argument.NameEquals?.Name.Identifier.ValueText == "Subdomain");
        var reasonArgument = arguments.Value.FirstOrDefault(argument => argument.NameEquals?.Name.Identifier.ValueText == "Reason");
        var declaredOwner = ownerArgument is null ? "" : ConventionInputSyntax.String(ownerArgument.Expression, token);
        var reason = reasonArgument is null ? "" : ConventionInputSyntax.String(reasonArgument.Expression, token);
        if (name is null || declaredOwner is null || reason is null ||
            contexts.TryGetValue(name, out var previous) && (previous.Owner != declaredOwner || previous.Reason != reason))
            return false;
        contexts[name] = (declaredOwner, reason, attribute.ToString());
        return true;
    }

    private sealed record InfraPlanProject(Project Project, string ProjectPath, string Namespace, Compilation Compilation,
        INamedTypeSymbol[] Types, ParsedInputs Inputs, Dictionary<string, (string Owner, string Reason, string Declaration)> ContextOwners)
    {
        internal string Directory { get; } = Path.GetDirectoryName(ProjectPath)!;
    }

    private sealed record AggregateTarget(Project Domain, Compilation DomainCompilation, string DomainPath,
        ParsedInputs DomainInputs, INamedTypeSymbol Entity, string EntityName, string Owner);

    private static string? RoleNamespace(Project project, string role)
    {
        var identity = ConventionGenerationPreflight.Identity(project);
        return identity.Role == role && ValidNamespace(identity.Namespace) ? identity.Namespace : null;
    }

    private sealed record ParsedInputs(string Evidence, Dictionary<string, (string Owner, string Reason, string Declaration)> Contexts,
        HashSet<string> OwnershipTargets);

    private static string Fingerprint(SourceText text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    private static string Escape(string? value) => EscapeMarkdown(value);
    private sealed record Plan(ProjectId Project, string ProjectName, string Context, string Name, string Path, string RelativePath, string Content);
}
