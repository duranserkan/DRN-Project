namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static partial class ConventionPersistenceAnalysis
{
    internal static ContextModel GetModel(ConventionFacts facts, INamedTypeSymbol context)
    {
        facts.Cancellation.ThrowIfCancellationRequested();
        if (facts.ContextModels.TryGetValue(context, out var cached)) return cached;
        var model = ReadModel(facts, context);
        facts.Cancellation.ThrowIfCancellationRequested();
        facts.ContextModels.Add(context, model);
        return model;
    }

    internal static bool DefaultDiscoverable(ConventionFacts facts, INamedTypeSymbol configuration, INamedTypeSymbol context) =>
        ConventionProject.IsNamespaceWithin(configuration.ContainingNamespace.ToDisplayString(), context.ContainingNamespace.ToDisplayString()) &&
        SymbolEqualityComparer.Default.Equals(configuration.ContainingAssembly, context.ContainingAssembly) &&
        !configuration.IsAbstract && !facts.Symbols.IsOpen(configuration, facts.Cancellation) &&
        configuration.InstanceConstructors.Any(constructor => constructor.Parameters.Length == 0);

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
        var semantic = facts.SemanticModel(body.SyntaxTree);

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
        else if (method.Name == "Entity" && method.Parameters.IsEmpty && method.TypeArguments is [INamedTypeSymbol included])
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
        var semantic = facts.SemanticModel(declaration.SyntaxTree);
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

    internal sealed class ContextModel
    {
        internal HashSet<INamedTypeSymbol> Included { get; } = new(SymbolEqualityComparer.Default);
        internal HashSet<INamedTypeSymbol> Candidates { get; } = new(SymbolEqualityComparer.Default);
        internal HashSet<INamedTypeSymbol> Excluded { get; } = new(SymbolEqualityComparer.Default);
        internal HashSet<INamedTypeSymbol> Ignored { get; } = new(SymbolEqualityComparer.Default);
        internal Dictionary<INamedTypeSymbol, HashSet<INamedTypeSymbol>> Applied { get; } = new(SymbolEqualityComparer.Default);
        internal bool DefaultScan { get; set; }
    }
}
