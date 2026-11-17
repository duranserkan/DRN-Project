using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionBoundaryAnalysis
{
    private const string SharedKernelPackage = "DRN.Framework.SharedKernel";
    private const string UtilsPackage = "DRN.Framework.Utils";
    private const string EntityFrameworkPackage = "DRN.Framework.EntityFramework";
    private const string HostingPackage = "DRN.Framework.Hosting";

    // This is an evidence bound, not a claim about the safety of larger serialized graphs.
    private const int PublicShapeLimit = 4096;
    private static readonly string[] FrameworkPackages =
    [
        SharedKernelPackage, UtilsPackage, EntityFrameworkPackage,
        HostingPackage, "DRN.Framework.Testing", "DRN.Framework.Jobs", "DRN.Framework.MassTransit"
    ];
    private static readonly string[] TransportMetadataNames =
    [
        "Microsoft.AspNetCore.Mvc.IActionResult", "Microsoft.AspNetCore.Mvc.ActionResult`1",
        "Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute", "Microsoft.AspNetCore.Mvc.RouteAttribute",
        "Microsoft.AspNetCore.Mvc.ApiControllerAttribute", "Microsoft.AspNetCore.Mvc.ControllerAttribute"
    ];

    internal static void Analyze(ConventionFacts facts)
    {
        AnalyzeReferences(facts);
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
        AnalyzeBoundUses(facts);
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
        if (ConventionSymbols.Is(type, symbols.DbContext, facts.Cancellation) || symbols.IsContext(type, facts.Cancellation))
            return (ConventionArchitecture.Infra, "context");
        if (ConventionSymbols.Is(type, symbols.Configuration, facts.Cancellation))
            return (ConventionArchitecture.Infra, "entity configuration");
        if (ConventionSymbols.Is(type, symbols.RepositoryContract, facts.Cancellation))
            return type.TypeKind == TypeKind.Interface
                ? (ConventionArchitecture.Domain, "repository contract")
                : (ConventionArchitecture.Infra, "repository");
        if (facts.IsEntity(type)) return (ConventionArchitecture.Domain, "entity");
        if (ConventionSymbols.Is(type, symbols.Dto, facts.Cancellation)) return (ConventionArchitecture.Contract, "DTO");
        if (ConventionSymbols.Is(type, symbols.ServiceContract, facts.Cancellation))
            return (ConventionArchitecture.Application, "use-case service");
        return null;
    }

    private static void AnalyzePublicShape(ConventionFacts facts, INamedTypeSymbol type)
    {
        var transport = facts.Symbols.IsTransport(type, facts.Cancellation);
        var nonAction = facts.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.NonActionAttribute");
        foreach (var member in PublicMembers(facts, type, transport))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!IsInspectablePublicMember(facts, type, member, transport, nonAction)) continue;
            var reportingSymbol = SymbolEqualityComparer.Default.Equals(member.ContainingType, type) ? member : type;
            var location = facts.Location(reportingSymbol);
            if (location == Location.None) continue;
            var exposed = FindExposedPersistenceType(facts, member, out var exhausted);
            if (exhausted)
                facts.Diagnostics.Add(ConventionProject.InputIssue(member.ToDisplayString(),
                    "beyond the public model analysis bound", "reviewed bounded transport model", location));
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
        var model = facts.Compilation.GetSemanticModel(initializer.SyntaxTree);
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

    private static IEnumerable<ITypeSymbol> SignatureTypes(ISymbol symbol)
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

    private static void AnalyzeBoundUses(ConventionFacts facts)
    {
        var tracker = BoundUseTracker.Create(facts);
        ScanSyntaxTrees(facts, tracker);
        ScanInheritedAccessibleMembers(facts, tracker);
        ReportForbiddenDependencies(facts, tracker);
        ReportCrossSubdomainUses(facts, tracker);
    }

    private static void ScanSyntaxTrees(ConventionFacts facts, BoundUseTracker tracker)
    {
        foreach (var tree in facts.Compilation.SyntaxTrees.OrderBy(tree => facts.LogicalPath(tree) ?? tree.FilePath, StringComparer.Ordinal))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var generated = facts.IsGenerated(tree);
            var model = facts.Compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot(facts.Cancellation).DescendantNodes().OfType<ExpressionSyntax>())
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (!node.Ancestors().Any(parent => parent is UsingDirectiveSyntax ||
                        parent is BaseNamespaceDeclarationSyntax ns && ns.Name.Span.Contains(node.Span)))
                    ScanExpressionNode(facts, tracker, model, node, generated);
            }
        }
    }

    private static void ScanExpressionNode(ConventionFacts facts, BoundUseTracker tracker, SemanticModel model,
        ExpressionSyntax node, bool generated)
    {
        var consumer = UserConsumer(facts, ReportingSymbol(facts, model, node), tracker.InputAnchors);
        var type = consumer as INamedTypeSymbol ?? consumer?.ContainingType;
        if (consumer is null || type is null) return;
        var role = facts.Role(type).Role;
        if (!facts.HasSubdomainBoundary(type) && role is not (ConventionArchitecture.Domain or
                ConventionArchitecture.Contract or ConventionArchitecture.SharedKernel or ConventionArchitecture.Utils)) return;
        tracker.Dependencies.Clear();
        CollectNodeDependencies(facts, model, node, tracker.Dependencies);
        foreach (var entity in NamedDependencies(tracker.Dependencies, tracker.PendingDependencies,
                     tracker.VisitedDependencies, facts.Cancellation))
        {
            if (!generated && ForbiddenUse(facts, role, entity, tracker.TransportTypes) is { } dependencyRole)
                tracker.RecordForbidden(type, dependencyRole.Family, dependencyRole.Role, node.GetLocation());
            tracker.RecordUse(facts, consumer, entity, node.GetLocation(), !generated);
        }
    }

    private static void CollectNodeDependencies(ConventionFacts facts, SemanticModel model, ExpressionSyntax node,
        List<ITypeSymbol> dependencies)
    {
        if (model.GetTypeInfo(node, facts.Cancellation).Type is { } valueType) dependencies.Add(valueType);
        if (node is not (SimpleNameSyntax or MemberAccessExpressionSyntax)) return;
        var bound = model.GetSymbolInfo(node, facts.Cancellation).Symbol;
        if (bound is INamedTypeSymbol named) dependencies.Add(named);
        else if (bound?.ContainingType is { } declaringType) dependencies.Add(declaringType);
    }

    private static void ScanInheritedAccessibleMembers(ConventionFacts facts, BoundUseTracker tracker)
    {
        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var consumer = UserConsumer(facts, type, tracker.InputAnchors);
            if (consumer is not null && facts.Owner(type) is not null)
                ScanTypeInheritedMembers(facts, tracker, type, consumer);
        }
    }

    private static void ScanTypeInheritedMembers(ConventionFacts facts, BoundUseTracker tracker,
        INamedTypeSymbol type, ISymbol consumer)
    {
        foreach (var inherited in ConventionSymbols.Bases(type, facts.Cancellation).Skip(1).Concat(type.AllInterfaces))
        {
            foreach (var member in inherited.GetMembers())
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (member is INamedTypeSymbol or IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } ||
                    !facts.Compilation.IsSymbolAccessibleWithin(member, type)) continue;
                RecordInheritedMemberDependencies(facts, tracker, consumer, member);
            }
        }
    }

    private static void RecordInheritedMemberDependencies(ConventionFacts facts, BoundUseTracker tracker,
        ISymbol consumer, ISymbol member)
    {
        var memberDependencies = SignatureTypes(member);
        if (member is IMethodSymbol method)
            memberDependencies = memberDependencies.Concat(method.TypeParameters.SelectMany(parameter => parameter.ConstraintTypes));
        foreach (var entity in NamedDependencies(memberDependencies, tracker.PendingDependencies,
                     tracker.VisitedDependencies, facts.Cancellation))
        {
            tracker.RecordUse(facts, consumer, entity, Location.None, false);
            foreach (var location in member.Locations.Where(location => location.SourceTree is { } source &&
                         facts.Compilation.ContainsSyntaxTree(source)))
                tracker.RecordUse(facts, consumer, entity, location, false);
        }
    }

    private static void ReportForbiddenDependencies(ConventionFacts facts, BoundUseTracker tracker)
    {
        var classified = facts.Diagnostics.Where(diagnostic => diagnostic.Id == "DRNCV016")
            .Select(diagnostic => diagnostic.Properties.TryGetValue(ConventionInputSyntax.TargetField, out var target) ? target : null)
            .Where(target => target is not null).ToHashSet(StringComparer.Ordinal);
        foreach (var (type, dependency) in tracker.Forbidden)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (classified.Contains(type.GetDocumentationCommentId())) continue;
            var location = facts.Location(type);
            if (location == Location.None) continue;
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.ImplementationRole, location,
                dependency.Locations.Distinct(), Properties(type), type.Name, dependency.Family, dependency.Role));
        }
    }

    private static void ReportCrossSubdomainUses(ConventionFacts facts, BoundUseTracker tracker)
    {
        foreach (var (consumer, entities) in tracker.Uses)
        {
            foreach (var (entity, locations) in entities)
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                var owner = facts.Owner(consumer as INamedTypeSymbol ?? consumer.ContainingType)!;
                var entityOwner = facts.Owner(entity)!;
                var primary = OrderLocations(facts, locations.UserUses).FirstOrDefault() ?? facts.Location(consumer);
                if (primary == Location.None && consumer is INamedTypeSymbol target) tracker.InputAnchors.TryGetValue(target, out primary);
                if (primary is null || primary == Location.None) continue;
                var additional = OrderLocations(facts, locations.UserUses.Concat(locations.Evidence).Distinct())
                    .Where(location => !location.Equals(primary));
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.CrossSubdomain, primary,
                    additional, Properties(consumer, entity), consumer.ToDisplayString(), owner.Subdomain,
                    entity.ToDisplayString(), entityOwner.Subdomain));
            }
        }
    }

    private sealed class BoundUseTracker
    {
        internal Dictionary<ISymbol, Dictionary<INamedTypeSymbol, EntityUseLocations>> Uses { get; } = new(SymbolEqualityComparer.Default);
        internal Dictionary<INamedTypeSymbol, Location> InputAnchors { get; }
        internal Dictionary<INamedTypeSymbol, (string Family, string Role, List<Location> Locations)> Forbidden { get; } = new(SymbolEqualityComparer.Default);
        internal List<ITypeSymbol> Dependencies { get; } = new(2);
        internal Stack<ITypeSymbol> PendingDependencies { get; } = new();
        internal HashSet<ITypeSymbol> VisitedDependencies { get; } = new(SymbolEqualityComparer.Default);
        internal INamedTypeSymbol[] TransportTypes { get; }

        private BoundUseTracker(Dictionary<INamedTypeSymbol, Location> inputAnchors, INamedTypeSymbol[] transportTypes)
        {
            InputAnchors = inputAnchors;
            TransportTypes = transportTypes;
        }

        internal static BoundUseTracker Create(ConventionFacts facts)
        {
            var inputAnchors = new Dictionary<INamedTypeSymbol, Location>(SymbolEqualityComparer.Default);
            foreach (var input in facts.Records.Where(input =>
                         input.Kind is ConventionInputSyntax.OwnershipKind or ConventionInputSyntax.ContextKind))
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (input.Location != Location.None && facts.Compilation.GetTypeByMetadataName(input.Identity) is { } type &&
                    !facts.HasInvalidOwner(type)) inputAnchors.TryAdd(type, input.Location);
            }
            var transportTypes = TransportMetadataNames.Select(facts.Compilation.GetTypeByMetadataName)
                .Where(type => type is not null).Cast<INamedTypeSymbol>().ToArray();
            return new(inputAnchors, transportTypes);
        }

        internal void RecordUse(ConventionFacts facts, ISymbol consumer, INamedTypeSymbol entity, Location evidence, bool userUse)
        {
            var type = consumer as INamedTypeSymbol ?? consumer.ContainingType;
            if (type is null || !facts.HasSubdomainBoundary(type) || facts.Owner(type) is null || !facts.IsEntity(entity) ||
                facts.Owner(entity) is null || facts.UsesOwnSubdomain(type, entity)) return;
            if (!Uses.TryGetValue(consumer, out var entities))
            {
                entities = new(SymbolEqualityComparer.Default);
                Uses.Add(consumer, entities);
            }
            if (!entities.TryGetValue(entity, out var locations))
            {
                locations = new();
                entities.Add(entity, locations);
            }
            if (evidence == Location.None) return;
            var destination = userUse ? locations.UserUses : locations.Evidence;
            if (!destination.Contains(evidence)) destination.Add(evidence);
        }

        internal void RecordForbidden(INamedTypeSymbol type, string family, string role, Location location)
        {
            if (!Forbidden.TryGetValue(type, out var entry))
            {
                entry = (family, role, []);
                Forbidden.Add(type, entry);
            }
            entry.Locations.Add(location);
        }
    }

    private static IEnumerable<Location> OrderLocations(ConventionFacts facts, IEnumerable<Location> locations) =>
        locations.OrderBy(location => location.SourceTree is { } tree ? facts.LogicalPath(tree) ?? tree.FilePath :
                location.GetLineSpan().Path, StringComparer.Ordinal).ThenBy(location => location.SourceSpan.Start);

    private static ISymbol? UserConsumer(ConventionFacts facts, ISymbol? symbol,
        Dictionary<INamedTypeSymbol, Location> inputAnchors)
    {
        symbol = symbol switch
        {
            IMethodSymbol { PartialDefinitionPart: { } definition } => definition,
            IPropertySymbol { PartialDefinitionPart: { } definition } => definition,
            IEventSymbol { PartialDefinitionPart: { } definition } => definition,
            _ => symbol
        };
        for (var current = symbol; current is not null; current = current.ContainingType)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (facts.Location(current) != Location.None || current is INamedTypeSymbol type && inputAnchors.ContainsKey(type)) return current;
            if (current is INamedTypeSymbol) return null;
        }
        return null;
    }

    private static IEnumerable<INamedTypeSymbol> NamedDependencies(IEnumerable<ITypeSymbol> dependencies,
        Stack<ITypeSymbol> pending, HashSet<ITypeSymbol> visited, CancellationToken cancellation)
    {
        pending.Clear();
        visited.Clear();
        foreach (var dependency in dependencies) pending.Push(dependency);
        while (pending.TryPop(out var dependency))
        {
            cancellation.ThrowIfCancellationRequested();
            if (!visited.Add(dependency)) continue;
            if (PushTypeDependencies(dependency, pending) is { } named) yield return named;
        }
    }

    private static INamedTypeSymbol? PushTypeDependencies(ITypeSymbol dependency, Stack<ITypeSymbol> pending)
    {
        if (dependency is IArrayTypeSymbol array) pending.Push(array.ElementType);
        if (dependency is ITypeParameterSymbol parameter)
        {
            foreach (var constraint in parameter.ConstraintTypes) pending.Push(constraint);
        }
        if (dependency is not INamedTypeSymbol named || named.TypeKind == TypeKind.Error) return null;
        foreach (var argument in named.TypeArguments) pending.Push(argument);
        return named;
    }

    private sealed class EntityUseLocations
    {
        internal List<Location> UserUses { get; } = [];
        internal List<Location> Evidence { get; } = [];
    }

    private static (string Family, string Role)? ForbiddenUse(ConventionFacts facts, string role, INamedTypeSymbol dependency,
        INamedTypeSymbol[] transportTypes)
    {
        if (role is not (ConventionArchitecture.Domain or ConventionArchitecture.Contract or
                ConventionArchitecture.SharedKernel or ConventionArchitecture.Utils)) return null;
        var symbols = facts.Symbols;
        var cancellation = facts.Cancellation;
        if (symbols.IsTransport(dependency, cancellation) || transportTypes.Any(target => symbols.IsKnown(dependency, target, cancellation)))
            return ("transport-dependent", ConventionArchitecture.Hosted);
        if (IsPersistenceDependency(symbols, dependency, cancellation))
            return ("persistence-dependent", ConventionArchitecture.Infra);
        if (symbols.IsKnown(dependency, symbols.ServiceContract, cancellation) &&
            !(role == ConventionArchitecture.SharedKernel && (dependency.TypeKind == TypeKind.Interface || dependency.IsAbstract)))
            return ("use-case-service-dependent", ConventionArchitecture.Application);
        if (role == ConventionArchitecture.Utils && (facts.IsEntity(dependency) ||
                symbols.IsKnown(dependency, symbols.RepositoryContract, cancellation)))
            return ("application-domain-dependent", ConventionArchitecture.Domain + " or " + ConventionArchitecture.Application);
        return null;
    }

    private static bool IsPersistenceDependency(ConventionSymbols symbols, INamedTypeSymbol dependency, CancellationToken cancellation) =>
        symbols.IsKnown(dependency, symbols.DbContext, cancellation) || symbols.IsContext(dependency, cancellation) ||
        symbols.IsKnown(dependency, symbols.ModelBuilder, cancellation) ||
        symbols.IsKnown(dependency, symbols.EntityBuilder, cancellation) ||
        symbols.IsKnown(dependency, symbols.GenericEntityBuilder, cancellation) ||
        symbols.IsKnown(dependency, symbols.Configuration, cancellation) ||
        symbols.IsKnown(dependency, symbols.Repository, cancellation) ||
        dependency.TypeKind != TypeKind.Interface && symbols.IsKnown(dependency, symbols.RepositoryContract, cancellation);

    private static ISymbol? ReportingSymbol(ConventionFacts facts, SemanticModel model, SyntaxNode node)
    {
        var declaration = node.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().FirstOrDefault();
        ISymbol? symbol = declaration switch
        {
            BaseFieldDeclarationSyntax field when field.Declaration.Variables.Count > 0 =>
                model.GetDeclaredSymbol(field.Declaration.Variables.FirstOrDefault(variable => variable.Span.Contains(node.Span)) ??
                    field.Declaration.Variables[0], facts.Cancellation),
            BaseMethodDeclarationSyntax or BasePropertyDeclarationSyntax or BaseTypeDeclarationSyntax =>
                model.GetDeclaredSymbol(declaration, facts.Cancellation),
            _ => model.GetEnclosingSymbol(node.SpanStart, facts.Cancellation)
        };
        while (symbol is not null)
        {
            if (symbol is IMethodSymbol { AssociatedSymbol: { } associated }) return associated;
            if (symbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction })
                symbol = symbol.ContainingSymbol;
            else if (symbol is INamedTypeSymbol or IMethodSymbol or IPropertySymbol or IFieldSymbol or IEventSymbol) return symbol;
            else symbol = symbol.ContainingSymbol;
        }
        return null;
    }

    private static ImmutableDictionary<string, string?> Properties(ISymbol target, INamedTypeSymbol? entity = null) =>
        ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField, target.GetDocumentationCommentId())
            .Add("Consumer", target.GetDocumentationCommentId())
            .Add(ConventionInputSyntax.EntityField, entity is null ? null : ConventionSymbols.MetadataName(entity));

    private static void AnalyzeReferences(ConventionFacts facts)
    {
        var inventory = ReferenceInventory.Create(facts);
        var reported = new HashSet<string>(StringComparer.Ordinal);
        ReportDeclaredPackageReferences(facts, inventory, reported);
        foreach (var resolved in inventory.References.Where(record => record.Get(ConventionInputSyntax.StageField) == "Resolved"))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            AnalyzeResolvedReference(facts, inventory, resolved, reported);
        }
    }

    private static void ReportDeclaredPackageReferences(ConventionFacts facts, ReferenceInventory inventory, HashSet<string> reported)
    {
        foreach (var declared in inventory.References.Where(record =>
                     record.Get(ConventionInputSyntax.StageField) == ConventionInputSyntax.PackageField))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (FrameworkPackage(declared.Identity) is not { } framework) continue;
            foreach (var role in inventory.Roles)
                ReportFrameworkReference(facts, declared, declared.Identity, framework, role, "package", reported);
        }
    }

    private static void AnalyzeResolvedReference(ConventionFacts facts, ReferenceInventory inventory,
        ConventionInput resolved, HashSet<string> reported)
    {
        var directProject = inventory.Projects.Contains(ConventionProject.NormalizeSeparators(resolved.Get(ConventionInputSyntax.ProjectField))) ||
            inventory.ProjectItems.Contains(ConventionProject.NormalizeSeparators(resolved.Get(ConventionInputSyntax.OriginalProjectField)));
        var directPackage = inventory.Packages.Contains(resolved.Get(ConventionInputSyntax.PackageField));
        if (!directProject && !directPackage ||
            !inventory.Metadata.TryGetValue(ConventionProject.NormalizeSeparators(resolved.Identity), out var reference) ||
            facts.Compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly) return;
        var project = facts.ReferenceProject(assembly);
        var target = ResolveDeclaredReferenceTarget(inventory.References, resolved, directProject);
        var kind = directProject ? "project" : "package";
        if (project is null)
        {
            if (FrameworkAssembly(facts, assembly) is { } framework)
                foreach (var role in inventory.Roles)
                    ReportFrameworkReference(facts, resolved, target, framework, role, kind, reported);
            return;
        }
        ReportDisallowedRoleReferences(facts, inventory.Roles, resolved, assembly, project, target, kind, reported);
    }

    private static string ResolveDeclaredReferenceTarget(ConventionInput[] references, ConventionInput resolved, bool directProject)
    {
        if (directProject)
        {
            var projectPath = ConventionProject.NormalizeSeparators(resolved.Get(ConventionInputSyntax.ProjectField));
            var originalPath = ConventionProject.NormalizeSeparators(resolved.Get(ConventionInputSyntax.OriginalProjectField));
            return references.First(record => record.Get(ConventionInputSyntax.StageField) == ConventionInputSyntax.ProjectField &&
                (ConventionProject.NormalizeSeparators(record.Identity) == projectPath ||
                 originalPath.Length > 0 && ConventionProject.NormalizeSeparators(record.Get(ConventionInputSyntax.SourceField)) == originalPath)).Identity;
        }
        return references.First(record => record.Get(ConventionInputSyntax.StageField) == ConventionInputSyntax.PackageField &&
            string.Equals(record.Identity, resolved.Get(ConventionInputSyntax.PackageField), StringComparison.OrdinalIgnoreCase)).Identity;
    }

    private static void ReportDisallowedRoleReferences(ConventionFacts facts, string[] roles, ConventionInput resolved,
        IAssemblySymbol assembly, ConventionProject project, string target, string kind, HashSet<string> reported)
    {
        foreach (var role in roles)
        {
            if (ConventionArchitecture.Allows(role, project.Role)) continue;
            var key = role + ":" + assembly.Identity.Name;
            if (!reported.Add(key)) continue;
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Reference, resolved.Location,
                ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField, target),
                kind, facts.Project.Name, project.Name));
        }
    }

    private sealed class ReferenceInventory
    {
        internal ConventionInput[] References { get; }
        internal HashSet<string> Projects { get; }
        internal HashSet<string> ProjectItems { get; }
        internal HashSet<string> Packages { get; }
        internal Dictionary<string, PortableExecutableReference> Metadata { get; }
        internal string[] Roles { get; }

        private ReferenceInventory(ConventionInput[] references, HashSet<string> projects, HashSet<string> projectItems,
            HashSet<string> packages, Dictionary<string, PortableExecutableReference> metadata, string[] roles)
        {
            References = references;
            Projects = projects;
            ProjectItems = projectItems;
            Packages = packages;
            Metadata = metadata;
            Roles = roles;
        }

        internal static ReferenceInventory Create(ConventionFacts facts)
        {
            var references = facts.Records.Where(record => record.Kind == ConventionInputSyntax.ReferenceKind).ToArray();
            var projects = references.Where(record => record.Get(ConventionInputSyntax.StageField) == ConventionInputSyntax.ProjectField)
                .Select(record => ConventionProject.NormalizeSeparators(record.Identity))
                .Where(value => value.Length > 0).ToHashSet(StringComparer.Ordinal);
            var projectItems = references.Where(record => record.Get(ConventionInputSyntax.StageField) == ConventionInputSyntax.ProjectField)
                .Select(record => ConventionProject.NormalizeSeparators(record.Get(ConventionInputSyntax.SourceField)))
                .Where(value => value.Length > 0).ToHashSet(StringComparer.Ordinal);
            var packages = references.Where(record => record.Get(ConventionInputSyntax.StageField) == ConventionInputSyntax.PackageField)
                .Select(record => record.Identity).Where(value => value.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var metadata = facts.Compilation.References.OfType<PortableExecutableReference>()
                .Where(reference => reference.FilePath is not null)
                .GroupBy(reference => ConventionProject.NormalizeSeparators(reference.FilePath!), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
            var roles = facts.Records.Where(record => record.Kind == ConventionInputSyntax.RoleKind).Select(record => record.Identity)
                .Append(facts.Project.Role).Where(ConventionProject.IsRole).Distinct(StringComparer.Ordinal).ToArray();
            return new(references, projects, projectItems, packages, metadata, roles);
        }
    }

    private static void ReportFrameworkReference(ConventionFacts facts, ConventionInput input, string target, string framework,
        string role, string kind, HashSet<string> reported)
    {
        var forbidden = role switch
        {
            ConventionArchitecture.Contract or ConventionArchitecture.SharedKernel => framework != SharedKernelPackage,
            ConventionArchitecture.Domain or ConventionArchitecture.Utils => framework is not (SharedKernelPackage or UtilsPackage),
            _ => false
        };
        if (!forbidden || !reported.Add(role + ":" + framework)) return;
        facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Reference, input.Location,
            ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField, target), kind,
            facts.Project.Name, framework));
    }

    [SuppressMessage("SonarQube", "S3267", Justification = "The direct loop avoids a predicate delegate capturing package on each lookup.")]
    private static string? FrameworkPackage(string package)
    {
        foreach (var known in FrameworkPackages)
            if (string.Equals(package, known, StringComparison.OrdinalIgnoreCase)) return known;
        return null;
    }

    private static string? FrameworkAssembly(ConventionFacts facts, IAssemblySymbol assembly)
    {
        if (SymbolEqualityComparer.Default.Equals(facts.Symbols.Entity?.ContainingAssembly, assembly)) return SharedKernelPackage;
        if (SymbolEqualityComparer.Default.Equals(facts.Symbols.Context?.ContainingAssembly, assembly)) return EntityFrameworkPackage;
        if (SymbolEqualityComparer.Default.Equals(facts.Symbols.RegistrationAttribute?.ContainingAssembly, assembly)) return UtilsPackage;
        var controller = facts.Compilation.GetTypeByMetadataName("DRN.Framework.Hosting.Controllers.SourceKnownControllerBase`2");
        if (SymbolEqualityComparer.Default.Equals(controller?.ContainingAssembly, assembly)) return HostingPackage;
        return null;
    }
}
