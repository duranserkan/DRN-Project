using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionBoundUseAnalysis
{
    private static readonly string[] TransportMetadataNames =
    [
        "Microsoft.AspNetCore.Mvc.IActionResult", "Microsoft.AspNetCore.Mvc.ActionResult`1",
        "Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute", "Microsoft.AspNetCore.Mvc.RouteAttribute",
        "Microsoft.AspNetCore.Mvc.ApiControllerAttribute", "Microsoft.AspNetCore.Mvc.ControllerAttribute"
    ];

    internal static void Analyze(ConventionFacts facts)
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
            if (tree.GetRoot(facts.Cancellation) is not CompilationUnitSyntax root || root.Members.Count == 0) continue;
            var generated = facts.IsGenerated(tree);
            SemanticModel? model = null;
            ScanMembers(facts, tracker, tree, ref model, root.Members, generated);
        }
    }

    private static void ScanMembers(ConventionFacts facts, BoundUseTracker tracker, SyntaxTree tree,
        ref SemanticModel? model, SyntaxList<MemberDeclarationSyntax> members, bool generated)
    {
        foreach (var member in members)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            switch (member)
            {
                case BaseNamespaceDeclarationSyntax ns:
                    ScanMembers(facts, tracker, tree, ref model, ns.Members, generated);
                    break;
                case BaseTypeDeclarationSyntax typeDeclaration:
                    ScanTypeDeclaration(facts, tracker, tree, ref model, typeDeclaration, generated);
                    break;
                case DelegateDeclarationSyntax delegateDeclaration:
                    ScanDelegateDeclaration(facts, tracker, tree, ref model, delegateDeclaration, generated);
                    break;
                case GlobalStatementSyntax globalStatement:
                    model ??= facts.SemanticModel(tree);
                    ScanFallbackNode(facts, tracker, model, globalStatement.Statement, generated);
                    break;
            }
        }
    }

    private static void ScanTypeDeclaration(ConventionFacts facts, BoundUseTracker tracker, SyntaxTree tree,
        ref SemanticModel? model, BaseTypeDeclarationSyntax typeDeclaration, bool generated)
    {
        model ??= facts.SemanticModel(tree);
        if (model.GetDeclaredSymbol(typeDeclaration, facts.Cancellation) is not INamedTypeSymbol declaredType)
        {
            if (typeDeclaration is TypeDeclarationSyntax brokenType)
                ScanNestedTypes(facts, tracker, tree, ref model, brokenType.Members, generated);
            return;
        }

        var role = facts.Role(declaredType).Role;
        if (!IsBoundUseType(facts, declaredType, role))
        {
            if (typeDeclaration is TypeDeclarationSyntax skippedType)
                ScanNestedTypes(facts, tracker, tree, ref model, skippedType.Members, generated);
            return;
        }

        var typeConsumer = UserConsumer(facts, declaredType, tracker.InputAnchors);
        if (typeConsumer is not null)
            ScanTypeHeader(facts, tracker, model, typeDeclaration, typeConsumer, declaredType, role, generated);

        switch (typeDeclaration)
        {
            case TypeDeclarationSyntax container:
                foreach (var member in container.Members)
                    ScanTypeMember(facts, tracker, tree, ref model, member, declaredType, role, generated);
                break;
            case EnumDeclarationSyntax enumDeclaration:
                foreach (var member in enumDeclaration.Members)
                    ScanDeclaredMember(facts, tracker, model, member, member, declaredType, role, generated);
                break;
        }
    }

    private static void ScanNestedTypes(ConventionFacts facts, BoundUseTracker tracker, SyntaxTree tree,
        ref SemanticModel? model, SyntaxList<MemberDeclarationSyntax> members, bool generated)
    {
        foreach (var member in members)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (member is BaseTypeDeclarationSyntax nestedType)
                ScanTypeDeclaration(facts, tracker, tree, ref model, nestedType, generated);
            else if (member is DelegateDeclarationSyntax nestedDelegate)
                ScanDelegateDeclaration(facts, tracker, tree, ref model, nestedDelegate, generated);
        }
    }

    private static void ScanTypeHeader(ConventionFacts facts, BoundUseTracker tracker, SemanticModel model,
        BaseTypeDeclarationSyntax typeDeclaration, ISymbol consumer, INamedTypeSymbol declaredType, string role, bool generated)
    {
        foreach (var attributeList in typeDeclaration.AttributeLists)
            ScanExpressions(facts, tracker, model, attributeList, consumer, declaredType, role, generated);
        if (typeDeclaration.BaseList is not null)
            ScanExpressions(facts, tracker, model, typeDeclaration.BaseList, consumer, declaredType, role, generated);
        if (typeDeclaration is not TypeDeclarationSyntax container) return;
        if (container.TypeParameterList is not null)
            ScanExpressions(facts, tracker, model, container.TypeParameterList, consumer, declaredType, role, generated);
        if (container.ParameterList is not null)
            ScanExpressions(facts, tracker, model, container.ParameterList, consumer, declaredType, role, generated);
        foreach (var constraint in container.ConstraintClauses)
            ScanExpressions(facts, tracker, model, constraint, consumer, declaredType, role, generated);
    }

    private static void ScanTypeMember(ConventionFacts facts, BoundUseTracker tracker, SyntaxTree tree,
        ref SemanticModel? model, MemberDeclarationSyntax member, INamedTypeSymbol declaredType, string role, bool generated)
    {
        facts.Cancellation.ThrowIfCancellationRequested();
        switch (member)
        {
            case BaseTypeDeclarationSyntax nestedType:
                ScanTypeDeclaration(facts, tracker, tree, ref model, nestedType, generated);
                break;
            case DelegateDeclarationSyntax nestedDelegate:
                ScanDelegateDeclaration(facts, tracker, tree, ref model, nestedDelegate, generated);
                break;
            case BaseFieldDeclarationSyntax field:
                ScanFieldDeclaration(facts, tracker, model!, field, declaredType, role, generated);
                break;
            case BaseMethodDeclarationSyntax or BasePropertyDeclarationSyntax:
                ScanDeclaredMember(facts, tracker, model!, member, member, declaredType, role, generated);
                break;
            default:
                ScanFallbackNode(facts, tracker, model!, member, generated);
                break;
        }
    }

    private static void ScanDelegateDeclaration(ConventionFacts facts, BoundUseTracker tracker, SyntaxTree tree,
        ref SemanticModel? model, DelegateDeclarationSyntax delegateDeclaration, bool generated)
    {
        model ??= facts.SemanticModel(tree);
        if (model.GetDeclaredSymbol(delegateDeclaration, facts.Cancellation) is not INamedTypeSymbol declaredType) return;
        var role = facts.Role(declaredType).Role;
        if (!IsBoundUseType(facts, declaredType, role)) return;
        var consumer = UserConsumer(facts, declaredType, tracker.InputAnchors);
        if (consumer is null) return;
        ScanExpressions(facts, tracker, model, delegateDeclaration, consumer, declaredType, role, generated);
    }

    private static void ScanFieldDeclaration(ConventionFacts facts, BoundUseTracker tracker, SemanticModel model,
        BaseFieldDeclarationSyntax field, INamedTypeSymbol declaredType, string role, bool generated)
    {
        if (field.Declaration.Variables.Count == 0)
        {
            ScanFallbackNode(facts, tracker, model, field, generated);
            return;
        }

        var firstSymbol = model.GetDeclaredSymbol(field.Declaration.Variables[0], facts.Cancellation);
        var headerConsumer = UserConsumer(facts, firstSymbol, tracker.InputAnchors);
        if (headerConsumer is not null)
        {
            foreach (var attributeList in field.AttributeLists)
                ScanExpressions(facts, tracker, model, attributeList, headerConsumer, declaredType, role, generated);
            ScanExpressions(facts, tracker, model, field.Declaration.Type, headerConsumer, declaredType, role, generated);
        }

        foreach (var variable in field.Declaration.Variables)
        {
            var symbol = ReferenceEquals(variable, field.Declaration.Variables[0])
                ? firstSymbol : model.GetDeclaredSymbol(variable, facts.Cancellation);
            var consumer = UserConsumer(facts, symbol, tracker.InputAnchors);
            if (consumer is not null)
                ScanExpressions(facts, tracker, model, variable, consumer, declaredType, role, generated);
        }
    }

    private static void ScanDeclaredMember(ConventionFacts facts, BoundUseTracker tracker, SemanticModel model,
        SyntaxNode declaration, SyntaxNode expressionRoot, INamedTypeSymbol declaredType, string role, bool generated)
    {
        var raw = model.GetDeclaredSymbol(declaration, facts.Cancellation);
        var symbol = UnwrapMemberSymbol(raw);
        var consumer = UserConsumer(facts, symbol, tracker.InputAnchors);
        if (consumer is not null)
            ScanExpressions(facts, tracker, model, expressionRoot, consumer, declaredType, role, generated);
    }

    private static void ScanFallbackNode(ConventionFacts facts, BoundUseTracker tracker, SemanticModel model,
        SyntaxNode root, bool generated)
    {
        foreach (var node in root.DescendantNodes(
                     static parent => parent is not UsingDirectiveSyntax && !IsNamespaceName(parent)).OfType<ExpressionSyntax>())
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (IsNamespaceName(node)) continue;
            var consumer = UserConsumer(facts, ReportingSymbol(facts, model, node), tracker.InputAnchors);
            var type = consumer as INamedTypeSymbol ?? consumer?.ContainingType;
            if (consumer is null || type is null) continue;
            var role = facts.Role(type).Role;
            if (!IsBoundUseType(facts, type, role)) continue;
            ScanExpressionNode(facts, tracker, model, node, consumer, type, role, generated);
        }
    }

    private static void ScanExpressions(ConventionFacts facts, BoundUseTracker tracker, SemanticModel model,
        SyntaxNode root, ISymbol consumer, INamedTypeSymbol type, string role, bool generated)
    {
        if (root is ExpressionSyntax rootExpression && !IsNamespaceName(rootExpression))
            ScanExpressionNode(facts, tracker, model, rootExpression, consumer, type, role, generated);
        foreach (var node in root.DescendantNodes(
                     static parent => parent is not UsingDirectiveSyntax && !IsNamespaceName(parent)).OfType<ExpressionSyntax>())
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!IsNamespaceName(node))
                ScanExpressionNode(facts, tracker, model, node, consumer, type, role, generated);
        }
    }

    private static bool IsBoundUseType(ConventionFacts facts, INamedTypeSymbol type, string role) =>
        facts.HasSubdomainBoundary(type) || role is (ConventionArchitecture.Domain or
            ConventionArchitecture.Contract or ConventionArchitecture.SharedKernel or ConventionArchitecture.Utils);

    private static bool IsNamespaceName(SyntaxNode node) => node.Parent is BaseNamespaceDeclarationSyntax ns && ns.Name == node;

    private static void ScanExpressionNode(ConventionFacts facts, BoundUseTracker tracker, SemanticModel model,
        ExpressionSyntax node, ISymbol consumer, INamedTypeSymbol type, string role, bool generated)
    {
        tracker.Dependencies.Clear();
        CollectNodeDependencies(facts, model, node, tracker.Dependencies);
        foreach (var entity in NamedDependencies(tracker.Dependencies, tracker.PendingDependencies,
                     tracker.VisitedDependencies, facts.Cancellation))
        {
            if (!generated && ForbiddenUse(facts, role, entity, tracker.TransportTypes) is { } family)
                tracker.RecordForbidden(type, family, node.GetLocation());
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
        var memberDependencies = ConventionBoundaryAnalysis.SignatureTypes(member);
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
        foreach (var (type, families) in tracker.Forbidden)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var location = facts.Location(type);
            if (location == Location.None) continue;
            foreach (var (family, evidence) in families)
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.ForbiddenDependency, location,
                    OrderLocations(facts, evidence), ConventionBoundaryAnalysis.Properties(type), type.Name, facts.Role(type).Role, family));
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
                    additional, ConventionBoundaryAnalysis.Properties(consumer, entity), consumer.ToDisplayString(), owner.Subdomain,
                    entity.ToDisplayString(), entityOwner.Subdomain));
            }
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

    private static string? ForbiddenUse(ConventionFacts facts, string role, INamedTypeSymbol dependency,
        INamedTypeSymbol[] transportTypes)
    {
        if (role is not (ConventionArchitecture.Domain or ConventionArchitecture.Contract or
                ConventionArchitecture.SharedKernel or ConventionArchitecture.Utils)) return null;
        var symbols = facts.Symbols;
        var cancellation = facts.Cancellation;
        if (symbols.IsTransport(dependency, cancellation) || transportTypes.Any(target => symbols.IsKnown(dependency, target, cancellation)))
            return "transport";
        if (IsPersistenceDependency(symbols, dependency, cancellation))
            return "persistence";
        if (symbols.IsKnown(dependency, symbols.ServiceContract, cancellation) &&
            !(role == ConventionArchitecture.SharedKernel && (dependency.TypeKind == TypeKind.Interface || dependency.IsAbstract)))
            return "use-case service";
        if (role == ConventionArchitecture.Utils && (facts.IsEntity(dependency) ||
                symbols.IsKnown(dependency, symbols.RepositoryContract, cancellation)))
            return "application domain";
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
        return UnwrapMemberSymbol(symbol);
    }

    private static ISymbol? UnwrapMemberSymbol(ISymbol? symbol)
    {
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

    private sealed class BoundUseTracker
    {
        internal Dictionary<ISymbol, Dictionary<INamedTypeSymbol, EntityUseLocations>> Uses { get; } = new(SymbolEqualityComparer.Default);
        internal Dictionary<INamedTypeSymbol, Location> InputAnchors { get; }
        internal Dictionary<INamedTypeSymbol, Dictionary<string, HashSet<Location>>> Forbidden { get; } = new(SymbolEqualityComparer.Default);
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

        internal void RecordForbidden(INamedTypeSymbol type, string family, Location location)
        {
            if (!Forbidden.TryGetValue(type, out var families))
            {
                families = new(StringComparer.Ordinal);
                Forbidden.Add(type, families);
            }
            if (!families.TryGetValue(family, out var evidence))
            {
                evidence = [];
                families.Add(family, evidence);
            }
            evidence.Add(location);
        }
    }

    private sealed class EntityUseLocations
    {
        internal List<Location> UserUses { get; } = [];
        internal List<Location> Evidence { get; } = [];
    }
}
