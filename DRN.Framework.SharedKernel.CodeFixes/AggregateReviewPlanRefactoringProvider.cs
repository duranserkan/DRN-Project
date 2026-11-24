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
        var domainInfo = ConventionGenerationPreflight.ReadProject(domain);
        if (domainInfo.Role != "Domain" || !domainInfo.Layout.Valid || !ValidNamespace(domainInfo.Namespace) ||
            document.FilePath is null || domain.FilePath is null || document.GetLinkedDocumentIds().Length != 0 ||
            !TryFullPath(document.FilePath, out var entityPath) || !TryFullPath(domain.FilePath, out var domainPath) ||
            !ConventionGenerationPreflight.IsUnderDirectory(entityPath, Path.GetDirectoryName(domainPath)!)) return null;
        var syntaxRoot = await document.GetSyntaxRootAsync(token);
        if (syntaxRoot is null || span.End > syntaxRoot.FullSpan.End) return null;
        var declaration = syntaxRoot.FindToken(span.Start).Parent?.AncestorsAndSelf().OfType<TypeDeclarationSyntax>().FirstOrDefault();
        if (declaration is null || !declaration.Identifier.Span.IntersectsWith(span)) return null;
        var syntax = await ClassificationEdit.FindEditableRoot(document, span.Start, token);
        var model = await document.GetSemanticModelAsync(token);
        if (syntax is null || model is null ||
            model.GetDeclaredSymbol(declaration, token) is not INamedTypeSymbol entity || entity.IsAbstract || IsOpen(entity) ||
            !Bases(entity).Any(type => Same(type, model.Compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.AggregateRoot"))) ||
            model.GetDiagnostics(declaration.Span, token).Any(item => item.Severity == DiagnosticSeverity.Error)) return null;
        var domainInputs = await ReadProjectInputs(domain, token);
        if (domainInputs is null) return null;
        return ValidateAggregateOwnership(domain, model.Compilation, domainPath, domainInfo.Namespace, domainInfo.Layout, domainInputs, entity);
    }

    private static AggregateTarget? ValidateAggregateOwnership(Project domain, Compilation compilation, string domainPath,
        string domainNamespace, ConventionGenerationPreflight.ProjectLayout layout, ParsedInputs domainInputs, INamedTypeSymbol entity)
    {
        var owner = ResolveNamespaceOwnerKey(entity, domainNamespace, layout);
        if (owner is null) return null;
        var ownership = compilation.GetTypeByMetadataName(OwnershipAttributeMetadataName);
        if (!AttributesPreserveOwner(entity, owner, compilation, out var hasExplicitDomain) ||
            entity.ContainingAssembly.GetAttributes().Any(attribute => Same(attribute.AttributeClass, ownership) &&
                attribute.ConstructorArguments.FirstOrDefault().Value is INamedTypeSymbol target &&
                SymbolEqualityComparer.Default.Equals(target, entity))) return null;
        var entityName = CodeFixSymbolFacts.MetadataName(entity);
        var metadata = ConventionGenerationPreflight.MetadataFromAssembly(entity.ContainingAssembly);
        if (domainInputs.OwnershipTargets.Contains(entityName) ||
            !metadata.TryValidateProducerOwnership(entityName, owner, out var producerExplicitDomain)) return null;
        return new(domain, compilation, domainPath, domainInputs, entity, entityName,
            owner.WithExplicitDomain(hasExplicitDomain || producerExplicitDomain));
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
        var infraInfo = ConventionGenerationPreflight.ReadProject(infra);
        if (infraInfo.Role != "Infra" || !ValidNamespace(infraInfo.Namespace) ||
            !infraInfo.Layout.TryResolveTargetOwner(target.Owner, out var infraOwner) ||
            !infra.ProjectReferences.Any(reference => reference.ProjectId == target.Domain.Id &&
                (reference.Aliases.IsEmpty || reference.Aliases.Contains("global", StringComparer.Ordinal))) ||
            infra.FilePath is null || !TryFullPath(infra.FilePath, out var infraPath)) return;
        var infraInputs = await ReadProjectInputs(infra, token);
        if (infraInputs is null || infraInputs.OwnershipTargets.Contains(target.EntityName)) return;
        var compilation = await infra.GetCompilationAsync(token);
        var contract = compilation?.GetTypeByMetadataName("DRN.Framework.EntityFramework.Context.IDrnContext");
        if (compilation is null || contract is null) return;
        var contextOwners = TryMergeContextOwners(target.DomainInputs.Contexts, infraInputs.Contexts,
            compilation, infraInfo.Namespace, infraInfo.Layout);
        if (contextOwners is null) return;
        var evidence = entityEvidence + target.DomainInputs.Evidence + infraInputs.Evidence;
        var types = Types(compilation.Assembly.GlobalNamespace, token).ToArray();
        var ownerPrefix = infraOwner.NamespacePrefix;
        var contexts = types.Where(type => !type.IsAbstract && !IsOpen(type) &&
            type.AllInterfaces.Any(item => Same(item, contract)) &&
            (type.ContainingNamespace.ToDisplayString() == infraInfo.Namespace + "." + ownerPrefix ||
             type.ContainingNamespace.ToDisplayString().StartsWith(infraInfo.Namespace + "." + ownerPrefix + ".", StringComparison.Ordinal))).ToArray();
        var selections = contexts.Length == 0 ? new INamedTypeSymbol?[] { null } : contexts.Cast<INamedTypeSymbol?>();
        var project = new InfraPlanProject(infra, infraPath, infraInfo.Namespace, infraOwner, compilation, types, infraInputs, contextOwners);
        foreach (var selected in selections)
        {
            var plan = await TryCreatePlanForContextAsync(target, project, evidence, selected, token);
            if (plan is not null) plans.Add(plan);
        }
    }

    internal static Dictionary<string, (string Domain, string Owner, string Reason, string Declaration)>? TryMergeContextOwners(
        Dictionary<string, (string Domain, string Owner, string Reason, string Declaration)> domainContexts,
        Dictionary<string, (string Domain, string Owner, string Reason, string Declaration)> infraContexts,
        Compilation compilation, string infraNamespace, ConventionGenerationPreflight.ProjectLayout infraLayout)
    {
        var merged = new Dictionary<string, (string Domain, string Owner, string Reason, string Declaration)>(domainContexts, StringComparer.Ordinal);
        foreach (var entry in infraContexts)
        {
            if (!merged.TryGetValue(entry.Key, out var previous))
            {
                merged[entry.Key] = entry.Value;
                continue;
            }
            var defaultDomain = ResolveContextDefaultDomain(entry.Key, compilation, infraNamespace, infraLayout);
            if (!TryMergeContextOwnerEntry(previous, entry.Value, defaultDomain, out var combined))
                return null;
            merged[entry.Key] = combined;
        }
        return merged;
    }

    private static string ResolveContextDefaultDomain(string contextMetadataName, Compilation compilation,
        string infraNamespace, ConventionGenerationPreflight.ProjectLayout infraLayout)
    {
        if (!infraLayout.IsFoldersLayout) return infraLayout.DefaultDomain;
        return compilation.GetTypeByMetadataName(contextMetadataName) is { } contextType &&
               ResolveNamespaceOwnerKey(contextType, infraNamespace, infraLayout) is { } inferred
            ? inferred.Domain
            : "";
    }

    private static bool TryMergeContextOwnerEntry(
        (string Domain, string Owner, string Reason, string Declaration) previous,
        (string Domain, string Owner, string Reason, string Declaration) current,
        string defaultDomain,
        out (string Domain, string Owner, string Reason, string Declaration) combined)
    {
        var previousDomain = previous.Domain.Length > 0 ? previous.Domain : defaultDomain;
        var currentDomain = current.Domain.Length > 0 ? current.Domain : defaultDomain;
        if (previousDomain != currentDomain || previous.Owner != current.Owner || previous.Reason != current.Reason)
        {
            combined = default;
            return false;
        }
        combined = current.Domain.Length > 0
            ? current
            : (previous.Domain, current.Owner, current.Reason, current.Declaration);
        return true;
    }

    private static async Task<Plan?> TryCreatePlanForContextAsync(AggregateTarget target, InfraPlanProject infra,
        string evidence, INamedTypeSymbol? selected, CancellationToken token)
    {
        if (selected is not null &&
            (target.DomainInputs.OwnershipTargets.Contains(CodeFixSymbolFacts.MetadataName(selected)) ||
             infra.Inputs.OwnershipTargets.Contains(CodeFixSymbolFacts.MetadataName(selected)))) return null;
        var contextName = selected?.Name ?? infra.Owner.Subdomain + "Context";
        var contextNamespace = selected?.ContainingNamespace.ToDisplayString() ?? infra.Namespace + "." + infra.Owner.NamespacePrefix;
        var contextSymbol = selected?.ToDisplayString() ?? contextNamespace + "." + contextName;
        if (!IsContextSelectionCompatible(infra, target.Owner, selected, contextNamespace, contextName, token))
            return null;
        var contextDocument = await ResolveEditableContextHomeAsync(infra.Project, selected, token);
        if (selected is not null && contextDocument is null) return null;
        var contextEvidence = await TryCollectContextEvidenceAsync(infra.Project, infra.Directory, infra.ContextOwners, selected, token);
        if (contextEvidence is null) return null;
        var contextPath = ResolveContextPlanSourcePath(infra, selected, contextDocument,
            contextName, contextSymbol, token);
        if (contextPath is null) return null;
        var planName = target.Entity.Name + "." + contextName + ".aggregate-review.md";
        var planPath = Path.Combine(Path.GetDirectoryName(contextPath)!, planName);
        if (!SafeDestination(infra.Directory, planPath) ||
            WorkspaceDestinationConflict(target.Domain.Solution, planPath, infra.Project.Id, token)) return null;
        return BuildAggregatePlan(target, infra, (contextSymbol, selected is null, contextPath), planPath, evidence, contextEvidence);
    }

    private static bool IsContextSelectionCompatible(InfraPlanProject infra, OwnerKey targetOwner,
        INamedTypeSymbol? selected, string contextNamespace, string contextName, CancellationToken token)
    {
        if (selected is null)
            return !ConventionGenerationPreflight.SymbolCollision(infra.Compilation, contextNamespace, contextName, token);
        return IsSelectedContextCompatible(infra, targetOwner, selected);
    }

    private static string? ResolveContextPlanSourcePath(InfraPlanProject infra,
        INamedTypeSymbol? selected, Document? contextDocument, string contextName,
        string contextSymbol, CancellationToken token)
    {
        var ownerPath = infra.Owner.NamespacePrefix.Replace('.', Path.DirectorySeparatorChar);
        var contextPath = contextDocument?.FilePath ?? Path.Combine(infra.Directory, ownerPath, contextName + ".cs");
        if (!TryFullPath(contextPath, out contextPath) ||
            !ConventionGenerationPreflight.IsUnderDirectory(contextPath, infra.Directory)) return null;
        if (selected is null && (infra.Types.Any(type => string.Equals(CodeFixSymbolFacts.MetadataName(type), contextSymbol, StringComparison.OrdinalIgnoreCase)) ||
                !SafeDocumentDestination(infra.Project, infra.Directory, contextPath, token))) return null;
        return contextPath;
    }

    private static bool IsSelectedContextCompatible(InfraPlanProject infra, OwnerKey targetOwner, INamedTypeSymbol selected)
    {
        var compilation = infra.Compilation;
        var infraOwner = infra.Owner;
        var ownerAttribute = compilation.GetTypeByMetadataName(OwnershipAttributeMetadataName);
        if (compilation.Assembly.GetAttributes().Any(attribute => Same(attribute.AttributeClass, ownerAttribute) &&
                attribute.ConstructorArguments.FirstOrDefault().Value is INamedTypeSymbol target &&
                SymbolEqualityComparer.Default.Equals(target, selected))) return false;
        var declaredOwners = selected.GetAttributes().Where(attribute => Same(attribute.AttributeClass, ownerAttribute)).ToArray();
        if (declaredOwners.Length > 1) return false;
        var hasExplicitDomain = false;
        var reasonedOwner = false;
        if (declaredOwners.Length == 1)
        {
            if (!MatchesSubdomainAttribute(declaredOwners[0], infraOwner, out hasExplicitDomain)) return false;
            reasonedOwner = HasReason(declaredOwners[0]);
        }
        if (!ApplyDeclaredContextOwner(infra, selected, ref hasExplicitDomain, ref reasonedOwner)) return false;
        if (!infraOwner.WithExplicitDomain(hasExplicitDomain).MatchesAcrossLayers(targetOwner)) return false;
        return ConventionNames.IsConventionalContextName(selected.Name, infraOwner.Domain, infraOwner.Subdomain, infraOwner.HasFolderDomain) ||
            reasonedOwner;
    }

    private static bool ApplyDeclaredContextOwner(InfraPlanProject infra, INamedTypeSymbol selected,
        ref bool hasExplicitDomain, ref bool reasonedOwner)
    {
        if (!infra.ContextOwners.TryGetValue(CodeFixSymbolFacts.MetadataName(selected), out var declared)) return true;
        if (declared.Domain.Length > 0 && declared.Domain != infra.Owner.Domain) return false;
        if (declared.Owner.Length > 0 && declared.Owner != infra.Owner.Subdomain) return false;
        hasExplicitDomain |= declared.Domain.Length > 0;
        reasonedOwner |= declared.Owner == infra.Owner.Subdomain && !string.IsNullOrWhiteSpace(declared.Reason);
        return true;
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
        Dictionary<string, (string Domain, string Owner, string Reason, string Declaration)> contextOwners,
        INamedTypeSymbol? selected, CancellationToken token)
    {
        var contextEvidence = new StringBuilder();
        if (selected is null) return contextEvidence.ToString();
        if (contextOwners.TryGetValue(CodeFixSymbolFacts.MetadataName(selected), out var selectedOwnership))
        {
            var ownerLabel = selectedOwnership.Domain.Length > 0
                ? selectedOwnership.Domain + "/" + selectedOwnership.Owner
                : selectedOwnership.Owner;
            contextEvidence.AppendLine("Selected context ownership input: " + EscapeMarkdown(ownerLabel) +
                "; reason: " + EscapeMarkdown(selectedOwnership.Reason));
        }
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

    internal static async Task<ParsedInputs?> ReadProjectInputs(Project project, CancellationToken token)
    {
        var files = project.AdditionalDocuments.Where(ConventionGenerationPreflight.IsInputsDocument).Take(2).ToArray();
        if (files.Length > 1) return null;
        var contexts = new Dictionary<string, (string Domain, string Owner, string Reason, string Declaration)>(StringComparer.Ordinal);
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
        Dictionary<string, (string Domain, string Owner, string Reason, string Declaration)> contexts,
        HashSet<string> ownershipTargets, StringBuilder evidence, CancellationToken token)
    {
        var text = await input.GetTextAsync(token);
        var inputRoot = ConventionInputSyntax.Parse(text, input.FilePath ?? input.Name, token, out _);
        if (inputRoot is null || !ConventionInputSyntax.SupportsGeneration(inputRoot, token) || HasRoleOverride(inputRoot)) return false;
        var attributes = inputRoot.AttributeLists.SelectMany(list => list.Attributes).ToArray();
        if (!TryCollectProjectInputRecords(attributes, contexts, ownershipTargets, token)) return false;
        evidence.AppendLine("- Supplied inputs for " + Escape(project.Name) + "; SHA-256 " + Fingerprint(text) +
            "; records: " + attributes.Length + ". Full inventory retained in DrnConventionInputs.g.cs.");
        return true;
    }

    private static bool TryCollectProjectInputRecords(AttributeSyntax[] attributes,
        Dictionary<string, (string Domain, string Owner, string Reason, string Declaration)> contexts,
        HashSet<string> ownershipTargets, CancellationToken token)
    {
        foreach (var attribute in attributes)
        {
            token.ThrowIfCancellationRequested();
            switch (ConventionInputSyntax.Kind(attribute))
            {
                case ConventionInputSyntax.OwnershipKind:
                    if (attribute.ArgumentList is not { Arguments.Count: > 0 } arguments ||
                        arguments.Arguments[0].NameEquals is not null || arguments.Arguments[0].NameColon is not null ||
                        ConventionInputSyntax.String(arguments.Arguments[0].Expression, token) is not { } target) return false;
                    ownershipTargets.Add(target);
                    break;
                case ConventionInputSyntax.ContextKind:
                    if (!TryReadContextAttributeInput(attribute, contexts, token)) return false;
                    break;
            }
        }
        return true;
    }

    private static bool TryReadContextAttributeInput(AttributeSyntax attribute,
        Dictionary<string, (string Domain, string Owner, string Reason, string Declaration)> contexts, CancellationToken token)
    {
        if (attribute.ArgumentList is not { Arguments.Count: > 0 } argumentList) return false;
        var arguments = argumentList.Arguments;
        if (arguments[0].NameEquals is not null || arguments[0].NameColon is not null ||
            ConventionInputSyntax.String(arguments[0].Expression, token) is not { } name)
            return false;
        if (!TryReadSingleOptionalField(arguments, ConventionInputSyntax.DomainField, token, out var declaredDomain) ||
            !TryReadSingleOptionalField(arguments, ConventionInputSyntax.SubdomainField, token, out var declaredOwner) ||
            !TryReadSingleOptionalField(arguments, ConventionInputSyntax.ReasonField, token, out var reason))
            return false;
        if (declaredDomain.Length > 0 && declaredOwner.Length == 0 ||
            contexts.TryGetValue(name, out var previous) &&
            (previous.Domain != declaredDomain || previous.Owner != declaredOwner || previous.Reason != reason))
            return false;
        contexts[name] = (declaredDomain, declaredOwner, reason, attribute.ToString());
        return true;
    }

    private sealed record InfraPlanProject(Project Project, string ProjectPath, string Namespace, OwnerKey Owner, Compilation Compilation,
        INamedTypeSymbol[] Types, ParsedInputs Inputs, Dictionary<string, (string Domain, string Owner, string Reason, string Declaration)> ContextOwners)
    {
        internal string Directory { get; } = Path.GetDirectoryName(ProjectPath)!;
    }

    private sealed record AggregateTarget(Project Domain, Compilation DomainCompilation, string DomainPath,
        ParsedInputs DomainInputs, INamedTypeSymbol Entity, string EntityName, OwnerKey Owner);

    internal sealed record ParsedInputs(string Evidence, Dictionary<string, (string Domain, string Owner, string Reason, string Declaration)> Contexts,
        HashSet<string> OwnershipTargets);

    private static string Fingerprint(SourceText text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    private static string Escape(string? value) => EscapeMarkdown(value);
    private sealed record Plan(ProjectId Project, string ProjectName, string Context, string Name, string Path, string RelativePath, string Content);
}
