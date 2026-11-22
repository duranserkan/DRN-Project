namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal enum PublicShapeRule
{
    Persistence = 0,
    Dto = 1,
    DomainOnlyEnum = 2
}

internal static class ConventionBoundaryAnalysis
{
    // This is an evidence bound, not a claim about the safety of larger serialized graphs.
    private const int PublicShapeLimit = 4096;

    internal static void Analyze(ConventionFacts facts)
    {
        ConventionReferenceBoundaryAnalysis.Analyze(facts);
        ConventionDependencyAnalysis.Analyze(facts);
        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var location = facts.Location(type);
            if (location == Location.None) continue;
            AnalyzeRole(facts, type, location);
            if (IsPublic(type) && facts.Role(type).Role == ConventionArchitecture.Contract ||
                IsExposedController(facts, type))
                AnalyzePublicShape(facts, type);
        }
        ConventionBoundUseAnalysis.Analyze(facts);
    }

    internal static bool IsExposedController(ConventionFacts facts, INamedTypeSymbol type) =>
        !type.IsAbstract &&
        type.ContainingType is null &&
        IsPublic(type) &&
        !facts.Symbols.IsOpen(type, facts.Cancellation) &&
        facts.Symbols.IsKnown(type, facts.Symbols.Controller, facts.Cancellation) &&
        !IsNonController(type, facts.Symbols.NonControllerAttribute);

    private static void AnalyzeRole(ConventionFacts facts, INamedTypeSymbol type, Location location)
    {
        var role = facts.Role(type).Role;
        if (role.Length == 0 || facts.IsNeutralValue(type)) return;
        if (ClassifyImplementationRole(facts, type) is not var (expected, family)) return;
        var sharedAbstraction = role == ConventionArchitecture.SharedKernel &&
            (type.TypeKind == TypeKind.Interface || type.IsAbstract) &&
            expected is ConventionArchitecture.Domain or ConventionArchitecture.Contract or ConventionArchitecture.Application;
        if (role != expected && !sharedAbstraction)
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.ImplementationRole, location,
                Properties(type), type.Name, family, expected));
    }

    private static (string Expected, string Family)? ClassifyImplementationRole(ConventionFacts facts, INamedTypeSymbol type)
    {
        var symbols = facts.Symbols;
        if (symbols.IsTransport(type, facts.Cancellation)) return (ConventionArchitecture.Hosted, "transport");
        if (symbols.IsContext(type, facts.Cancellation))
            return (ConventionArchitecture.Infra, "context");
        if (symbols.IsKnown(type, symbols.Configuration, facts.Cancellation))
            return (ConventionArchitecture.Infra, "entity configuration");
        if (symbols.IsKnown(type, symbols.RepositoryContract, facts.Cancellation))
            return type.TypeKind == TypeKind.Interface
                ? (ConventionArchitecture.Domain, "repository contract")
                : (ConventionArchitecture.Infra, "repository");
        if (facts.IsEntity(type)) return (ConventionArchitecture.Domain, "entity");
        if (symbols.IsKnown(type, symbols.Dto, facts.Cancellation)) return (ConventionArchitecture.Contract, "DTO");
        if (symbols.IsKnown(type, symbols.ServiceContract, facts.Cancellation))
            return (ConventionArchitecture.Application, "use-case service");
        return null;
    }

    private static void AnalyzePublicShape(ConventionFacts facts, INamedTypeSymbol type)
    {
        var transport = facts.Symbols.IsTransport(type, facts.Cancellation);
        var nonAction = facts.Symbols.NonActionAttribute;
        foreach (var member in PublicMembers(facts, type, transport))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!IsInspectablePublicMember(facts, type, member, transport, nonAction) ||
                transport && member is IMethodSymbol method && IsObjectOrDisposeMethod(facts, method) ||
                !TryGetReportingTarget(facts, type, member, out var reportingSymbol, out var location)) continue;
            var exposed = FindExposedPersistenceType(facts, type, member, transport, out var exhausted);
            if (exhausted)
                facts.ReportIncompleteContract(reportingSymbol, member, location);
            if (exposed is not null)
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Contract, location,
                    exposed.Locations.Where(item => item.SourceTree is { } tree && facts.Compilation.ContainsSyntaxTree(tree)),
                    Properties(reportingSymbol, exposed),
                    member.ToDisplayString(), exposed.ToDisplayString()));
        }
    }

    internal static bool IsInspectablePublicMember(ConventionFacts facts, INamedTypeSymbol type, ISymbol member,
        bool transport, INamedTypeSymbol? nonAction)
    {
        if (member.DeclaredAccessibility != Accessibility.Public || member.IsImplicitlyDeclared || member is INamedTypeSymbol ||
            member is IMethodSymbol { AssociatedSymbol: not null }) return false;
        if (transport && (member is not IMethodSymbol
            { MethodKind: MethodKind.Ordinary, IsStatic: false, IsAbstract: false, IsGenericMethod: false } ||
            IsNonAction((IMethodSymbol)member, nonAction))) return false;
        return ShouldInspectInheritedContractMember(facts, type, member);
    }

    internal static bool ShouldInspectInheritedContractMember(ConventionFacts facts, INamedTypeSymbol type, ISymbol member)
    {
        var declaredHere = SymbolEqualityComparer.Default.Equals(member.ContainingType, type);
        return declaredHere || !facts.IsLocal(member.ContainingType) ||
               facts.Role(member.ContainingType).Role != ConventionArchitecture.Contract || !IsPublic(member.ContainingType) ||
               !SymbolEqualityComparer.Default.Equals(member.ContainingType, member.ContainingType.OriginalDefinition);
    }

    internal static bool TryGetReportingTarget(ConventionFacts facts, INamedTypeSymbol type, ISymbol member,
        out ISymbol reportingSymbol, out Location location)
    {
        reportingSymbol = SymbolEqualityComparer.Default.Equals(member.ContainingType, type) ? member : type;
        location = facts.Location(reportingSymbol);
        return location != Location.None;
    }

    internal static IMethodSymbol RootOverriddenMethod(IMethodSymbol method)
    {
        var root = method;
        while (root.OverriddenMethod is { } overridden) root = overridden;
        return root;
    }

    internal static bool IsObjectOrDisposeMethod(ConventionFacts facts, IMethodSymbol method)
    {
        var root = RootOverriddenMethod(method);
        if (root.ContainingType.SpecialType == SpecialType.System_Object) return true;
        return facts.Symbols.DisposeMethod is { } dispose && SymbolEqualityComparer.Default.Equals(
            root.ContainingType.FindImplementationForInterfaceMember(dispose), root);
    }

    private static INamedTypeSymbol? FindExposedPersistenceType(ConventionFacts facts, INamedTypeSymbol ownerType,
        ISymbol member, bool transport, out bool exhausted) =>
        FindExposedType(facts, PublicShapeRule.Persistence, PublicSignatureTypes(facts, ownerType, member, transport),
            IsPersistenceTarget, ShouldExpandPersistenceModel, HasPublicAccessor, out exhausted);

    private static bool IsPersistenceTarget(ConventionFacts facts, INamedTypeSymbol named) =>
        facts.IsEntity(named) || facts.Symbols.IsPersistenceType(named, facts.Cancellation);

    private static bool ShouldExpandPersistenceModel(ConventionFacts facts, INamedTypeSymbol named) =>
        facts.IsLocal(named) || ConventionSymbols.Is(named, facts.Symbols.Dto, facts.Cancellation) ||
        facts.IsNeutralValue(named) || facts.ReferenceProject(named.ContainingAssembly)?.Role == ConventionArchitecture.Contract;

    internal static bool HasPublicAccessor(IPropertySymbol property) =>
        property.GetMethod?.DeclaredAccessibility == Accessibility.Public ||
        property.SetMethod?.DeclaredAccessibility == Accessibility.Public;

    internal static INamedTypeSymbol? FindExposedType(ConventionFacts facts, PublicShapeRule rule,
        IEnumerable<ITypeSymbol> rootTypes,
        Func<ConventionFacts, INamedTypeSymbol, bool> isExposed,
        Func<ConventionFacts, INamedTypeSymbol, bool> shouldExpand,
        Func<IPropertySymbol, bool> shouldInspectProperty, out bool exhausted)
    {
        var pending = facts.PublicShapePending;
        var visited = facts.PublicShapeVisited;
        var clean = facts.CleanPublicShapeTypes(rule);
        pending.Clear();
        visited.Clear();
        exhausted = false;
        try
        {
            foreach (var root in rootTypes) pending.Push(root);
            var work = 0;
            while (pending.TryPop(out var current))
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (++work > PublicShapeLimit) { exhausted = true; return null; }
                if (clean.Contains(current) || !visited.Add(current)) continue;
                if (UnwrapNamedType(current, pending) is not { } named) continue;
                if (isExposed(facts, named)) return named;
                if (!ExpandNamedModel(facts, named, shouldExpand, shouldInspectProperty, pending, ref work))
                {
                    exhausted = true;
                    return null;
                }
            }
            facts.Cancellation.ThrowIfCancellationRequested();
            foreach (var item in visited) clean.Add(item);
            return null;
        }
        finally
        {
            pending.Clear();
            visited.Clear();
        }
    }

    private static INamedTypeSymbol? UnwrapNamedType(ITypeSymbol current, Stack<ITypeSymbol> pending)
    {
        if (current is IArrayTypeSymbol array) pending.Push(array.ElementType);
        if (current is ITypeParameterSymbol parameter)
        {
            foreach (var constraint in parameter.ConstraintTypes) pending.Push(constraint);
        }
        return current is INamedTypeSymbol { TypeKind: not TypeKind.Error } named ? named : null;
    }

    private static bool ExpandNamedModel(ConventionFacts facts, INamedTypeSymbol named,
        Func<ConventionFacts, INamedTypeSymbol, bool> shouldExpand,
        Func<IPropertySymbol, bool> shouldInspectProperty, Stack<ITypeSymbol> pending, ref int work)
    {
        foreach (var argument in named.TypeArguments) pending.Push(argument);
        if (!shouldExpand(facts, named)) return true;
        var inheritedContracts = named.TypeKind == TypeKind.Interface ? named.AllInterfaces : [];
        foreach (var model in ConventionSymbols.Bases(named, facts.Cancellation).Concat(inheritedContracts))
        {
            if (!PushModelTypeMembers(facts, model, shouldInspectProperty, pending, ref work)) return false;
        }
        return true;
    }

    private static bool PushModelTypeMembers(ConventionFacts facts, INamedTypeSymbol model,
        Func<IPropertySymbol, bool> shouldInspectProperty, Stack<ITypeSymbol> pending, ref int work)
    {
        foreach (var member in model.GetMembers())
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (++work > PublicShapeLimit) return false;
            if (member.DeclaredAccessibility != Accessibility.Public || member.IsStatic) continue;
            if (member is IPropertySymbol property && shouldInspectProperty(property)) pending.Push(property.Type);
            if (member is IFieldSymbol field) pending.Push(field.Type);
        }
        return true;
    }

    internal static ImmutableArray<ISymbol> PublicMembers(ConventionFacts facts, INamedTypeSymbol type, bool transport) =>
        facts.GetOrAddPublicMembers(type, transport, CollectPublicMembers);

    private static ImmutableArray<ISymbol> CollectPublicMembers(ConventionFacts facts, INamedTypeSymbol type, bool transport)
    {
        var members = ImmutableArray.CreateBuilder<ISymbol>();
        var overridden = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var contracts = type.TypeKind == TypeKind.Interface ? type.AllInterfaces : [];
        foreach (var current in ConventionSymbols.Bases(type, facts.Cancellation).Concat(contracts))
        {
            foreach (var member in current.GetMembers())
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (ShouldSkipPublicMember(current, type, member, transport, overridden)) continue;
                RecordOverriddenMembers(member, overridden);
                members.Add(member);
            }
        }
        return members.ToImmutable();
    }

    private static bool ShouldSkipPublicMember(INamedTypeSymbol current, INamedTypeSymbol type, ISymbol member,
        bool transport, HashSet<ISymbol> overridden) =>
        transport && member is not IMethodSymbol || overridden.Contains(member.OriginalDefinition) ||
        !SymbolEqualityComparer.Default.Equals(current, type) && member is IMethodSymbol
        { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor };

    private static void RecordOverriddenMembers(ISymbol member, HashSet<ISymbol> overridden)
    {
        for (var parent = Overridden(member); parent is not null; parent = Overridden(parent))
            overridden.Add(parent.OriginalDefinition);
    }

    private static ISymbol? Overridden(ISymbol symbol) => symbol switch
    {
        IMethodSymbol method => method.OverriddenMethod,
        IPropertySymbol property => property.OverriddenProperty,
        IEventSymbol @event => @event.OverriddenEvent,
        _ => null
    };

    internal static bool IsNonAction(IMethodSymbol method, INamedTypeSymbol? nonAction)
    {
        for (var current = method; current is not null; current = current.OverriddenMethod)
            if (current.GetAttributes().Any(attribute => ConventionSymbols.Same(attribute.AttributeClass, nonAction))) return true;
        return false;
    }

    internal static bool IsNonController(INamedTypeSymbol type, INamedTypeSymbol? nonController)
    {
        if (nonController is null) return false;
        for (var current = type; current is not null; current = current.BaseType)
            if (current.GetAttributes().Any(attribute => ConventionSymbols.Same(attribute.AttributeClass, nonController))) return true;
        return false;
    }

    private static IParameterSymbol? MappingParameter(ConventionFacts facts, ISymbol member)
    {
        if (member is not IMethodSymbol { MethodKind: MethodKind.Constructor } constructor ||
            !ConventionSymbols.Is(constructor.ContainingType, facts.Symbols.Dto, facts.Cancellation)) return null;
        foreach (var reference in constructor.DeclaringSyntaxReferences)
        {
            if (FindEntityMappingParameter(facts, reference) is { } parameter) return parameter;
        }
        return null;
    }

    private static IParameterSymbol? FindEntityMappingParameter(ConventionFacts facts, SyntaxReference reference)
    {
        SyntaxNode? initializer = reference.GetSyntax(facts.Cancellation) switch
        {
            ConstructorDeclarationSyntax constructorDeclaration => constructorDeclaration.Initializer,
            TypeDeclarationSyntax typeDeclaration => typeDeclaration.BaseList?.Types.OfType<PrimaryConstructorBaseTypeSyntax>().FirstOrDefault(),
            _ => null
        };
        if (initializer is null || !facts.Compilation.ContainsSyntaxTree(initializer.SyntaxTree)) return null;
        var model = facts.SemanticModel(initializer.SyntaxTree);
        if (model.GetSymbolInfo(initializer, facts.Cancellation).Symbol is not IMethodSymbol target ||
            !ConventionSymbols.Same(target.ContainingType, facts.Symbols.Dto)) return null;
        var arguments = initializer switch
        {
            ConstructorInitializerSyntax ordinary => ordinary.ArgumentList.Arguments,
            PrimaryConstructorBaseTypeSyntax primary => primary.ArgumentList.Arguments,
            _ => default
        };
        foreach (var argument in arguments)
        {
            if (model.GetSymbolInfo(argument.Expression, facts.Cancellation).Symbol is IParameterSymbol parameter &&
                parameter.Type is INamedTypeSymbol entity && facts.IsEntity(entity)) return parameter;
        }
        return null;
    }

    [SuppressMessage("SonarQube", "S3267", Justification = "Iterating Parameters directly avoids boxing ImmutableArray and capturing mapping in a predicate delegate.")]
    private static IEnumerable<ITypeSymbol> PublicSignatureTypes(ConventionFacts facts, INamedTypeSymbol ownerType,
        ISymbol member, bool transport)
    {
        if (member is not IMethodSymbol method)
        {
            foreach (var type in SignatureTypes(member)) yield return type;
            yield break;
        }
        yield return method.ReturnType;
        var mapping = MappingParameter(facts, method);
        foreach (var parameter in method.Parameters)
            if (!SymbolEqualityComparer.Default.Equals(parameter, mapping) &&
                !(transport && IsInjectedServiceParameter(facts, parameter)))
                yield return parameter.Type;
        foreach (var parameter in method.TypeParameters)
        {
            foreach (var constraint in parameter.ConstraintTypes) yield return constraint;
        }
        if (!transport) yield break;
        foreach (var responseType in ProducesResponseTypes(facts, ownerType, method))
            yield return responseType;
    }

    internal static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? target) =>
        target is not null && symbol.GetAttributes().Any(attribute => ConventionSymbols.Same(attribute.AttributeClass, target));

    internal static bool IsInjectedServiceParameter(ConventionFacts facts, IParameterSymbol parameter) =>
        HasAttribute(parameter, facts.Symbols.FromServicesAttribute) ||
        HasAttribute(parameter, facts.Symbols.FromKeyedServicesAttribute);

    [SuppressMessage("SonarQube", "S3267", Justification = "Iterating Parameters directly avoids boxing ImmutableArray and capturing facts in a predicate delegate.")]
    internal static IEnumerable<ITypeSymbol> ExposureRootTypes(ConventionFacts facts, INamedTypeSymbol ownerType, ISymbol member,
        bool transport, bool includeProducesResponseTypes)
    {
        if (member is not IMethodSymbol method)
        {
            foreach (var type in SignatureTypes(member)) yield return type;
            yield break;
        }
        yield return method.ReturnType;
        foreach (var parameter in method.Parameters)
        {
            if (transport && IsInjectedServiceParameter(facts, parameter)) continue;
            yield return parameter.Type;
        }
        foreach (var parameter in method.TypeParameters)
        {
            foreach (var constraint in parameter.ConstraintTypes) yield return constraint;
        }
        if (!includeProducesResponseTypes || !transport) yield break;
        foreach (var responseType in ProducesResponseTypes(facts, ownerType, method))
            yield return responseType;
    }

    internal static IEnumerable<ITypeSymbol> ProducesResponseTypes(ConventionFacts facts, INamedTypeSymbol ownerType, IMethodSymbol method)
    {
        if (facts.Symbols.ProducesResponseTypeAttribute is null &&
            facts.Symbols.ProducesResponseTypeGenericAttribute is null)
            yield break;
        var methodEntries = facts.GetOrAddMethodProducesResponseTypes(method, CollectMethodProducesResponseTypes);
        var controllerEntries = facts.GetOrAddControllerProducesResponseTypes(ownerType, CollectControllerProducesResponseTypes);
        HashSet<int>? actionStatusCodes = null;
        foreach (var (statusCode, type) in methodEntries)
        {
            if (!controllerEntries.IsEmpty) (actionStatusCodes ??= []).Add(statusCode);
            if (type is not null) yield return type;
        }
        foreach (var (statusCode, type) in controllerEntries)
        {
            if (actionStatusCodes?.Contains(statusCode) == true) continue;
            yield return type;
        }
    }

    private static ImmutableArray<(int StatusCode, ITypeSymbol? Type)> CollectMethodProducesResponseTypes(
        ConventionFacts facts, IMethodSymbol method)
    {
        var nonGeneric = facts.Symbols.ProducesResponseTypeAttribute;
        var generic = facts.Symbols.ProducesResponseTypeGenericAttribute;
        var builder = ImmutableArray.CreateBuilder<(int StatusCode, ITypeSymbol? Type)>();
        for (var current = method; current is not null; current = current.OverriddenMethod)
        {
            foreach (var response in ReadProducesResponseTypes(facts, current.GetAttributes(), nonGeneric, generic))
                builder.Add(response);
        }
        return builder.ToImmutable();
    }

    private static ImmutableArray<(int StatusCode, ITypeSymbol Type)> CollectControllerProducesResponseTypes(
        ConventionFacts facts, INamedTypeSymbol ownerType)
    {
        var nonGeneric = facts.Symbols.ProducesResponseTypeAttribute;
        var generic = facts.Symbols.ProducesResponseTypeGenericAttribute;
        var builder = ImmutableArray.CreateBuilder<(int StatusCode, ITypeSymbol Type)>();
        foreach (var controller in ConventionSymbols.Bases(ownerType, facts.Cancellation))
        {
            foreach (var response in ReadProducesResponseTypes(facts, controller.GetAttributes(), nonGeneric, generic))
            {
                if (response.Type is null) continue;
                builder.Add((response.StatusCode, response.Type));
            }
        }
        return builder.ToImmutable();
    }

    private static IEnumerable<(int StatusCode, ITypeSymbol? Type)> ReadProducesResponseTypes(ConventionFacts facts,
        ImmutableArray<AttributeData> attributes, INamedTypeSymbol? nonGeneric, INamedTypeSymbol? generic)
    {
        foreach (var attribute in attributes)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (attribute.AttributeClass is not { } attributeClass) continue;
            var isGeneric = ConventionSymbols.Same(attributeClass, generic);
            if (!isGeneric && !ConventionSymbols.Same(attributeClass, nonGeneric)) continue;
            ITypeSymbol? type = isGeneric ? attributeClass.TypeArguments[0] : null;
            int? statusCode = null;
            foreach (var argument in attribute.ConstructorArguments)
            {
                if (argument.Kind == TypedConstantKind.Type) type = argument.Value as ITypeSymbol;
                if (argument.Kind == TypedConstantKind.Primitive && argument.Value is int code) statusCode = code;
            }
            foreach (var argument in attribute.NamedArguments)
            {
                if (argument.Key == "Type") type = argument.Value.Value as ITypeSymbol;
                if (argument.Key == "StatusCode" && argument.Value.Value is int code) statusCode = code;
            }
            if (statusCode is int status) yield return (status, type);
        }
    }

    internal static bool IsPublic(ISymbol symbol)
    {
        for (var current = symbol; current is not null && current is not INamespaceSymbol; current = current.ContainingSymbol)
            if (current is INamedTypeSymbol or IMethodSymbol or IFieldSymbol or IPropertySymbol or IEventSymbol &&
                current.DeclaredAccessibility != Accessibility.Public) return false;
        return true;
    }

    internal static IEnumerable<ITypeSymbol> SignatureTypes(ISymbol symbol)
    {
        switch (symbol)
        {
            case IPropertySymbol property:
                yield return property.Type;
                foreach (var parameter in property.Parameters) yield return parameter.Type;
                break;
            case IFieldSymbol field: yield return field.Type; break;
            case IEventSymbol @event: yield return @event.Type; break;
            case IMethodSymbol method:
                yield return method.ReturnType;
                foreach (var parameter in method.Parameters) yield return parameter.Type;
                break;
        }
    }

    internal static ImmutableDictionary<string, string?> Properties(ISymbol target, INamedTypeSymbol? entity = null) =>
        ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField, target.GetDocumentationCommentId())
            .Add("Consumer", target.GetDocumentationCommentId())
            .Add(ConventionInputSyntax.EntityField, entity is null ? null : ConventionSymbols.MetadataName(entity));
}
