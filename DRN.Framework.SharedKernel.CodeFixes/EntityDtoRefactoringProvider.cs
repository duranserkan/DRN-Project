using System.Collections.Immutable;
using DRN.Framework.SharedKernel.Analyzers.Conventions;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.CodeRefactorings;
using static DRN.Framework.SharedKernel.CodeFixes.CodeFixDestinationGuard;
using static DRN.Framework.SharedKernel.CodeFixes.CodeFixSymbolFacts;

namespace DRN.Framework.SharedKernel.CodeFixes;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(EntityDtoRefactoringProvider)), Shared]
public sealed class EntityDtoRefactoringProvider : CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var solution = context.Document.Project.Solution;
        if (context.Span.IsEmpty || !solution.Workspace.CanApplyChange(ApplyChangesKind.AddDocument) ||
            !solution.Workspace.CanApplyChange(ApplyChangesKind.ChangeDocument) ||
            ConventionGenerationPreflight.Identity(context.Document.Project).Role != "Domain") return;
        var selection = await ReadSelection(context.Document, context.Span, context.CancellationToken);
        if (selection is null) return;
        foreach (var project in solution.Projects)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var candidate = await Plan(context.Document, selection, project, context.CancellationToken);
            if (candidate is null) continue;
            var edited = await AddProjectionAsync(context.Document, selection, project.Id, candidate, context.CancellationToken);
            if (edited is null) continue;
            if (!ReferenceEquals(solution.Workspace.CurrentSolution, solution) || !SafeExistingDocument(context.Document)) return;
            context.RegisterRefactoring(CodeAction.Create(
                "Add DTO and mapping: " + project.Name + " / " + candidate.RelativeDtoPath + " (" +
                string.Join(", ", selection.Properties.Select(property => property.Name + (RequiresInitialization(property) ? " required" : ""))) + ")",
                token => Apply(context.Document, edited, project, candidate, token),
                "DrnEntityDto:" + project.Id + ":" + MetadataName(selection.Entity)));
        }
    }

    private static async Task<Solution> Apply(Document document, Document edited, Project target, DtoPlan plan, CancellationToken token)
    {
        var snapshot = document.Project.Solution;
        token.ThrowIfCancellationRequested();
        if (!ReferenceEquals(snapshot.Workspace.CurrentSolution, snapshot)) return snapshot.Workspace.CurrentSolution;
        var formatted = await Formatter.FormatAsync(edited, Formatter.Annotation, cancellationToken: token);
        token.ThrowIfCancellationRequested();
        if (!SafeExistingDocument(document) || target.FilePath is null || !TryFullPath(target.FilePath, out var projectPath) ||
            !SafeDocumentDestination(target, Path.GetDirectoryName(projectPath)!, plan.DtoPath, token) ||
            !ReferenceEquals(snapshot.Workspace.CurrentSolution, snapshot))
            return snapshot.Workspace.CurrentSolution;
        return formatted.Project.Solution;
    }

    private static async Task<Document?> AddProjectionAsync(Document document, Selection selection, ProjectId targetId,
        DtoPlan plan, CancellationToken token)
    {
        var root = await document.GetSyntaxRootAsync(token);
        var propertySpan = selection.Properties[0].DeclaringSyntaxReferences[0].Span;
        var declaration = root?.FindNode(propertySpan).FirstAncestorOrSelf<TypeDeclarationSyntax>();
        if (root is null || declaration is null || SyntaxFactory.ParseMemberDeclaration(plan.MapperSource) is not MethodDeclarationSyntax mapper)
            return null;
        var marker = new SyntaxAnnotation();
        var updated = declaration.AddMembers(mapper.WithAdditionalAnnotations(marker, Formatter.Annotation));
        var dtoId = DocumentId.CreateNewId(targetId);
        var proposed = document.WithSyntaxRoot(root.ReplaceNode(declaration, updated)).Project.Solution
            .AddDocument(dtoId, plan.DtoName + ".cs", SourceText.From(plan.DtoSource),
                plan.RelativeDtoPath.Split('/')[..^1], plan.DtoPath);
        var edited = proposed.GetDocument(document.Id)!;
        var model = await edited.GetSemanticModelAsync(token);
        var added = (await edited.GetSyntaxRootAsync(token))?.GetAnnotatedNodes(marker).SingleOrDefault();
        var expectedName = plan.DtoNamespace + "." + plan.DtoName;
        if (added is not MethodDeclarationSyntax method || model?.GetDeclaredSymbol(method, token) is not IMethodSymbol symbol ||
            symbol.ReturnType is not INamedTypeSymbol { TypeKind: TypeKind.Class } returnType ||
            proposed.GetProject(returnType.ContainingAssembly, token) is not { } targetProject ||
            targetProject.Id != targetId || MetadataName(returnType) != expectedName)
            return null;
        if (await IntroducesBlockingDiagnosticsAsync(document, proposed, targetId, method.FullSpan, token))
            return null;

        return await ChangesExistingBindingsAsync(document, proposed, symbol, method.FullSpan, token) ? null : edited;
    }

    private static async Task<bool> ChangesExistingBindingsAsync(Document document, Solution proposed, IMethodSymbol symbol,
        TextSpan insertion, CancellationToken token)
    {
        // Ask the compiler which callers in the proposed solution actually bind to the new mapper.
        var references = await SymbolFinder.FindReferencesAsync(symbol, proposed, token);
        foreach (var location in references.Where(reference => SymbolEqualityComparer.Default.Equals(reference.Definition, symbol))
                     .SelectMany(reference => reference.Locations))
        {
            token.ThrowIfCancellationRequested();
            if (location.IsImplicit) continue;
            var original = document.Project.Solution.GetDocument(location.Document.Id);
            if (original is null || await original.GetSemanticModelAsync(token) is not { } before) return true;
            var position = location.Location.SourceSpan.Start;
            // Appending the unformatted member was the only edit to this tree.
            if (original.Id == document.Id && position >= insertion.End) position -= insertion.Length;
            var name = (await before.SyntaxTree.GetRootAsync(token)).FindToken(position).Parent;
            if (name is not null && BindsToMethod(before, name, token)) return true;
        }
        return false;
    }

    private static bool BindsToMethod(SemanticModel model, SyntaxNode name, CancellationToken token)
    {
        var call = name.FirstAncestorOrSelf<InvocationExpressionSyntax>();
        var site = call is not null && call.Expression.Span.End == name.Span.End ? call : name;
        return model.GetSymbolInfo(site, token).Symbol is IMethodSymbol;
    }

    private static async Task<bool> IntroducesBlockingDiagnosticsAsync(Document document, Solution proposed,
        ProjectId targetId, TextSpan insertion, CancellationToken token)
    {
        var original = document.Project.Solution;
        var graph = proposed.GetProjectDependencyGraph();
        // The Domain project and its consumers are already dependents of the Contract target.
        foreach (var id in graph.GetProjectsThatTransitivelyDependOnThisProject(targetId).Prepend(targetId))
        {
            token.ThrowIfCancellationRequested();
            var project = proposed.GetProject(id)!;
            if (!project.SupportsCompilation) continue;
            if (await ProjectIntroducesBlockingDiagnosticsAsync(original.GetProject(id)!, project, document.Id, insertion, token))
                return true;
        }
        return false;
    }

    private static async Task<bool> ProjectIntroducesBlockingDiagnosticsAsync(Project original, Project proposed,
        DocumentId editedDocument, TextSpan insertion, CancellationToken token)
    {
        if (await proposed.GetCompilationAsync(token) is not { } after) return true;
        var blockers = after.GetDiagnostics(token).Where(BlocksProjection).ToArray();
        if (blockers.Length == 0) return false;
        if (await original.GetCompilationAsync(token) is not { } before) return true;
        var existing = before.GetDiagnostics(token).Where(BlocksProjection)
            .Select(diagnostic => BlockingDiagnosticKey(original.Solution, diagnostic, editedDocument, insertion))
            .CountBy(key => key).ToDictionary();
        foreach (var diagnostic in blockers)
        {
            var key = BlockingDiagnosticKey(proposed.Solution, diagnostic);
            if (!existing.TryGetValue(key, out var count) || count == 0) return true;
            existing[key] = count - 1;
        }
        return false;
    }

    private static bool BlocksProjection(Diagnostic diagnostic) =>
        diagnostic.Severity == DiagnosticSeverity.Error || diagnostic.Id == "CS0436";

    private static (string Id, DiagnosticSeverity Severity, string Message, DocumentId? Document, string? Path, TextSpan Span)
        BlockingDiagnosticKey(Solution solution, Diagnostic diagnostic, DocumentId? editedDocument = null, TextSpan insertion = default)
    {
        var location = diagnostic.Location;
        var tree = location.SourceTree;
        var documentId = tree is null ? null : solution.GetDocument(tree)?.Id;
        var span = location.SourceSpan;
        if (editedDocument is not null && documentId == editedDocument)
        {
            // Compare locations in the proposed tree's coordinates, including errors after the inserted member.
            if (span.Start >= insertion.Start) span = new TextSpan(span.Start + insertion.Length, span.Length);
            else if (span.End > insertion.Start) span = new TextSpan(span.Start, span.Length + insertion.Length);
        }
        return (diagnostic.Id, diagnostic.Severity, diagnostic.GetMessage(), documentId, tree?.FilePath, span);
    }

    private static async Task<Selection?> ReadSelection(Document document, TextSpan span, CancellationToken token)
    {
        if (span.IsEmpty || document.GetLinkedDocumentIds().Length != 0) return null;
        var syntaxRoot = await document.GetSyntaxRootAsync(token);
        if (syntaxRoot is null || span.End > syntaxRoot.FullSpan.End) return null;
        var declarations = CollectSelectedPropertyNodes(syntaxRoot, span, token);
        if (declarations is null || !SafeExistingDocument(document)) return null;
        var root = await ClassificationEdit.FindEditableRoot(document, span.Start, token);
        var model = await document.GetSemanticModelAsync(token);
        if (root is null || model is null ||
            model.GetDiagnostics(span, token).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)) return null;
        return await TryBuildEntitySelectionAsync(document, model, declarations, token);
    }

    private static List<PropertyDeclarationSyntax>? CollectSelectedPropertyNodes(SyntaxNode syntaxRoot, TextSpan span,
        CancellationToken token)
    {
        var declarations = new List<PropertyDeclarationSyntax>();
        TypeDeclarationSyntax? containingType = null;
        foreach (var declaration in syntaxRoot.DescendantNodes(span).OfType<PropertyDeclarationSyntax>())
        {
            token.ThrowIfCancellationRequested();
            if (!declaration.Span.IntersectsWith(span)) continue;
            if (!span.Contains(declaration.Span) || declaration.Modifiers.Any(SyntaxKind.PartialKeyword) ||
                declaration.ContainsDirectives || declaration.AttributeLists.Any(list => list.Target is not null) ||
                declaration.Parent is not TypeDeclarationSyntax parent) return null;
            containingType ??= parent;
            if (!ReferenceEquals(containingType, parent)) return null;
            declarations.Add(declaration);
            if (declarations.Count > 64) return null;
        }
        if (containingType is null) return null;
        declarations.Sort(static (left, right) => left.SpanStart.CompareTo(right.SpanStart));
        return HasConditionalCompilationConflict(syntaxRoot, containingType, declarations, token) ? null : declarations;
    }

    private static bool HasConditionalCompilationConflict(SyntaxNode root, TypeDeclarationSyntax containingType,
        List<PropertyDeclarationSyntax> declarations, CancellationToken token)
    {
        if (containingType.CloseBraceToken.IsMissing) return true;
        if (!root.ContainsDirectives) return false;
        var insertionPoint = containingType.CloseBraceToken.FullSpan.Start;
        var depth = 0;
        var index = 0;
        for (var directive = root.GetFirstDirective(); directive is not null; directive = directive.GetNextDirective())
        {
            token.ThrowIfCancellationRequested();
            var position = directive.SpanStart;
            while (index < declarations.Count && declarations[index].SpanStart < position)
            {
                if (depth > 0) return true;
                index++;
            }
            if (position >= insertionPoint) break;
            if (directive is IfDirectiveTriviaSyntax) depth++;
            else if (directive is EndIfDirectiveTriviaSyntax && depth > 0) depth--;
        }
        return depth > 0;
    }

    private static bool HasConditionalTypeHeader(TypeDeclarationSyntax declaration, CancellationToken token)
    {
        if (!declaration.ContainsDirectives) return false;
        var headerEnd = declaration.OpenBraceToken.RawKind == 0 ? declaration.Span.End : declaration.OpenBraceToken.SpanStart;
        var header = TextSpan.FromBounds(declaration.FullSpan.Start, headerEnd);
        foreach (var trivia in declaration.DescendantTrivia(header))
        {
            token.ThrowIfCancellationRequested();
            if (trivia.SpanStart >= declaration.SpanStart &&
                trivia.GetStructure() is ConditionalDirectiveTriviaSyntax or ElseDirectiveTriviaSyntax or EndIfDirectiveTriviaSyntax)
                return true;
            if (!trivia.IsKind(SyntaxKind.DisabledTextTrivia)) continue;
            var disabled = trivia.ToString();
            // A disabled attribute or partial modifier can change the classification of the following type.
            var probe = SyntaxFactory.ParseCompilationUnit(disabled + "\nclass __Header {}",
                options: (CSharpParseOptions)declaration.SyntaxTree.Options);
            if (probe.FindToken(disabled.Length + 1).Parent is ClassDeclarationSyntax headerType &&
                (headerType.AttributeLists.Count != 0 || headerType.Modifiers.Any(SyntaxKind.PartialKeyword))) return true;
        }
        return false;
    }

    private static async Task<Selection?> TryBuildEntitySelectionAsync(Document document, SemanticModel model,
        List<PropertyDeclarationSyntax> declarations, CancellationToken token)
    {
        var properties = ImmutableArray.CreateBuilder<IPropertySymbol>();
        INamedTypeSymbol? entity = null;
        foreach (var declaration in declarations)
        {
            token.ThrowIfCancellationRequested();
            if (model.GetDeclaredSymbol(declaration, token) is not IPropertySymbol property ||
                !IsEligibleEntityProperty(property)) return null;
            entity ??= property.ContainingType;
            if (!SymbolEqualityComparer.Default.Equals(entity, property.ContainingType)) return null;
            properties.Add(property);
        }
        return entity is null || !await IsEditablePublicEntityAsync(document, model.Compilation, entity, token)
            ? null
            : new(entity, properties.ToImmutable());
    }

    private static bool IsEligibleEntityProperty(IPropertySymbol property) =>
        !property.IsStatic && !property.IsIndexer && property.PartialDefinitionPart is null &&
        property.DeclaredAccessibility == Accessibility.Public &&
        property.GetMethod?.DeclaredAccessibility == Accessibility.Public &&
        !property.IsOverride && property.ExplicitInterfaceImplementations.Length == 0 &&
        property.GetMethod.GetAttributes().Length == 0 &&
        property.GetMethod.GetReturnTypeAttributes().Length == 0 &&
        (property.SetMethod is null || property.SetMethod.GetAttributes().Length == 0);

    private static async Task<bool> IsEditablePublicEntityAsync(Document document, Compilation compilation,
        INamedTypeSymbol entity, CancellationToken token)
    {
        var entityBase = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.SourceKnownEntity");
        if (entity.IsAbstract || IsOpen(entity) || !IsPublicType(entity) || !Inherits(entity, entityBase)) return false;
        foreach (var type in Bases(entity))
        {
            if (!await HasStableDeclarationsAsync(document.Project.Solution, compilation, type, token)) return false;
        }
        return await ArePartsEditableAsync(document.Project, entity, rejectSpanErrors: false, token);
    }

    private static async Task<bool> HasStableDeclarationsAsync(Solution solution, Compilation compilation,
        INamedTypeSymbol type, CancellationToken token)
    {
        Compilation? definingCompilation = compilation;
        var definition = type;
        if (!SymbolEqualityComparer.Default.Equals(type.ContainingAssembly, compilation.Assembly))
        {
            var owner = solution.GetProject(type.ContainingAssembly, token);
            definingCompilation = owner is null ? null : await owner.GetCompilationAsync(token);
            definition = definingCompilation?.Assembly.GetTypeByMetadataName(MetadataName(type)) ?? type;
        }
        var partial = false;
        foreach (var reference in definition.DeclaringSyntaxReferences)
        {
            if (await reference.GetSyntaxAsync(token) is not TypeDeclarationSyntax declaration) continue;
            if (HasConditionalTypeHeader(declaration, token)) return false;
            partial |= declaration.Modifiers.Any(SyntaxKind.PartialKeyword);
        }
        return !partial || definingCompilation is not null && !await HasInactiveDeclarationAsync(definingCompilation, type.Name, token);
    }

    private static async Task<bool> HasInactiveDeclarationAsync(Compilation compilation, string typeName, CancellationToken token)
    {
        // A wholly disabled part has no symbol. Its class name can also be split from the keyword by a directive.
        foreach (var tree in compilation.SyntaxTrees)
        {
            var root = await tree.GetRootAsync(token);
            if (!root.ContainsDirectives) continue;
            foreach (var trivia in root.DescendantTrivia(descendIntoChildren: node => node.ContainsDirectives))
            {
                token.ThrowIfCancellationRequested();
                if (trivia.IsKind(SyntaxKind.DisabledTextTrivia) && ContainsInactiveTypeName(trivia, typeName, (CSharpParseOptions)tree.Options))
                    return true;
            }
        }
        return false;
    }

    private static bool ContainsInactiveTypeName(SyntaxTrivia trivia, string typeName, CSharpParseOptions options)
    {
        var previous = SyntaxKind.None;
        foreach (var part in SyntaxFactory.ParseTokens(trivia.ToString(), options: options))
        {
            if (part.IsKind(SyntaxKind.IdentifierToken) && part.ValueText == typeName &&
                previous is SyntaxKind.None or SyntaxKind.ClassKeyword) return true;
            previous = part.Kind();
        }
        return false;
    }

    private static async Task<DtoPlan?> Plan(Document document, Selection selection, Project target, CancellationToken token)
    {
        var resolved = await ResolveDtoCompilationsAsync(document, selection, target, token);
        if (resolved is null) return null;
        var destinations = TryBuildDtoDestinations(target, selection, resolved, token);
        if (destinations is null) return null;
        var typeAttributes = TypeClassifications(selection.Entity, resolved.SourceCompilation, resolved.TargetCompilation);
        if (typeAttributes is null) return null;
        var fields = TryBuildDtoFields(selection, resolved.Dto, destinations.DtoName, resolved.SourceCompilation, resolved.TargetCompilation);
        return fields is null ? null : Render(fields, typeAttributes, destinations);
    }

    private static async Task<DtoCompilations?> ResolveDtoCompilationsAsync(Document document, Selection selection,
        Project target, CancellationToken token)
    {
        var source = document.Project;
        var sourceIdentity = ConventionGenerationPreflight.Identity(source);
        var targetIdentity = ConventionGenerationPreflight.Identity(target);
        if (sourceIdentity.Role != "Domain" || targetIdentity.Role != "Contract" || target.Language != LanguageNames.CSharp ||
            !source.ProjectReferences.Any(reference => reference.ProjectId == target.Id &&
                (reference.Aliases.IsEmpty || reference.Aliases.Contains("global", StringComparer.Ordinal))) ||
            target.FilePath is null || !TryFullPath(target.FilePath, out var targetProjectPath)) return null;
        var sourceInputs = await ReadInputsWithoutRoleOverrideAsync(source, required: false, token);
        if (sourceInputs is null || await HasRoleOverrides(target, token)) return null;
        var targetRoot = Path.GetDirectoryName(targetProjectPath)!;
        return await ResolveDtoCompilationModelsAsync((source, sourceIdentity.Namespace),
            (target, targetIdentity.Namespace, targetRoot), selection, sourceInputs, token);
    }

    private static async Task<DtoCompilations?> ResolveDtoCompilationModelsAsync(
        (Project Project, string Namespace) source, (Project Project, string Namespace, string Root) target,
        Selection selection, CompilationUnitSyntax sourceInputs, CancellationToken token)
    {
        var sourceCompilation = await source.Project.GetCompilationAsync(token);
        var targetCompilation = await target.Project.GetCompilationAsync(token);
        if (sourceCompilation is null || targetCompilation is null ||
            !selection.Entity.ContainingAssembly.Identity.Equals(sourceCompilation.Assembly.Identity)) return null;
        var dto = targetCompilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.Dto");
        var entityBase = targetCompilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.SourceKnownEntity");
        if (dto is null || entityBase is null || !targetCompilation.IsSymbolAccessibleWithin(dto, targetCompilation.Assembly) ||
            sourceCompilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.Dto") is not { } sourceDto ||
            !sourceDto.ContainingAssembly.Identity.Equals(dto.ContainingAssembly.Identity)) return null;
        var sourceNamespace = selection.Entity.ContainingNamespace.ToDisplayString();
        if (!ValidNamespace(source.Namespace) || !ValidNamespace(target.Namespace) ||
            !sourceNamespace.StartsWith(source.Namespace + ".", StringComparison.Ordinal)) return null;
        var area = sourceNamespace[(source.Namespace.Length + 1)..];
        if (!ValidNamespace(area) || !PreservesOwnership(selection.Entity, sourceCompilation, sourceInputs, area.Split('.')[0], token))
            return null;
        var folder = ConventionNames.DtoFolder(ConventionGenerationPreflight.EntityStem(selection.Entity.Name));
        if (!ConventionGenerationPreflight.ValidNamespaceSegment(folder)) return null;
        var dtoArea = area.Split('.')[0] + "." + folder;
        return new(sourceCompilation, targetCompilation, dto, target.Root, target.Namespace + "." + dtoArea, dtoArea);
    }

    private static DtoDestinations? TryBuildDtoDestinations(Project target, Selection selection, DtoCompilations resolved, CancellationToken token)
    {
        var dtoName = ConventionGenerationPreflight.EntityStem(selection.Entity.Name) + "Dto";
        if (!SyntaxFacts.IsValidIdentifier(dtoName) ||
            Bases(selection.Entity).Any(type => type.GetMembers("ToDto").Any(ConflictsWithMapper)) ||
            ConventionGenerationPreflight.SymbolCollision(resolved.TargetCompilation, resolved.DtoNamespace, dtoName, token)) return null;
        var dtoPath = Path.Combine(resolved.TargetRoot, resolved.Area.Replace('.', Path.DirectorySeparatorChar), dtoName + ".cs");
        if (!SafeDocumentDestination(target, resolved.TargetRoot, dtoPath, token)) return null;
        return new(resolved.DtoNamespace, dtoName, dtoPath, Path.GetRelativePath(resolved.TargetRoot, dtoPath).Replace('\\', '/'));

        bool ConflictsWithMapper(ISymbol member) => member is not IMethodSymbol method || method.Arity == 0 &&
            (method.Parameters.IsEmpty || !method.IsStatic && resolved.SourceCompilation.IsSymbolAccessibleWithin(method, selection.Entity) &&
                method.Parameters.All(parameter => parameter.IsOptional || parameter.IsParams));
    }

    private static List<(string Name, string Type, string Attributes, bool Required)>? TryBuildDtoFields(
        Selection selection, INamedTypeSymbol dto, string dtoName, Compilation sourceCompilation, Compilation targetCompilation)
    {
        var fields = new List<(string Name, string Type, string Attributes, bool Required)>();
        var format = SymbolDisplayFormat.FullyQualifiedFormat.WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);
        foreach (var property in selection.Properties)
        {
            if (string.Equals(property.Name, "entity", StringComparison.OrdinalIgnoreCase) ||
                property.Name == dtoName || Bases(dto).Any(type => type.GetMembers(property.Name).Length != 0) ||
                IsInternalNumericForeignKey(property) ||
                !ScalarAvailable(property.Type, sourceCompilation, targetCompilation)) return null;
            var attributes = ClassificationAttributes(property.GetAttributes(), sourceCompilation, targetCompilation);
            if (attributes is null || HasDisallowedInterfaceClassification(selection.Entity, property, sourceCompilation, targetCompilation))
                return null;
            fields.Add((EscapeIdentifier(property.Name), property.Type.ToDisplayString(format), attributes, RequiresInitialization(property)));
        }
        return fields;
    }

    private static bool HasDisallowedInterfaceClassification(INamedTypeSymbol entity, IPropertySymbol property,
        Compilation sourceCompilation, Compilation targetCompilation)
    {
        foreach (var contract in entity.AllInterfaces)
        {
            foreach (var member in contract.GetMembers().OfType<IPropertySymbol>())
            {
                if (!SymbolEqualityComparer.Default.Equals(entity.FindImplementationForInterfaceMember(member), property)) continue;
                var inherited = ClassificationAttributes(member.GetAttributes(), sourceCompilation, targetCompilation);
                if (inherited is null || inherited.Length > 0 ||
                    HasClassificationOrErrorAttribute(contract.GetAttributes(), sourceCompilation) ||
                    member.Type is INamedTypeSymbol memberType &&
                    HasClassificationOrErrorAttribute(memberType.GetAttributes(), sourceCompilation))
                    return true;
            }
        }
        return false;
    }

    private static bool IsInternalNumericForeignKey(IPropertySymbol property)
    {
        if (!property.Name.EndsWith("Id", StringComparison.Ordinal)) return false;
        var type = property.Type;
        if (type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments: [var underlying] })
            type = underlying;
        return type.SpecialType == SpecialType.System_Int64;
    }

    private static bool HasClassificationOrErrorAttribute(ImmutableArray<AttributeData> attributes, Compilation compilation)
    {
        var classification = compilation.GetTypeByMetadataName("Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute");
        var ignore = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.IgnoreLogAttribute");
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass is not { TypeKind: not TypeKind.Error } type) return true;
            if (Bases(type).Any(parent => Same(parent, classification) || Same(parent, ignore))) return true;
        }
        return false;
    }

    private static bool ScalarAvailable(ITypeSymbol type, Compilation source, Compilation target)
    {
        if (type is not INamedTypeSymbol named || named.TypeKind == TypeKind.Error) return false;
        if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)
            return named.TypeArguments.Length == 1 && ScalarAvailable(named.TypeArguments[0], source, target);
        if (named.SpecialType is SpecialType.System_Boolean or SpecialType.System_Byte or SpecialType.System_SByte or
            SpecialType.System_Int16 or SpecialType.System_UInt16 or SpecialType.System_Int32 or SpecialType.System_UInt32 or
            SpecialType.System_Int64 or SpecialType.System_UInt64 or SpecialType.System_Single or SpecialType.System_Double or
            SpecialType.System_Decimal or SpecialType.System_Char or SpecialType.System_String)
            return target.GetSpecialType(named.SpecialType) is { TypeKind: not TypeKind.Error } scalar &&
                   scalar.ContainingAssembly.Identity.Equals(named.ContainingAssembly.Identity);
        var name = MetadataName(named);
        return named.TypeKind == TypeKind.Enum
            ? IsAccessibleTargetEnum(named, name, target)
            : IsSupportedSystemValueType(named, name, source, target);
    }

    private static bool IsAccessibleTargetEnum(INamedTypeSymbol named, string name, Compilation target)
    {
        for (var containing = named.ContainingType; containing is not null; containing = containing.ContainingType)
            if (containing.IsGenericType) return false;
        if (target.GetTypeByMetadataName(name) is not { TypeKind: TypeKind.Enum } enumeration ||
            !enumeration.ContainingAssembly.Identity.Equals(named.ContainingAssembly.Identity) ||
            !target.IsSymbolAccessibleWithin(enumeration, target.Assembly)) return false;
        if (SymbolEqualityComparer.Default.Equals(enumeration.ContainingAssembly, target.Assembly)) return true;
        var reference = target.GetMetadataReference(enumeration.ContainingAssembly);
        return reference is not null &&
               (reference.Properties.Aliases.IsEmpty || reference.Properties.Aliases.Contains("global", StringComparer.Ordinal));
    }

    private static bool IsSupportedSystemValueType(INamedTypeSymbol named, string name, Compilation source, Compilation target)
    {
        if (name is not ("System.Guid" or "System.DateTime" or "System.DateTimeOffset" or "System.DateOnly" or "System.TimeOnly" or "System.TimeSpan"))
            return false;
        return target.GetTypeByMetadataName(name) is { } available && source.GetTypeByMetadataName(name) is { } original &&
               SymbolEqualityComparer.Default.Equals(named, original) && available.ContainingAssembly.Identity.Equals(named.ContainingAssembly.Identity);
    }

    private static string? ClassificationAttributes(IEnumerable<AttributeData> attributes, Compilation source, Compilation target,
        string indent = "    ", bool allowFalse = true)
    {
        var rendered = new List<string>();
        foreach (var attribute in attributes)
        {
            if (!TryFormatClassificationAttribute(attribute, source, target, indent, allowFalse, out var formatted))
                return null;
            rendered.Add(formatted);
        }
        return string.Concat(rendered);
    }

    private static bool TryFormatClassificationAttribute(AttributeData attribute, Compilation source, Compilation target,
        string indent, bool allowFalse, out string formatted)
    {
        formatted = "";
        if (attribute.AttributeClass is not { TypeKind: not TypeKind.Error } type || attribute.AttributeConstructor is null) return false;
        var name = MetadataName(type);
        if (name is not ("DRN.Framework.SharedKernel.Attributes.SensitiveAttribute" or
            "DRN.Framework.SharedKernel.Attributes.PersonalAttribute" or "DRN.Framework.SharedKernel.Attributes.IgnoreLogAttribute")) return false;
        if (source.GetTypeByMetadataName(name) is not { } exact || !SymbolEqualityComparer.Default.Equals(type, exact) ||
            target.GetTypeByMetadataName(name) is not { } available || !available.ContainingAssembly.Identity.Equals(type.ContainingAssembly.Identity) ||
            !target.IsSymbolAccessibleWithin(available, target.Assembly)) return false;
        var arguments = TryFormatClassificationArguments(attribute, allowFalse);
        if (arguments is null) return false;
        formatted = indent + "[global::" + name + (arguments.Count == 0 ? "" : "(" + string.Join(", ", arguments) + ")") + "]\n";
        return true;
    }

    private static List<string>? TryFormatClassificationArguments(AttributeData attribute, bool allowFalse)
    {
        var arguments = new List<string>();
        foreach (var argument in attribute.ConstructorArguments)
        {
            if (argument.Value is bool boolean && (allowFalse || boolean)) arguments.Add(boolean ? "true" : "false");
            else return null;
        }
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key != "Justification" || argument.Value.Value is not string reason) return null;
            arguments.Add("Justification = " + SymbolDisplay.FormatLiteral(reason, true));
        }
        return arguments;
    }

    private static string? TypeClassifications(INamedTypeSymbol entity, Compilation source, Compilation target)
    {
        var classification = source.GetTypeByMetadataName("Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute");
        var ignore = source.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.IgnoreLogAttribute");
        var rawAttributes = Bases(entity).SelectMany(type => type.GetAttributes()).ToArray();
        if (rawAttributes.Any(attribute => attribute.AttributeClass is null or { TypeKind: TypeKind.Error })) return null;
        var attributes = rawAttributes.Where(attribute =>
            attribute.AttributeClass is { } type && Bases(type).Any(parent => Same(parent, classification) || Same(parent, ignore))).ToArray();
        // Repeated/inherited class annotations require effective-attribute resolution before safe projection.
        if (attributes.GroupBy(attribute => MetadataName(attribute.AttributeClass!), StringComparer.Ordinal).Any(group => group.Count() > 1)) return null;
        return ClassificationAttributes(attributes, source, target, indent: "", allowFalse: false);
    }

    private static bool RequiresInitialization(IPropertySymbol property) =>
        property.IsRequired || property.Type.IsReferenceType && property.NullableAnnotation != NullableAnnotation.Annotated;

    private static bool PreservesOwnership(INamedTypeSymbol entity, Compilation compilation,
        CompilationUnitSyntax root, string owner, CancellationToken token)
    {
        var ownership = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomainAttribute");
        foreach (var attribute in entity.GetAttributes().Where(attribute => Same(attribute.AttributeClass, ownership)))
            if (attribute.ConstructorArguments is not [{ Value: string declared }] || declared != owner) return false;
        return OwnershipCompatible(root, MetadataName(entity), owner, token);
    }

    private static DtoPlan Render(List<(string Name, string Type, string Attributes, bool Required)> fields,
        string typeAttributes, DtoDestinations destinations)
    {
        var declarations = string.Join("\n", fields.Select(field => field.Attributes + "    public " + (field.Required ? "required " : "") +
            field.Type + " " + field.Name + " { get; init; }"));
        var dtoSource = $$"""
            #nullable enable
            namespace {{destinations.DtoNamespace}};

            {{typeAttributes}}public sealed class {{destinations.DtoName}} : global::DRN.Framework.SharedKernel.Domain.Dto
            {
                public {{destinations.DtoName}}(global::DRN.Framework.SharedKernel.Domain.SourceKnownEntity? entity = null) : base(entity)
                {
                }

            {{declarations}}
            }

            """;
        var qualifiedDto = "global::" + destinations.DtoNamespace + "." + destinations.DtoName;
        var values = string.Join(",\n", fields.Select(field => "    " + field.Name + " = this." + field.Name));
        var mapperSource = $$"""
            public {{qualifiedDto}} ToDto() => new(this)
            {
            {{values}}
            };
            """;
        return new(destinations.DtoNamespace, destinations.DtoName, destinations.DtoPath, destinations.RelativeDtoPath, dtoSource, mapperSource);
    }

    private sealed record DtoCompilations(Compilation SourceCompilation, Compilation TargetCompilation, INamedTypeSymbol Dto,
        string TargetRoot, string DtoNamespace, string Area);
    private sealed record DtoDestinations(string DtoNamespace, string DtoName, string DtoPath, string RelativeDtoPath);
    private sealed record Selection(INamedTypeSymbol Entity, ImmutableArray<IPropertySymbol> Properties);
    private sealed record DtoPlan(string DtoNamespace, string DtoName, string DtoPath, string RelativeDtoPath, string DtoSource, string MapperSource);
}
