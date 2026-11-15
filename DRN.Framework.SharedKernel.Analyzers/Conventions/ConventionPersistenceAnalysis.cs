using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionPersistenceAnalysis
{
    internal static void Analyze(ConventionFacts facts)
    {
        var models = new Dictionary<INamedTypeSymbol, ContextModel>(SymbolEqualityComparer.Default);
        ContextModel Model(INamedTypeSymbol context)
        {
            if (!models.TryGetValue(context, out var model))
                models.Add(context, model = ReadModel(facts, context));
            return model;
        }

        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (type.IsAbstract || ConventionSymbols.IsOpen(type, facts.Cancellation)) continue;
            if (facts.Symbols.IsContext(type, facts.Cancellation) && facts.Role(type).Role == "Infra")
            {
                var model = Model(type);
                foreach (var entity in model.Included)
                    if (model.Excluded.Contains(entity) && facts.Location(type) != Location.None)
                        facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Persistence,
                            facts.Location(type), Properties(type, type, entity), entity.Name, type.Name, "contradictory explicit membership"));
            }

            if (facts.Location(type) == Location.None) continue;
            if (facts.Role(type).Role == "Infra") CheckRepository(facts, type, Model);
            if (facts.Role(type).Role == "Application" && !facts.Symbols.IsTransport(type, facts.Cancellation))
                CheckService(facts, type);
        }

        CheckConfigurations(facts, Model);
        CheckMigrations(facts);
    }

    private static ContextModel ReadModel(ConventionFacts facts, INamedTypeSymbol context)
    {
        var result = new ContextModel();
        foreach (var attribute in context.GetAttributes())
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!ConventionSymbols.Same(attribute.AttributeClass, facts.Symbols.PersistenceAttribute)) continue;
            if (attribute.ConstructorArguments is not [{ Value: INamedTypeSymbol entity }] ||
                entity.TypeKind == TypeKind.Error || ConventionSymbols.IsOpen(entity, facts.Cancellation)) continue;
            var inclusion = attribute.NamedArguments.FirstOrDefault(pair => pair.Key == "Inclusion").Value.Value;
            if (inclusion is not null and not 0 and not 1)
            {
                facts.Diagnostics.Add(ConventionProject.InputIssue(context.Name, "an undefined persistence inclusion",
                    "Include or Exclude membership", facts.AttributeLocation(attribute, context)));
                continue;
            }
            if (inclusion is 1) result.Excluded.Add(entity);
            else
            {
                result.Included.Add(entity);
                result.Candidates.Add(entity);
            }
        }

        foreach (var type in ConventionSymbols.Bases(context, facts.Cancellation))
        foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (property.IsStatic || property.Type is not INamedTypeSymbol propertyType) continue;
            foreach (var binding in ConventionSymbols.Bindings(propertyType, facts.Symbols.DbSet, facts.Cancellation))
                if (binding.TypeArguments is [INamedTypeSymbol entity] && !ConventionSymbols.IsOpen(entity, facts.Cancellation))
                    result.Candidates.Add(entity);
        }

        var modelContract = facts.Symbols.DbContext?.GetMembers("OnModelCreating").OfType<IMethodSymbol>()
            .SingleOrDefault(method => !method.IsStatic && method.Parameters.Length == 1 &&
                ConventionSymbols.Same(method.Parameters[0].Type, facts.Symbols.ModelBuilder));
        if (modelContract is null) return result;
        var implementation = ConventionSymbols.Bases(context, facts.Cancellation)
            .SelectMany(type => type.GetMembers("OnModelCreating").OfType<IMethodSymbol>())
            .FirstOrDefault(method => OverridesModelContract(method, modelContract, facts.Cancellation));
        if (implementation is null) return result;
        result.DefaultScan = ConventionSymbols.Same(implementation.ContainingType, facts.Symbols.DrnContext) ||
                             ConventionSymbols.Same(implementation.ContainingType, facts.Symbols.DrnIdentityContext);
        if (implementation.DeclaringSyntaxReferences.Length != 1 ||
            !facts.Compilation.ContainsSyntaxTree(implementation.DeclaringSyntaxReferences[0].SyntaxTree)) return result;
        var declaration = implementation.DeclaringSyntaxReferences[0].GetSyntax(facts.Cancellation) as MethodDeclarationSyntax;
        if (declaration?.Body is not { } body) return result;
        if (body.Statements.Any(statement => statement is not ExpressionStatementSyntax)) return result;
        var semantic = facts.Compilation.GetSemanticModel(body.SyntaxTree);

        // Only unconditional, top-level calls establish exclusion or application. Helpers remain unknown.
        foreach (var statement in body.Statements.OfType<ExpressionStatementSyntax>())
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (statement.Expression is not InvocationExpressionSyntax invocation ||
                semantic.GetSymbolInfo(invocation, facts.Cancellation).Symbol is not IMethodSymbol method ||
                invocation.Expression is not MemberAccessExpressionSyntax access ||
                semantic.GetSymbolInfo(access.Expression, facts.Cancellation).Symbol is not IParameterSymbol receiver ||
                !SymbolEqualityComparer.Default.Equals(receiver, implementation.Parameters[0]) ||
                !ConventionSymbols.Same(method.ContainingType, facts.Symbols.ModelBuilder))
            {
                result.Ignored.Clear();
                result.Applied.Clear();
                continue;
            }
            if (method.Name == "Ignore" && method.TypeArguments is [INamedTypeSymbol ignored])
            {
                result.Ignored.Add(ignored);
                result.Applied.RemoveWhere(configuration => ConventionSymbols.Bindings(configuration,
                    facts.Symbols.Configuration, facts.Cancellation).Any(binding => binding.TypeArguments.Length == 1 &&
                    SymbolEqualityComparer.Default.Equals(binding.TypeArguments[0], ignored)));
            }
            else if (method.Name == "Entity" && method.TypeArguments is [INamedTypeSymbol included])
                result.Ignored.Remove(included);
            else
            {
                result.Ignored.Clear();
                if (method.Name != "ApplyConfiguration") result.Applied.Clear();
            }
            if (method.Name == "ApplyConfiguration" && invocation.ArgumentList.Arguments.Count == 1 &&
                semantic.GetTypeInfo(invocation.ArgumentList.Arguments[0].Expression, facts.Cancellation).Type is INamedTypeSymbol configuration)
                result.Applied.Add(configuration);
        }
        return result;
    }

    private static bool OverridesModelContract(IMethodSymbol method, IMethodSymbol contract, CancellationToken cancellation)
    {
        for (var current = method; current is not null; current = current.OverriddenMethod)
        {
            cancellation.ThrowIfCancellationRequested();
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, contract.OriginalDefinition)) return true;
        }
        return false;
    }

    private static void CheckRepository(ConventionFacts facts, INamedTypeSymbol repository,
        Func<INamedTypeSymbol, ContextModel> model)
    {
        var bindings = ConventionSymbols.Bindings(repository, facts.Symbols.Repository, facts.Cancellation).ToArray();
        if (bindings.Length == 0) return;
        foreach (var reference in repository.DeclaringSyntaxReferences)
        {
            var declaration = reference.GetSyntax(facts.Cancellation);
            if (facts.Compilation.GetSemanticModel(declaration.SyntaxTree).GetDiagnostics(declaration.Span, facts.Cancellation)
                .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)) return;
        }
        foreach (var binding in bindings)
        {
            if (binding.TypeArguments is not [INamedTypeSymbol context, INamedTypeSymbol entity] ||
                facts.HasInvalidContext(context) || ConventionSymbols.IsOpen(binding, facts.Cancellation)) continue;
            var membership = model(context);
            membership.Candidates.Add(entity);
            if (!membership.Excluded.Contains(entity) && !membership.Ignored.Contains(entity)) continue;
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Repository, facts.Location(repository), Properties(repository, context, entity),
                repository.Name, entity.Name, context.Name + " explicitly excludes the entity"));
        }
    }

    private static void CheckConfigurations(ConventionFacts facts, Func<INamedTypeSymbol, ContextModel> model)
    {
        var seen = new HashSet<(string Configuration, string Context)>();
        var declared = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        var uses = ReadConfigurationUses(facts);
        foreach (var input in facts.Records.Where(input => input.Kind == "Configuration"))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!seen.Add((input.Identity, input.Get("Context")))) continue;
            var configuration = facts.Compilation.GetTypeByMetadataName(input.Identity);
            var context = facts.Compilation.GetTypeByMetadataName(input.Get("Context"));
            if (configuration is not null) declared.Add(configuration);
            if (configuration is null || context is null || !facts.Symbols.IsContext(context, facts.Cancellation) ||
                !ConventionSymbols.Is(configuration, facts.Symbols.Configuration, facts.Cancellation))
            {
                facts.Diagnostics.Add(ConventionProject.InputIssue(input.Identity, "an invalid configuration association",
                    "configuration type and exact context metadata name", input.Location));
                continue;
            }
            CheckConfiguration(facts, configuration, context, model(context), uses, input.Location);
        }

        var contextsByNamespace = new Dictionary<string, List<INamedTypeSymbol>>(StringComparer.Ordinal);
        var contextsByEntity = new Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
        foreach (var context in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (context.IsAbstract || ConventionSymbols.IsOpen(context, facts.Cancellation) ||
                !facts.Symbols.IsContext(context, facts.Cancellation) || facts.HasInvalidContext(context) ||
                facts.Role(context).Role != "Infra") continue;
            var ns = context.ContainingNamespace.ToDisplayString();
            if (!contextsByNamespace.TryGetValue(ns, out var area)) contextsByNamespace.Add(ns, area = []);
            area.Add(context);
            var evidence = model(context);
            foreach (var entity in evidence.Candidates)
            {
                if (evidence.Excluded.Contains(entity) || evidence.Ignored.Contains(entity)) continue;
                if (!contextsByEntity.TryGetValue(entity, out var owners))
                    contextsByEntity.Add(entity, owners = new(SymbolEqualityComparer.Default));
                owners.Add(context);
            }
        }

        foreach (var configuration in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (declared.Contains(configuration) || configuration.IsAbstract || ConventionSymbols.IsOpen(configuration, facts.Cancellation) ||
                facts.Role(configuration).Role != "Infra" || facts.Location(configuration) == Location.None ||
                !ConventionSymbols.Is(configuration, facts.Symbols.Configuration, facts.Cancellation)) continue;
            var configurationNamespace = configuration.ContainingNamespace.ToDisplayString();
            List<INamedTypeSymbol>? nearest = null;
            for (var ns = configuration.ContainingNamespace; !ns.IsGlobalNamespace; ns = ns.ContainingNamespace)
                if (contextsByNamespace.TryGetValue(ns.ToDisplayString(), out nearest)) break;
            if (nearest is not null)
            {
                // Same-area contexts intentionally share the scan. Nested CQRS areas select their nearest context area.
                foreach (var context in nearest)
                    CheckConfiguration(facts, configuration, context, model(context), uses, facts.Location(configuration));
                continue;
            }

            // Outside a known scan area, ordinary Configurations placement plus a unique positive membership
            // supplies intent. Shared entities in multiple areas need an explicit association instead.
            if (!configurationNamespace.Split('.').Contains("Configurations", StringComparer.Ordinal)) continue;
            var owner = facts.Owner(configuration);
            if (owner is null) continue;
            var candidates = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (var binding in ConventionSymbols.Bindings(configuration, facts.Symbols.Configuration, facts.Cancellation))
                if (binding.TypeArguments is [INamedTypeSymbol entity] && contextsByEntity.TryGetValue(entity, out var contexts))
                    foreach (var context in contexts)
                    {
                        var contextOwner = facts.Owner(context);
                        if (SymbolEqualityComparer.Default.Equals(context.ContainingAssembly, configuration.ContainingAssembly) &&
                            contextOwner?.Subdomain == owner.Subdomain) candidates.Add(context);
                    }
            if (candidates.Count == 1)
            {
                var context = candidates.Single();
                CheckConfiguration(facts, configuration, context, model(context), uses, facts.Location(configuration));
            }
        }
    }

    private static void CheckConfiguration(ConventionFacts facts, INamedTypeSymbol configuration, INamedTypeSymbol context,
        ContextModel evidence, ConfigurationUses uses, Location fallback)
    {
        if (facts.HasInvalidContext(context) || facts.Role(context).Role != "Infra" ||
            evidence.Applied.Contains(configuration) || !evidence.DefaultScan) return;
        if (DefaultDiscoverable(facts, configuration, context) || HasPossibleApplication(facts, configuration, context, evidence, uses)) return;
        foreach (var binding in ConventionSymbols.Bindings(configuration, facts.Symbols.Configuration, facts.Cancellation)
                     .OrderBy(binding => ConventionSymbols.MetadataName(binding.TypeArguments[0] as INamedTypeSymbol ?? configuration), StringComparer.Ordinal))
        {
            if (binding.TypeArguments is not [INamedTypeSymbol entity] || evidence.Excluded.Contains(entity) || evidence.Ignored.Contains(entity)) continue;
            if (entity.GetAttributes().Any(attribute =>
                    ConventionSymbols.Same(attribute.AttributeClass, facts.Symbols.ConfigurationAttribute) &&
                    attribute.ConstructorArguments is [{ Value: INamedTypeSymbol attributed }] &&
                    SymbolEqualityComparer.Default.Equals(attributed, configuration))) continue;
            var location = facts.Location(configuration);
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Configuration,
                location == Location.None ? fallback : location, Properties(configuration, context, entity), configuration.Name, entity.Name, context.Name));
            break;
        }
    }

    private static bool DefaultDiscoverable(ConventionFacts facts, INamedTypeSymbol configuration, INamedTypeSymbol context) =>
        ConventionProject.IsNamespaceWithin(configuration.ContainingNamespace.ToDisplayString(), context.ContainingNamespace.ToDisplayString()) &&
        SymbolEqualityComparer.Default.Equals(configuration.ContainingAssembly, context.ContainingAssembly) &&
        !configuration.IsAbstract && !ConventionSymbols.IsOpen(configuration, facts.Cancellation) &&
        configuration.InstanceConstructors.Any(constructor => constructor.Parameters.Length == 0);

    private static ConfigurationUses ReadConfigurationUses(ConventionFacts facts)
    {
        var result = new ConfigurationUses();
        if (facts.Symbols.Configuration is null) return result;
        var callers = new Dictionary<IMethodSymbol, HashSet<IMethodSymbol>>(SymbolEqualityComparer.Default);
        var applications = new List<(INamedTypeSymbol Configuration, IMethodSymbol Method)>();
        foreach (var tree in facts.Compilation.SyntaxTrees)
        {
            var semantic = facts.Compilation.GetSemanticModel(tree);
            foreach (var invocation in tree.GetRoot(facts.Cancellation).DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (semantic.GetSymbolInfo(invocation, facts.Cancellation).Symbol is not IMethodSymbol method ||
                    semantic.GetEnclosingSymbol(invocation.SpanStart, facts.Cancellation) is not IMethodSymbol ownerMethod ||
                    ownerMethod.MethodKind is not (MethodKind.Ordinary or MethodKind.LocalFunction) ||
                    ownerMethod.Parameters is not [var builder] || invocation.ArgumentList.Arguments is not [var argument] ||
                    !SymbolEqualityComparer.Default.Equals(semantic.GetSymbolInfo(argument.Expression, facts.Cancellation).Symbol, builder)) continue;
                // Follow only same-type helpers that forward the original builder unchanged.
                if (method.Parameters.Length == 1 && !method.IsGenericMethod && !method.IsVirtual && !method.IsAbstract &&
                    !method.IsOverride && SymbolEqualityComparer.Default.Equals(method.ContainingType, ownerMethod.ContainingType))
                {
                    if (!callers.TryGetValue(method, out var parents)) callers.Add(method, parents = new(SymbolEqualityComparer.Default));
                    parents.Add(ownerMethod);
                }
                if (invocation.Expression is MemberAccessExpressionSyntax access &&
                    semantic.GetTypeInfo(access.Expression, facts.Cancellation).Type is INamedTypeSymbol configuration &&
                    configuration.TypeKind != TypeKind.Interface && IsConfigureImplementation(configuration, method))
                    applications.Add((configuration, ownerMethod));
            }
        }
        foreach (var (configuration, method) in applications)
        {
            var pending = new Stack<IMethodSymbol>();
            var visited = new HashSet<IMethodSymbol>(SymbolEqualityComparer.Default);
            pending.Push(method);
            while (pending.TryPop(out var current))
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (!visited.Add(current)) continue;
                if (callers.TryGetValue(current, out var parents))
                    foreach (var parent in parents) pending.Push(parent);
                // Uninvoked helpers have no path back to the effective Configure implementation.
                if (current.ContainingType is not { } owner || !IsConfigureImplementation(owner, current)) continue;
                if (!result.Owners.TryGetValue(configuration, out var owners))
                    result.Owners.Add(configuration, owners = new(SymbolEqualityComparer.Default));
                owners.Add(owner);
            }
        }
        return result;

        bool IsConfigureImplementation(INamedTypeSymbol type, IMethodSymbol method) =>
            ConventionSymbols.Bindings(type, facts.Symbols.Configuration, facts.Cancellation)
                .SelectMany(binding => binding.GetMembers("Configure"))
                .Any(member => SymbolEqualityComparer.Default.Equals(type.FindImplementationForInterfaceMember(member)?.OriginalDefinition,
                    method.OriginalDefinition));
    }

    private static bool HasPossibleApplication(ConventionFacts facts, INamedTypeSymbol configuration, INamedTypeSymbol context,
        ContextModel evidence, ConfigurationUses uses)
    {
        var pending = new Stack<INamedTypeSymbol>();
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        pending.Push(configuration);
        while (pending.TryPop(out var current))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!visited.Add(current)) continue;
            if (!uses.Owners.TryGetValue(current, out var owners)) continue;
            foreach (var owner in owners)
            {
                if (facts.Symbols.IsContext(owner, facts.Cancellation))
                {
                    if (SymbolEqualityComparer.Default.Equals(owner, context)) return true;
                    continue;
                }
                if (evidence.Applied.Contains(owner) || DefaultDiscoverable(facts, owner, context)) return true;
                pending.Push(owner);
            }
        }
        return false;
    }

    private static void CheckService(ConventionFacts facts, INamedTypeSymbol service)
    {
        foreach (var binding in ConventionSymbols.Bindings(service, facts.Symbols.ServiceContract, facts.Cancellation))
        {
            if (binding.TypeArguments is not [INamedTypeSymbol entity]) continue;
            var contract = binding.GetMembers("Repository").OfType<IPropertySymbol>().SingleOrDefault();
            if (contract is null || service.FindImplementationForInterfaceMember(contract) is not IPropertySymbol property) continue;
            foreach (var reference in property.DeclaringSyntaxReferences)
            {
                if (!facts.Compilation.ContainsSyntaxTree(reference.SyntaxTree)) continue;
                if (reference.GetSyntax(facts.Cancellation) is not PropertyDeclarationSyntax declaration) continue;
                var expression = declaration.ExpressionBody?.Expression ?? declaration.Initializer?.Value;
                if (expression is null) continue;
                var semantic = facts.Compilation.GetSemanticModel(expression.SyntaxTree);
                if (semantic.GetDiagnostics(declaration.Span, facts.Cancellation)
                    .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)) continue;
                while (expression is CastExpressionSyntax cast) expression = cast.Expression;
                if (semantic.GetTypeInfo(expression, facts.Cancellation).Type is not INamedTypeSymbol actual) continue;
                var repositories = ConventionSymbols.Bindings(actual, facts.Symbols.RepositoryContract, facts.Cancellation).ToArray();
                if (repositories.Length == 0 || repositories.Any(repository => repository.TypeArguments.Length == 1 &&
                        SymbolEqualityComparer.Default.Equals(repository.TypeArguments[0], entity))) continue;
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Service,
                    declaration.Identifier.GetLocation(), Properties(service, entity: entity), service.Name, actual.ToDisplayString()));
            }
        }
    }

    private static void CheckMigrations(ConventionFacts facts)
    {
        foreach (var artifact in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!ConventionSymbols.Is(artifact, facts.Symbols.Migration, facts.Cancellation) &&
                !ConventionSymbols.Is(artifact, facts.Symbols.Snapshot, facts.Cancellation)) continue;
            foreach (var attribute in artifact.GetAttributes())
            {
                if (!ConventionSymbols.Same(attribute.AttributeClass, facts.Symbols.ContextAttribute) ||
                    attribute.ConstructorArguments is not [{ Value: INamedTypeSymbol context }]) continue;
                var input = facts.ContextInput(context);
                if (input is null || facts.HasInvalidContext(context)) continue;
                var path = ConventionProject.NormalizeSeparators(input.Get("Migrations"));
                if (!ConventionProject.IsLogicalPath(path)) continue;
                var assembly = input.Get("MigrationsAssembly");
                if (assembly.Length == 0) assembly = context.ContainingAssembly.Name;
                // A separate assembly's namespace belongs to that project, not the context project.
                var role = facts.Role(artifact);
                var prefix = ConventionProject.NormalizeSeparators(role.PathPrefix);
                string? namespacePath = prefix.Length == 0 ? path : path == prefix ? "" :
                    path.StartsWith(prefix + "/", StringComparison.Ordinal) ? path[(prefix.Length + 1)..] : null;
                var namespaceConflict = false;
                if (role.Namespace.Length > 0 && namespacePath is not null)
                {
                    var expectedNamespace = role.Namespace + (namespacePath.Length == 0 ? "" : "." + namespacePath.Replace('/', '.'));
                    namespaceConflict = !ConventionProject.IsNamespaceWithin(artifact.ContainingNamespace.ToDisplayString(), expectedNamespace);
                }
                var locations = artifact.Locations.Where(location => location.IsInSource).ToArray();
                var pathConflict = locations.Any(location => facts.LogicalPath(location.SourceTree!) is { } logical &&
                    !facts.HasInvalidPath(location.SourceTree!) && !ConventionProject.NormalizeSeparators(logical).StartsWith(path + "/", StringComparison.Ordinal));
                if (assembly == artifact.ContainingAssembly.Name && !namespaceConflict && !pathConflict) continue;
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Migration, input.Location,
                    additionalLocations: locations, properties: Properties(artifact, context),
                    messageArgs: [artifact.Name, context.Name, assembly + ":" + path]));
            }
        }
    }

    private static ImmutableDictionary<string, string?> Properties(INamedTypeSymbol target,
        INamedTypeSymbol? context = null, INamedTypeSymbol? entity = null)
    {
        var values = ImmutableDictionary<string, string?>.Empty.Add("Target", target.GetDocumentationCommentId());
        if (context is not null) values = values.Add("Context", ConventionSymbols.MetadataName(context));
        if (entity is not null) values = values.Add("Entity", ConventionSymbols.MetadataName(entity));
        return values;
    }

    private sealed class ContextModel
    {
        internal HashSet<INamedTypeSymbol> Included { get; } = new(SymbolEqualityComparer.Default);
        internal HashSet<INamedTypeSymbol> Candidates { get; } = new(SymbolEqualityComparer.Default);
        internal HashSet<INamedTypeSymbol> Excluded { get; } = new(SymbolEqualityComparer.Default);
        internal HashSet<INamedTypeSymbol> Ignored { get; } = new(SymbolEqualityComparer.Default);
        internal HashSet<INamedTypeSymbol> Applied { get; } = new(SymbolEqualityComparer.Default);
        internal bool DefaultScan { get; set; }
    }

    private sealed class ConfigurationUses
    {
        internal Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>> Owners { get; } = new(SymbolEqualityComparer.Default);
    }
}
