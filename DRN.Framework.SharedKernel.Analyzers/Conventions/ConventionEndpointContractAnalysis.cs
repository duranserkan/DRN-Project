namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionEndpointContractAnalysis
{
    private static readonly PublicShapePolicy DtoExposurePolicy = new(
        PublicShapeRule.Dto,
        ConventionDiagnosticDescriptors.DtoExposure,
        IsDtoType,
        ConventionBoundaryAnalysis.ShouldExpandPublicModel,
        IncludeExposedLocations: false,
        ExpandExposedMembers: true);

    private readonly record struct EndpointCheckContext(
        ConventionFacts Facts,
        INamedTypeSymbol Controller,
        IMethodSymbol Method,
        ISymbol ReportingSymbol,
        Location Location,
        bool IsSourceKnown);

    internal static void CheckContractDtoExposure(ConventionFacts facts, INamedTypeSymbol type)
    {
        var isNonMessageDto = facts.Symbols.IsKnown(type, facts.Symbols.Dto, facts.Cancellation) &&
            ConventionDtoPlacementAnalysis.MessageStemLength(type.Name) == 0;
        if (isNonMessageDto) return;
        ConventionBoundaryAnalysis.AnalyzeExposedPublicShape(facts, type, transport: false, DtoExposurePolicy);
    }

    internal static void CheckControllerContracts(ConventionFacts facts, INamedTypeSymbol controller)
    {
        var nonAction = facts.Symbols.NonActionAttribute;
        var isSourceKnown = facts.Symbols.IsKnown(controller, facts.Symbols.SourceKnownReadOnlyController, facts.Cancellation);
        ConventionBoundaryAnalysis.AnalyzeExposedPublicShape(facts, controller, transport: true, DtoExposurePolicy);
        HashSet<INamedTypeSymbol>? reportedFrameworkResponses = null;
        HashSet<INamedTypeSymbol>? reportedFrameworkRequests = null;
        foreach (var member in ConventionBoundaryAnalysis.PublicMembers(facts, controller, transport: true))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var isInspectableAction = ConventionBoundaryAnalysis.IsInspectablePublicMember(
                    facts, controller, member, transport: true, nonAction, DtoExposurePolicy) &&
                member is IMethodSymbol method &&
                !ConventionBoundaryAnalysis.IsObjectOrDisposeMethod(facts, method);
            if (!isInspectableAction) continue;

            var action = (IMethodSymbol)member;
            if (!ConventionBoundaryAnalysis.TryGetReportingTarget(facts, controller, action, out var reportingSymbol, out var location))
                continue;

            var reportsOnType = SymbolEqualityComparer.Default.Equals(reportingSymbol, controller);
            var context = new EndpointCheckContext(facts, controller, action, reportingSymbol, location, isSourceKnown);
            var inheritsStandardAction = isSourceKnown && reportsOnType &&
                (ConventionSymbols.Same(action.ContainingType, facts.Symbols.SourceKnownReadOnlyController) ||
                 ConventionSymbols.Same(action.ContainingType, facts.Symbols.SourceKnownCombinedController));
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

    private static void CheckExplicitResponseType(
        EndpointCheckContext context, ITypeSymbol responseType, ref HashSet<INamedTypeSymbol>? reported)
    {
        var facts = context.Facts;
        var payload = UnwrapResponsePayload(facts, responseType);
        if (payload is null || IsExemptResponseType(facts, payload)) return;
        if (IsDtoType(facts, payload)) return;
        var isContractResponse = facts.Role(payload).Role == ConventionArchitecture.Contract &&
            payload.Name.EndsWith(ConventionDtoPlacementAnalysis.ResponseSuffix, StringComparison.Ordinal) &&
            payload.Name.Length > ConventionDtoPlacementAnalysis.ResponseSuffix.Length;
        if (isContractResponse || !RequiresExplicitContractMessage(facts, payload, context.IsSourceKnown)) return;
        ReportExplicitMessage(context, payload, ConventionDtoPlacementAnalysis.ResponseSuffix, ref reported);
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
            if (IsDtoType(facts, payload)) continue;
            var isContractRequest = facts.Role(payload).Role == ConventionArchitecture.Contract &&
                payload.Name.EndsWith(ConventionDtoPlacementAnalysis.RequestSuffix, StringComparison.Ordinal) &&
                payload.Name.Length > ConventionDtoPlacementAnalysis.RequestSuffix.Length;
            if (isContractRequest || !RequiresExplicitContractMessage(facts, payload, context.IsSourceKnown)) continue;
            ReportExplicitMessage(context, payload, ConventionDtoPlacementAnalysis.RequestSuffix, ref reported);
        }
    }

    private static void ReportExplicitMessage(
        EndpointCheckContext context, INamedTypeSymbol payload, string suffix, ref HashSet<INamedTypeSymbol>? reported)
    {
        reported ??= new(SymbolEqualityComparer.Default);
        if (!reported.Add(payload)) return;
        context.Facts.Diagnostics.Add(Diagnostic.Create(
            ConventionDiagnosticDescriptors.ExplicitContractMessage,
            context.Location,
            ConventionBoundaryAnalysis.Properties(context.ReportingSymbol, payload),
            context.Method.ToDisplayString(),
            suffix,
            payload.ToDisplayString()));
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

    private static INamedTypeSymbol? UnwrapPayload(
        ConventionFacts facts, ITypeSymbol type, bool unwrapResponseWrappers, int maxDepth)
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
        if (named.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T || IsCollectionWrapper(facts, named))
            return true;
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
}
