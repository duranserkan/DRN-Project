using System.Collections.Immutable;
using System.Composition;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static DRN.Framework.SharedKernel.CodeFixes.EntityConfigurationRefactoringProvider;

namespace DRN.Framework.SharedKernel.CodeFixes;

[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(EntityDtoRefactoringProvider)), Shared]
public sealed class EntityDtoRefactoringProvider : CodeRefactoringProvider
{
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        var solution = context.Document.Project.Solution;
        if (context.Span.IsEmpty || !solution.Workspace.CanApplyChange(ApplyChangesKind.AddDocument)) return;
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
        var root = await ClassificationEdit.FindEditableRoot(document, span.Start, token);
        var model = await document.GetSemanticModelAsync(token);
        if (root is null || model is null || span.End > root.FullSpan.End ||
            model.GetDiagnostics(span, token).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)) return null;
        var declarations = root.DescendantNodes().OfType<PropertyDeclarationSyntax>()
            .Where(property => span.Contains(property.Span)).OrderBy(property => property.SpanStart).ToArray();
        if (declarations.Length == 0 || declarations.Length > 64) return null;
        var properties = ImmutableArray.CreateBuilder<IPropertySymbol>();
        INamedTypeSymbol? entity = null;
        foreach (var declaration in declarations)
        {
            token.ThrowIfCancellationRequested();
            if (model.GetDeclaredSymbol(declaration, token) is not IPropertySymbol property || property.IsStatic || property.IsIndexer ||
                property.DeclaredAccessibility != Accessibility.Public || property.GetMethod?.DeclaredAccessibility != Accessibility.Public ||
                property.IsOverride || property.ExplicitInterfaceImplementations.Length != 0 || declaration.ContainsDirectives ||
                declaration.AttributeLists.Any(list => list.Target is not null) ||
                property.GetMethod.GetAttributes().Length != 0 || property.GetMethod.GetReturnTypeAttributes().Length != 0 ||
                property.SetMethod?.GetAttributes().Length > 0) return null;
            entity ??= property.ContainingType;
            if (!SymbolEqualityComparer.Default.Equals(entity, property.ContainingType)) return null;
            properties.Add(property);
        }
        var entityBase = model.Compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.SourceKnownEntity");
        if (entity is null || entity.IsAbstract || IsOpen(entity) || entity.DeclaredAccessibility != Accessibility.Public ||
            !Bases(entity).Any(type => Same(type, entityBase))) return null;
        for (var containing = entity.ContainingType; containing is not null; containing = containing.ContainingType)
            if (containing.DeclaredAccessibility != Accessibility.Public) return null;
        foreach (var reference in entity.DeclaringSyntaxReferences)
        {
            var part = document.Project.GetDocument(reference.SyntaxTree);
            if (part is null || part.GetLinkedDocumentIds().Length != 0 ||
                await ClassificationEdit.FindEditableRoot(part, reference.Span.Start, token) is null) return null;
        }
        return new(entity, properties.ToImmutable());
    }

    private static async Task<DtoPlan?> Plan(Document document, Selection selection, Project target, CancellationToken token)
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
        if (await HasRoleOverrides(source, token) || await HasRoleOverrides(target, token)) return null;
        var sourceRoot = Path.GetDirectoryName(sourceProjectPath)!;
        var targetRoot = Path.GetDirectoryName(targetProjectPath)!;
        if (!entityPath.StartsWith(sourceRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)) return null;
        var sourceCompilation = await source.GetCompilationAsync(token);
        var targetCompilation = await target.GetCompilationAsync(token);
        if (sourceCompilation is null || targetCompilation is null ||
            !selection.Entity.ContainingAssembly.Identity.Equals(sourceCompilation.Assembly.Identity)) return null;
        var dto = targetCompilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.Dto");
        var entityBase = targetCompilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.SourceKnownEntity");
        if (dto is null || entityBase is null || !targetCompilation.IsSymbolAccessibleWithin(dto, targetCompilation.Assembly) ||
            sourceCompilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Domain.Dto") is not { } sourceDto ||
            !sourceDto.ContainingAssembly.Identity.Equals(dto.ContainingAssembly.Identity)) return null;
        var sourceNamespace = selection.Entity.ContainingNamespace.ToDisplayString();
        var roleNamespace = sourceIdentity.Namespace;
        var targetRoleNamespace = targetIdentity.Namespace;
        if (roleNamespace.Split('.').Any(part => !SyntaxFacts.IsValidIdentifier(part)) ||
            targetRoleNamespace.Split('.').Any(part => !SyntaxFacts.IsValidIdentifier(part))) return null;
        if (!sourceNamespace.StartsWith(roleNamespace + ".", StringComparison.Ordinal)) return null;
        var area = sourceNamespace[(roleNamespace.Length + 1)..];
        if (area.Split('.').Any(part => !SyntaxFacts.IsValidIdentifier(part))) return null;
        if (!await PreservesOwnership(source, selection.Entity, sourceCompilation, area.Split('.')[0], token)) return null;
        var dtoNamespace = targetRoleNamespace + "." + area;
        var dtoName = selection.Entity.Name + "Dto";
        var mapperName = selection.Entity.Name + "DtoMapping";
        if (!SyntaxFacts.IsValidIdentifier(dtoName) || !SyntaxFacts.IsValidIdentifier(mapperName) ||
            targetCompilation.GetTypeByMetadataName(dtoNamespace + "." + dtoName) is not null ||
            sourceCompilation.GetTypeByMetadataName(sourceNamespace + "." + mapperName) is not null ||
            sourceCompilation.GetSymbolsWithName("To" + dtoName, SymbolFilter.Member, token).OfType<IMethodSymbol>().Any()) return null;
        if (SymbolCollision(targetCompilation, dtoNamespace, dtoName, token) ||
            SymbolCollision(sourceCompilation, sourceNamespace, mapperName, token)) return null;
        var dtoPath = Path.Combine(targetRoot, area.Replace('.', Path.DirectorySeparatorChar), dtoName + ".cs");
        var mapperPath = Path.Combine(Path.GetDirectoryName(entityPath)!, mapperName + ".cs");
        if (!SafeDestination(targetRoot, dtoPath) || !SafeDestination(sourceRoot, mapperPath) ||
            WorkspaceDestinationConflict(source.Solution, dtoPath, token) ||
            WorkspaceDestinationConflict(source.Solution, mapperPath, token)) return null;
        var typeAttributes = TypeClassifications(selection.Entity, sourceCompilation, targetCompilation);
        if (typeAttributes is null) return null;
        var fields = new List<(string Name, string Type, string Attributes, bool Required)>();
        foreach (var property in selection.Properties)
        {
            if (property.Name is "entity" or "Entity" || property.Name == dtoName || Bases(dto).Any(type => type.GetMembers(property.Name).Length != 0) ||
                !ScalarAvailable(property.Type, sourceCompilation, targetCompilation)) return null;
            var attributes = ClassificationAttributes(property.GetAttributes(), sourceCompilation, targetCompilation);
            if (attributes is null) return null;
            foreach (var contract in selection.Entity.AllInterfaces)
            foreach (var member in contract.GetMembers().OfType<IPropertySymbol>())
            {
                if (!SymbolEqualityComparer.Default.Equals(selection.Entity.FindImplementationForInterfaceMember(member), property)) continue;
                var inherited = ClassificationAttributes(member.GetAttributes(), sourceCompilation, targetCompilation);
                if (inherited is null || inherited.Length > 0) return null;
            }
            fields.Add((Escape(property.Name), property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat
                .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
                                          SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers)), attributes, RequiresInitialization(property)));
        }
        return Render(selection, fields, typeAttributes, dtoNamespace, dtoName, mapperName, sourceNamespace, dtoPath, mapperPath,
            Path.GetRelativePath(targetRoot, dtoPath).Replace('\\', '/'), Path.GetRelativePath(sourceRoot, mapperPath).Replace('\\', '/'));
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
        if (named.TypeKind == TypeKind.Enum)
            return target.GetTypeByMetadataName(name) is { TypeKind: TypeKind.Enum } enumeration &&
                   enumeration.ContainingAssembly.Identity.Equals(named.ContainingAssembly.Identity) &&
                   target.IsSymbolAccessibleWithin(enumeration, target.Assembly);
        if (name is not ("System.Guid" or "System.DateTime" or "System.DateTimeOffset" or "System.DateOnly" or "System.TimeOnly" or "System.TimeSpan")) return false;
        return target.GetTypeByMetadataName(name) is { } available && source.GetTypeByMetadataName(name) is { } original &&
               SymbolEqualityComparer.Default.Equals(named, original) && available.ContainingAssembly.Identity.Equals(named.ContainingAssembly.Identity);
    }

    private static string? ClassificationAttributes(IEnumerable<AttributeData> attributes, Compilation source, Compilation target)
    {
        var rendered = new List<string>();
        foreach (var attribute in attributes)
        {
            if (attribute.AttributeClass is not { } type || attribute.AttributeConstructor is null) return null;
            var name = MetadataName(type);
            if (name is not ("DRN.Framework.SharedKernel.Attributes.SensitiveAttribute" or
                "DRN.Framework.SharedKernel.Attributes.PersonalAttribute" or "DRN.Framework.SharedKernel.Attributes.IgnoreLogAttribute")) return null;
            if (source.GetTypeByMetadataName(name) is not { } exact || !SymbolEqualityComparer.Default.Equals(type, exact) ||
                target.GetTypeByMetadataName(name) is not { } available || !available.ContainingAssembly.Identity.Equals(type.ContainingAssembly.Identity) ||
                !target.IsSymbolAccessibleWithin(available, target.Assembly)) return null;
            var arguments = new List<string>();
            foreach (var argument in attribute.ConstructorArguments)
            {
                if (argument.Value is bool boolean) arguments.Add(boolean ? "true" : "false");
                else return null;
            }
            foreach (var argument in attribute.NamedArguments)
            {
                if (argument.Key != "Justification" || argument.Value.Value is not string reason) return null;
                arguments.Add("Justification = " + SymbolDisplay.FormatLiteral(reason, true));
            }
            rendered.Add("    [global::" + name + (arguments.Count == 0 ? "" : "(" + string.Join(", ", arguments) + ")") + "]\n");
        }
        return string.Concat(rendered);
    }

    private static string? TypeClassifications(INamedTypeSymbol entity, Compilation source, Compilation target)
    {
        var classification = source.GetTypeByMetadataName("Microsoft.Extensions.Compliance.Classification.DataClassificationAttribute");
        var ignore = source.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.IgnoreLogAttribute");
        var attributes = Bases(entity).SelectMany(type => type.GetAttributes()).Where(attribute =>
            attribute.AttributeClass is { } type && Bases(type).Any(parent => Same(parent, classification) || Same(parent, ignore))).ToArray();
        // Repeated/inherited class annotations require effective-attribute resolution before safe projection.
        if (attributes.GroupBy(attribute => MetadataName(attribute.AttributeClass!), StringComparer.Ordinal).Any(group => group.Count() > 1)) return null;
        return ClassificationAttributes(attributes, source, target)?.Replace("    [", "[", StringComparison.Ordinal);
    }

    private static bool RequiresInitialization(IPropertySymbol property) =>
        property.IsRequired || property.Type.IsReferenceType && property.NullableAnnotation != NullableAnnotation.Annotated;

    private static bool SymbolCollision(Compilation compilation, string ns, string name, CancellationToken token) =>
        ConventionGenerationPreflight.SymbolCollision(compilation, ns, name, token);

    private static async Task<bool> PreservesOwnership(Project source, INamedTypeSymbol entity, Compilation compilation,
        string owner, CancellationToken token)
    {
        var ownership = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.SourceKnownSubdomainAttribute");
        foreach (var attribute in entity.GetAttributes().Where(attribute => Same(attribute.AttributeClass, ownership)))
            if (attribute.ConstructorArguments is not [{ Value: string declared }] || declared != owner) return false;
        var root = await ConventionGenerationPreflight.ReadInputs(source, required: false, token);
        if (root is null) return false;
        foreach (var attribute in root.AttributeLists.SelectMany(list => list.Attributes))
        {
            token.ThrowIfCancellationRequested();
            if (attribute.Name.ToString() is not ("DRN.Conventions.Ownership" or "global::DRN.Conventions.Ownership")) continue;
            if (attribute.ArgumentList is not { Arguments.Count: > 0 } arguments ||
                !LiteralString(arguments.Arguments[0].Expression, token, out var target)) return false;
            if (target != MetadataName(entity)) continue;
            var owners = arguments.Arguments.Where(argument => argument.NameEquals?.Name.Identifier.ValueText == "Subdomain").ToArray();
            if (arguments.Arguments[0].NameEquals is not null || arguments.Arguments[0].NameColon is not null || owners.Length != 1 ||
                !LiteralString(owners[0].Expression, token, out var declaredOwner) || declaredOwner != owner) return false;
        }
        return true;
    }

    private static bool LiteralString(ExpressionSyntax expression, CancellationToken token, out string value)
    {
        var parsed = DRN.Framework.SharedKernel.Analyzers.Conventions.ConventionInputSyntax.String(expression, token);
        value = parsed ?? "";
        return parsed is not null;
    }

    private static IEnumerable<INamedTypeSymbol> Bases(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType) yield return current;
    }

    private static string Escape(string name) => SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : "@" + name;

    private static DtoPlan Render(Selection selection, List<(string Name, string Type, string Attributes, bool Required)> fields,
        string typeAttributes, string dtoNamespace, string dtoName, string mapperName, string sourceNamespace, string dtoPath, string mapperPath,
        string relativeDtoPath, string relativeMapperPath)
    {
        var declarations = string.Join("\n", fields.Select(field => field.Attributes + "    public " + (field.Required ? "required " : "") +
            field.Type + " " + field.Name + " { get; init; }"));
        var dtoSource = $$"""
            #nullable enable
            namespace {{dtoNamespace}};

            {{typeAttributes}}public sealed class {{dtoName}} : global::DRN.Framework.SharedKernel.Domain.Dto
            {
                public {{dtoName}}(global::DRN.Framework.SharedKernel.Domain.SourceKnownEntity? entity = null) : base(entity)
                {
                }

            {{declarations}}
            }

            """;
        var qualifiedDto = "global::" + dtoNamespace + "." + dtoName;
        var entityType = selection.Entity.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        var values = string.Join(",\n", fields.Select(field => "            " + field.Name + " = entity." + field.Name));
        var mapperSource = $$"""
            #nullable enable
            namespace {{sourceNamespace}};

            public static class {{mapperName}}
            {
                public static {{qualifiedDto}} To{{dtoName}}(this {{entityType}} entity)
                {
                    global::System.ArgumentNullException.ThrowIfNull(entity);
                    return new {{qualifiedDto}}(entity)
                    {
            {{values}}
                    };
                }
            }

            """;
        return new(dtoName, mapperName, dtoPath, mapperPath, relativeDtoPath, relativeMapperPath, dtoSource, mapperSource);
    }

    private sealed record Selection(INamedTypeSymbol Entity, ImmutableArray<IPropertySymbol> Properties);
    private sealed record DtoPlan(string DtoName, string MapperName, string DtoPath, string MapperPath,
        string RelativeDtoPath, string RelativeMapperPath, string DtoSource, string MapperSource);
}
