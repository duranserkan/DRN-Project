namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionDtoAnalysis
{
    internal static void Analyze(ConventionFacts facts)
    {
        var classifications = new Lazy<ClassificationContract?>(() => CreateClassifications(facts.Compilation), isThreadSafe: false);
        var dtoStems = new Dictionary<ConventionDtoPlacementAnalysis.DtoKey, ConventionDtoPlacementAnalysis.DtoPlacement?>();
        var messages = new List<INamedTypeSymbol>();
        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            AnalyzeType(facts, type, classifications, dtoStems, messages);
        }
        for (var i = 0; i < messages.Count; i++)
            ConventionDtoPlacementAnalysis.CheckMessagePlacement(facts, messages[i], dtoStems);
    }

    private static void AnalyzeType(
        ConventionFacts facts, INamedTypeSymbol type, Lazy<ClassificationContract?> classifications,
        Dictionary<ConventionDtoPlacementAnalysis.DtoKey, ConventionDtoPlacementAnalysis.DtoPlacement?> dtoStems,
        List<INamedTypeSymbol> messages)
    {
        var role = facts.Role(type);
        var hasLocation = facts.Location(type) != Location.None;
        if (hasLocation)
            AnalyzeTypeSurface(facts, type, role.Role, classifications, messages);
        if (type.IsAbstract) return;
        AnalyzeConcreteType(facts, type, role, hasLocation, classifications, dtoStems);
    }

    private static void AnalyzeTypeSurface(
        ConventionFacts facts, INamedTypeSymbol type, string role,
        Lazy<ClassificationContract?> classifications, List<INamedTypeSymbol> messages)
    {
        if (role == ConventionArchitecture.Contract && ConventionBoundaryAnalysis.IsPublic(type))
        {
            if (ConventionDtoPlacementAnalysis.MessageStemLength(type.Name) > 0) messages.Add(type);
            ConventionEndpointContractAnalysis.CheckContractDtoExposure(facts, type);
        }
        if (ConventionBoundaryAnalysis.IsExposedController(facts, type))
            ConventionEndpointContractAnalysis.CheckControllerContracts(facts, type);

        var mayProjectResponses = role is ConventionArchitecture.Hosted or ConventionArchitecture.Application or
            ConventionArchitecture.Domain or ConventionArchitecture.Infra or ConventionArchitecture.Contract;
        if (mayProjectResponses)
            ResponseProjectionAnalysis.CheckResponseProjections(facts, type, classifications);
    }

    private static void AnalyzeConcreteType(
        ConventionFacts facts, INamedTypeSymbol type, (string Role, string Namespace, string PathPrefix) role,
        bool hasLocation, Lazy<ClassificationContract?> classifications,
        Dictionary<ConventionDtoPlacementAnalysis.DtoKey, ConventionDtoPlacementAnalysis.DtoPlacement?> dtoStems)
    {
        var isClosedDomainEntity = hasLocation &&
            role.Role == ConventionArchitecture.Domain &&
            !facts.Symbols.IsOpen(type, facts.Cancellation) &&
            facts.Symbols.IsKnown(type, facts.Symbols.Entity, facts.Cancellation);
        if (isClosedDomainEntity)
            ConventionDtoPlacementAnalysis.CheckMapper(facts, type, classifications);

        var isNamedContractDto = role.Role == ConventionArchitecture.Contract &&
            type.Name.Length > 3 &&
            type.Name.EndsWith("Dto", StringComparison.Ordinal) &&
            facts.Symbols.IsKnown(type, facts.Symbols.Dto, facts.Cancellation);
        if (isNamedContractDto)
            ConventionDtoPlacementAnalysis.CheckDtoPlacement(facts, type, role.Namespace, dtoStems);
    }

    private static ClassificationContract? CreateClassifications(Compilation compilation)
    {
        var sensitive = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.SensitiveAttribute");
        var personal = compilation.GetTypeByMetadataName("DRN.Framework.SharedKernel.Attributes.PersonalAttribute");
        return sensitive is null || personal is null ? null : new ClassificationContract(sensitive, personal, compilation);
    }
}
