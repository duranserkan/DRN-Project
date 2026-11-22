namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionEnumAnalysis
{
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
        var domainOnly = FindAttribute(enumType, facts.Symbols.DomainOnlyAttribute);
        var allowZero = FindAttribute(enumType, facts.Symbols.AllowZeroAttribute);

        if (role == ConventionArchitecture.Domain && domainOnly is null)
            Report(facts, ConventionDiagnosticDescriptors.DomainEnumRequiresMarker, enumType, location, enumType.Name);
        else if (role != ConventionArchitecture.Domain && domainOnly is not null)
            Report(facts, ConventionDiagnosticDescriptors.DomainOnlyOutsideDomain, enumType,
                facts.AttributeLocation(domainOnly, enumType), enumType.Name);

        if (role is not (ConventionArchitecture.Contract or ConventionArchitecture.Domain)) return;

        var zeroMember = FindZeroMember(enumType, facts.Cancellation);
        if (zeroMember is not null && allowZero is null)
        {
            var zeroLocation = facts.Location(zeroMember);
            if (zeroLocation == Location.None) zeroLocation = location;
            Report(facts, ConventionDiagnosticDescriptors.EnumZeroRequiresAllowZero, enumType, zeroLocation,
                enumType.Name, zeroMember.Name);
        }
        else if (zeroMember is null && allowZero is not null)
        {
            Report(facts, ConventionDiagnosticDescriptors.AllowZeroWithoutZeroMember, enumType,
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
        var nonAction = facts.Symbols.NonActionAttribute;
        foreach (var member in ConventionBoundaryAnalysis.PublicMembers(facts, type, transport))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!IsInspectableMember(facts, type, member, transport, nonAction) ||
                !ConventionBoundaryAnalysis.TryGetReportingTarget(facts, type, member, out var reportingSymbol, out var location))
                continue;
            var exposed = ConventionBoundaryAnalysis.FindExposedType(facts, PublicShapeRule.DomainOnlyEnum,
                ConventionBoundaryAnalysis.ExposureRootTypes(facts, type, member, transport, includeProducesResponseTypes: transport),
                IsDomainOnlyEnum, ShouldExpand, ConventionBoundaryAnalysis.HasPublicAccessor, out var exhausted);
            if (exhausted)
                facts.ReportIncompleteContract(reportingSymbol, member, location);
            if (exposed is not null)
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.DomainOnlyEnumExposed, location,
                    exposed.Locations.Where(item => item.SourceTree is { } tree && facts.Compilation.ContainsSyntaxTree(tree)),
                    ConventionBoundaryAnalysis.Properties(reportingSymbol, exposed),
                    member.ToDisplayString(), exposed.ToDisplayString()));
        }
    }

    private static bool IsInspectableMember(ConventionFacts facts, INamedTypeSymbol type, ISymbol member,
        bool transport, INamedTypeSymbol? nonAction) =>
        ConventionBoundaryAnalysis.IsInspectablePublicMember(facts, type, member, transport, nonAction) &&
        !(transport && member is IMethodSymbol method &&
          ConventionBoundaryAnalysis.IsObjectOrDisposeMethod(facts, method));

    private static bool IsDomainOnlyEnum(ConventionFacts facts, INamedTypeSymbol named) =>
        named.TypeKind == TypeKind.Enum && HasDomainOnlyAttribute(named, facts.Symbols.DomainOnlyAttribute);

    private static bool ShouldExpand(ConventionFacts facts, INamedTypeSymbol named) =>
        named.TypeKind is not TypeKind.Enum &&
        (facts.IsLocal(named) ||
         ConventionSymbols.Is(named, facts.Symbols.Dto, facts.Cancellation) ||
         facts.IsNeutralValue(named) ||
         facts.ReferenceProject(named.ContainingAssembly)?.Role is ConventionArchitecture.Contract or ConventionArchitecture.Domain or
             ConventionArchitecture.Application or ConventionArchitecture.Hosted);

    private static AttributeData? FindAttribute(INamedTypeSymbol type, INamedTypeSymbol? target)
    {
        if (target is null) return null;
        foreach (var attribute in type.GetAttributes())
            if (ConventionSymbols.Same(attribute.AttributeClass, target)) return attribute;
        return null;
    }

    private static bool HasDomainOnlyAttribute(INamedTypeSymbol enumType, INamedTypeSymbol? domainOnly) =>
        FindAttribute(enumType, domainOnly) is not null;

    private static void Report(ConventionFacts facts, DiagnosticDescriptor descriptor, INamedTypeSymbol target,
        Location location, params object[] args) =>
        facts.Diagnostics.Add(Diagnostic.Create(descriptor, location,
            ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField, target.GetDocumentationCommentId()),
            args));
}
