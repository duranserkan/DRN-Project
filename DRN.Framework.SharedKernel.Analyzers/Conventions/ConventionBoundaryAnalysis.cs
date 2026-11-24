namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal enum PublicShapeRule
{
    Persistence = 0,
    Dto = 1,
    DomainOnlyEnum = 2
}

internal readonly record struct PublicShapePolicy(
    PublicShapeRule Rule,
    DiagnosticDescriptor Descriptor,
    Func<ConventionFacts, INamedTypeSymbol, bool> IsExposed,
    Func<ConventionFacts, INamedTypeSymbol, bool> ShouldExpand,
    bool IncludeExposedLocations);

internal static class ConventionBoundaryAnalysis
{
    // This is an evidence bound, not a claim about the safety of larger serialized graphs.
    private const int PublicShapeLimit = 4096;
    private static readonly PublicShapePolicy PersistencePolicy = new(
        PublicShapeRule.Persistence,
        ConventionDiagnosticDescriptors.Contract,
        IsPersistenceTarget,
        ShouldExpandPersistenceModel,
        IncludeExposedLocations: true);

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
        AnalyzeExposedPublicShape(facts, type, transport, PersistencePolicy);
    }

    internal static void AnalyzeExposedPublicShape(ConventionFacts facts, INamedTypeSymbol type, bool transport,
        PublicShapePolicy policy)
    {
        var reportedOnType = transport ? null : ReportExposedTypeHeader(facts, type, policy);
        var memberExposures = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        foreach (var member in PublicMembers(facts, type, transport))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!ShouldAnalyzePublicShape(facts, type, member, transport) ||
                !TryGetReportingTarget(facts, type, member, out var reportingSymbol, out var location)) continue;

            memberExposures.Clear();
            var roots = ExposureRootTypes(facts, type, member, transport);
            CollectExposedTypes(facts, policy, roots, memberExposures, out var exhausted, firstOnly: policy.Rule != PublicShapeRule.Dto);
            if (exhausted) facts.ReportIncompleteContract(reportingSymbol, member, location);
            var inherited = SymbolEqualityComparer.Default.Equals(reportingSymbol, type);
            foreach (var exposed in memberExposures)
            {
                if (inherited && !(reportedOnType ??= new(SymbolEqualityComparer.Default)).Add(exposed)) continue;
                AddPublicShapeDiagnostic(facts, policy, reportingSymbol, member, exposed, location);
            }
        }
    }

    private static bool ShouldAnalyzePublicShape(ConventionFacts facts, INamedTypeSymbol type, ISymbol member, bool transport) =>
        IsInspectablePublicMember(facts, type, member, transport, facts.Symbols.NonActionAttribute) &&
        !(transport && member is IMethodSymbol method && IsObjectOrDisposeMethod(facts, method));

    private static void AddPublicShapeDiagnostic(ConventionFacts facts, PublicShapePolicy policy,
        ISymbol reportingSymbol, ISymbol reportedMember, INamedTypeSymbol exposed, Location location)
    {
        var properties = Properties(reportingSymbol, exposed);
        if (policy.IncludeExposedLocations)
            facts.Diagnostics.Add(Diagnostic.Create(policy.Descriptor, location,
                exposed.Locations.Where(item => item.SourceTree is { } tree && facts.Compilation.ContainsSyntaxTree(tree)),
                properties, reportedMember.ToDisplayString(), exposed.ToDisplayString()));
        else
            facts.Diagnostics.Add(Diagnostic.Create(policy.Descriptor, location,
                properties, reportedMember.ToDisplayString(), exposed.ToDisplayString()));
    }

    private static HashSet<INamedTypeSymbol>? ReportExposedTypeHeader(ConventionFacts facts, INamedTypeSymbol type,
        PublicShapePolicy policy)
    {
        var location = facts.Location(type);
        if (location == Location.None) return null;
        var roots = ImmutableArray.CreateBuilder<ITypeSymbol>();
        foreach (var parameter in type.TypeParameters)
            roots.Add(parameter);
        var work = 0;
        if (!TryCollectTypeHeaderRoots(facts, type, policy.IsExposed, roots, ref work))
        {
            facts.ReportIncompleteContract(type, type, location);
            return null;
        }
        if (roots.Count == 0) return null;
        var exposures = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        CollectExposedTypes(facts, policy, roots, exposures, out var exhausted, work);
        if (exhausted) facts.ReportIncompleteContract(type, type, location);
        foreach (var exposed in exposures)
            AddPublicShapeDiagnostic(facts, policy, type, type, exposed, location);
        return exposures;
    }

    private static bool TryCollectTypeHeaderRoots(ConventionFacts facts, INamedTypeSymbol type,
        Func<ConventionFacts, INamedTypeSymbol, bool> isExposed, ImmutableArray<ITypeSymbol>.Builder roots,
        ref int work)
    {
        var visitedInterfaces = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        for (var current = type.BaseType;
             current is not null && current.SpecialType is not (SpecialType.System_Object or SpecialType.System_ValueType);
             current = current.BaseType)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (++work > PublicShapeLimit) return false;
            if (IsCheckedLocalContractType(facts, current)) break;
            CollectTypeArguments(current, roots);
            if (!TryCollectPublicInterfaces(facts, current.Interfaces, isExposed, visitedInterfaces, roots, ref work)) return false;
            if (isExposed(facts, current))
            {
                roots.Add(current);
                break;
            }
        }
        return TryCollectPublicInterfaces(facts, type.Interfaces, isExposed, visitedInterfaces, roots, ref work);
    }

    private static bool TryCollectPublicInterfaces(ConventionFacts facts, ImmutableArray<INamedTypeSymbol> interfaces,
        Func<ConventionFacts, INamedTypeSymbol, bool> isExposed, HashSet<INamedTypeSymbol> visited,
        ImmutableArray<ITypeSymbol>.Builder roots, ref int work)
    {
        if (interfaces.IsEmpty) return true;
        var parents = new Stack<ImmutableArray<INamedTypeSymbol>.Enumerator>();
        var siblings = interfaces.GetEnumerator();
        do
        {
            while (siblings.MoveNext())
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (++work > PublicShapeLimit) return false;
                var contract = siblings.Current;
                if (!visited.Add(contract) || IsCheckedLocalContractType(facts, contract)) continue;
                if (IsPublic(contract))
                {
                    if (isExposed(facts, contract)) roots.Add(contract);
                    CollectTypeArguments(contract, roots);
                }
                if (contract.Interfaces.IsEmpty) continue;
                parents.Push(siblings);
                siblings = contract.Interfaces.GetEnumerator();
            }
        } while (parents.TryPop(out siblings));
        return true;
    }

    private static void CollectTypeArguments(INamedTypeSymbol type, ImmutableArray<ITypeSymbol>.Builder roots)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            foreach (var argument in current.TypeArguments)
                roots.Add(argument);
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

    internal static bool ShouldInspectInheritedContractMember(ConventionFacts facts, INamedTypeSymbol type, ISymbol member) =>
        SymbolEqualityComparer.Default.Equals(member.ContainingType, type) ||
        !IsCheckedLocalContractType(facts, member.ContainingType);

    private static bool IsCheckedLocalContractType(ConventionFacts facts, INamedTypeSymbol? candidate) =>
        candidate is not null &&
        facts.IsLocal(candidate) &&
        facts.Role(candidate).Role == ConventionArchitecture.Contract &&
        IsPublic(candidate) &&
        SymbolEqualityComparer.Default.Equals(candidate, candidate.OriginalDefinition) &&
        facts.Location(candidate) != Location.None;

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

    private static bool IsPersistenceTarget(ConventionFacts facts, INamedTypeSymbol named) =>
        facts.IsEntity(named) || facts.Symbols.IsPersistenceType(named, facts.Cancellation);

    private static bool ShouldExpandPersistenceModel(ConventionFacts facts, INamedTypeSymbol named) =>
        ShouldExpandPublicModel(facts, named, includeDto: true);

    internal static bool ShouldExpandPublicModel(ConventionFacts facts, INamedTypeSymbol named, bool includeDto) =>
        named.TypeKind is not TypeKind.Enum &&
        (facts.IsLocal(named) ||
         includeDto && facts.Symbols.IsKnown(named, facts.Symbols.Dto, facts.Cancellation) ||
         facts.IsNeutralValue(named) ||
         facts.Role(named).Role is ConventionArchitecture.Contract or ConventionArchitecture.Domain or
             ConventionArchitecture.Infra or ConventionArchitecture.Application or ConventionArchitecture.Hosted);

    private static bool HasPublicAccessor(IPropertySymbol property) =>
        property.GetMethod?.DeclaredAccessibility == Accessibility.Public ||
        property.SetMethod?.DeclaredAccessibility == Accessibility.Public;

    private static void CollectExposedTypes(ConventionFacts facts, PublicShapePolicy policy,
        IEnumerable<ITypeSymbol> rootTypes, HashSet<INamedTypeSymbol> exposures, out bool exhausted, int work = 0, bool firstOnly = false)
    {
        var pending = facts.PublicShapePending;
        var visited = facts.PublicShapeVisited;
        var clean = facts.CleanPublicShapeTypes(policy.Rule);
        pending.Clear();
        visited.Clear();
        exhausted = false;
        try
        {
            foreach (var root in rootTypes) pending.Push(root);
            while (pending.TryPop(out var current))
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (++work > PublicShapeLimit) { exhausted = true; return; }
                if (clean.Contains(current) || !visited.Add(current)) continue;
                if (UnwrapNamedType(current, pending) is not { } named) continue;
                if (policy.IsExposed(facts, named))
                {
                    exposures.Add(named);
                    if (firstOnly) return;
                    continue;
                }
                if (!ExpandNamedModel(facts, named, policy.ShouldExpand, pending, ref work))
                {
                    exhausted = true;
                    return;
                }
            }
            facts.Cancellation.ThrowIfCancellationRequested();
            if (exposures.Count == 0)
                foreach (var item in visited) clean.Add(item);
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
        Func<ConventionFacts, INamedTypeSymbol, bool> shouldExpand, Stack<ITypeSymbol> pending, ref int work)
    {
        if (named.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType) return true;
        PushTypeArguments(named, pending);
        if (!shouldExpand(facts, named)) return true;
        foreach (var model in ConventionSymbols.Bases(named, facts.Cancellation))
        {
            if (model.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType) continue;
            if (!SymbolEqualityComparer.Default.Equals(model, named))
                PushTypeArguments(model, pending);
            if (!PushModelTypeMembers(facts, model, pending, ref work)) return false;
        }
        return PushPublicInterfaces(facts, named, pending, ref work);
    }

    private static bool PushPublicInterfaces(ConventionFacts facts, INamedTypeSymbol named, Stack<ITypeSymbol> pending,
        ref int work)
    {
        foreach (var contract in named.AllInterfaces)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (++work > PublicShapeLimit) return false;
            if (!IsPublic(contract)) continue;
            PushTypeArguments(contract, pending);
            if (named.TypeKind == TypeKind.Interface &&
                !PushModelTypeMembers(facts, contract, pending, ref work))
                return false;
        }
        return true;
    }

    private static void PushTypeArguments(INamedTypeSymbol type, Stack<ITypeSymbol> pending)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            foreach (var argument in current.TypeArguments)
                pending.Push(argument);
        }
    }

    private static bool PushModelTypeMembers(ConventionFacts facts, INamedTypeSymbol model,
        Stack<ITypeSymbol> pending, ref int work)
    {
        foreach (var member in model.GetMembers())
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (++work > PublicShapeLimit) return false;
            if (member.DeclaredAccessibility != Accessibility.Public || member.IsStatic) continue;
            if (member is IPropertySymbol property && HasPublicAccessor(property)) pending.Push(property.Type);
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
        foreach (var current in ConventionSymbols.Bases(type, facts.Cancellation))
        {
            if (current.SpecialType is SpecialType.System_Object or SpecialType.System_ValueType) continue;
            CollectTypeMembers(facts, current, type, transport, overridden, members);
        }
        if (type.TypeKind == TypeKind.Interface)
        {
            foreach (var contract in type.AllInterfaces)
                CollectTypeMembers(facts, contract, type, transport, overridden, members);
        }
        return members.ToImmutable();
    }

    private static void CollectTypeMembers(ConventionFacts facts, INamedTypeSymbol current, INamedTypeSymbol type,
        bool transport, HashSet<ISymbol> overridden, ImmutableArray<ISymbol>.Builder members)
    {
        foreach (var member in current.GetMembers())
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (ShouldSkipPublicMember(current, type, member, transport, overridden)) continue;
            RecordOverriddenMembers(member, overridden);
            members.Add(member);
        }
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
        if (nonAction is null) return false;
        for (var current = method; current is not null; current = current.OverriddenMethod)
            if (HasAttribute(current, nonAction)) return true;
        return false;
    }

    internal static bool IsNonController(INamedTypeSymbol type, INamedTypeSymbol? nonController)
    {
        if (nonController is null) return false;
        for (var current = type; current is not null; current = current.BaseType)
            if (HasAttribute(current, nonController)) return true;
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

    [SuppressMessage("SonarQube", "S3267", Justification = "Scanning the immutable attribute array directly avoids boxing ImmutableArray and allocating a predicate capturing target.")]
    internal static AttributeData? FindAttribute(ISymbol symbol, INamedTypeSymbol? target)
    {
        if (target is null) return null;
        foreach (var attribute in symbol.GetAttributes())
            if (ConventionSymbols.Same(attribute.AttributeClass, target)) return attribute;
        return null;
    }

    internal static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? target) =>
        FindAttribute(symbol, target) is not null;

    internal static bool IsInjectedServiceParameter(ConventionFacts facts, IParameterSymbol parameter) =>
        HasAttribute(parameter, facts.Symbols.FromServicesAttribute) ||
        HasAttribute(parameter, facts.Symbols.FromKeyedServicesAttribute);

    [SuppressMessage("SonarQube", "S3267", Justification = "Iterating Parameters directly avoids boxing ImmutableArray and capturing mapping or facts in a predicate delegate.")]
    private static IEnumerable<ITypeSymbol> ExposureRootTypes(ConventionFacts facts, INamedTypeSymbol ownerType, ISymbol member,
        bool transport)
    {
        if (member is not IMethodSymbol method)
        {
            foreach (var type in SignatureTypes(member)) yield return type;
            yield break;
        }
        yield return method.ReturnType;
        var mapping = transport ? null : MappingParameter(facts, method);
        foreach (var parameter in method.Parameters)
        {
            if (SymbolEqualityComparer.Default.Equals(parameter, mapping) ||
                transport && IsInjectedServiceParameter(facts, parameter))
                continue;
            yield return parameter.Type;
        }
        foreach (var parameter in method.TypeParameters)
        {
            foreach (var constraint in parameter.ConstraintTypes) yield return constraint;
        }
        if (!transport) yield break;
        foreach (var responseType in ProducesResponseTypes(facts, ownerType, method))
            yield return responseType;
    }

    internal static ImmutableArray<ITypeSymbol> ProducesResponseTypes(ConventionFacts facts, INamedTypeSymbol ownerType, IMethodSymbol method)
    {
        if (facts.Symbols.ProducesResponseTypeAttribute is null &&
            facts.Symbols.ProducesResponseTypeGenericAttribute is null)
            return [];
        var methodEntries = facts.GetOrAddMethodProducesResponseTypes(method, CollectMethodProducesResponseTypes);
        var controllerEntries = facts.GetOrAddControllerProducesResponseTypes(ownerType, CollectControllerProducesResponseTypes);
        if (methodEntries.IsEmpty && controllerEntries.IsEmpty) return [];
        return facts.GetOrAddMergedProducesResponseTypes(ownerType, method, CollectMergedProducesResponseTypes);
    }

    private static ImmutableArray<ITypeSymbol> CollectMergedProducesResponseTypes(
        ConventionFacts facts, INamedTypeSymbol ownerType, IMethodSymbol method)
    {
        var methodEntries = facts.GetOrAddMethodProducesResponseTypes(method, CollectMethodProducesResponseTypes);
        var controllerEntries = facts.GetOrAddControllerProducesResponseTypes(ownerType, CollectControllerProducesResponseTypes);
        var builder = ImmutableArray.CreateBuilder<ITypeSymbol>(methodEntries.Length + controllerEntries.Length);
        HashSet<int>? actionStatusCodes = null;
        foreach (var (statusCode, type) in methodEntries)
        {
            if (!controllerEntries.IsEmpty) (actionStatusCodes ??= []).Add(statusCode);
            if (type is not null) builder.Add(type);
        }
        foreach (var (statusCode, type) in controllerEntries)
        {
            if (actionStatusCodes?.Contains(statusCode) == true) continue;
            builder.Add(type);
        }
        return builder.ToImmutable();
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
            if (TryReadProducesResponseType(attribute, nonGeneric, generic, out var response))
                yield return response;
        }
    }

    private static bool TryReadProducesResponseType(AttributeData attribute, INamedTypeSymbol? nonGeneric,
        INamedTypeSymbol? generic, out (int StatusCode, ITypeSymbol? Type) response)
    {
        response = default;
        if (attribute.AttributeClass is not { } attributeClass) return false;
        var isGeneric = ConventionSymbols.Same(attributeClass, generic);
        if (!isGeneric && !ConventionSymbols.Same(attributeClass, nonGeneric)) return false;
        ITypeSymbol? type = isGeneric ? attributeClass.TypeArguments[0] : null;
        int? statusCode = null;
        ReadProducesResponseArguments(attribute, ref type, ref statusCode);
        if (statusCode is not int status) return false;
        response = (status, type);
        return true;
    }

    private static void ReadProducesResponseArguments(AttributeData attribute, ref ITypeSymbol? type, ref int? statusCode)
    {
        foreach (var argument in attribute.ConstructorArguments)
        {
            if (argument.Kind == TypedConstantKind.Type) type = argument.Value as ITypeSymbol;
            else if (argument.Kind == TypedConstantKind.Primitive && argument.Value is int code) statusCode = code;
        }
        foreach (var argument in attribute.NamedArguments)
        {
            if (argument.Key == "Type") type = argument.Value.Value as ITypeSymbol;
            else if (argument.Key == "StatusCode" && argument.Value.Value is int code) statusCode = code;
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

    internal static Location PartLocation(INamedTypeSymbol type, SyntaxReference reference, CancellationToken cancellation) =>
        type.Locations.FirstOrDefault(candidate => candidate.SourceTree == reference.SyntaxTree &&
            reference.Span.Contains(candidate.SourceSpan)) ?? reference.GetSyntax(cancellation).GetLocation();

    internal static void Report(ConventionFacts facts, DiagnosticDescriptor descriptor, ISymbol target,
        Location location, params object[] args) =>
        facts.Diagnostics.Add(Diagnostic.Create(descriptor, location,
            ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField, target.GetDocumentationCommentId()),
            args));

    internal static ImmutableDictionary<string, string?> Properties(ISymbol target, INamedTypeSymbol? entity = null) =>
        ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField, target.GetDocumentationCommentId())
            .Add("Consumer", target.GetDocumentationCommentId())
            .Add(ConventionInputSyntax.EntityField, entity is null ? null : ConventionSymbols.MetadataName(entity));
}
