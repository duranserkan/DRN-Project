using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Operations;
using static DRN.Framework.SharedKernel.Analyzers.ClassificationContract;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionDtoAnalysis
{
    private const string RequestSuffix = "Request";
    private const string ResponseSuffix = "Response";
    private static readonly PublicShapePolicy DtoExposurePolicy = new(
        PublicShapeRule.Dto,
        ConventionDiagnosticDescriptors.DtoExposure,
        IsDtoType,
        ShouldExpandForDtoExposure,
        IncludeExposedLocations: false);

    private readonly record struct DtoPlacement(string Namespace, string? Directory);
    private readonly record struct DtoKey(string Root, string Domain, string Subdomain, bool HasFolderDomain, string Stem);
    private readonly record struct EndpointCheckContext(ConventionFacts Facts, INamedTypeSymbol Controller,
        IMethodSymbol Method, ISymbol ReportingSymbol, Location Location, bool IsSourceKnown);
    private readonly record struct ProjectionTarget(IPropertySymbol Property, INamedTypeSymbol TargetReceiver, ITypeSymbol? View, Location Location);
    private readonly record struct ProjectionSource(IPropertySymbol Property, INamedTypeSymbol SourceReceiver, ITypeSymbol? View = null);
    private readonly record struct ProjectionCheckContext(ConventionFacts Facts, INamedTypeSymbol OwnerType,
        ClassificationContract Classifications, Dictionary<IPropertySymbol, PropertyClassifications> Reported);
    private readonly record struct ProjectionNode(IOperation Operation, ProjectionTarget? Target, bool IsInput);

    internal static void Analyze(ConventionFacts facts)
    {
        var classifications = new Lazy<ClassificationContract?>(() => CreateClassifications(facts.Compilation), isThreadSafe: false);
        var dtoStems = new Dictionary<DtoKey, DtoPlacement?>();
        var messages = new List<INamedTypeSymbol>();
        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            AnalyzeType(facts, type, classifications, dtoStems, messages);
        }
        foreach (var message in messages)
            CheckMessagePlacement(facts, message, dtoStems);
    }

    private static void AnalyzeType(ConventionFacts facts, INamedTypeSymbol type,
        Lazy<ClassificationContract?> classifications, Dictionary<DtoKey, DtoPlacement?> dtoStems,
        List<INamedTypeSymbol> messages)
    {
        var role = facts.Role(type);
        var hasLocation = facts.Location(type) != Location.None;
        if (hasLocation)
            AnalyzeTypeSurface(facts, type, role.Role, classifications, messages);
        if (type.IsAbstract) return;
        AnalyzeConcreteType(facts, type, role, hasLocation, classifications, dtoStems);
    }

    private static void AnalyzeTypeSurface(ConventionFacts facts, INamedTypeSymbol type, string role,
        Lazy<ClassificationContract?> classifications, List<INamedTypeSymbol> messages)
    {
        if (role == ConventionArchitecture.Contract && ConventionBoundaryAnalysis.IsPublic(type))
        {
            if (MessageStemLength(type.Name) > 0) messages.Add(type);
            CheckContractDtoExposure(facts, type);
        }
        if (ConventionBoundaryAnalysis.IsExposedController(facts, type))
            CheckControllerContracts(facts, type);
        if (role is ConventionArchitecture.Hosted or ConventionArchitecture.Application or
            ConventionArchitecture.Domain or ConventionArchitecture.Infra or ConventionArchitecture.Contract)
            CheckResponseProjections(facts, type, classifications);
    }

    private static void AnalyzeConcreteType(ConventionFacts facts, INamedTypeSymbol type,
        (string Role, string Namespace, string PathPrefix) role, bool hasLocation,
        Lazy<ClassificationContract?> classifications, Dictionary<DtoKey, DtoPlacement?> dtoStems)
    {
        if (hasLocation && !facts.Symbols.IsOpen(type, facts.Cancellation) &&
            role.Role == ConventionArchitecture.Domain &&
            facts.Symbols.IsKnown(type, facts.Symbols.Entity, facts.Cancellation))
            CheckMapper(facts, type, classifications);
        if (role.Role == ConventionArchitecture.Contract && type.Name.EndsWith("Dto", StringComparison.Ordinal) &&
            type.Name.Length > 3 && facts.Symbols.IsKnown(type, facts.Symbols.Dto, facts.Cancellation))
            CheckDtoPlacement(facts, type, role.Namespace, dtoStems);
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
            ConventionBoundaryAnalysis.Report(facts, ConventionDiagnosticDescriptors.DtoFolder, dto, location, dto.Name, folder);
    }

    private static int MessageStemLength(string name)
    {
        if (name.EndsWith(RequestSuffix, StringComparison.Ordinal)) return name.Length - RequestSuffix.Length;
        if (name.EndsWith(ResponseSuffix, StringComparison.Ordinal)) return name.Length - ResponseSuffix.Length;
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
            ConventionBoundaryAnalysis.Report(facts, ConventionDiagnosticDescriptors.EntityDto, entity, facts.Location(entity), entity.Name, stem + "Dto");
        if (creation?.Type is INamedTypeSymbol projected && facts.Symbols.IsKnown(projected, facts.Symbols.Dto, facts.Cancellation) &&
            classifications.Value is { } contract)
            CheckProjection(facts, entity, projected, creation, contract);
    }

    private static void CheckContractDtoExposure(ConventionFacts facts, INamedTypeSymbol type)
    {
        var isDto = facts.Symbols.IsKnown(type, facts.Symbols.Dto, facts.Cancellation);
        if (isDto)
        {
            if (MessageStemLength(type.Name) > 0)
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.DtoExposure, facts.Location(type),
                    ConventionBoundaryAnalysis.Properties(type, type.BaseType ?? type),
                    type.ToDisplayString(), type.BaseType?.ToDisplayString() ?? type.ToDisplayString()));
            return;
        }
        ConventionBoundaryAnalysis.AnalyzeExposedPublicShape(facts, type, transport: false, DtoExposurePolicy);
    }

    private static void CheckControllerContracts(ConventionFacts facts, INamedTypeSymbol controller)
    {
        var nonAction = facts.Symbols.NonActionAttribute;
        var isSourceKnown = facts.Symbols.IsKnown(controller, facts.Symbols.SourceKnownReadOnlyController, facts.Cancellation);
        ConventionBoundaryAnalysis.AnalyzeExposedPublicShape(facts, controller, transport: true, DtoExposurePolicy);
        HashSet<INamedTypeSymbol>? reportedFrameworkResponses = null;
        HashSet<INamedTypeSymbol>? reportedFrameworkRequests = null;
        foreach (var member in ConventionBoundaryAnalysis.PublicMembers(facts, controller, transport: true))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!ConventionBoundaryAnalysis.IsInspectablePublicMember(facts, controller, member, transport: true, nonAction) ||
                member is not IMethodSymbol method || ConventionBoundaryAnalysis.IsObjectOrDisposeMethod(facts, method) ||
                !ConventionBoundaryAnalysis.TryGetReportingTarget(facts, controller, method, out var reportingSymbol, out var location))
                continue;

            var reportsOnType = SymbolEqualityComparer.Default.Equals(reportingSymbol, controller);
            var context = new EndpointCheckContext(facts, controller, method, reportingSymbol, location, isSourceKnown);
            var inheritsStandardAction = isSourceKnown && reportsOnType &&
                (ConventionSymbols.Same(method.ContainingType, facts.Symbols.SourceKnownReadOnlyController) ||
                 ConventionSymbols.Same(method.ContainingType, facts.Symbols.SourceKnownCombinedController));
            if (inheritsStandardAction)
            {
                CheckEndpointExplicitResponse(context, ref reportedFrameworkResponses);
                CheckEndpointExplicitRequests(context, ref reportedFrameworkRequests);
            }
            else
            {
                HashSet<INamedTypeSymbol>? reportedResponses = null;
                HashSet<INamedTypeSymbol>? reportedRequests = null;
                CheckEndpointExplicitResponse(context, ref reportedResponses);
                CheckEndpointExplicitRequests(context, ref reportedRequests);
            }
        }
    }

    private static void CheckEndpointExplicitResponse(EndpointCheckContext context, ref HashSet<INamedTypeSymbol>? reported)
    {
        CheckExplicitResponseType(context, context.Method.ReturnType, ref reported);
        foreach (var responseType in ConventionBoundaryAnalysis.ProducesResponseTypes(context.Facts, context.Controller, context.Method))
            CheckExplicitResponseType(context, responseType, ref reported);
    }

    private static void CheckExplicitResponseType(EndpointCheckContext context, ITypeSymbol responseType,
        ref HashSet<INamedTypeSymbol>? reported)
    {
        var facts = context.Facts;
        var payload = UnwrapResponsePayload(facts, responseType);
        if (payload is null || IsExemptResponseType(facts, payload)) return;
        if (facts.Symbols.IsKnown(payload, facts.Symbols.Dto, facts.Cancellation)) return;
        var isContractResponse = facts.Role(payload).Role == ConventionArchitecture.Contract &&
            payload.Name.EndsWith(ResponseSuffix, StringComparison.Ordinal) &&
            payload.Name.Length > ResponseSuffix.Length;
        if (isContractResponse) return;
        if (RequiresExplicitContractMessage(facts, payload, context.IsSourceKnown))
        {
            reported ??= new(SymbolEqualityComparer.Default);
            if (!reported.Add(payload)) return;
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.ExplicitContractMessage, context.Location,
                ConventionBoundaryAnalysis.Properties(context.ReportingSymbol, payload),
                context.Method.ToDisplayString(), ResponseSuffix, payload.ToDisplayString()));
        }
    }

    private static void CheckEndpointExplicitRequests(EndpointCheckContext context, ref HashSet<INamedTypeSymbol>? reported)
    {
        var facts = context.Facts;
        foreach (var parameter in context.Method.Parameters)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (IsExemptRequestParameter(facts, parameter)) continue;
            var payload = UnwrapRequestPayload(facts, parameter.Type);
            if (payload is null || IsExemptRequestType(facts, payload)) continue;
            if (facts.Symbols.IsKnown(payload, facts.Symbols.Dto, facts.Cancellation)) continue;
            var isContractRequest = facts.Role(payload).Role == ConventionArchitecture.Contract &&
                payload.Name.EndsWith(RequestSuffix, StringComparison.Ordinal) &&
                payload.Name.Length > RequestSuffix.Length;
            if (isContractRequest || !RequiresExplicitContractMessage(facts, payload, context.IsSourceKnown)) continue;
            reported ??= new(SymbolEqualityComparer.Default);
            if (!reported.Add(payload)) continue;
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.ExplicitContractMessage, context.Location,
                ConventionBoundaryAnalysis.Properties(context.ReportingSymbol, payload),
                context.Method.ToDisplayString(), RequestSuffix, payload.ToDisplayString()));
        }
    }

    private static bool RequiresExplicitContractMessage(ConventionFacts facts, INamedTypeSymbol payload, bool isSourceKnown) =>
        isSourceKnown ||
        payload.SpecialType == SpecialType.System_Object ||
        IsCollectionWrapper(facts, payload) ||
        facts.Role(payload).Role is ConventionArchitecture.Contract or ConventionArchitecture.Application;

    private static INamedTypeSymbol? UnwrapResponsePayload(ConventionFacts facts, ITypeSymbol type) =>
        UnwrapPayload(facts, type, unwrapResponseWrappers: true, maxDepth: 64);

    private static INamedTypeSymbol? UnwrapRequestPayload(ConventionFacts facts, ITypeSymbol type) =>
        UnwrapPayload(facts, type, unwrapResponseWrappers: false, maxDepth: 64);

    private static INamedTypeSymbol? UnwrapPayload(ConventionFacts facts, ITypeSymbol type,
        bool unwrapResponseWrappers, int maxDepth)
    {
        var current = type;
        for (var depth = 0; depth < maxDepth; depth++)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (current.TypeKind == TypeKind.Error) return null;
            if (current is IDynamicTypeSymbol) return DynamicAsObject(facts);
            if (current is IArrayTypeSymbol array)
            {
                current = array.ElementType;
                continue;
            }
            if (current is not INamedTypeSymbol named) return null;
            if (!IsPayloadWrapper(facts, named, unwrapResponseWrappers)) return named;
            current = named.TypeArguments[0];
        }
        return current switch
        {
            IDynamicTypeSymbol or IArrayTypeSymbol => DynamicAsObject(facts),
            INamedTypeSymbol { TypeKind: not TypeKind.Error } result => result,
            _ => null
        };
    }

    private static bool IsPayloadWrapper(ConventionFacts facts, INamedTypeSymbol named, bool unwrapResponseWrappers)
    {
        if (named.TypeArguments.Length != 1) return false;
        if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T || IsCollectionWrapper(facts, named)) return true;
        return unwrapResponseWrappers &&
            (ConventionSymbols.Same(named, facts.Symbols.TaskGeneric) ||
             ConventionSymbols.Same(named, facts.Symbols.ValueTaskGeneric) ||
             ConventionSymbols.Same(named, facts.Symbols.ActionResultGeneric) ||
             ConventionSymbols.Same(named, facts.Symbols.PaginationResultModel));
    }

    private static INamedTypeSymbol? DynamicAsObject(ConventionFacts facts) =>
        facts.Compilation.GetSpecialType(SpecialType.System_Object) is { TypeKind: not TypeKind.Error } obj ? obj : null;

    private static bool IsCollectionWrapper(ConventionFacts facts, INamedTypeSymbol named)
    {
        if (named.TypeArguments.Length != 1) return false;
        var definition = named.OriginalDefinition;
        if (definition.SpecialType is
            SpecialType.System_Collections_Generic_IEnumerable_T or
            SpecialType.System_Collections_Generic_IReadOnlyList_T or
            SpecialType.System_Collections_Generic_IReadOnlyCollection_T or
            SpecialType.System_Collections_Generic_IList_T or
            SpecialType.System_Collections_Generic_ICollection_T)
            return true;
        var symbols = facts.Symbols;
        return ConventionSymbols.Same(definition, symbols.ListGeneric) ||
               ConventionSymbols.Same(definition, symbols.AsyncEnumerableGeneric) ||
               ConventionSymbols.Same(definition, symbols.ImmutableArrayGeneric) ||
               ConventionSymbols.Same(definition, symbols.ImmutableListGeneric) ||
               ConventionSymbols.Same(definition, symbols.SetGeneric) ||
               ConventionSymbols.Same(definition, symbols.ReadOnlySetGeneric) ||
               ConventionSymbols.Same(definition, symbols.HashSetGeneric) ||
               ConventionSymbols.Same(definition, symbols.CollectionGeneric) ||
               ConventionSymbols.Same(definition, symbols.ReadOnlyCollectionGeneric);
    }

    private static bool IsExemptResponseType(ConventionFacts facts, INamedTypeSymbol type) =>
        type.SpecialType == SpecialType.System_Void ||
        IsExemptScalarType(facts, type) ||
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
        IsExemptScalarType(facts, type) ||
        facts.Symbols.IsKnown(type, facts.Symbols.FormFile, facts.Cancellation) ||
        facts.Symbols.IsKnown(type, facts.Symbols.FormFileCollection, facts.Cancellation) ||
        facts.Symbols.IsKnown(type, facts.Symbols.PaginationRequest, facts.Cancellation) ||
        facts.Symbols.IsKnown(type, facts.Symbols.PaginationOptions, facts.Cancellation) ||
        facts.Symbols.IsKnown(type, facts.Symbols.PaginationResultInfo, facts.Cancellation) ||
        facts.Symbols.IsKnown(type, facts.Symbols.EntityCreatedFilter, facts.Cancellation);

    private static bool IsExemptScalarType(ConventionFacts facts, INamedTypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Enum) return true;
        if (type.SpecialType is
            SpecialType.System_Boolean or SpecialType.System_Char or SpecialType.System_SByte or
            SpecialType.System_Byte or SpecialType.System_Int16 or SpecialType.System_UInt16 or
            SpecialType.System_Int32 or SpecialType.System_UInt32 or SpecialType.System_Int64 or
            SpecialType.System_UInt64 or SpecialType.System_Decimal or SpecialType.System_Single or
            SpecialType.System_Double or SpecialType.System_String or SpecialType.System_DateTime or
            SpecialType.System_IntPtr or SpecialType.System_UIntPtr)
            return true;
        var symbols = facts.Symbols;
        return ConventionSymbols.Same(type, symbols.Guid) ||
               ConventionSymbols.Same(type, symbols.DateTimeOffset) ||
               ConventionSymbols.Same(type, symbols.DateOnly) ||
               ConventionSymbols.Same(type, symbols.TimeOnly) ||
               ConventionSymbols.Same(type, symbols.TimeSpan) ||
               ConventionSymbols.Same(type, symbols.Uri);
    }

    private static bool IsDtoType(ConventionFacts facts, INamedTypeSymbol named) =>
        facts.Symbols.IsKnown(named, facts.Symbols.Dto, facts.Cancellation);

    private static bool ShouldExpandForDtoExposure(ConventionFacts facts, INamedTypeSymbol named) =>
        ConventionBoundaryAnalysis.ShouldExpandPublicModel(facts, named, includeDto: false);

    private static void CheckResponseProjections(ConventionFacts facts, INamedTypeSymbol type,
        Lazy<ClassificationContract?> classifications)
    {
        foreach (var reference in type.DeclaringSyntaxReferences)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var tree = reference.SyntaxTree;
            if (facts.IsGenerated(tree) || !facts.Compilation.ContainsSyntaxTree(tree)) continue;
            if (!CheckResponseProjectionsInSyntax(facts, type, tree, reference.GetSyntax(facts.Cancellation), classifications))
                return;
        }
    }

    private static bool CheckResponseProjectionsInSyntax(ConventionFacts facts, INamedTypeSymbol type, SyntaxTree tree,
        SyntaxNode syntax, Lazy<ClassificationContract?> classifications)
    {
        SemanticModel? model = null;
        HashSet<SyntaxNode>? walkedCreations = null;
        bool? hasResponseAlias = null;
        foreach (var node in syntax.DescendantNodes(descendIntoChildren: child =>
                     ReferenceEquals(child, syntax) || child is not BaseTypeDeclarationSyntax))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (walkedCreations?.Contains(node) == true) continue;
            if (!IsCandidateResponseProjection(facts, tree, node, ref hasResponseAlias, out var isWith)) continue;
            model ??= facts.SemanticModel(tree);
            if (ResolveResponseProjectionRoot(facts, model, node, isWith) is not { } projectionRoot) continue;
            if (classifications.Value is not { } contract) return false;
            walkedCreations ??= [];
            CheckResponseProjectionReads(facts, type, projectionRoot, contract, walkedCreations);
        }
        return true;
    }

    private static bool IsCandidateResponseProjection(ConventionFacts facts, SyntaxTree tree, SyntaxNode node,
        ref bool? hasResponseAlias, out bool isWith)
    {
        isWith = false;
        if (node is BaseObjectCreationExpressionSyntax creationSyntax)
            return MayProjectContractResponse(facts, tree, creationSyntax, ref hasResponseAlias);
        isWith = node is WithExpressionSyntax { Initializer.Expressions.Count: > 0 };
        return isWith;
    }

    private static IOperation? ResolveResponseProjectionRoot(ConventionFacts facts, SemanticModel model,
        SyntaxNode node, bool isWith)
    {
        var operation = model.GetOperation(node, facts.Cancellation);
        if (isWith)
            return operation is IWithOperation { Type: INamedTypeSymbol withType } withOperation &&
                   facts.IsResponseModel(withType)
                ? withOperation.Initializer
                : null;
        return operation is IObjectCreationOperation { Type: INamedTypeSymbol responseType } creation &&
               facts.IsResponseModel(responseType)
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
        if (ConventionFacts.RightmostTypeIdentifier(explicitCreation.Type) is { } name &&
            facts.MayBeResponseModel(name))
            return true;
        return hasResponseAlias ??= facts.HasResponseUsingAlias(tree);
    }

    private static void CheckResponseProjectionReads(ConventionFacts facts, INamedTypeSymbol ownerType,
        IOperation projectionRoot, ClassificationContract classifications, HashSet<SyntaxNode> walkedCreations)
    {
        var context = new ProjectionCheckContext(facts, ownerType, classifications,
            new Dictionary<IPropertySymbol, PropertyClassifications>(SymbolEqualityComparer.Default));
        foreach (var (source, target) in MappedReadsCore(projectionRoot, facts, walkedCreations, matchConstructorArguments: true))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!HasEntityOrDtoOrigin(source, facts)) continue;
            CheckProjectedProperty(context, new(source.Property, Receiver(source, facts), source.Instance?.Type), target);
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
                IInvocationOperation { Instance: { } receiver } => receiver,
                IInvocationOperation { TargetMethod.IsExtensionMethod: true } extensionCall =>
                    extensionCall.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 0)?.Value,
                IArrayElementReferenceOperation arrayElement => arrayElement.ArrayReference,
                IParameterReferenceOperation parameter => SelectSource(parameter, facts),
                _ => null
            };
        }
        return IsEntityOrDto(facts, Receiver(source, facts));
    }

    private static IOperation? SelectSource(IParameterReferenceOperation reference, ConventionFacts facts)
    {
        if (reference.Parameter.Ordinal != 0 ||
            reference.Parameter.ContainingSymbol is not IMethodSymbol { MethodKind: MethodKind.AnonymousFunction } owner)
            return null;
        for (var current = reference.Parent; current is not null; current = current.Parent)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (current is not IAnonymousFunctionOperation lambda ||
                !SymbolEqualityComparer.Default.Equals(lambda.Symbol, owner)) continue;
            var parent = lambda.Parent;
            while (parent is IDelegateCreationOperation or IConversionOperation or IParenthesizedOperation)
                parent = parent.Parent;
            if (parent is not IArgumentOperation { Parameter.Ordinal: 1, Parent: IInvocationOperation call } ||
                !IsLinqSelect(call.TargetMethod, facts.Symbols)) return null;
            return call.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 0)?.Value;
        }
        return null;
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

    private static bool IsLinqSelect(IMethodSymbol method, ConventionSymbols symbols) =>
        method.Name == "Select" && (ConventionSymbols.Same(method.ContainingType, symbols.Enumerable) ||
            ConventionSymbols.Same(method.ContainingType, symbols.Queryable) ||
            ConventionSymbols.Same(method.ContainingType, symbols.ImmutableArrayExtensions));

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
        type.TypeKind != TypeKind.Error &&
        (facts.Symbols.IsKnown(type, facts.Symbols.Dto, facts.Cancellation) ||
         facts.IsEntity(type));

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
        if (matchConstructorArguments && !isInput && operation is IInvocationOperation call &&
            PushSequenceProjection(call, target, facts.Symbols, pending)) return;
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
        var canMatchProperties = destinationType is { TypeKind: not TypeKind.Error } &&
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

    private static bool PushSequenceProjection(IInvocationOperation call, ProjectionTarget? target,
        ConventionSymbols symbols, Stack<ProjectionNode> pending)
    {
        var method = call.TargetMethod;
        if (call.Arguments is [var sequence] && ((method.Name is "ToArray" or "ToList") &&
                ConventionSymbols.Same(method.ContainingType, symbols.Enumerable) ||
                method.Name == "ToImmutableArray" && ConventionSymbols.Same(method.ContainingType, symbols.ImmutableArrayFactory)))
        {
            pending.Push(new(sequence.Value, target, false));
            return true;
        }
        if (!IsLinqSelect(method, symbols)) return false;
        var selector = call.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 1)?.Value;
        while (selector is IConversionOperation { Conversion.IsUserDefined: false } or IDelegateCreationOperation or IParenthesizedOperation)
            selector = selector.ChildOperations.First();
        if (selector is not IAnonymousFunctionOperation lambda ||
            call.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 0) is not { } source) return false;
        pending.Push(new(lambda.Body, target, false));
        pending.Push(new(source.Value, target, true));
        return true;
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
        IBlockOperation or IReturnOperation => true,
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
            if (!IsElementContract(contract, facts.Symbols)) continue;
            // A derived Add can reimplement the storage contract even when the DTO assembly is metadata-only.
            if (method.ContainingType.BaseType?.AllInterfaces.Contains(contract, SymbolEqualityComparer.Default) == true) continue;
            if (contract.GetMembers("Add").Any(member => SymbolEqualityComparer.Default.Equals(method, member) ||
                SymbolEqualityComparer.Default.Equals(method, collection.FindImplementationForInterfaceMember(member))))
                return true;
        }
        return false;
    }

    private static bool IsElementContract(INamedTypeSymbol contract, ConventionSymbols symbols) =>
        contract.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_ICollection_T ||
        ConventionSymbols.Same(contract, symbols.SetGeneric) ||
        ConventionSymbols.Same(contract, symbols.NonGenericList);

    private static void CheckProjection(ConventionFacts facts, INamedTypeSymbol entity, INamedTypeSymbol dto,
        IObjectCreationOperation creation, ClassificationContract classifications)
    {
        var context = new ProjectionCheckContext(facts, entity, classifications,
            new Dictionary<IPropertySymbol, PropertyClassifications>(SymbolEqualityComparer.Default));
        var assigned = AssignedProperties(creation.Initializer);
        if (creation.Initializer is { } initializer)
        {
            foreach (var (source, target) in MappedReads(initializer, facts))
                CheckProjectedProperty(context, new(source.Property, Receiver(source, facts), source.Instance?.Type), target);
        }
        foreach (var (source, target) in ImplicitMappings(facts, entity, dto, assigned))
            CheckProjectedProperty(context, new(source, entity), new(target, dto, null, creation.Syntax.GetLocation()));
    }

    private static void CheckProjectedProperty(ProjectionCheckContext context, ProjectionSource source, ProjectionTarget target)
    {
        var facts = context.Facts;
        var classifications = context.Classifications;
        facts.Cancellation.ThrowIfCancellationRequested();
        var (property, contextualKind) = classifications.ProjectionClassification(
            source.Property, source.SourceReceiver, source.View, facts.Cancellation);
        context.Reported.TryGetValue(target.Property, out var previous);
        if (((property | contextualKind) & ~previous) == PropertyClassifications.None) return;
        var onTarget = classifications.SymbolKinds(target.Property, facts.Cancellation);
        var missing = property & ~onTarget;
        var contextual = contextualKind & ~(onTarget | previous | missing);
        if (contextual != PropertyClassifications.None)
        {
            var effective = classifications.ProjectionClassification(
                target.Property, target.TargetReceiver, target.View, facts.Cancellation);
            missing |= contextual & ~(effective.Property | effective.Context);
        }
        missing &= ~previous;
        if (missing == PropertyClassifications.None) return;
        context.Reported[target.Property] = previous | missing;
        if (missing.HasFlag(PropertyClassifications.Personal))
            ConventionBoundaryAnalysis.Report(facts, ConventionDiagnosticDescriptors.DtoClassification, context.OwnerType, target.Location,
                target.Property.Name, "Personal", source.Property.Name);
        if (missing.HasFlag(PropertyClassifications.Sensitive))
            ConventionBoundaryAnalysis.Report(facts, ConventionDiagnosticDescriptors.DtoClassification, context.OwnerType, target.Location,
                target.Property.Name, "Sensitive", source.Property.Name);
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
}
