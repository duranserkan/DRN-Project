namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

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
            if (IsPublic(type) && (facts.Role(type).Role == ConventionArchitecture.Contract ||
                facts.Symbols.IsKnown(type, facts.Symbols.Controller, facts.Cancellation)))
                AnalyzePublicShape(facts, type);
        }
        ConventionBoundUseAnalysis.Analyze(facts);
    }

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
            if (!IsInspectablePublicMember(facts, type, member, transport, nonAction)) continue;
            var reportingSymbol = SymbolEqualityComparer.Default.Equals(member.ContainingType, type) ? member : type;
            var location = facts.Location(reportingSymbol);
            if (location == Location.None) continue;
            var exposed = FindExposedPersistenceType(facts, member, out var exhausted);
            if (exhausted)
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.IncompleteContract, location,
                    Properties(reportingSymbol), member.ToDisplayString()));
            if (exposed is not null)
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Contract, location,
                    exposed.Locations.Where(item => item.SourceTree is { } tree && facts.Compilation.ContainsSyntaxTree(tree)),
                    Properties(reportingSymbol, exposed),
                    member.ToDisplayString(), exposed.ToDisplayString()));
        }
    }

    private static bool IsInspectablePublicMember(ConventionFacts facts, INamedTypeSymbol type, ISymbol member,
        bool transport, INamedTypeSymbol? nonAction)
    {
        if (!IsPublic(member) || member.IsImplicitlyDeclared || member is INamedTypeSymbol ||
            member is IMethodSymbol { AssociatedSymbol: not null }) return false;
        if (transport && (member is not IMethodSymbol
            { MethodKind: MethodKind.Ordinary, IsStatic: false, IsAbstract: false, IsGenericMethod: false } ||
            IsNonAction((IMethodSymbol)member, nonAction))) return false;
        var declaredHere = SymbolEqualityComparer.Default.Equals(member.ContainingType, type);
        return declaredHere || !facts.IsLocal(member.ContainingType) ||
               facts.Role(member.ContainingType).Role != ConventionArchitecture.Contract || !IsPublic(member.ContainingType) ||
               !SymbolEqualityComparer.Default.Equals(member.ContainingType, member.ContainingType.OriginalDefinition);
    }

    private static INamedTypeSymbol? FindExposedPersistenceType(ConventionFacts facts, ISymbol member, out bool exhausted)
    {
        var pending = new Stack<ITypeSymbol>(PublicSignatureTypes(facts, member));
        var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var work = 0;
        exhausted = false;
        while (pending.TryPop(out var current))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (++work > PublicShapeLimit) { exhausted = true; return null; }
            if (!visited.Add(current)) continue;
            if (UnwrapNamedType(current, pending) is not { } named) continue;
            if (facts.IsEntity(named) || facts.Symbols.IsPersistenceType(named, facts.Cancellation)) return named;
            if (!ExpandNamedModel(facts, named, pending, ref work)) { exhausted = true; return null; }
        }
        return null;
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

    private static bool ExpandNamedModel(ConventionFacts facts, INamedTypeSymbol named, Stack<ITypeSymbol> pending, ref int work)
    {
        foreach (var argument in named.TypeArguments) pending.Push(argument);
        if (!facts.IsLocal(named) && !ConventionSymbols.Is(named, facts.Symbols.Dto, facts.Cancellation) &&
            !facts.IsNeutralValue(named) && facts.ReferenceProject(named.ContainingAssembly)?.Role != ConventionArchitecture.Contract)
            return true;
        var inheritedContracts = named.TypeKind == TypeKind.Interface ? named.AllInterfaces : [];
        foreach (var model in ConventionSymbols.Bases(named, facts.Cancellation).Concat(inheritedContracts))
        {
            if (!PushModelTypeMembers(facts, model, pending, ref work)) return false;
        }
        return true;
    }

    private static bool PushModelTypeMembers(ConventionFacts facts, INamedTypeSymbol model,
        Stack<ITypeSymbol> pending, ref int work)
    {
        foreach (var property in model.GetMembers())
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (++work > PublicShapeLimit) return false;
            if (property.DeclaredAccessibility != Accessibility.Public || property.IsStatic) continue;
            if (property is IPropertySymbol { GetMethod.DeclaredAccessibility: Accessibility.Public } p) pending.Push(p.Type);
            if (property is IFieldSymbol f) pending.Push(f.Type);
        }
        return true;
    }

    private static IEnumerable<ISymbol> PublicMembers(ConventionFacts facts, INamedTypeSymbol type, bool transport)
    {
        var overridden = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var contracts = type.TypeKind == TypeKind.Interface ? type.AllInterfaces : [];
        foreach (var current in ConventionSymbols.Bases(type, facts.Cancellation).Concat(contracts))
        {
            foreach (var member in current.GetMembers())
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (ShouldSkipPublicMember(current, type, member, transport, overridden)) continue;
                RecordOverriddenMembers(member, overridden);
                yield return member;
            }
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

    private static bool IsNonAction(IMethodSymbol method, INamedTypeSymbol? nonAction)
    {
        for (var current = method; current is not null; current = current.OverriddenMethod)
            if (current.GetAttributes().Any(attribute => ConventionSymbols.Same(attribute.AttributeClass, nonAction))) return true;
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
    private static IEnumerable<ITypeSymbol> PublicSignatureTypes(ConventionFacts facts, ISymbol member)
    {
        if (member is not IMethodSymbol method)
        {
            foreach (var type in SignatureTypes(member)) yield return type;
            yield break;
        }
        yield return method.ReturnType;
        var mapping = MappingParameter(facts, method);
        foreach (var parameter in method.Parameters)
            if (!SymbolEqualityComparer.Default.Equals(parameter, mapping)) yield return parameter.Type;
        foreach (var parameter in method.TypeParameters)
        {
            foreach (var constraint in parameter.ConstraintTypes) yield return constraint;
        }
    }

    private static bool IsPublic(ISymbol symbol)
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
