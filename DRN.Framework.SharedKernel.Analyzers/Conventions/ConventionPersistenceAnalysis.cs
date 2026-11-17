using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ConventionPersistenceAnalysis
{
    internal static void Analyze(ConventionFacts facts)
    {
        var checkedDbSets = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
        ContextModel Model(INamedTypeSymbol context) => GetModel(facts, context);

        foreach (var type in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            CheckDbSetAggregates(facts, type, checkedDbSets);
            AnalyzeConcreteType(facts, type, Model);
        }

        CheckConfigurations(facts, Model);
        CheckMigrations(facts);
    }

    private static void AnalyzeConcreteType(ConventionFacts facts, INamedTypeSymbol type, Func<INamedTypeSymbol, ContextModel> model)
    {
        if (type.IsAbstract || facts.Symbols.IsOpen(type, facts.Cancellation)) return;
        if (facts.Symbols.IsContext(type, facts.Cancellation) && facts.Role(type).Role == ConventionArchitecture.Infra)
            ReportContradictoryInclusion(facts, type, model(type));

        if (facts.Location(type) == Location.None) return;
        if (facts.Role(type).Role == ConventionArchitecture.Infra) CheckRepository(facts, type, model);
        if (facts.Role(type).Role == ConventionArchitecture.Application && !facts.Symbols.IsTransport(type, facts.Cancellation))
            CheckService(facts, type);
    }

    private static void ReportContradictoryInclusion(ConventionFacts facts, INamedTypeSymbol type, ContextModel model)
    {
        var location = facts.Location(type);
        if (location == Location.None) return;
        foreach (var entity in model.Included)
        {
            if (model.Excluded.Contains(entity))
                facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Persistence,
                    location, Properties(type, type, entity), entity.Name, type.Name, "contradictory explicit membership"));
        }
    }

    private static void CheckDbSetAggregates(ConventionFacts facts, INamedTypeSymbol context,
        HashSet<IPropertySymbol> checkedProperties)
    {
        if (!ConventionSymbols.Is(context, facts.Symbols.DrnContext, facts.Cancellation) || facts.Symbols.Aggregate is null) return;
        foreach (var type in ConventionSymbols.Bases(context, facts.Cancellation))
        {
            foreach (var member in type.GetMembers())
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (member is IPropertySymbol { IsStatic: false, Type: INamedTypeSymbol } property &&
                    !checkedProperties.Contains(property))
                    CheckPropertyDbSets(facts, context, property, checkedProperties);
            }
        }
    }

    private static void CheckPropertyDbSets(ConventionFacts facts, INamedTypeSymbol context, IPropertySymbol property,
        HashSet<IPropertySymbol> checkedProperties)
    {
        var propertyType = (INamedTypeSymbol)property.Type;
        foreach (var binding in facts.Symbols.Bindings(propertyType, facts.Symbols.DbSet, facts.Cancellation))
        {
            if (binding.TypeArguments is not [INamedTypeSymbol entity] || entity.TypeKind == TypeKind.Error ||
                facts.Symbols.IsOpen(entity, facts.Cancellation)) continue;
            if (facts.Symbols.IsKnown(entity, facts.Symbols.Aggregate, facts.Cancellation))
            {
                checkedProperties.Add(property);
                continue;
            }
            var location = facts.Location(property);
            if (location == Location.None) location = facts.Location(context);
            if (location == Location.None) continue;
            checkedProperties.Add(property);
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.DbSetAggregate, location,
                Properties(property, context, entity), property.Name, context.Name, entity.ToDisplayString()));
        }
    }

    internal static ContextModel GetModel(ConventionFacts facts, INamedTypeSymbol context)
    {
        facts.Cancellation.ThrowIfCancellationRequested();
        if (facts.ContextModels.TryGetValue(context, out var cached)) return cached;
        var model = ReadModel(facts, context);
        facts.Cancellation.ThrowIfCancellationRequested();
        facts.ContextModels.Add(context, model);
        return model;
    }

    private static ContextModel ReadModel(ConventionFacts facts, INamedTypeSymbol context)
    {
        var result = new ContextModel();
        ReadPersistenceAttributes(facts, context, result);
        ReadContextDbSetCandidates(facts, context, result);

        var modelContract = facts.Symbols.DbContext?.GetMembers("OnModelCreating").OfType<IMethodSymbol>()
            .SingleOrDefault(method => !method.IsStatic && method.Parameters.Length == 1 &&
                ConventionSymbols.Same(method.Parameters[0].Type, facts.Symbols.ModelBuilder));
        if (modelContract is null) return result;
        var implementation = ConventionSymbols.Bases(context, facts.Cancellation)
            .SelectMany(type => type.GetMembers("OnModelCreating").OfType<IMethodSymbol>())
            .FirstOrDefault(method => OverridesModelContract(method, modelContract, facts.Cancellation));
        if (implementation is null) return result;
        result.DefaultScan = HasDefaultScan(facts, implementation);
        var modelMethods = CollectOverriddenModelMethods(facts, implementation, result.DefaultScan);
        // Proven forwarding runs the base setup before each derived override's model calls.
        while (modelMethods.TryPop(out var method)) ReadModelCalls(facts, method, result);
        return result;
    }

    private static void ReadPersistenceAttributes(ConventionFacts facts, INamedTypeSymbol context, ContextModel result)
    {
        foreach (var attribute in context.GetAttributes())
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!ConventionSymbols.Same(attribute.AttributeClass, facts.Symbols.PersistenceAttribute)) continue;
            if (attribute.ConstructorArguments is not [{ Value: INamedTypeSymbol entity }] ||
                entity.TypeKind == TypeKind.Error || facts.Symbols.IsOpen(entity, facts.Cancellation)) continue;
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
    }

    private static void ReadContextDbSetCandidates(ConventionFacts facts, INamedTypeSymbol context, ContextModel result)
    {
        foreach (var type in ConventionSymbols.Bases(context, facts.Cancellation))
        {
            foreach (var property in type.GetMembers().OfType<IPropertySymbol>())
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (!property.IsStatic && property.Type is INamedTypeSymbol propertyType)
                    AddDbSetCandidates(facts, propertyType, result.Candidates);
            }
        }
    }

    private static void AddDbSetCandidates(ConventionFacts facts, INamedTypeSymbol propertyType, HashSet<INamedTypeSymbol> candidates)
    {
        foreach (var binding in facts.Symbols.Bindings(propertyType, facts.Symbols.DbSet, facts.Cancellation))
            if (binding.TypeArguments is [INamedTypeSymbol entity] && !facts.Symbols.IsOpen(entity, facts.Cancellation))
                candidates.Add(entity);
    }

    private static Stack<IMethodSymbol> CollectOverriddenModelMethods(ConventionFacts facts,
        IMethodSymbol implementation, bool defaultScan)
    {
        var modelMethods = new Stack<IMethodSymbol>();
        for (var current = implementation; current is not null; current = current.OverriddenMethod)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (ConventionSymbols.Same(current.ContainingType, facts.Symbols.DrnContext) ||
                ConventionSymbols.Same(current.ContainingType, facts.Symbols.DrnIdentityContext)) break;
            modelMethods.Push(current);
            if (!defaultScan) break;
        }
        return modelMethods;
    }

    private static void ReadModelCalls(ConventionFacts facts, IMethodSymbol implementation, ContextModel result)
    {
        if (implementation.DeclaringSyntaxReferences.Length != 1 ||
            !facts.Compilation.ContainsSyntaxTree(implementation.DeclaringSyntaxReferences[0].SyntaxTree)) return;
        var declaration = implementation.DeclaringSyntaxReferences[0].GetSyntax(facts.Cancellation) as MethodDeclarationSyntax;
        if (declaration?.Body is not { } body) return;
        if (body.Statements.Any(statement => statement is not ExpressionStatementSyntax)) return;
        var semantic = facts.Compilation.GetSemanticModel(body.SyntaxTree);

        // Only unconditional, top-level calls establish exclusion or application. Helpers remain unknown.
        foreach (var expression in body.Statements.OfType<ExpressionStatementSyntax>().Select(statement => statement.Expression))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            ApplyModelCall(facts, implementation, semantic, expression, result);
        }
    }

    private static void ApplyModelCall(ConventionFacts facts, IMethodSymbol implementation, SemanticModel semantic,
        ExpressionSyntax expression, ContextModel result)
    {
        if (result.DefaultScan && expression is InvocationExpressionSyntax
            { Expression: MemberAccessExpressionSyntax { Expression: BaseExpressionSyntax } }) return;
        if (expression is not InvocationExpressionSyntax invocation ||
            semantic.GetSymbolInfo(invocation, facts.Cancellation).Symbol is not IMethodSymbol method ||
            invocation.Expression is not MemberAccessExpressionSyntax access ||
            semantic.GetSymbolInfo(access.Expression, facts.Cancellation).Symbol is not IParameterSymbol receiver ||
            !SymbolEqualityComparer.Default.Equals(receiver, implementation.Parameters[0]) ||
            !ConventionSymbols.Same(method.ContainingType, facts.Symbols.ModelBuilder))
        {
            result.Ignored.Clear();
            result.Applied.Clear();
            return;
        }
        UpdateModelCallState(facts, semantic, invocation, method, result);
    }

    private static void UpdateModelCallState(ConventionFacts facts, SemanticModel semantic,
        InvocationExpressionSyntax invocation, IMethodSymbol method, ContextModel result)
    {
        if (method.Name == "Ignore" && method.TypeArguments is [INamedTypeSymbol ignored])
        {
            result.Ignored.Add(ignored);
            result.Applied.Remove(ignored);
        }
        else if (method.Name == "Entity" && method.TypeArguments is [INamedTypeSymbol included])
            result.Ignored.Remove(included);
        else
        {
            result.Ignored.Clear();
            if (method.Name != "ApplyConfiguration") result.Applied.Clear();
        }
        if (method.Name == "ApplyConfiguration" && method.TypeArguments is [INamedTypeSymbol selectedEntity] &&
            invocation.ArgumentList.Arguments.Count == 1 &&
            semantic.GetTypeInfo(invocation.ArgumentList.Arguments[0].Expression, facts.Cancellation).Type is INamedTypeSymbol configuration)
        {
            if (!result.Applied.TryGetValue(selectedEntity, out var configurations))
            {
                configurations = new(SymbolEqualityComparer.Default);
                result.Applied.Add(selectedEntity, configurations);
            }
            configurations.Add(configuration);
        }
    }

    private static bool HasDefaultScan(ConventionFacts facts, IMethodSymbol implementation)
    {
        List<IMethodSymbol>? pending = null;
        var result = false;
        for (var current = implementation; current is not null; current = current.OverriddenMethod)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (facts.DefaultModelScans.TryGetValue(current, out result)) break;
            (pending ??= []).Add(current);
            if (ConventionSymbols.Same(current.ContainingType, facts.Symbols.DrnContext) ||
                ConventionSymbols.Same(current.ContainingType, facts.Symbols.DrnIdentityContext))
            {
                result = true;
                break;
            }
            if (current.DeclaringSyntaxReferences is not [var reference] ||
                !facts.Compilation.ContainsSyntaxTree(reference.SyntaxTree) ||
                reference.GetSyntax(facts.Cancellation) is not MethodDeclarationSyntax declaration) break;
            if (!ForwardsDefaultScan(facts, current, declaration)) break;
        }
        facts.Cancellation.ThrowIfCancellationRequested();
        if (pending is not null)
            foreach (var method in pending)
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                facts.DefaultModelScans.Add(method, result);
            }
        return result;
    }

    private static bool ForwardsDefaultScan(ConventionFacts facts, IMethodSymbol method, MethodDeclarationSyntax declaration)
    {
        if (method.Parameters is not [var parameter]) return false;
        var semantic = facts.Compilation.GetSemanticModel(declaration.SyntaxTree);
        var forwarded = false;
        if (declaration.ExpressionBody is { } expressionBody)
            return TryAcceptDefaultScanCall(facts, method, parameter, semantic, expressionBody.Expression, ref forwarded) && forwarded;
        if (declaration.Body is not { } body) return false;
        foreach (var statement in body.Statements)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (statement is not ExpressionStatementSyntax expression ||
                !TryAcceptDefaultScanCall(facts, method, parameter, semantic, expression.Expression, ref forwarded)) return false;
        }
        return forwarded;
    }

    private static bool TryAcceptDefaultScanCall(ConventionFacts facts, IMethodSymbol method, IParameterSymbol parameter,
        SemanticModel semantic, ExpressionSyntax expression, ref bool forwarded)
    {
        facts.Cancellation.ThrowIfCancellationRequested();
        if (expression is not InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax access } invocation ||
            semantic.GetSymbolInfo(invocation, facts.Cancellation).Symbol is not IMethodSymbol called) return false;
        if (access.Expression is BaseExpressionSyntax && invocation.ArgumentList.Arguments is [var argument] &&
            SymbolEqualityComparer.Default.Equals(called, method.OverriddenMethod) &&
            SymbolEqualityComparer.Default.Equals(semantic.GetSymbolInfo(argument.Expression, facts.Cancellation).Symbol, parameter))
        {
            if (forwarded) return false;
            forwarded = true;
            return true;
        }
        var supportedModelCall = called.TypeArguments.Length == 1 &&
            (called.Name == "ApplyConfiguration" ? invocation.ArgumentList.Arguments.Count == 1 :
                called.Name is ("Ignore" or "Entity") && invocation.ArgumentList.Arguments.Count == 0);
        return forwarded && supportedModelCall &&
               ConventionSymbols.Same(called.ContainingType, facts.Symbols.ModelBuilder) &&
               SymbolEqualityComparer.Default.Equals(semantic.GetSymbolInfo(access.Expression, facts.Cancellation).Symbol, parameter);
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
        var bindings = facts.Symbols.Bindings(repository, facts.Symbols.Repository, facts.Cancellation);
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
                facts.HasInvalidContext(context) || facts.Symbols.IsOpen(binding, facts.Cancellation)) continue;
            var membership = model(context);
            membership.Candidates.Add(entity);
            if (!membership.Excluded.Contains(entity) && !membership.Ignored.Contains(entity)) continue;
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Repository, facts.Location(repository), Properties(repository, context, entity),
                repository.Name, entity.Name, context.Name + " explicitly excludes the entity"));
        }
    }

    private static void CheckConfigurations(ConventionFacts facts, Func<INamedTypeSymbol, ContextModel> model)
    {
        var uses = new Lazy<ConfigurationUses>(() => ReadConfigurationUses(facts));
        var declared = CheckExplicitConfigurationInputs(facts, model, uses);
        var (contextsByNamespace, contextsByEntity) = IndexInfraContexts(facts, model);
        CheckImplicitConfigurations(facts, model, uses, declared, contextsByNamespace, contextsByEntity);
    }

    private static HashSet<INamedTypeSymbol> CheckExplicitConfigurationInputs(ConventionFacts facts,
        Func<INamedTypeSymbol, ContextModel> model, Lazy<ConfigurationUses> uses)
    {
        var seen = new HashSet<(string Configuration, string Context)>();
        var declared = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var input in facts.Records.Where(input => input.Kind == ConventionInputSyntax.ConfigurationKind))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            var contextName = input.Get(ConventionInputSyntax.ContextKind);
            if (!seen.Add((input.Identity, contextName))) continue;
            var configuration = facts.Compilation.GetTypeByMetadataName(input.Identity);
            var context = facts.Compilation.GetTypeByMetadataName(contextName);
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
        return declared;
    }

    private static (Dictionary<string, List<INamedTypeSymbol>> ByNamespace,
        Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>> ByEntity) IndexInfraContexts(
        ConventionFacts facts, Func<INamedTypeSymbol, ContextModel> model)
    {
        var contextsByNamespace = new Dictionary<string, List<INamedTypeSymbol>>(StringComparer.Ordinal);
        var contextsByEntity = new Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>>(SymbolEqualityComparer.Default);
        foreach (var context in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (context.IsAbstract || facts.Symbols.IsOpen(context, facts.Cancellation) ||
                !facts.Symbols.IsContext(context, facts.Cancellation) || facts.HasInvalidContext(context) ||
                facts.Role(context).Role != ConventionArchitecture.Infra) continue;
            var ns = context.ContainingNamespace.ToDisplayString();
            if (!contextsByNamespace.TryGetValue(ns, out var area))
            {
                area = [];
                contextsByNamespace.Add(ns, area);
            }
            area.Add(context);
            IndexContextEntities(context, model(context), contextsByEntity);
        }
        return (contextsByNamespace, contextsByEntity);
    }

    private static void IndexContextEntities(INamedTypeSymbol context, ContextModel evidence,
        Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>> contextsByEntity)
    {
        foreach (var entity in evidence.Candidates)
        {
            if (evidence.Excluded.Contains(entity) || evidence.Ignored.Contains(entity)) continue;
            if (!contextsByEntity.TryGetValue(entity, out var owners))
            {
                owners = new(SymbolEqualityComparer.Default);
                contextsByEntity.Add(entity, owners);
            }
            owners.Add(context);
        }
    }

    private static void CheckImplicitConfigurations(ConventionFacts facts, Func<INamedTypeSymbol, ContextModel> model,
        Lazy<ConfigurationUses> uses, HashSet<INamedTypeSymbol> declared,
        Dictionary<string, List<INamedTypeSymbol>> contextsByNamespace,
        Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>> contextsByEntity)
    {
        foreach (var configuration in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (declared.Contains(configuration) || configuration.IsAbstract || facts.Symbols.IsOpen(configuration, facts.Cancellation) ||
                facts.Role(configuration).Role != ConventionArchitecture.Infra || facts.Location(configuration) == Location.None ||
                !facts.Symbols.IsKnown(configuration, facts.Symbols.Configuration, facts.Cancellation)) continue;
            if (FindNearestAreaContexts(configuration, contextsByNamespace) is { } nearest)
            {
                // Same-area contexts intentionally share the scan. Nested CQRS areas select their nearest context area.
                foreach (var context in nearest)
                    CheckConfiguration(facts, configuration, context, model(context), uses, facts.Location(configuration));
                continue;
            }
            if (FindSingleSubdomainContext(facts, configuration, contextsByEntity) is { } single)
                CheckConfiguration(facts, configuration, single, model(single), uses, facts.Location(configuration));
        }
    }

    private static List<INamedTypeSymbol>? FindNearestAreaContexts(INamedTypeSymbol configuration,
        Dictionary<string, List<INamedTypeSymbol>> contextsByNamespace)
    {
        for (var ns = configuration.ContainingNamespace; !ns.IsGlobalNamespace; ns = ns.ContainingNamespace)
            if (contextsByNamespace.TryGetValue(ns.ToDisplayString(), out var nearest)) return nearest;
        return null;
    }

    private static INamedTypeSymbol? FindSingleSubdomainContext(ConventionFacts facts, INamedTypeSymbol configuration,
        Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>> contextsByEntity)
    {
        // Outside a known scan area, ordinary Configurations placement plus a unique positive membership
        // supplies intent. Shared entities in multiple areas need an explicit association instead.
        if (!configuration.ContainingNamespace.ToDisplayString().Split('.').Contains("Configurations", StringComparer.Ordinal))
            return null;
        var owner = facts.Owner(configuration);
        if (owner is null) return null;
        var candidates = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        foreach (var binding in facts.Symbols.Bindings(configuration, facts.Symbols.Configuration, facts.Cancellation))
        {
            if (binding.TypeArguments is not [INamedTypeSymbol entity] ||
                !contextsByEntity.TryGetValue(entity, out var contexts)) continue;
            foreach (var context in contexts)
            {
                if (SymbolEqualityComparer.Default.Equals(context.ContainingAssembly, configuration.ContainingAssembly) &&
                    facts.Owner(context)?.Subdomain == owner.Subdomain) candidates.Add(context);
            }
        }
        return candidates.Count == 1 ? candidates.Single() : null;
    }

    private static void CheckConfiguration(ConventionFacts facts, INamedTypeSymbol configuration, INamedTypeSymbol context,
        ContextModel evidence, Lazy<ConfigurationUses> uses, Location fallback)
    {
        if (facts.HasInvalidContext(context) || facts.Role(context).Role != ConventionArchitecture.Infra || !evidence.DefaultScan ||
            DefaultDiscoverable(facts, configuration, context)) return;
        foreach (var binding in facts.Symbols.Bindings(configuration, facts.Symbols.Configuration, facts.Cancellation)
                     .OrderBy(binding => ConventionSymbols.MetadataName(binding.TypeArguments[0] as INamedTypeSymbol ?? configuration), StringComparer.Ordinal))
        {
            if (binding.TypeArguments is not [INamedTypeSymbol entity] || evidence.Excluded.Contains(entity) || evidence.Ignored.Contains(entity)) continue;
            if (IsEntityExplicitlyConfigured(facts, configuration, entity, evidence)) continue;
            if (HasPossibleApplication(facts, configuration, context, evidence, uses.Value)) return;
            var location = facts.Location(configuration);
            facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Configuration,
                location == Location.None ? fallback : location, Properties(configuration, context, entity), configuration.Name, entity.Name, context.Name));
            break;
        }
    }

    private static bool IsEntityExplicitlyConfigured(ConventionFacts facts, INamedTypeSymbol configuration,
        INamedTypeSymbol entity, ContextModel evidence)
    {
        if (evidence.Applied.TryGetValue(entity, out var applied) && applied.Contains(configuration)) return true;
        return entity.GetAttributes().Any(attribute =>
            ConventionSymbols.Same(attribute.AttributeClass, facts.Symbols.ConfigurationAttribute) &&
            attribute.ConstructorArguments is [{ Value: INamedTypeSymbol attributed }] &&
            SymbolEqualityComparer.Default.Equals(attributed, configuration));
    }

    internal static bool DefaultDiscoverable(ConventionFacts facts, INamedTypeSymbol configuration, INamedTypeSymbol context) =>
        ConventionProject.IsNamespaceWithin(configuration.ContainingNamespace.ToDisplayString(), context.ContainingNamespace.ToDisplayString()) &&
        SymbolEqualityComparer.Default.Equals(configuration.ContainingAssembly, context.ContainingAssembly) &&
        !configuration.IsAbstract && !facts.Symbols.IsOpen(configuration, facts.Cancellation) &&
        configuration.InstanceConstructors.Any(constructor => constructor.Parameters.Length == 0);

    private static ConfigurationUses ReadConfigurationUses(ConventionFacts facts)
    {
        var result = new ConfigurationUses();
        if (facts.Symbols.Configuration is null) return result;
        var callers = new Dictionary<IMethodSymbol, HashSet<IMethodSymbol>>(SymbolEqualityComparer.Default);
        var applications = new List<(INamedTypeSymbol Configuration, IMethodSymbol Method)>();
        CollectConfigurationCalls(facts, callers, applications);
        PropagateConfigurationOwners(facts, callers, applications, result);
        return result;
    }

    private static void CollectConfigurationCalls(ConventionFacts facts,
        Dictionary<IMethodSymbol, HashSet<IMethodSymbol>> callers,
        List<(INamedTypeSymbol Configuration, IMethodSymbol Method)> applications)
    {
        foreach (var tree in facts.Compilation.SyntaxTrees)
        {
            SemanticModel? semantic = null;
            foreach (var invocation in tree.GetRoot(facts.Cancellation).DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                facts.Cancellation.ThrowIfCancellationRequested();
                if (invocation.ArgumentList.Arguments is not [var argument]) continue;
                semantic ??= facts.Compilation.GetSemanticModel(tree);
                TryRecordConfigurationCall(facts, semantic, invocation, argument, callers, applications);
            }
        }
    }

    private static void TryRecordConfigurationCall(ConventionFacts facts, SemanticModel semantic,
        InvocationExpressionSyntax invocation, ArgumentSyntax argument,
        Dictionary<IMethodSymbol, HashSet<IMethodSymbol>> callers,
        List<(INamedTypeSymbol Configuration, IMethodSymbol Method)> applications)
    {
        if (semantic.GetSymbolInfo(invocation, facts.Cancellation).Symbol is not IMethodSymbol method ||
            semantic.GetEnclosingSymbol(invocation.SpanStart, facts.Cancellation) is not IMethodSymbol ownerMethod ||
            ownerMethod.MethodKind is not (MethodKind.Ordinary or MethodKind.LocalFunction) ||
            ownerMethod.Parameters is not [var builder] ||
            !SymbolEqualityComparer.Default.Equals(semantic.GetSymbolInfo(argument.Expression, facts.Cancellation).Symbol, builder))
            return;
        // Follow only same-type helpers that forward the original builder unchanged.
        if (method.Parameters.Length == 1 && !method.IsGenericMethod && !method.IsVirtual && !method.IsAbstract &&
            !method.IsOverride && SymbolEqualityComparer.Default.Equals(method.ContainingType, ownerMethod.ContainingType))
        {
            if (!callers.TryGetValue(method, out var parents))
            {
                parents = new(SymbolEqualityComparer.Default);
                callers.Add(method, parents);
            }
            parents.Add(ownerMethod);
        }
        if (invocation.Expression is MemberAccessExpressionSyntax access &&
            semantic.GetTypeInfo(access.Expression, facts.Cancellation).Type is INamedTypeSymbol configuration &&
            configuration.TypeKind != TypeKind.Interface && IsConfigureImplementation(facts, configuration, method))
            applications.Add((configuration, ownerMethod));
    }

    private static void PropagateConfigurationOwners(ConventionFacts facts,
        Dictionary<IMethodSymbol, HashSet<IMethodSymbol>> callers,
        List<(INamedTypeSymbol Configuration, IMethodSymbol Method)> applications,
        ConfigurationUses result)
    {
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
                if (current.ContainingType is not { } owner || !IsConfigureImplementation(facts, owner, current)) continue;
                if (!result.Owners.TryGetValue(configuration, out var owners))
                {
                    owners = new(SymbolEqualityComparer.Default);
                    result.Owners.Add(configuration, owners);
                }
                owners.Add(owner);
            }
        }
    }

    private static bool IsConfigureImplementation(ConventionFacts facts, INamedTypeSymbol type, IMethodSymbol method) =>
        facts.Symbols.Bindings(type, facts.Symbols.Configuration, facts.Cancellation)
            .SelectMany(binding => binding.GetMembers("Configure"))
            .Any(member => SymbolEqualityComparer.Default.Equals(type.FindImplementationForInterfaceMember(member)?.OriginalDefinition,
                method.OriginalDefinition));

    private static bool HasPossibleApplication(ConventionFacts facts, INamedTypeSymbol configuration, INamedTypeSymbol context,
        ContextModel evidence, ConfigurationUses uses)
    {
        var pending = new Stack<INamedTypeSymbol>();
        var visited = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);
        pending.Push(configuration);
        while (pending.TryPop(out var current))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!visited.Add(current) || !uses.Owners.TryGetValue(current, out var owners)) continue;
            if (TryVisitOwnerContexts(facts, context, evidence, owners, pending)) return true;
        }
        return false;
    }

    private static bool TryVisitOwnerContexts(ConventionFacts facts, INamedTypeSymbol context, ContextModel evidence,
        HashSet<INamedTypeSymbol> owners, Stack<INamedTypeSymbol> pending)
    {
        foreach (var owner in owners)
        {
            if (facts.Symbols.IsContext(owner, facts.Cancellation))
            {
                if (SymbolEqualityComparer.Default.Equals(owner, context)) return true;
                continue;
            }
            if (evidence.Applied.Values.Any(configurations => configurations.Contains(owner)) ||
                DefaultDiscoverable(facts, owner, context)) return true;
            pending.Push(owner);
        }
        return false;
    }

    private static void CheckService(ConventionFacts facts, INamedTypeSymbol service)
    {
        foreach (var binding in facts.Symbols.Bindings(service, facts.Symbols.ServiceContract, facts.Cancellation))
        {
            if (binding.TypeArguments is not [INamedTypeSymbol entity]) continue;
            var contract = binding.GetMembers("Repository").OfType<IPropertySymbol>().SingleOrDefault();
            if (contract is null || service.FindImplementationForInterfaceMember(contract) is not IPropertySymbol property) continue;
            foreach (var reference in property.DeclaringSyntaxReferences)
                CheckServiceRepositoryProperty(facts, service, entity, reference);
        }
    }

    private static void CheckServiceRepositoryProperty(ConventionFacts facts, INamedTypeSymbol service,
        INamedTypeSymbol entity, SyntaxReference reference)
    {
        if (!facts.Compilation.ContainsSyntaxTree(reference.SyntaxTree)) return;
        if (reference.GetSyntax(facts.Cancellation) is not PropertyDeclarationSyntax declaration) return;
        var expression = declaration.ExpressionBody?.Expression ?? declaration.Initializer?.Value;
        if (expression is null) return;
        var semantic = facts.Compilation.GetSemanticModel(expression.SyntaxTree);
        if (semantic.GetDiagnostics(declaration.Span, facts.Cancellation)
            .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)) return;
        while (expression is CastExpressionSyntax cast) expression = cast.Expression;
        if (semantic.GetTypeInfo(expression, facts.Cancellation).Type is not INamedTypeSymbol actual) return;
        var repositories = facts.Symbols.Bindings(actual, facts.Symbols.RepositoryContract, facts.Cancellation);
        if (repositories.Length == 0 || repositories.Any(repository => repository.TypeArguments.Length == 1 &&
                SymbolEqualityComparer.Default.Equals(repository.TypeArguments[0], entity))) return;
        facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Service,
            declaration.Identifier.GetLocation(), Properties(service, entity: entity), service.Name, actual.ToDisplayString()));
    }

    private static void CheckMigrations(ConventionFacts facts)
    {
        foreach (var artifact in facts.Types)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (!ConventionSymbols.Is(artifact, facts.Symbols.Migration, facts.Cancellation) &&
                !ConventionSymbols.Is(artifact, facts.Symbols.Snapshot, facts.Cancellation)) continue;
            foreach (var attribute in artifact.GetAttributes())
                CheckMigrationContextAttribute(facts, artifact, attribute);
        }
    }

    private static void CheckMigrationContextAttribute(ConventionFacts facts, INamedTypeSymbol artifact, AttributeData attribute)
    {
        if (!ConventionSymbols.Same(attribute.AttributeClass, facts.Symbols.ContextAttribute) ||
            attribute.ConstructorArguments is not [{ Value: INamedTypeSymbol context }]) return;
        var input = facts.ContextInput(context);
        if (input is null || facts.HasInvalidContext(context)) return;
        var path = ConventionProject.NormalizeSeparators(input.Get(ConventionInputSyntax.MigrationsField));
        if (!ConventionProject.IsLogicalPath(path)) return;
        var assembly = input.Get(ConventionInputSyntax.MigrationsAssemblyField);
        if (assembly.Length == 0) assembly = context.ContainingAssembly.Name;
        var namespaceConflict = HasMigrationNamespaceConflict(facts, artifact, path);
        var locations = artifact.Locations.Where(location => location.IsInSource).ToArray();
        var pathConflict = locations.Any(location => facts.LogicalPath(location.SourceTree!) is { } logical &&
            !facts.HasInvalidPath(location.SourceTree!) &&
            !ConventionProject.NormalizeSeparators(logical).StartsWith(path + "/", StringComparison.Ordinal));
        if (assembly == artifact.ContainingAssembly.Name && !namespaceConflict && !pathConflict) return;
        facts.Diagnostics.Add(Diagnostic.Create(ConventionDiagnosticDescriptors.Migration, input.Location,
            additionalLocations: locations, properties: Properties(artifact, context),
            messageArgs: [artifact.Name, context.Name, assembly + ":" + path]));
    }

    private static bool HasMigrationNamespaceConflict(ConventionFacts facts, INamedTypeSymbol artifact, string path)
    {
        // A separate assembly's namespace belongs to that project, not the context project.
        var role = facts.Role(artifact);
        var prefix = ConventionProject.NormalizeSeparators(role.PathPrefix);
        var namespacePath = ResolveMigrationNamespacePath(path, prefix);
        if (role.Namespace.Length == 0 || namespacePath is null) return false;
        var expectedNamespace = role.Namespace + (namespacePath.Length == 0 ? "" : "." + namespacePath.Replace('/', '.'));
        return !ConventionProject.IsNamespaceWithin(artifact.ContainingNamespace.ToDisplayString(), expectedNamespace);
    }

    private static string? ResolveMigrationNamespacePath(string path, string prefix)
    {
        if (prefix.Length == 0) return path;
        if (path == prefix) return "";
        return path.StartsWith(prefix + "/", StringComparison.Ordinal) ? path[(prefix.Length + 1)..] : null;
    }

    private static ImmutableDictionary<string, string?> Properties(ISymbol target,
        INamedTypeSymbol? context = null, INamedTypeSymbol? entity = null)
    {
        var values = ImmutableDictionary<string, string?>.Empty.Add(ConventionInputSyntax.TargetField, target.GetDocumentationCommentId());
        if (context is not null) values = values.Add(ConventionInputSyntax.ContextKind, ConventionSymbols.MetadataName(context));
        if (entity is not null) values = values.Add(ConventionInputSyntax.EntityField, ConventionSymbols.MetadataName(entity));
        return values;
    }

    internal sealed class ContextModel
    {
        internal HashSet<INamedTypeSymbol> Included { get; } = new(SymbolEqualityComparer.Default);
        internal HashSet<INamedTypeSymbol> Candidates { get; } = new(SymbolEqualityComparer.Default);
        internal HashSet<INamedTypeSymbol> Excluded { get; } = new(SymbolEqualityComparer.Default);
        internal HashSet<INamedTypeSymbol> Ignored { get; } = new(SymbolEqualityComparer.Default);
        internal Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>> Applied { get; } = new(SymbolEqualityComparer.Default);
        internal bool DefaultScan { get; set; }
    }

    private sealed class ConfigurationUses
    {
        internal Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>> Owners { get; } = new(SymbolEqualityComparer.Default);
    }
}
