namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionEnumAnalysis
{
    private static readonly PublicShapePolicy DomainOnlyEnumPolicy = new(
        PublicShapeRule.DomainOnlyEnum,
        ConventionDiagnosticDescriptors.DomainOnlyEnumExposed,
        IsDomainOnlyEnum,
        ConventionBoundaryAnalysis.ShouldExpandPublicModel,
        IncludeExposedLocations: true);

    internal static void Analyze(ConventionFacts facts)
    {
        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var location = facts.Location(type);
            if (location == Location.None) continue;
            var role = facts.Role(type).Role;
            if (type.TypeKind == TypeKind.Enum)
            {
                AnalyzeEnumDeclaration(facts, type, role, location);
                continue;
            }
            if (ConventionBoundaryAnalysis.IsPublic(type) && role == ConventionArchitecture.Contract ||
                ConventionBoundaryAnalysis.IsExposedController(facts, type))
            {
                AnalyzeDomainOnlyExposure(facts, type);
            }
        }
    }

    private static void AnalyzeEnumDeclaration(ConventionFacts facts, INamedTypeSymbol enumType, string role, Location location)
    {
        if (role.Length == 0) return;
        var domainOnly = ConventionBoundaryAnalysis.FindAttribute(enumType, facts.Symbols.DomainOnlyAttribute);
        var allowZero = ConventionBoundaryAnalysis.FindAttribute(enumType, facts.Symbols.AllowZeroAttribute);

        if (role == ConventionArchitecture.Domain && domainOnly is null)
            ConventionBoundaryAnalysis.Report(facts, ConventionDiagnosticDescriptors.DomainEnumRequiresMarker, enumType, location, enumType.Name);
        else if (role != ConventionArchitecture.Domain && domainOnly is not null)
            ConventionBoundaryAnalysis.Report(facts, ConventionDiagnosticDescriptors.DomainOnlyOutsideDomain, enumType,
                facts.AttributeLocation(domainOnly, enumType), enumType.Name);

        if (role is not (ConventionArchitecture.Contract or ConventionArchitecture.Domain)) return;

        var zeroMember = FindZeroMember(enumType, facts.Cancellation);
        if (zeroMember is not null && allowZero is null)
        {
            var zeroLocation = facts.Location(zeroMember);
            if (zeroLocation == Location.None) zeroLocation = location;
            ConventionBoundaryAnalysis.Report(facts, ConventionDiagnosticDescriptors.EnumZeroRequiresAllowZero, enumType, zeroLocation,
                enumType.Name, zeroMember.Name);
        }
        else if (zeroMember is null && allowZero is not null)
        {
            ConventionBoundaryAnalysis.Report(facts, ConventionDiagnosticDescriptors.AllowZeroWithoutZeroMember, enumType,
                facts.AttributeLocation(allowZero, enumType), enumType.Name);
        }
    }

    private static IFieldSymbol? FindZeroMember(INamedTypeSymbol enumType, CancellationToken cancellation)
    {
        foreach (var member in enumType.GetMembers())
        {
            cancellation.ThrowIfCancellationRequested();
            if (member is IFieldSymbol { IsImplicitlyDeclared: false, HasConstantValue: true } field &&
                IsZeroConstant(field.ConstantValue))
                return field;
        }
        return null;
    }

    private static bool IsZeroConstant(object? value) => value switch
    {
        sbyte v => v == 0,
        byte v => v == 0,
        short v => v == 0,
        ushort v => v == 0,
        int v => v == 0,
        uint v => v == 0,
        long v => v == 0,
        ulong v => v == 0,
        _ => false
    };

    private static void AnalyzeDomainOnlyExposure(ConventionFacts facts, INamedTypeSymbol type)
    {
        var transport = facts.Symbols.IsTransport(type, facts.Cancellation);
        ConventionBoundaryAnalysis.AnalyzeExposedPublicShape(facts, type, transport, DomainOnlyEnumPolicy);
    }

    private static bool IsDomainOnlyEnum(ConventionFacts facts, INamedTypeSymbol named) =>
        named.TypeKind == TypeKind.Enum &&
        ConventionBoundaryAnalysis.HasAttribute(named, facts.Symbols.DomainOnlyAttribute);
}
