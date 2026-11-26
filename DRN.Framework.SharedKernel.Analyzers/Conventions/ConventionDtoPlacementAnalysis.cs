using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionDtoPlacementAnalysis
{
    internal const string RequestSuffix = "Request";
    internal const string ResponseSuffix = "Response";

    internal readonly record struct DtoPlacement(string Namespace, string? Directory);
    internal readonly record struct DtoKey(string Root, string Domain, string Subdomain, bool HasFolderDomain, string Stem);

    internal static int MessageStemLength(string name)
    {
        if (name.EndsWith(RequestSuffix, StringComparison.Ordinal)) return name.Length - RequestSuffix.Length;
        if (name.EndsWith(ResponseSuffix, StringComparison.Ordinal)) return name.Length - ResponseSuffix.Length;
        return 0;
    }

    internal static void CheckDtoPlacement(
        ConventionFacts facts, INamedTypeSymbol dto, string root, Dictionary<DtoKey, DtoPlacement?> dtoStems)
    {
        var owner = facts.Owner(dto);
        if (facts.HasInvalidOwner(dto)) return;
        var stem = dto.Name[..^3];
        var folder = ConventionNames.DtoFolder(stem);
        var location = facts.Location(dto);
        if (owner is not null && ConventionBoundaryAnalysis.IsPublic(dto) && location.SourceTree is { } tree)
        {
            var key = new DtoKey(root, owner.Domain, owner.Subdomain, owner.HasFolderDomain, stem);
            var directory = facts.HasInvalidPath(tree) ? null : facts.LogicalDirectory(tree)?.Directory;
            var placement = new DtoPlacement(dto.ContainingNamespace.ToDisplayString(), directory);
            // A shared stem can identify more than one DTO. Only report a unique destination.
            if (!dtoStems.TryAdd(key, placement) && dtoStems[key] != placement) dtoStems[key] = null;
        }
        // DRNSTRUCTURE001 independently verifies each declaration's logical folder against its namespace.
        if (location != Location.None && !InDtoNamespace(facts, dto, owner, folder))
            ConventionBoundaryAnalysis.Report(facts, ConventionDiagnosticDescriptors.DtoFolder, dto, location, dto.Name, folder);
    }

    internal static void CheckMessagePlacement(
        ConventionFacts facts, INamedTypeSymbol message, Dictionary<DtoKey, DtoPlacement?> dtoStems)
    {
        facts.Cancellation.ThrowIfCancellationRequested();
        if (facts.Owner(message) is not { } owner) return;
        var role = facts.Role(message);
        var name = message.Name;
        var stemLength = MessageStemLength(name);
        for (var end = stemLength; end > 0; end--)
        {
            if (end != stemLength && !IsWordBoundary(name, end)) continue;
            var stem = name[..end];
            var key = new DtoKey(role.Namespace, owner.Domain, owner.Subdomain, owner.HasFolderDomain, stem);
            if (!dtoStems.TryGetValue(key, out var placement)) continue;
            if (placement is { } dto)
                ConventionOwnershipAnalysis.AnalyzeTypePlacement(facts, message, facts.Location(message),
                    message.ContainingNamespace.ToDisplayString(), dto.Namespace, dto.Directory,
                    ConventionDiagnosticDescriptors.DtoMessagePlacement);
            return;
        }
    }

    internal static void CheckMapper(
        ConventionFacts facts, INamedTypeSymbol entity, Lazy<ClassificationContract?> classifications)
    {
        var owner = facts.Owner(entity);
        if (facts.HasInvalidOwner(entity)) return;
        var stem = ConventionNames.EntityStem(entity.Name);
        var expectedDtoName = stem + "Dto";
        var method = FindParameterlessPublicToDto(entity);
        var overridesEntityToDto = method is { IsOverride: true } &&
            ConventionSymbols.Same(RoslynSymbolHelpers.RootOverriddenMethod(method).ContainingType, facts.Symbols.Entity);
        var returnType = method?.ReturnType;
        var returnsDto = method is not null &&
            returnType is INamedTypeSymbol result &&
            facts.Symbols.IsKnown(result, facts.Symbols.Dto, facts.Cancellation);
        var creation = returnsDto ? GetConstruction(facts, method!) : null;

        var hasValidDtoConstruction = overridesEntityToDto &&
            creation?.Type is INamedTypeSymbol dto &&
            SymbolEqualityComparer.Default.Equals(dto, returnType) &&
            ConstructsWithEntity(facts, creation) &&
            dto.Name == expectedDtoName &&
            IsPublicDtoType(dto) &&
            InDtoNamespace(facts, dto, owner, ConventionNames.DtoFolder(stem));
        if (!hasValidDtoConstruction)
            ConventionBoundaryAnalysis.Report(facts, ConventionDiagnosticDescriptors.EntityDto, entity,
                facts.Location(entity), entity.Name, expectedDtoName);

        if (creation?.Type is INamedTypeSymbol projected &&
            facts.Symbols.IsKnown(projected, facts.Symbols.Dto, facts.Cancellation) &&
            classifications.Value is { } contract)
            ResponseProjectionAnalysis.CheckDtoInitializerProjection(facts, entity, projected, creation, contract);
    }

    private static IMethodSymbol? FindParameterlessPublicToDto(INamedTypeSymbol entity)
    {
        foreach (var member in entity.GetMembers("ToDto"))
        {
            if (member is IMethodSymbol { IsStatic: false, Arity: 0, Parameters.Length: 0, DeclaredAccessibility: Accessibility.Public } method)
                return method;
        }
        return null;
    }

    private static bool IsWordBoundary(string name, int index) => char.IsUpper(name[index]) &&
        (!char.IsUpper(name[index - 1]) || char.IsLower(name[index + 1]));

    private static bool InDtoNamespace(ConventionFacts facts, INamedTypeSymbol dto, ConventionOwner? expectedOwner, string folder)
    {
        if (expectedOwner is null || facts.Role(dto).Role != ConventionArchitecture.Contract) return false;
        var dtoOwner = facts.Owner(dto);
        if (dtoOwner is null || !expectedOwner.MatchesAcrossLayers(dtoOwner)) return false;
        return dto.ContainingNamespace.ToDisplayString() == facts.Role(dto).Namespace + "." + dtoOwner.NamespacePrefix + "." + folder;
    }

    [SuppressMessage("SonarQube", "S3267", Justification = "Scanning TypeArguments directly avoids boxing ImmutableArray and allocating a predicate delegate during recursive DTO checks.")]
    private static bool IsPublicDtoType(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array) return IsPublicDtoType(array.ElementType);
        if (type is IDynamicTypeSymbol) return true;
        if (type is not INamedTypeSymbol named || named.DeclaredAccessibility != Accessibility.Public) return false;
        foreach (var argument in named.TypeArguments)
        {
            if (!IsPublicDtoType(argument)) return false;
        }
        return named.ContainingType is null || IsPublicDtoType(named.ContainingType);
    }

    private static IObjectCreationOperation? GetConstruction(ConventionFacts facts, IMethodSymbol method)
    {
        foreach (var reference in (method.PartialImplementationPart ?? method).DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(facts.Cancellation) is not MethodDeclarationSyntax syntax) continue;
            var expression = syntax.ExpressionBody?.Expression ??
                (syntax.Body?.Statements is [ReturnStatementSyntax statement] ? statement.Expression : null);
            if (expression is null) continue;
            if (facts.SemanticModel(syntax.SyntaxTree).GetOperation(expression, facts.Cancellation)
                is IObjectCreationOperation creation)
                return creation;
        }
        return null;
    }

    private static bool ConstructsWithEntity(ConventionFacts facts, IObjectCreationOperation creation)
    {
        // The operation also contains compiler-supplied arguments for omitted optional parameters.
        var arguments = creation.Arguments;
        if (arguments.Length == 0 || arguments[0].ArgumentKind != ArgumentKind.Explicit) return false;
        for (var i = 1; i < arguments.Length; i++)
        {
            if (arguments[i].ArgumentKind != ArgumentKind.DefaultValue) return false;
        }
        var value = arguments[0].Value;
        while (value is IConversionOperation { Conversion.IsReference: true } conversion) value = conversion.Operand;
        var passesThis = value is IInstanceReferenceOperation
            { Syntax: ThisExpressionSyntax, ReferenceKind: InstanceReferenceKind.ContainingTypeInstance };
        return passesThis && ConventionSymbols.Same(arguments[0].Parameter?.Type, facts.Symbols.Entity);
    }
}
