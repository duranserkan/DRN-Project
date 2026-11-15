using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionBoundaryAnalysis
{
    // This is an evidence bound, not a claim about the safety of larger serialized graphs.
    private const int PublicShapeLimit = 4096;
    private static readonly string[] FrameworkPackages =
    [
        "DRN.Framework.SharedKernel", "DRN.Framework.Utils", "DRN.Framework.EntityFramework",
        "DRN.Framework.Hosting", "DRN.Framework.Testing", "DRN.Framework.Jobs", "DRN.Framework.MassTransit"
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
            if (IsPublic(type) && (facts.Role(type).Role == "Contract" || facts.Symbols.IsTransport(type, facts.Cancellation)))
                AnalyzePublicShape(facts, type);
        }
        AnalyzeBoundUses(facts);
    }

    private static void AnalyzeRole(ConventionFacts facts, INamedTypeSymbol type, Location location)
    {
        var role = facts.Role(type).Role;
        if (role.Length == 0 || facts.IsNeutralValue(type)) return;
        var symbols = facts.Symbols;
        string expected;
        string family;
        if (symbols.IsTransport(type, facts.Cancellation)) (expected, family) = ("Hosted", "transport");
        else if (ConventionSymbols.Is(type, symbols.DbContext, facts.Cancellation) || symbols.IsContext(type, facts.Cancellation))
            (expected, family) = ("Infra", "context");
        else if (ConventionSymbols.Is(type, symbols.Configuration, facts.Cancellation)) (expected, family) = ("Infra", "entity configuration");
        else if (ConventionSymbols.Is(type, symbols.RepositoryContract, facts.Cancellation))
            (expected, family) = type.TypeKind == TypeKind.Interface ? ("Domain", "repository contract") : ("Infra", "repository");
        else if (facts.IsEntity(type)) (expected, family) = ("Domain", "entity");
        else if (ConventionSymbols.Is(type, symbols.Dto, facts.Cancellation)) (expected, family) = ("Contract", "DTO");
        else if (ConventionSymbols.Is(type, symbols.ServiceContract, facts.Cancellation)) (expected, family) = ("Application", "use-case service");
        else return;
        var sharedAbstraction = role == "SharedKernel" &&
            (type.TypeKind == TypeKind.Interface || type.IsAbstract) && expected is "Domain" or "Contract" or "Application";
        if (role != expected && !sharedAbstraction)
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.ImplementationRole, location,
                Properties(type), type.Name, family, expected));
    }

    private static void AnalyzePublicShape(ConventionFacts facts, INamedTypeSymbol type)
    {
        var transport = facts.Symbols.IsTransport(type, facts.Cancellation);
        var nonAction = facts.Compilation.GetTypeByMetadataName("Microsoft.AspNetCore.Mvc.NonActionAttribute");
        foreach (var member in PublicMembers(facts, type, transport))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!IsPublic(member) || member.IsImplicitlyDeclared || member is INamedTypeSymbol ||
                member is IMethodSymbol { AssociatedSymbol: not null }) continue;
            if (transport && (member is not IMethodSymbol
                { MethodKind: MethodKind.Ordinary, IsStatic: false, IsAbstract: false, IsGenericMethod: false } ||
                IsNonAction((IMethodSymbol)member, nonAction))) continue;
            var declaredHere = SymbolEqualityComparer.Default.Equals(member.ContainingType, type);
            if (!declaredHere && facts.IsLocal(member.ContainingType) && facts.Role(member.ContainingType).Role == "Contract" &&
                IsPublic(member.ContainingType) && SymbolEqualityComparer.Default.Equals(member.ContainingType, member.ContainingType.OriginalDefinition)) continue;
            var reportingSymbol = declaredHere ? member : type;
            var location = facts.Location(reportingSymbol);
            if (location == Location.None) continue;
            var pending = new Stack<ITypeSymbol>(PublicSignatureTypes(facts, member));
            var visited = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
            INamedTypeSymbol? exposed = null;
            var work = 0;
            var exhausted = false;
            while (pending.TryPop(out var current))
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (++work > PublicShapeLimit)
                {
                    exhausted = true;
                    break;
                }
                if (!visited.Add(current)) continue;
                if (current is IArrayTypeSymbol array) pending.Push(array.ElementType);
                if (current is ITypeParameterSymbol parameter)
                    foreach (var constraint in parameter.ConstraintTypes) pending.Push(constraint);
                if (current is not INamedTypeSymbol named || named.TypeKind == TypeKind.Error) continue;
                if (facts.IsEntity(named) || facts.Symbols.IsPersistenceType(named, facts.Cancellation))
                {
                    exposed = named;
                    break;
                }
                foreach (var argument in named.TypeArguments) pending.Push(argument);
                if (!facts.IsLocal(named) && !ConventionSymbols.Is(named, facts.Symbols.Dto, facts.Cancellation) &&
                    !facts.IsNeutralValue(named) && facts.ReferenceProject(named.ContainingAssembly)?.Role != "Contract") continue;
                var inheritedContracts = named.TypeKind == TypeKind.Interface ? named.AllInterfaces : [];
                foreach (var model in ConventionSymbols.Bases(named, facts.Cancellation).Concat(inheritedContracts))
                {
                    foreach (var property in model.GetMembers())
                    {
                        facts.Cancellation.ThrowIfCancellationRequested();
                        if (++work > PublicShapeLimit) { exhausted = true; break; }
                        if (property.DeclaredAccessibility != Accessibility.Public || property.IsStatic) continue;
                        if (property is IPropertySymbol { GetMethod: { DeclaredAccessibility: Accessibility.Public } } p) pending.Push(p.Type);
                        if (property is IFieldSymbol f) pending.Push(f.Type);
                    }
                    if (exhausted) break;
                }
                if (exhausted) break;
            }
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

    private static IEnumerable<ISymbol> PublicMembers(ConventionFacts facts, INamedTypeSymbol type, bool transport)
    {
        var overridden = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        var contracts = type.TypeKind == TypeKind.Interface ? type.AllInterfaces : [];
        foreach (var current in ConventionSymbols.Bases(type, facts.Cancellation).Concat(contracts))
        foreach (var member in current.GetMembers())
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (transport && member is not IMethodSymbol || overridden.Contains(member.OriginalDefinition)) continue;
            if (!SymbolEqualityComparer.Default.Equals(current, type) && member is IMethodSymbol
                { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor }) continue;
            for (var parent = Overridden(member); parent is not null; parent = Overridden(parent))
                overridden.Add(parent.OriginalDefinition);
            yield return member;
        }
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
            var declaration = reference.GetSyntax(facts.Cancellation);
            SyntaxNode? initializer = declaration switch
            {
                ConstructorDeclarationSyntax constructorDeclaration => constructorDeclaration.Initializer,
                TypeDeclarationSyntax typeDeclaration => typeDeclaration.BaseList?.Types.OfType<PrimaryConstructorBaseTypeSyntax>().FirstOrDefault(),
                _ => null
            };
            if (initializer is null || !facts.Compilation.ContainsSyntaxTree(initializer.SyntaxTree)) continue;
            var arguments = initializer switch
            {
                ConstructorInitializerSyntax ordinary => ordinary.ArgumentList.Arguments,
                PrimaryConstructorBaseTypeSyntax primary => primary.ArgumentList.Arguments,
                _ => default
            };
            var model = facts.Compilation.GetSemanticModel(initializer.SyntaxTree);
            if (model.GetSymbolInfo(initializer, facts.Cancellation).Symbol is IMethodSymbol target &&
                ConventionSymbols.Same(target.ContainingType, facts.Symbols.Dto))
            {
                foreach (var argument in arguments)
                    if (model.GetSymbolInfo(argument.Expression, facts.Cancellation).Symbol is IParameterSymbol parameter &&
                        parameter.Type is INamedTypeSymbol entity && facts.IsEntity(entity)) return parameter;
            }
        }
        return null;
    }

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
            foreach (var constraint in parameter.ConstraintTypes) yield return constraint;
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
        var uses = new Dictionary<ISymbol, Dictionary<INamedTypeSymbol, EntityUseLocations>>(SymbolEqualityComparer.Default);
        var inputAnchors = new Dictionary<INamedTypeSymbol, Location>(SymbolEqualityComparer.Default);
        foreach (var input in facts.Records.Where(input => input.Kind is "Ownership" or "Context"))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (input.Location != Location.None && facts.Compilation.GetTypeByMetadataName(input.Identity) is { } type &&
                !facts.HasInvalidOwner(type)) inputAnchors.TryAdd(type, input.Location);
        }

        void RecordUse(ISymbol consumer, INamedTypeSymbol entity, Location evidence, bool userUse)
        {
            var type = consumer as INamedTypeSymbol ?? consumer.ContainingType;
            if (type is null || facts.Owner(type) is null || !facts.IsEntity(entity) ||
                facts.Owner(entity) is null || facts.UsesOwnSubdomain(type, entity)) return;
            if (!uses.TryGetValue(consumer, out var entities)) uses.Add(consumer, entities = new(SymbolEqualityComparer.Default));
            if (!entities.TryGetValue(entity, out var locations)) entities.Add(entity, locations = new());
            if (evidence == Location.None) return;
            var destination = userUse ? locations.UserUses : locations.Evidence;
            if (!destination.Contains(evidence)) destination.Add(evidence);
        }

        var forbidden = new Dictionary<INamedTypeSymbol, (string Family, string Role, List<Location> Locations)>(SymbolEqualityComparer.Default);
        var dependencies = new List<ITypeSymbol>(2);
        var pendingDependencies = new Stack<ITypeSymbol>();
        var visitedDependencies = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
        var transportTypes = new[]
        {
            "Microsoft.AspNetCore.Mvc.IActionResult", "Microsoft.AspNetCore.Mvc.ActionResult`1",
            "Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute", "Microsoft.AspNetCore.Mvc.RouteAttribute",
            "Microsoft.AspNetCore.Mvc.ApiControllerAttribute", "Microsoft.AspNetCore.Mvc.ControllerAttribute"
        }.Select(facts.Compilation.GetTypeByMetadataName).Where(type => type is not null).Cast<INamedTypeSymbol>().ToArray();
        foreach (var tree in facts.Compilation.SyntaxTrees.OrderBy(tree => facts.LogicalPath(tree) ?? tree.FilePath, StringComparer.Ordinal))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var generated = facts.IsGenerated(tree);
            var model = facts.Compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot(facts.Cancellation).DescendantNodes().OfType<ExpressionSyntax>())
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (node.Ancestors().Any(parent => parent is UsingDirectiveSyntax ||
                    parent is BaseNamespaceDeclarationSyntax ns && ns.Name.Span.Contains(node.Span))) continue;
                var consumer = UserConsumer(facts, ReportingSymbol(facts, model, node), inputAnchors);
                if (consumer is null) continue;
                var type = consumer as INamedTypeSymbol ?? consumer.ContainingType;
                if (type is null) continue;
                var role = facts.Role(type).Role;
                dependencies.Clear();
                if (model.GetTypeInfo(node, facts.Cancellation).Type is { } valueType) dependencies.Add(valueType);
                if (node is SimpleNameSyntax or MemberAccessExpressionSyntax &&
                    model.GetSymbolInfo(node, facts.Cancellation).Symbol is { } bound)
                {
                    if (bound is INamedTypeSymbol named) dependencies.Add(named);
                    else if (bound.ContainingType is { } declaringType) dependencies.Add(declaringType);
                }
                foreach (var entity in NamedDependencies(dependencies, pendingDependencies, visitedDependencies, facts.Cancellation))
                {
                    var forbiddenUse = generated ? null : ForbiddenUse(facts, role, entity, transportTypes);
                    if (forbiddenUse is { } dependencyRole)
                    {
                        if (!forbidden.TryGetValue(type, out var entry))
                        {
                            entry = (dependencyRole.Family, dependencyRole.Role, []);
                            forbidden.Add(type, entry);
                        }
                        entry.Locations.Add(node.GetLocation());
                    }
                    RecordUse(consumer, entity, node.GetLocation(), !generated);
                }
            }
        }

        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var consumer = UserConsumer(facts, type, inputAnchors);
            if (consumer is null || facts.Owner(type) is null) continue;
            foreach (var inherited in ConventionSymbols.Bases(type, facts.Cancellation).Skip(1).Concat(type.AllInterfaces))
            foreach (var member in inherited.GetMembers())
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (member is INamedTypeSymbol || member is IMethodSymbol
                    { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } ||
                    !facts.Compilation.IsSymbolAccessibleWithin(member, type)) continue;
                var memberDependencies = SignatureTypes(member);
                if (member is IMethodSymbol method)
                    memberDependencies = memberDependencies.Concat(method.TypeParameters.SelectMany(parameter => parameter.ConstraintTypes));
                foreach (var entity in NamedDependencies(memberDependencies, pendingDependencies, visitedDependencies, facts.Cancellation))
                {
                    RecordUse(consumer, entity, Location.None, false);
                    foreach (var location in member.Locations.Where(location => location.SourceTree is { } source &&
                                 facts.Compilation.ContainsSyntaxTree(source)))
                        RecordUse(consumer, entity, location, false);
                }
            }
        }
        var classified = facts.Diagnostics.Where(diagnostic => diagnostic.Id == "DRNCV016")
            .Select(diagnostic => diagnostic.Properties.TryGetValue("Target", out var target) ? target : null)
            .Where(target => target is not null).ToHashSet(StringComparer.Ordinal);
        foreach (var (type, dependency) in forbidden)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (classified.Contains(type.GetDocumentationCommentId())) continue;
            var location = facts.Location(type);
            if (location == Location.None) continue;
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.ImplementationRole, location,
                dependency.Locations.Distinct(), Properties(type), type.Name, dependency.Family, dependency.Role));
        }
        foreach (var (consumer, entities) in uses)
        foreach (var (entity, locations) in entities)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var owner = facts.Owner(consumer as INamedTypeSymbol ?? consumer.ContainingType)!;
            var entityOwner = facts.Owner(entity)!;
            var primary = OrderLocations(facts, locations.UserUses).FirstOrDefault() ?? facts.Location(consumer);
            if (primary == Location.None && consumer is INamedTypeSymbol target) inputAnchors.TryGetValue(target, out primary);
            if (primary is null || primary == Location.None) continue;
            var additional = OrderLocations(facts, locations.UserUses.Concat(locations.Evidence).Distinct())
                .Where(location => !location.Equals(primary));
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.CrossSubdomain, primary,
                additional, Properties(consumer, entity), consumer.ToDisplayString(), owner.Subdomain,
                entity.ToDisplayString(), entityOwner.Subdomain));
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
            if (dependency is IArrayTypeSymbol array) pending.Push(array.ElementType);
            if (dependency is ITypeParameterSymbol parameter)
                foreach (var constraint in parameter.ConstraintTypes) pending.Push(constraint);
            if (dependency is not INamedTypeSymbol named || named.TypeKind == TypeKind.Error) continue;
            foreach (var argument in named.TypeArguments) pending.Push(argument);
            yield return named;
        }
    }

    private sealed class EntityUseLocations
    {
        internal List<Location> UserUses { get; } = [];
        internal List<Location> Evidence { get; } = [];
    }

    private static (string Family, string Role)? ForbiddenUse(ConventionFacts facts, string role, INamedTypeSymbol dependency,
        INamedTypeSymbol[] transportTypes)
    {
        if (role is not ("Domain" or "Contract" or "SharedKernel" or "Utils")) return null;
        var symbols = facts.Symbols;
        var cancellation = facts.Cancellation;
        if (symbols.IsTransport(dependency, cancellation) || transportTypes.Any(target => ConventionSymbols.Is(dependency, target, cancellation)))
            return ("transport-dependent", "Hosted");
        if (ConventionSymbols.Is(dependency, symbols.DbContext, cancellation) || symbols.IsContext(dependency, cancellation) ||
            ConventionSymbols.Is(dependency, symbols.ModelBuilder, cancellation) ||
            ConventionSymbols.Is(dependency, symbols.EntityBuilder, cancellation) ||
            ConventionSymbols.Is(dependency, symbols.GenericEntityBuilder, cancellation) ||
            ConventionSymbols.Is(dependency, symbols.Configuration, cancellation) ||
            ConventionSymbols.Is(dependency, symbols.Repository, cancellation) ||
            dependency.TypeKind != TypeKind.Interface && ConventionSymbols.Is(dependency, symbols.RepositoryContract, cancellation))
            return ("persistence-dependent", "Infra");
        if (ConventionSymbols.Is(dependency, symbols.ServiceContract, cancellation) &&
            !(role == "SharedKernel" && (dependency.TypeKind == TypeKind.Interface || dependency.IsAbstract)))
            return ("use-case-service-dependent", "Application");
        if (role == "Utils" && (facts.IsEntity(dependency) || ConventionSymbols.Is(dependency, symbols.RepositoryContract, cancellation)))
            return ("application-domain-dependent", "Domain or Application");
        return null;
    }

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
        ImmutableDictionary<string, string?>.Empty.Add("Target", target.GetDocumentationCommentId())
            .Add("Consumer", target.GetDocumentationCommentId())
            .Add("Entity", entity is null ? null : ConventionSymbols.MetadataName(entity));

    private static void AnalyzeReferences(ConventionFacts facts)
    {
        var references = facts.Records.Where(record => record.Kind == "Reference").ToArray();
        var projects = references.Where(record => record.Get("Stage") == "Project")
            .Select(record => ConventionProject.NormalizeSeparators(record.Identity)).Where(value => value.Length > 0).ToHashSet(StringComparer.Ordinal);
        var projectItems = references.Where(record => record.Get("Stage") == "Project")
            .Select(record => ConventionProject.NormalizeSeparators(record.Get("Source"))).Where(value => value.Length > 0).ToHashSet(StringComparer.Ordinal);
        var packages = references.Where(record => record.Get("Stage") == "Package")
            .Select(record => record.Identity).Where(value => value.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var metadata = facts.Compilation.References.OfType<PortableExecutableReference>()
            .Where(reference => reference.FilePath is not null)
            .GroupBy(reference => ConventionProject.NormalizeSeparators(reference.FilePath!), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var reported = new HashSet<string>(StringComparer.Ordinal);
        var roles = facts.Records.Where(record => record.Kind == "Role").Select(record => record.Identity)
            .Append(facts.Project.Role).Where(ConventionProject.IsRole).Distinct(StringComparer.Ordinal).ToArray();
        foreach (var declared in references.Where(record => record.Get("Stage") == "Package"))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (FrameworkPackage(declared.Identity) is not { } framework) continue;
            foreach (var role in roles)
                ReportFrameworkReference(facts, declared, declared.Identity, framework, role, "package", reported);
        }
        foreach (var resolved in references.Where(record => record.Get("Stage") == "Resolved"))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var directProject = projects.Contains(ConventionProject.NormalizeSeparators(resolved.Get("Project"))) ||
                projectItems.Contains(ConventionProject.NormalizeSeparators(resolved.Get("OriginalProject")));
            var directPackage = packages.Contains(resolved.Get("Package"));
            if (!directProject && !directPackage || !metadata.TryGetValue(ConventionProject.NormalizeSeparators(resolved.Identity), out var reference) ||
                facts.Compilation.GetAssemblyOrModuleSymbol(reference) is not IAssemblySymbol assembly) continue;
            var project = facts.ReferenceProject(assembly);
            var target = directProject
                ? references.First(record => record.Get("Stage") == "Project" &&
                    (ConventionProject.NormalizeSeparators(record.Identity) == ConventionProject.NormalizeSeparators(resolved.Get("Project")) ||
                     resolved.Get("OriginalProject").Length > 0 && ConventionProject.NormalizeSeparators(record.Get("Source")) ==
                     ConventionProject.NormalizeSeparators(resolved.Get("OriginalProject")))).Identity
                : references.First(record => record.Get("Stage") == "Package" &&
                    string.Equals(record.Identity, resolved.Get("Package"), StringComparison.OrdinalIgnoreCase)).Identity;
            if (project is null)
            {
                if (FrameworkAssembly(facts, assembly) is { } framework)
                    foreach (var role in roles)
                        ReportFrameworkReference(facts, resolved, target, framework, role, directProject ? "project" : "package", reported);
                continue;
            }
            foreach (var role in roles)
            {
                if (ConventionArchitecture.Allows(role, project.Role)) continue;
                var key = role + ":" + assembly.Identity.Name;
                if (!reported.Add(key)) continue;
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Reference, resolved.Location,
                    ImmutableDictionary<string, string?>.Empty.Add("Target", target),
                    directProject ? "project" : "package", facts.Project.Name, project.Name));
            }
        }
    }

    private static void ReportFrameworkReference(ConventionFacts facts, ConventionInput input, string target, string framework,
        string role, string kind, HashSet<string> reported)
    {
        var forbidden = role switch
        {
            "Contract" or "SharedKernel" => framework != "DRN.Framework.SharedKernel",
            "Domain" or "Utils" => framework is not ("DRN.Framework.SharedKernel" or "DRN.Framework.Utils"),
            _ => false
        };
        if (!forbidden || !reported.Add(role + ":" + framework)) return;
        facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Reference, input.Location,
            ImmutableDictionary<string, string?>.Empty.Add("Target", target), kind,
            facts.Project.Name, framework));
    }

    private static string? FrameworkPackage(string package)
    {
        foreach (var known in FrameworkPackages)
            if (string.Equals(package, known, StringComparison.OrdinalIgnoreCase)) return known;
        return null;
    }

    private static string? FrameworkAssembly(ConventionFacts facts, IAssemblySymbol assembly)
    {
        if (SymbolEqualityComparer.Default.Equals(facts.Symbols.Entity?.ContainingAssembly, assembly)) return "DRN.Framework.SharedKernel";
        if (SymbolEqualityComparer.Default.Equals(facts.Symbols.Context?.ContainingAssembly, assembly)) return "DRN.Framework.EntityFramework";
        if (SymbolEqualityComparer.Default.Equals(facts.Symbols.RegistrationAttribute?.ContainingAssembly, assembly)) return "DRN.Framework.Utils";
        var controller = facts.Compilation.GetTypeByMetadataName("DRN.Framework.Hosting.Controllers.SourceKnownControllerBase`2");
        if (SymbolEqualityComparer.Default.Equals(controller?.ContainingAssembly, assembly)) return "DRN.Framework.Hosting";
        return null;
    }
}
