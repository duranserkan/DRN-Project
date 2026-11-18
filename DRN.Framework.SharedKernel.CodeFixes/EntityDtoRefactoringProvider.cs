using System.Collections.Immutable;
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

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(EntityDtoRefactoringProvider)), Shared]
public sealed class EntityDtoRefactoringProvider : CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var solution = context.Document.Project.Solution;
        if (context.Span.IsEmpty || !solution.Workspace.CanApplyChange(ApplyChangesKind.AddDocument) ||
            ConventionGenerationPreflight.Identity(context.Document.Project).Role != "Domain") return;
        var selection = await ReadSelection(context.Document, context.Span, context.CancellationToken);
        if (selection is null) return;
        foreach (var project in solution.Projects)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var candidate = await Plan(context.Document, selection, project, context.CancellationToken);
            if (candidate is null) continue;
            if (!ReferenceEquals(solution.Workspace.CurrentSolution, solution)) return;
            context.RegisterRefactoring(CodeAction.Create(
                "Add DTO and mapping: " + project.Name + " / " + candidate.RelativeDtoPath + " (" +
                string.Join(", ", selection.Properties.Select(property => property.Name + (RequiresInitialization(property) ? " required" : ""))) + ")",
                token => Apply(context.Document, context.Span, project.Id, candidate, solution, token),
                "DrnEntityDto:" + project.Id + ":" + MetadataName(selection.Entity)));
        }
    }

    private static async Task<Solution> Apply(Document document, TextSpan span, ProjectId targetId, DtoPlan expected,
        Solution snapshot, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (!ReferenceEquals(snapshot.Workspace.CurrentSolution, snapshot)) return snapshot.Workspace.CurrentSolution;
        var target = snapshot.GetProject(targetId);
        var selection = await ReadSelection(document, span, token);
        if (target is null || selection is null) return snapshot.Workspace.CurrentSolution;
        var plan = await Plan(document, selection, target, token);
        if (plan is null || plan != expected) return snapshot.Workspace.CurrentSolution;
        token.ThrowIfCancellationRequested();
        if (!ReferenceEquals(snapshot.Workspace.CurrentSolution, snapshot)) return snapshot.Workspace.CurrentSolution;
        return snapshot.AddDocument(DocumentId.CreateNewId(targetId), plan.DtoName + ".cs", SourceText.From(plan.DtoSource),
                plan.RelativeDtoPath.Split('/')[..^1], plan.DtoPath)
            .AddDocument(DocumentId.CreateNewId(document.Project.Id), plan.MapperName + ".cs", SourceText.From(plan.MapperSource),
                plan.RelativeMapperPath.Split('/')[..^1], plan.MapperPath);
    }

    private static async Task<Selection?> ReadSelection(Document document, TextSpan span, CancellationToken token)
    {
        if (span.IsEmpty || document.GetLinkedDocumentIds().Length != 0) return null;
        var syntaxRoot = await document.GetSyntaxRootAsync(token);
        if (syntaxRoot is null || span.End > syntaxRoot.FullSpan.End) return null;
        var declarations = CollectSelectedPropertyNodes(syntaxRoot, span, token);
        if (declarations is null) return null;
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
        if (declarations.Count == 0) return null;
        declarations.Sort(static (left, right) => left.SpanStart.CompareTo(right.SpanStart));
        return declarations;
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
        return !entity.IsAbstract && !IsOpen(entity) && IsPublicType(entity) &&
               Inherits(entity, entityBase) &&
               await ArePartsEditableAsync(document.Project, entity, rejectSpanErrors: false, token);
    }

    private static async Task<DtoPlan?> Plan(Document document, Selection selection, Project target, CancellationToken token)
    {
        var resolved = await ResolveDtoCompilationsAsync(document, selection, target, token);
        if (resolved is null) return null;
        var destinations = TryBuildDtoDestinations(document.Project, target, selection, resolved, token);
        if (destinations is null) return null;
        var typeAttributes = TypeClassifications(selection.Entity, resolved.SourceCompilation, resolved.TargetCompilation);
        if (typeAttributes is null) return null;
        var fields = TryBuildDtoFields(selection, resolved.Dto, destinations.DtoName, resolved.SourceCompilation, resolved.TargetCompilation);
        return fields is null ? null : Render(selection, fields, typeAttributes, destinations);
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
            source.FilePath is null || target.FilePath is null || document.FilePath is null ||
            !TryFullPath(source.FilePath, out var sourceProjectPath) || !TryFullPath(target.FilePath, out var targetProjectPath) ||
            !TryFullPath(document.FilePath, out var entityPath)) return null;
        var sourceInputs = await ReadInputsWithoutRoleOverrideAsync(source, required: false, token);
        if (sourceInputs is null || await HasRoleOverrides(target, token)) return null;
        var sourceRoot = Path.GetDirectoryName(sourceProjectPath)!;
        var targetRoot = Path.GetDirectoryName(targetProjectPath)!;
        if (!ConventionGenerationPreflight.IsUnderDirectory(entityPath, sourceRoot)) return null;
        return await ResolveDtoCompilationModelsAsync((source, sourceIdentity.Namespace, sourceRoot),
            (target, targetIdentity.Namespace, targetRoot), selection, sourceInputs, entityPath, token);
    }

    private static async Task<DtoCompilations?> ResolveDtoCompilationModelsAsync(
        (Project Project, string Namespace, string Root) source, (Project Project, string Namespace, string Root) target,
        Selection selection, CompilationUnitSyntax sourceInputs, string entityPath, CancellationToken token)
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
        return new(sourceCompilation, targetCompilation, dto, source.Root, target.Root, entityPath,
            sourceNamespace, target.Namespace + "." + area, area);
    }

    private static DtoDestinations? TryBuildDtoDestinations(Project source, Project target, Selection selection,
        DtoCompilations resolved, CancellationToken token)
    {
        var stem = ConventionGenerationPreflight.EntityStem(selection.Entity.Name);
        var dtoName = stem + "Dto";
        var mapperName = stem + "DtoMapping";
        if (!SyntaxFacts.IsValidIdentifier(dtoName) || !SyntaxFacts.IsValidIdentifier(mapperName) ||
            resolved.TargetCompilation.GetTypeByMetadataName(resolved.DtoNamespace + "." + dtoName) is not null ||
            resolved.SourceCompilation.GetTypeByMetadataName(resolved.SourceNamespace + "." + mapperName) is not null ||
            ConflictingMapperMethod(resolved.SourceCompilation, selection.Entity, resolved.SourceNamespace, dtoName, token))
            return null;
        if (ConventionGenerationPreflight.SymbolCollision(resolved.TargetCompilation, resolved.DtoNamespace, dtoName, token) ||
            ConventionGenerationPreflight.SymbolCollision(resolved.SourceCompilation, resolved.SourceNamespace, mapperName, token)) return null;
        var dtoPath = Path.Combine(resolved.TargetRoot, resolved.Area.Replace('.', Path.DirectorySeparatorChar), dtoName + ".cs");
        var mapperPath = Path.Combine(Path.GetDirectoryName(resolved.EntityPath)!, mapperName + ".cs");
        if (!SafeDocumentDestination(target, resolved.TargetRoot, dtoPath, token) ||
            !SafeDocumentDestination(source, resolved.SourceRoot, mapperPath, token)) return null;
        return new(resolved.DtoNamespace, dtoName, dtoPath, Path.GetRelativePath(resolved.TargetRoot, dtoPath).Replace('\\', '/'),
            resolved.SourceNamespace, mapperName, mapperPath, Path.GetRelativePath(resolved.SourceRoot, mapperPath).Replace('\\', '/'));
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

    private static bool ConflictingMapperMethod(Compilation compilation, INamedTypeSymbol entity,
        string sourceNamespace, string dtoName, CancellationToken token)
    {
        foreach (var method in compilation.GetSymbolsWithName("To" + dtoName, SymbolFilter.Member, token).OfType<IMethodSymbol>())
        {
            if (method.IsStatic && method.IsExtensionMethod && method.Parameters.Length >= 1 &&
                (method.ContainingNamespace.ToDisplayString() == sourceNamespace ||
                 method.Parameters[0].Type is ITypeParameterSymbol ||
                 compilation.ClassifyCommonConversion(entity, method.Parameters[0].Type).IsImplicit))
                return true;
            if (!method.IsStatic && Bases(entity).Any(type => SymbolEqualityComparer.Default.Equals(type, method.ContainingType)))
                return true;
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

    private static DtoPlan Render(Selection selection, List<(string Name, string Type, string Attributes, bool Required)> fields,
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
        var entityType = selection.Entity.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var values = string.Join(",\n", fields.Select(field => "            " + field.Name + " = entity." + field.Name));
        var mapperSource = $$"""
            #nullable enable
            namespace {{destinations.SourceNamespace}};

            public static class {{destinations.MapperName}}
            {
                public static {{qualifiedDto}} To{{destinations.DtoName}}(this {{entityType}} entity)
                {
                    global::System.ArgumentNullException.ThrowIfNull(entity);
                    return new {{qualifiedDto}}(entity)
                    {
            {{values}}
                    };
                }
            }

            """;
        return new(destinations.DtoName, destinations.MapperName, destinations.DtoPath, destinations.MapperPath,
            destinations.RelativeDtoPath, destinations.RelativeMapperPath, dtoSource, mapperSource);
    }

    private sealed record DtoCompilations(Compilation SourceCompilation, Compilation TargetCompilation, INamedTypeSymbol Dto,
        string SourceRoot, string TargetRoot, string EntityPath, string SourceNamespace, string DtoNamespace, string Area);
    private sealed record DtoDestinations(string DtoNamespace, string DtoName, string DtoPath, string RelativeDtoPath,
        string SourceNamespace, string MapperName, string MapperPath, string RelativeMapperPath);
    private sealed record Selection(INamedTypeSymbol Entity, ImmutableArray<IPropertySymbol> Properties);
    private sealed record DtoPlan(string DtoName, string MapperName, string DtoPath, string MapperPath,
        string RelativeDtoPath, string RelativeMapperPath, string DtoSource, string MapperSource);
}
