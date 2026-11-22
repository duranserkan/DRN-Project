using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using static DRN.Framework.SharedKernel.Analyzers.ClassificationContract;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionDtoAnalysis
{
    private readonly record struct DtoPlacement(string Namespace, string? Directory);
    private readonly record struct DtoKey(string Root, string Domain, string Subdomain, bool HasFolderDomain, string Stem);
    private readonly record struct ProjectionTarget(IPropertySymbol Property, INamedTypeSymbol Receiver, ITypeSymbol? View, Location Location);
    private readonly record struct ProjectionNode(IOperation Operation, ProjectionTarget? Target, bool IsInput);

    internal static void Analyze(ConventionFacts facts)
    {
        var classifications = new Lazy<ClassificationContract?>(() => CreateClassifications(facts.Compilation), isThreadSafe: false);
        var dtoStems = new Dictionary<DtoKey, DtoPlacement?>();
        var messages = new List<INamedTypeSymbol>();
        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var role = facts.Role(type);
            if (role.Role == ConventionArchitecture.Contract && MessageStemLength(type.Name) > 0 &&
                ConventionBoundaryAnalysis.IsPublic(type) && facts.Location(type) != Location.None)
                messages.Add(type);
            if (role.Role == ConventionArchitecture.Contract && ConventionBoundaryAnalysis.IsPublic(type) &&
                facts.Location(type) != Location.None)
                CheckContractDtoExposure(facts, type);
            if (ConventionBoundaryAnalysis.IsExposedController(facts, type) &&
                facts.Location(type) != Location.None)
                CheckControllerContracts(facts, type);
            if (role.Role is (ConventionArchitecture.Hosted or ConventionArchitecture.Application or
                    ConventionArchitecture.Domain or ConventionArchitecture.Contract) &&
                facts.Location(type) != Location.None)
                CheckResponseProjections(facts, type, classifications);
            if (type.IsAbstract) continue;
            if (!facts.Symbols.IsOpen(type, facts.Cancellation) && facts.Location(type) != Location.None)
                CheckServiceDtoProjection(facts, type);
            if (role.Role == ConventionArchitecture.Domain && facts.Symbols.IsKnown(type, facts.Symbols.Entity, facts.Cancellation) &&
                !facts.Symbols.IsOpen(type, facts.Cancellation) && facts.Location(type) != Location.None)
                CheckMapper(facts, type, classifications);
            if (role.Role == ConventionArchitecture.Contract && type.Name.EndsWith("Dto", StringComparison.Ordinal) &&
                type.Name.Length > 3 && facts.Symbols.IsKnown(type, facts.Symbols.Dto, facts.Cancellation))
                CheckDtoPlacement(facts, type, role.Namespace, dtoStems);
        }
        foreach (var message in messages)
            CheckMessagePlacement(facts, message, dtoStems);
    }

    private static ClassificationContract? CreateClassifications(Compilation compilation)
    {
        var sensitive = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.SensitiveAttribute");
        var personal = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.PersonalAttribute");
        return sensitive is null || personal is null ? null : new ClassificationContract(sensitive, personal, compilation);
    }

    private static void CheckDtoPlacement(ConventionFacts facts, INamedTypeSymbol dto, string root,
        Dictionary<DtoKey, DtoPlacement?> dtoStems)
    {
        var owner = facts.Owner(dto);
        if (facts.HasInvalidOwner(dto)) return;
        var stem = dto.Name[..^3];
        var folder = ConventionNames.DtoFolder(stem);
        var location = facts.Location(dto);
        if (owner is not null && ConventionBoundaryAnalysis.IsPublic(dto) && location.SourceTree is { } tree)
        {
            var key = new DtoKey(root, owner.Domain, owner.Subdomain, owner.HasFolderDomain, stem);
            var placement = new DtoPlacement(dto.ContainingNamespace.ToDisplayString(),
                facts.HasInvalidPath(tree) ? null : facts.LogicalDirectory(tree)?.Directory);
            // A shared stem can identify more than one DTO. Only report a unique destination.
            if (!dtoStems.TryAdd(key, placement) && dtoStems[key] != placement) dtoStems[key] = null;
        }
        // DRNSTRUCTURE001 independently verifies each declaration's logical folder against its namespace.
        if (location != Location.None && !InDtoNamespace(facts, dto, owner, folder))
            Report(facts, ConventionDiagnosticDescriptors.DtoFolder, dto, location, dto.Name, folder);
    }

    private static int MessageStemLength(string name)
    {
        if (name.EndsWith("Request", StringComparison.Ordinal)) return name.Length - "Request".Length;
        if (name.EndsWith("Response", StringComparison.Ordinal)) return name.Length - "Response".Length;
        return 0;
    }

    private static void CheckMessagePlacement(ConventionFacts facts, INamedTypeSymbol message,
        Dictionary<DtoKey, DtoPlacement?> dtoStems)
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
                    message.ContainingNamespace.ToDisplayString(), dto.Namespace, dto.Directory, ConventionDiagnosticDescriptors.DtoMessagePlacement);
            return;
        }
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

    private static void CheckMapper(ConventionFacts facts, INamedTypeSymbol entity, Lazy<ClassificationContract?> classifications)
    {
        var owner = facts.Owner(entity);
        if (facts.HasInvalidOwner(entity)) return;
        var stem = ConventionNames.EntityStem(entity.Name);
        var method = entity.GetMembers("ToDto").OfType<IMethodSymbol>().FirstOrDefault(candidate =>
            candidate is { IsStatic: false, Arity: 0, Parameters.Length: 0, DeclaredAccessibility: Accessibility.Public });
        var overridesEntityToDto = method is { IsOverride: true } &&
            ConventionSymbols.Same(ConventionBoundaryAnalysis.RootOverriddenMethod(method).ContainingType, facts.Symbols.Entity);
        var returnType = method?.ReturnType;
        var creation = method is not null && returnType is INamedTypeSymbol result &&
            facts.Symbols.IsKnown(result, facts.Symbols.Dto, facts.Cancellation) ? GetConstruction(facts, method) : null;
        if (!overridesEntityToDto || creation?.Type is not INamedTypeSymbol dto || !SymbolEqualityComparer.Default.Equals(dto, returnType) ||
            !ConstructsWithEntity(facts, creation) || dto.Name != stem + "Dto" || !IsPublicDtoType(dto) ||
            !InDtoNamespace(facts, dto, owner, ConventionNames.DtoFolder(stem)))
            Report(facts, ConventionDiagnosticDescriptors.EntityDto, entity, facts.Location(entity), entity.Name, stem + "Dto");
        if (creation?.Type is INamedTypeSymbol projected && facts.Symbols.IsKnown(projected, facts.Symbols.Dto, facts.Cancellation) &&
            classifications.Value is { } contract)
            CheckProjection(facts, entity, projected, creation, contract);
    }

    private static void CheckServiceDtoProjection(ConventionFacts facts, INamedTypeSymbol service)
    {
        if (facts.Symbols.SourceKnownServiceDto is not { } serviceBase || facts.Symbols.Entity is null)
            return;
        var boundBase = ConventionSymbols.Bases(service, facts.Cancellation)
            .FirstOrDefault(current => ConventionSymbols.Same(current, serviceBase));
        if (boundBase is not { TypeArguments: [INamedTypeSymbol entity, INamedTypeSymbol dto] } ||
            entity.TypeKind == TypeKind.Error || dto.TypeKind == TypeKind.Error ||
            facts.Symbols.IsOpen(entity, facts.Cancellation) || facts.Symbols.IsOpen(dto, facts.Cancellation))
            return;
        if (OverridesServiceToDto(service, boundBase, facts.Cancellation))
            return;
        var entityToDto = FindEntityToDtoMethod(entity, facts.Symbols.Entity, facts.Cancellation);
        if (entityToDto is null || entityToDto.IsAbstract && !entity.IsAbstract ||
            entityToDto.ReturnType is not INamedTypeSymbol returnType || returnType.TypeKind == TypeKind.Error)
            return;
        if (IsAssignableToDto(returnType, dto, facts.Cancellation))
            return;
        Report(facts, ConventionDiagnosticDescriptors.ServiceDtoProjection, service, facts.Location(service),
            service.Name, entity.Name, dto.Name, returnType.Name);
    }

    private static bool OverridesServiceToDto(INamedTypeSymbol service, INamedTypeSymbol serviceBase, CancellationToken cancellation)
    {
        for (var current = service; current is not null && !SymbolEqualityComparer.Default.Equals(current, serviceBase); current = current.BaseType)
        {
            cancellation.ThrowIfCancellationRequested();
            foreach (var method in current.GetMembers("ToDto").OfType<IMethodSymbol>())
            {
                if (method is { IsStatic: false, Arity: 0, Parameters.Length: 1, IsOverride: true } &&
                    ConventionSymbols.Same(ConventionBoundaryAnalysis.RootOverriddenMethod(method).ContainingType, serviceBase))
                    return true;
            }
        }
        return false;
    }

    private static IMethodSymbol? FindEntityToDtoMethod(INamedTypeSymbol entity, INamedTypeSymbol entityBase, CancellationToken cancellation)
    {
        foreach (var current in ConventionSymbols.Bases(entity, cancellation))
        {
            foreach (var method in current.GetMembers("ToDto").OfType<IMethodSymbol>())
            {
                if (method is { IsStatic: false, Arity: 0, Parameters.Length: 0 } &&
                    ConventionSymbols.Same(ConventionBoundaryAnalysis.RootOverriddenMethod(method).ContainingType, entityBase))
                    return method;
            }
        }
        return null;
    }

    private static bool IsAssignableToDto(INamedTypeSymbol returnType, INamedTypeSymbol dto, CancellationToken cancellation)
    {
        foreach (var current in ConventionSymbols.Bases(returnType, cancellation))
        {
            if (SymbolEqualityComparer.Default.Equals(current, dto))
                return true;
        }
        return false;
    }

    private static void CheckContractDtoExposure(ConventionFacts facts, INamedTypeSymbol type)
    {
        var isDto = facts.Symbols.IsKnown(type, facts.Symbols.Dto, facts.Cancellation);
        if (isDto)
        {
            if (MessageStemLength(type.Name) > 0)
                Report(facts, ConventionDiagnosticDescriptors.DtoExposure, type, facts.Location(type),
                    type.ToDisplayString(), type.BaseType?.ToDisplayString() ?? type.ToDisplayString());
            return;
        }
        foreach (var member in ConventionBoundaryAnalysis.PublicMembers(facts, type, transport: false))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!ConventionBoundaryAnalysis.IsInspectablePublicMember(facts, type, member, transport: false, nonAction: null) ||
                !ConventionBoundaryAnalysis.TryGetReportingTarget(facts, type, member, out var reportingSymbol, out var location))
                continue;
            var exposedDto = FindExposedDtoType(facts, type, member, transport: false, out var exhausted);
            if (exhausted)
                facts.ReportIncompleteContract(reportingSymbol, member, location);
            if (exposedDto is not null)
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.DtoExposure, location,
                    ConventionBoundaryAnalysis.Properties(reportingSymbol, exposedDto),
                    member.ToDisplayString(), exposedDto.ToDisplayString()));
        }
    }

    private static void CheckControllerContracts(ConventionFacts facts, INamedTypeSymbol controller)
    {
        var nonAction = facts.Symbols.NonActionAttribute;
        var isSourceKnown = facts.Symbols.IsKnown(controller, facts.Symbols.SourceKnownController, facts.Cancellation);
        foreach (var member in ConventionBoundaryAnalysis.PublicMembers(facts, controller, transport: true))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!ConventionBoundaryAnalysis.IsInspectablePublicMember(facts, controller, member, transport: true, nonAction) ||
                member is not IMethodSymbol method || ConventionBoundaryAnalysis.IsObjectOrDisposeMethod(facts, method) ||
                !ConventionBoundaryAnalysis.TryGetReportingTarget(facts, controller, method, out var reportingSymbol, out var location))
                continue;

            var exposedDto = FindExposedDtoType(facts, controller, method, transport: true, out var exhausted);
            if (exhausted)
                facts.ReportIncompleteContract(reportingSymbol, method, location);
            if (exposedDto is not null)
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.DtoExposure, location,
                    ConventionBoundaryAnalysis.Properties(reportingSymbol, exposedDto),
                    method.ToDisplayString(), exposedDto.ToDisplayString()));

            CheckEndpointExplicitResponse(facts, controller, method, reportingSymbol, location, isSourceKnown);
            CheckEndpointExplicitRequests(facts, method, reportingSymbol, location, isSourceKnown);
        }
    }

    private static void CheckEndpointExplicitResponse(ConventionFacts facts, INamedTypeSymbol controller, IMethodSymbol method,
        ISymbol reportingSymbol, Location location, bool isSourceKnown)
    {
        HashSet<INamedTypeSymbol>? reported = null;
        CheckExplicitResponseType(facts, method, reportingSymbol, location, isSourceKnown, method.ReturnType, ref reported);
        foreach (var responseType in ConventionBoundaryAnalysis.ProducesResponseTypes(facts, controller, method))
            CheckExplicitResponseType(facts, method, reportingSymbol, location, isSourceKnown, responseType, ref reported);
    }

    private static void CheckExplicitResponseType(ConventionFacts facts, IMethodSymbol method, ISymbol reportingSymbol,
        Location location, bool isSourceKnown, ITypeSymbol responseType, ref HashSet<INamedTypeSymbol>? reported)
    {
        var payload = UnwrapResponsePayload(facts, responseType);
        if (payload is null || IsExemptResponseType(facts, payload)) return;
        if (facts.Symbols.IsKnown(payload, facts.Symbols.Dto, facts.Cancellation)) return;
        var isContractResponse = facts.Role(payload).Role == ConventionArchitecture.Contract &&
            payload.Name.EndsWith("Response", StringComparison.Ordinal) &&
            payload.Name.Length > "Response".Length;
        if (isContractResponse) return;
        if (isSourceKnown || facts.Role(payload).Role is ConventionArchitecture.Contract or ConventionArchitecture.Application)
        {
            reported ??= new(SymbolEqualityComparer.Default);
            if (!reported.Add(payload)) return;
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.ExplicitContractMessage, location,
                ConventionBoundaryAnalysis.Properties(reportingSymbol, payload),
                method.ToDisplayString(), "Response", payload.ToDisplayString()));
        }
    }

    private static void CheckEndpointExplicitRequests(ConventionFacts facts, IMethodSymbol method, ISymbol reportingSymbol,
        Location location, bool isSourceKnown)
    {
        foreach (var parameter in method.Parameters)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (IsExemptRequestParameter(facts, parameter)) continue;
            var payload = UnwrapRequestPayload(facts, parameter.Type);
            if (payload is null || IsExemptRequestType(facts, payload)) continue;
            if (facts.Symbols.IsKnown(payload, facts.Symbols.Dto, facts.Cancellation)) continue;
            var isContractRequest = facts.Role(payload).Role == ConventionArchitecture.Contract &&
                payload.Name.EndsWith("Request", StringComparison.Ordinal) &&
                payload.Name.Length > "Request".Length;
            if (isContractRequest) continue;
            if (isSourceKnown || facts.Role(payload).Role is ConventionArchitecture.Contract or ConventionArchitecture.Application)
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.ExplicitContractMessage, location,
                    ConventionBoundaryAnalysis.Properties(reportingSymbol, payload),
                    method.ToDisplayString(), "Request", payload.ToDisplayString()));
        }
    }

    private static INamedTypeSymbol? UnwrapResponsePayload(ConventionFacts facts, ITypeSymbol type) =>
        UnwrapPayload(facts, type, unwrapResponseWrappers: true, maxDepth: 8);

    private static INamedTypeSymbol? UnwrapRequestPayload(ConventionFacts facts, ITypeSymbol type) =>
        UnwrapPayload(facts, type, unwrapResponseWrappers: false, maxDepth: 4);

    private static INamedTypeSymbol? UnwrapPayload(ConventionFacts facts, ITypeSymbol type,
        bool unwrapResponseWrappers, int maxDepth)
    {
        var current = type;
        for (var depth = 0; depth < maxDepth; depth++)
        {
            if (current is IArrayTypeSymbol array)
            {
                current = array.ElementType;
                continue;
            }
            if (current is not INamedTypeSymbol named) return null;
            if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T && named.TypeArguments.Length == 1)
            {
                current = named.TypeArguments[0];
                continue;
            }
            if (named.TypeArguments.Length == 1 &&
                (IsCollectionWrapper(named) ||
                 unwrapResponseWrappers &&
                 (ConventionSymbols.Same(named, facts.Symbols.TaskGeneric) ||
                  ConventionSymbols.Same(named, facts.Symbols.ValueTaskGeneric) ||
                  ConventionSymbols.Same(named, facts.Symbols.ActionResultGeneric) ||
                  ConventionSymbols.Same(named, facts.Symbols.PaginationResultModel))))
            {
                current = named.TypeArguments[0];
                continue;
            }
            return named;
        }
        return current as INamedTypeSymbol;
    }

    private static bool IsCollectionWrapper(INamedTypeSymbol named)
    {
        if (named.TypeArguments.Length != 1) return false;
        var name = ConventionSymbols.MetadataName(named.OriginalDefinition);
        return name is "System.Collections.Generic.IEnumerable`1" or
            "System.Collections.Generic.IReadOnlyList`1" or
            "System.Collections.Generic.IReadOnlyCollection`1" or
            "System.Collections.Generic.IList`1" or
            "System.Collections.Generic.ICollection`1" or
            "System.Collections.Generic.List`1";
    }

    private static bool IsExemptResponseType(ConventionFacts facts, INamedTypeSymbol type) =>
        IsExemptScalarType(type) ||
        ConventionSymbols.Same(type, facts.Symbols.Task) ||
        ConventionSymbols.Same(type, facts.Symbols.ValueTask) ||
        facts.Symbols.IsKnown(type, facts.Symbols.NoContentResult, facts.Cancellation) ||
        facts.Symbols.IsKnown(type, facts.Symbols.StatusCodeResult, facts.Cancellation) ||
        facts.Symbols.IsKnown(type, facts.Symbols.FileResult, facts.Cancellation) ||
        facts.Symbols.IsKnown(type, facts.Symbols.ProblemDetails, facts.Cancellation) ||
        ConventionSymbols.Same(type, facts.Symbols.IActionResult) ||
        ConventionSymbols.Same(type, facts.Symbols.ActionResult) ||
        ConventionSymbols.Same(type, facts.Symbols.HttpResult);

    private static bool IsExemptRequestParameter(ConventionFacts facts, IParameterSymbol parameter) =>
        ConventionSymbols.Same(parameter.Type, facts.Symbols.CancellationToken) ||
        ConventionBoundaryAnalysis.IsInjectedServiceParameter(facts, parameter) ||
        ConventionBoundaryAnalysis.HasAttribute(parameter, facts.Symbols.FromRouteAttribute) ||
        ConventionBoundaryAnalysis.HasAttribute(parameter, facts.Symbols.FromHeaderAttribute);

    private static bool IsExemptRequestType(ConventionFacts facts, INamedTypeSymbol type) =>
        IsExemptScalarType(type) ||
        facts.Symbols.IsKnown(type, facts.Symbols.FormFile, facts.Cancellation) ||
        facts.Symbols.IsKnown(type, facts.Symbols.FormFileCollection, facts.Cancellation) ||
        facts.Symbols.IsKnown(type, facts.Symbols.PaginationRequest, facts.Cancellation) ||
        facts.Symbols.IsKnown(type, facts.Symbols.PaginationOptions, facts.Cancellation) ||
        facts.Symbols.IsKnown(type, facts.Symbols.PaginationResultInfo, facts.Cancellation) ||
        facts.Symbols.IsKnown(type, facts.Symbols.EntityCreatedFilter, facts.Cancellation);

    private static bool IsExemptScalarType(INamedTypeSymbol type) =>
        type.SpecialType != SpecialType.None || type.TypeKind == TypeKind.Enum ||
        ConventionSymbols.MetadataName(type) is "System.Guid" or "System.DateTime" or "System.DateTimeOffset" or
            "System.DateOnly" or "System.TimeOnly" or "System.TimeSpan";

    private static INamedTypeSymbol? FindExposedDtoType(ConventionFacts facts, INamedTypeSymbol ownerType, ISymbol member,
        bool transport, out bool exhausted) =>
        ConventionBoundaryAnalysis.FindExposedType(facts, PublicShapeRule.Dto,
            ConventionBoundaryAnalysis.ExposureRootTypes(facts, ownerType, member, transport, includeProducesResponseTypes: transport),
            IsDtoType, ShouldExpandForDtoExposure, ConventionBoundaryAnalysis.HasPublicAccessor, out exhausted);

    private static bool IsDtoType(ConventionFacts facts, INamedTypeSymbol named) =>
        facts.Symbols.IsKnown(named, facts.Symbols.Dto, facts.Cancellation);

    private static bool ShouldExpandForDtoExposure(ConventionFacts facts, INamedTypeSymbol named) =>
        named.TypeKind is not TypeKind.Enum &&
        (facts.IsLocal(named) ||
         facts.IsNeutralValue(named) ||
         facts.ReferenceProject(named.ContainingAssembly)?.Role is ConventionArchitecture.Contract or
             ConventionArchitecture.Application or ConventionArchitecture.Hosted);

    private static void CheckResponseProjections(ConventionFacts facts, INamedTypeSymbol type,
        Lazy<ClassificationContract?> classifications)
    {
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var tree = reference.SyntaxTree;
            if (facts.IsGenerated(tree) || !facts.Compilation.ContainsSyntaxTree(tree)) continue;
            var syntax = reference.GetSyntax(facts.Cancellation);
            SemanticModel? model = null;
            HashSet<SyntaxNode>? walkedCreations = null;
            bool? hasResponseAlias = null;
            foreach (var node in syntax.DescendantNodes(descendIntoChildren: child =>
                         ReferenceEquals(child, syntax) || child is not BaseTypeDeclarationSyntax))
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (walkedCreations?.Contains(node) == true) continue;
                var isCreation = node is BaseObjectCreationExpressionSyntax creationSyntax &&
                    MayProjectContractResponse(facts, tree, creationSyntax, ref hasResponseAlias);
                var isWith = !isCreation && node is WithExpressionSyntax { Initializer.Expressions.Count: > 0 };
                if (!isCreation && !isWith) continue;
                model ??= facts.SemanticModel(tree);
                var projectionRoot = ResolveResponseProjectionRoot(facts, model, node, isWith);
                if (projectionRoot is null) continue;
                if (classifications.Value is not { } contract) return;
                walkedCreations ??= [];
                CheckResponseProjectionReads(facts, type, projectionRoot, contract, walkedCreations);
            }
        }
    }

    private static IOperation? ResolveResponseProjectionRoot(ConventionFacts facts, SemanticModel model,
        SyntaxNode node, bool isWith)
    {
        var operation = model.GetOperation(node, facts.Cancellation);
        if (isWith)
            return operation is IWithOperation { Type: INamedTypeSymbol withType } withOperation &&
                   IsContractResponseType(facts, withType)
                ? withOperation.Initializer
                : null;
        return operation is IObjectCreationOperation { Type: INamedTypeSymbol responseType } creation &&
               IsContractResponseType(facts, responseType)
            ? creation
            : null;
    }

    private static bool MayProjectContractResponse(ConventionFacts facts, SyntaxTree tree,
        BaseObjectCreationExpressionSyntax creation, ref bool? hasResponseAlias)
    {
        // An empty creation with no arguments and no initializer cannot read entity or DTO properties.
        if (creation.Initializer is null && creation.ArgumentList is not { Arguments.Count: > 0 })
            return false;
        if (creation is not ObjectCreationExpressionSyntax explicitCreation)
            return true;
        if (explicitCreation.Type is PredefinedTypeSyntax)
            return false;
        if (hasResponseAlias ??= facts.HasResponseUsingAlias(tree))
            return true;
        return ConventionFacts.RightmostTypeIdentifier(explicitCreation.Type) is { } name &&
               name.EndsWith("Response", StringComparison.Ordinal);
    }

    private static bool IsContractResponseType(ConventionFacts facts, INamedTypeSymbol type) =>
        facts.Role(type).Role == ConventionArchitecture.Contract &&
        type.Name.EndsWith("Response", StringComparison.Ordinal) &&
        !facts.Symbols.IsKnown(type, facts.Symbols.Dto, facts.Cancellation);

    private static void CheckResponseProjectionReads(ConventionFacts facts, INamedTypeSymbol ownerType,
        IOperation projectionRoot, ClassificationContract classifications, HashSet<SyntaxNode> walkedCreations)
    {
        var reported = new Dictionary<IPropertySymbol, PropertyClassifications>(SymbolEqualityComparer.Default);
        foreach (var (source, target) in MappedReadsCore(projectionRoot, facts, walkedCreations, matchConstructorArguments: true))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!HasEntityOrDtoOrigin(source, facts)) continue;
            CheckProjectedProperty(facts, ownerType, classifications, reported, source.Property, Receiver(source, facts),
                target.Property, target.Receiver, target.Location, source.Instance?.Type, target.View);
        }
    }

    private static bool HasEntityOrDtoOrigin(IPropertyReferenceOperation source, ConventionFacts facts)
    {
        var current = source.Instance;
        for (var depth = 0; current is not null && depth < 32; depth++)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            current = UnwrapInstance(current, facts);
            if (current is null) break;
            if (IsEntityOrDtoType(facts, UnwrapNullableType(current.Type), 0))
                return true;
            current = current switch
            {
                IPropertyReferenceOperation parentProperty => parentProperty.Instance,
                IFieldReferenceOperation parentField => parentField.Instance,
                IInvocationOperation parentInvocation => parentInvocation.Instance,
                IArrayElementReferenceOperation arrayElement => arrayElement.ArrayReference,
                _ => null
            };
        }
        return IsEntityOrDto(facts, Receiver(source, facts));
    }

    private static IOperation? UnwrapInstance(IOperation? instance, ConventionFacts facts)
    {
        while (instance is not null)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var operand = instance switch
            {
                IConditionalAccessInstanceOperation placeholder => ConditionalReceiver(placeholder, facts.Cancellation),
                IConversionOperation { Conversion.IsUserDefined: false } conversion => conversion.Operand,
                IParenthesizedOperation parenthesized => parenthesized.Operand,
                _ => null
            };
            if (operand is null) break;
            instance = operand;
        }
        return instance;
    }

    private static ITypeSymbol? UnwrapNullableType(ITypeSymbol? type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T, TypeArguments: [var underlying] }
            ? underlying
            : type;

    private static bool IsEntityOrDtoType(ConventionFacts facts, ITypeSymbol? type, int depth)
    {
        if (type is null || depth >= 8) return false;
        if (type is INamedTypeSymbol named) return IsEntityOrDto(facts, named);
        return type is ITypeParameterSymbol parameter &&
               parameter.ConstraintTypes.Any(constraint => IsEntityOrDtoType(facts, UnwrapNullableType(constraint), depth + 1));
    }

    private static bool IsEntityOrDto(ConventionFacts facts, INamedTypeSymbol type) =>
        facts.Symbols.IsKnown(type, facts.Symbols.Dto, facts.Cancellation) ||
        facts.IsEntity(type);

    private static bool IsPublicDtoType(ITypeSymbol type) => type switch
    {
        IArrayTypeSymbol array => IsPublicDtoType(array.ElementType),
        INamedTypeSymbol named => named.DeclaredAccessibility == Accessibility.Public &&
            named.TypeArguments.All(IsPublicDtoType) && (named.ContainingType is null || IsPublicDtoType(named.ContainingType)),
        IDynamicTypeSymbol => true,
        _ => false
    };

    private static IObjectCreationOperation? GetConstruction(ConventionFacts facts, IMethodSymbol method)
    {
        foreach (var reference in (method.PartialImplementationPart ?? method).DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax(facts.Cancellation) is not MethodDeclarationSyntax syntax) continue;
            var expression = syntax.ExpressionBody?.Expression ??
                (syntax.Body?.Statements is [ReturnStatementSyntax statement] ? statement.Expression : null);
            if (expression is null || facts.SemanticModel(syntax.SyntaxTree).GetOperation(expression, facts.Cancellation)
                is not IObjectCreationOperation creation) continue;
            return creation;
        }
        return null;
    }

    private static bool ConstructsWithEntity(ConventionFacts facts, IObjectCreationOperation creation)
    {
        // The operation also contains compiler-supplied arguments for omitted optional parameters.
        if (creation.Arguments is not [{ ArgumentKind: ArgumentKind.Explicit } argument, ..] ||
            creation.Arguments.Skip(1).Any(arg => arg.ArgumentKind != ArgumentKind.DefaultValue)) return false;
        var value = argument.Value;
        while (value is IConversionOperation { Conversion.IsReference: true } conversion) value = conversion.Operand;
        return value is IInstanceReferenceOperation { Syntax: ThisExpressionSyntax, ReferenceKind: InstanceReferenceKind.ContainingTypeInstance } &&
            ConventionSymbols.Same(argument.Parameter?.Type, facts.Symbols.Entity);
    }

    private static IEnumerable<(IPropertyReferenceOperation Source, ProjectionTarget Target)> MappedReads(
        IObjectOrCollectionInitializerOperation initializer, ConventionFacts facts) =>
        MappedReadsCore(initializer, facts, walkedCreations: null, matchConstructorArguments: false);

    private static IEnumerable<(IPropertyReferenceOperation Source, ProjectionTarget Target)> MappedReadsCore(
        IOperation root, ConventionFacts facts, HashSet<SyntaxNode>? walkedCreations, bool matchConstructorArguments)
    {
        var pending = new Stack<ProjectionNode>();
        pending.Push(new(root, null, false));
        while (pending.TryPop(out var entry))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (entry.Operation is INameOfOperation) continue;
            if (entry.Target is { } target && entry.Operation is IPropertyReferenceOperation source)
                yield return (source, target);
            PushProjectionChildren(entry, facts, walkedCreations, matchConstructorArguments, pending);
        }
    }

    private static void PushProjectionChildren(ProjectionNode entry, ConventionFacts facts,
        HashSet<SyntaxNode>? walkedCreations, bool matchConstructorArguments, Stack<ProjectionNode> pending)
    {
        var (operation, target, isInput) = entry;
        if (matchConstructorArguments && !isInput && operation is IObjectCreationOperation creation)
        {
            walkedCreations?.Add(creation.Syntax);
            PushCreationChildren(creation, target, facts, pending);
            return;
        }
        if (!isInput && operation is IWithOperation)
            walkedCreations?.Add(operation.Syntax);
        if (operation is ICollectionExpressionOperation collection)
        {
            PushCollection(collection, target, isInput, facts, pending);
            return;
        }
        if (!isInput && operation is IInvocationOperation { IsImplicit: true, Parent: IObjectOrCollectionInitializerOperation } add &&
            add.Arguments is [var argument] && add.Instance?.Type is INamedTypeSymbol collectionType &&
            IsStorageAdd(add.TargetMethod, collectionType, facts))
        {
            pending.Push(new(argument.Value, target, false));
            return;
        }
        var (written, value) = ReadWrite(operation);
        if (value is not null)
        {
            PushWrite(entry, written, value, facts, pending);
            return;
        }
        // Only operations that store a constructed value directly into the DTO keep its initializer as the destination.
        foreach (var child in operation.ChildOperations.Reverse())
            pending.Push(new(child, target, isInput || !PreservesDestination(operation, child)));
    }

    private static void PushCreationChildren(IObjectCreationOperation creation, ProjectionTarget? outerTarget,
        ConventionFacts facts, Stack<ProjectionNode> pending)
    {
        if (creation.Initializer is { } initializer)
            pending.Push(new(initializer, outerTarget, false));
        var destinationType = creation.Type as INamedTypeSymbol;
        var canMatchProperties = destinationType is not null &&
            (facts.Role(destinationType).Role == ConventionArchitecture.Contract || facts.IsLocal(destinationType));
        for (var i = creation.Arguments.Length - 1; i >= 0; i--)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var argument = creation.Arguments[i];
            if (argument.ArgumentKind == ArgumentKind.DefaultValue) continue;
            var matched = canMatchProperties ? FindDestinationProperty(destinationType!, argument.Parameter) : null;
            if (matched is not null)
            {
                var argTarget = new ProjectionTarget(matched, destinationType!, destinationType, argument.Syntax.GetLocation());
                pending.Push(new(argument.Value, argTarget, false));
            }
            else if (outerTarget is not null)
            {
                pending.Push(new(argument.Value, outerTarget, true));
            }
        }
    }

    private static IPropertySymbol? FindDestinationProperty(INamedTypeSymbol destinationType, IParameterSymbol? parameter)
    {
        if (parameter is null) return null;
        IPropertySymbol? caseInsensitive = null;
        for (var current = destinationType; current is not null; current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (property.IsStatic || property.IsIndexer || property.DeclaredAccessibility != Accessibility.Public)
                    continue;
                if (string.Equals(property.Name, parameter.Name, StringComparison.Ordinal))
                    return property;
                if (caseInsensitive is null &&
                    string.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase))
                    caseInsensitive = property;
            }
        }
        return caseInsensitive;
    }

    private static (IOperation? Target, IOperation? Value) ReadWrite(IOperation operation) => operation switch
    {
        ISimpleAssignmentOperation assignment => (assignment.Target, assignment.Value),
        IMemberInitializerOperation member => (member.InitializedMember, member.Initializer),
        _ => (null, null)
    };

    private static void PushWrite(ProjectionNode entry, IOperation? written, IOperation value,
        ConventionFacts facts, Stack<ProjectionNode> pending)
    {
        var (operation, target, isInput) = entry;
        if (written is IPropertyReferenceOperation { Property.IsIndexer: true } indexer)
        {
            pending.Push(new(value, target, true));
            foreach (var key in indexer.Arguments.Reverse()) pending.Push(new(key, target, true));
            return;
        }
        if (!isInput && operation.Parent is IObjectOrCollectionInitializerOperation && written is IPropertyReferenceOperation property)
            target = new ProjectionTarget(property.Property, Receiver(property, facts), property.Instance?.Type,
                (property.Parent ?? property).Syntax.GetLocation());
        pending.Push(new(value, target, isInput));
    }

    private static void PushCollection(ICollectionExpressionOperation collection, ProjectionTarget? target,
        bool isInput, ConventionFacts facts, Stack<ProjectionNode> pending)
    {
        var constructor = collection.ConstructMethod?.MethodKind == MethodKind.Constructor;
        if (!isInput && constructor && collection.Type is INamedTypeSymbol { TypeKind: not TypeKind.Interface } addReceiver)
        {
            PushAddElements(collection, target, addReceiver, facts, pending);
        }
        else
        {
            // Arrays, spans and compiler-provided interface collections store the element directly.
            var direct = !isInput && (collection.ConstructMethod is null && collection.Type is IArrayTypeSymbol or INamedTypeSymbol ||
                constructor && collection.Type?.TypeKind == TypeKind.Interface);
            for (var i = collection.Elements.Length - 1; i >= 0; i--)
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                pending.Push(new(collection.Elements[i], target, !direct));
            }
        }
        for (var i = collection.ConstructArguments.Length - 1; i >= 0; i--)
            pending.Push(new(collection.ConstructArguments[i], target, true));
    }

    private static void PushAddElements(ICollectionExpressionOperation collection, ProjectionTarget? target,
        INamedTypeSymbol addReceiver, ConventionFacts facts, Stack<ProjectionNode> pending)
    {
        var model = facts.SemanticModel(collection.Syntax.SyntaxTree);
        var singleAdd = model.LookupSymbols(collection.Syntax.SpanStart, addReceiver, "Add")
            is [IMethodSymbol { Parameters.Length: 1 } only] ? only : null;
        var singleStores = singleAdd is not null && IsStorageAdd(singleAdd, addReceiver, facts);
        for (var i = collection.Elements.Length - 1; i >= 0; i--)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var element = collection.Elements[i];
            var elementType = element is ISpreadOperation spread ? spread.ElementType : element.Type;
            var stored = singleAdd is not null && SymbolEqualityComparer.Default.Equals(singleAdd.Parameters[0].Type, elementType)
                ? singleStores : BindsToStorageAdd(model, addReceiver, element, facts);
            pending.Push(new(element, target, !stored));
        }
    }

    private static bool PreservesDestination(IOperation parent, IOperation child) => parent switch
    {
        IObjectOrCollectionInitializerOperation => true,
        IObjectCreationOperation creation => ReferenceEquals(child, creation.Initializer),
        IWithOperation with => ReferenceEquals(child, with.Initializer) || ReferenceEquals(child, with.Operand),
        IArrayCreationOperation array => ReferenceEquals(child, array.Initializer),
        IArrayInitializerOperation => true,
        ISpreadOperation spread => !spread.ElementConversion.IsUserDefined,
        IParenthesizedOperation => true,
        IConversionOperation { Conversion.IsUserDefined: false } => true,
        IConditionalOperation conditional => ReferenceEquals(child, conditional.WhenTrue) || ReferenceEquals(child, conditional.WhenFalse),
        ICoalesceOperation coalesce => ReferenceEquals(child, coalesce.WhenNull) || !coalesce.ValueConversion.IsUserDefined,
        ISwitchExpressionOperation switchExpression => !ReferenceEquals(child, switchExpression.Value),
        ISwitchExpressionArmOperation arm => ReferenceEquals(child, arm.Value),
        _ => false
    };

    private static bool BindsToStorageAdd(SemanticModel model, INamedTypeSymbol collection, IOperation element, ConventionFacts facts)
    {
        // Bracket expressions expose the argument but omit the implicit Add invocation.
        var argument = element.Syntax as ExpressionSyntax;
        if (element is ISpreadOperation { ElementType: { } itemType }) argument = DefaultValue(itemType);
        if (argument is null) return false;
        var add = SyntaxFactory.MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, DefaultValue(collection),
            SyntaxFactory.IdentifierName("Add"));
        var invocation = SyntaxFactory.InvocationExpression(add,
            SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(SyntaxFactory.Argument(argument))));
        return model.GetSpeculativeSymbolInfo(element.Syntax.SpanStart, invocation, SpeculativeBindingOption.BindAsExpression).Symbol
            is IMethodSymbol method && IsStorageAdd(method, collection, facts);

        static ExpressionSyntax DefaultValue(ITypeSymbol type) => SyntaxFactory.DefaultExpression(
            SyntaxFactory.ParseTypeName(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
    }

    private static bool IsStorageAdd(IMethodSymbol method, INamedTypeSymbol collection, ConventionFacts facts)
    {
        if (method.Name != "Add" || method.DeclaringSyntaxReferences.Length != 0) return false;
        // Collection contracts identify a stored element. Other Add methods can consume arbitrary helper inputs.
        foreach (var contract in collection.AllInterfaces.Prepend(collection))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!IsElementContract(contract, facts.Compilation)) continue;
            // A derived Add can reimplement the storage contract even when the DTO assembly is metadata-only.
            if (method.ContainingType.BaseType?.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default) == true) continue;
            if (contract.GetMembers("Add").Any(member => SymbolEqualityComparer.Default.Equals(method, member) ||
                SymbolEqualityComparer.Default.Equals(method, collection.FindImplementationForInterfaceMember(member))))
                return true;
        }
        return false;
    }

    private static bool IsElementContract(INamedTypeSymbol contract, Compilation compilation) =>
        contract.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_ICollection_T ||
        contract.MetadataName == "ISet`1" && ConventionSymbols.Same(contract, compilation.GetTypeByMetadataName("System.Collections.Generic.ISet`1")) ||
        contract.MetadataName == "IList" && ConventionSymbols.Same(contract, compilation.GetTypeByMetadataName("System.Collections.IList"));

    private static void CheckProjection(ConventionFacts facts, INamedTypeSymbol entity, INamedTypeSymbol dto,
        IObjectCreationOperation creation, ClassificationContract classifications)
    {
        var reported = new Dictionary<IPropertySymbol, PropertyClassifications>(SymbolEqualityComparer.Default);
        var assigned = AssignedProperties(creation.Initializer);
        if (creation.Initializer is { } initializer)
        {
            foreach (var (source, target) in MappedReads(initializer, facts))
                CheckProjectedProperty(facts, entity, classifications, reported, source.Property, Receiver(source, facts),
                    target.Property, target.Receiver, target.Location, source.Instance?.Type, target.View);
        }
        foreach (var (source, target) in ImplicitMappings(facts, entity, dto, assigned))
            CheckProjectedProperty(facts, entity, classifications, reported, source, entity, target, dto, creation.Syntax.GetLocation());
    }

    private static void CheckProjectedProperty(ConventionFacts facts, INamedTypeSymbol ownerType,
        ClassificationContract classifications, Dictionary<IPropertySymbol, PropertyClassifications> reported,
        IPropertySymbol source, INamedTypeSymbol receiver, IPropertySymbol target, INamedTypeSymbol targetReceiver,
        Location location, ITypeSymbol? sourceView = null, ITypeSymbol? targetView = null)
    {
        facts.Cancellation.ThrowIfCancellationRequested();
        var (property, context) = classifications.ProjectionClassification(source, receiver, sourceView, facts.Cancellation);
        reported.TryGetValue(target, out var previous);
        if (((property | context) & ~previous) == PropertyClassifications.None) return;
        var onTarget = classifications.SymbolKinds(target, facts.Cancellation);
        var missing = property & ~onTarget;
        var contextual = context & ~(onTarget | previous | missing);
        if (contextual != PropertyClassifications.None)
        {
            var effective = classifications.ProjectionClassification(target, targetReceiver, targetView, facts.Cancellation);
            missing |= contextual & ~(effective.Property | effective.Context);
        }
        missing &= ~previous;
        if (missing == PropertyClassifications.None) return;
        reported[target] = previous | missing;
        if (missing.HasFlag(PropertyClassifications.Personal))
            Report(facts, ConventionDiagnosticDescriptors.DtoClassification, ownerType, location, target.Name, "Personal", source.Name);
        if (missing.HasFlag(PropertyClassifications.Sensitive))
            Report(facts, ConventionDiagnosticDescriptors.DtoClassification, ownerType, location, target.Name, "Sensitive", source.Name);
    }

    private static HashSet<IPropertySymbol> AssignedProperties(IObjectOrCollectionInitializerOperation? initializer)
    {
        var assigned = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
        if (initializer is null) return assigned;
        foreach (var assignment in initializer.Initializers)
            if (ReadWrite(assignment).Target is IPropertyReferenceOperation property)
                assigned.Add(property.Property);
        return assigned;
    }

    private static IEnumerable<(IPropertySymbol Source, IPropertySymbol Target)> ImplicitMappings(
        ConventionFacts facts, INamedTypeSymbol entity, INamedTypeSymbol dto, HashSet<IPropertySymbol> assigned)
    {
        var overridden = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
        for (var contract = dto; contract is not null && !ConventionSymbols.Same(contract, facts.Symbols.Dto); contract = contract.BaseType)
        {
            foreach (var target in contract.GetMembers().OfType<IPropertySymbol>().Where(Eligible))
            {
                if (target.OverriddenProperty is { } parent) overridden.Add(parent);
                if (overridden.Contains(target) || assigned.Contains(target)) continue;
                if (FindSourceProperty(entity, target.Name, facts.Symbols.Entity) is { } source)
                    yield return (source, target);
            }
        }
    }

    private static IPropertySymbol? FindSourceProperty(INamedTypeSymbol entity, string name, INamedTypeSymbol? entityBase)
    {
        for (var domain = entity; domain is not null && !ConventionSymbols.Same(domain, entityBase); domain = domain.BaseType)
        {
            if (domain.GetMembers(name).OfType<IPropertySymbol>().FirstOrDefault() is { } source)
                return source;
        }
        return null;
    }

    private static INamedTypeSymbol Receiver(IPropertyReferenceOperation property, ConventionFacts facts)
    {
        var instance = property.Instance;
        while (instance is not null)
        {
            var operand = instance switch
            {
                IConditionalAccessInstanceOperation { Type.IsReferenceType: true } placeholder => ConditionalReceiver(placeholder, facts.Cancellation),
                IConversionOperation { Conversion.IsUserDefined: false, Operand.Type: INamedTypeSymbol from, Type: { } to } conversion
                    when IsImplicitUpcastOrBoxing(facts.Compilation, from, to) => conversion.Operand,
                _ => null
            };
            if (operand is null) break;
            instance = operand;
        }
        var receiver = instance?.Type;
        if (receiver is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable &&
            !SymbolEqualityComparer.Default.Equals(nullable, property.Property.ContainingType))
            receiver = nullable.TypeArguments[0];
        return receiver as INamedTypeSymbol ?? property.Property.ContainingType;
    }

    private static bool IsImplicitUpcastOrBoxing(Compilation compilation, ITypeSymbol from, ITypeSymbol to) =>
        compilation is CSharpCompilation csharp
            ? csharp.ClassifyConversion(from, to) is { IsImplicit: true } conversion &&
              (conversion.IsReference || conversion.IsBoxing || conversion.IsIdentity)
            : compilation.ClassifyCommonConversion(from, to) is { IsImplicit: true } common &&
              (common.IsReference || common.IsIdentity);

    private static IOperation? ConditionalReceiver(IConditionalAccessInstanceOperation placeholder, CancellationToken cancellation)
    {
        // In a?.B?.C the placeholder for B is in the receiver of the inner conditional and the non-null branch of the outer one.
        IOperation child = placeholder;
        while (child.Parent is { } parent)
        {
            cancellation.ThrowIfCancellationRequested();
            if (parent is IConditionalAccessOperation access && ReferenceEquals(access.WhenNotNull, child))
                return access.Operation;
            child = parent;
        }
        return null;
    }

    private static void Report(ConventionFacts facts, DiagnosticDescriptor descriptor, INamedTypeSymbol target,
        Location location, params object[] args) => facts.Diagnostics.Add(Diagnostic.Create(descriptor, location,
        ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField, target.GetDocumentationCommentId()), args));
}
