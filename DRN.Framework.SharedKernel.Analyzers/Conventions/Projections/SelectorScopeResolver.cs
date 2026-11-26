using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class SelectorScopeResolver
{
    internal static SelectorScope? SeedEnclosingSelectorScopes(
        IOperation root, ConventionFacts facts,
        Dictionary<SelectorScopeKey, SelectorFlowResult>? cache,
        Dictionary<IBlockOperation, SelectorFlowResult>? bodyFlows)
    {
        List<IAnonymousFunctionOperation>? lambdas = null;
        IInvocationOperation? outermostSelect = null;
        for (var current = root.Parent; current is not null; current = current.Parent)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (current is IAnonymousFunctionOperation lambda &&
                LinqSequenceFacts.FindEnclosingSelectCall(lambda, facts.Symbols) is { } select)
            {
                (lambdas ??= []).Add(lambda);
                outermostSelect = select;
            }
        }
        if (lambdas is null) return null;
        var scope = ResolveEnclosingBodyScope(outermostSelect!, facts, bodyFlows);
        for (var i = lambdas.Count - 1; i >= 0; i--)
            scope = GetOrCollect(lambdas[i], scope, facts, cache).Scope;
        return scope;
    }

    internal static SelectorScope? ResolveEnclosingBodyScope(
        IInvocationOperation select, ConventionFacts facts,
        Dictionary<IBlockOperation, SelectorFlowResult>? bodyFlows)
    {
        if (!NeedsEnclosingScope(select, facts.Cancellation))
            return null;

        var bodies = CollectEnclosingFunctionBodies(select, facts.Cancellation);
        if (bodies.Count == 0) return null;

        SelectorScope? scope = null;
        for (var i = bodies.Count - 1; i >= 0; i--)
        {
            var (body, ownerLambda) = bodies[i];
            var parentScope = ownerLambda is not null ? scope?.ForChild(ownerLambda) : scope;
            var canCacheRootBodyFlow = parentScope is null && bodyFlows is not null;
            if (canCacheRootBodyFlow && bodyFlows!.TryGetValue(body, out var cached))
            {
                scope = cached.Scope;
                continue;
            }
            var collected = SelectorFlowCollector.Collect(body, parentScope, facts);
            if (canCacheRootBodyFlow)
                bodyFlows![body] = collected;
            scope = collected.Scope;
        }
        return scope;
    }

    internal static SelectorFlowResult GetOrCollect(
        IAnonymousFunctionOperation lambda, SelectorScope? parentScope,
        ConventionFacts facts, Dictionary<SelectorScopeKey, SelectorFlowResult>? cache)
    {
        var effectiveParent = parentScope?.ForChild(lambda);
        var key = new SelectorScopeKey(lambda, effectiveParent);
        if (cache is not null && cache.TryGetValue(key, out var cached))
            return cached;
        var collected = SelectorFlowCollector.Collect(lambda.Body, effectiveParent, facts);
        if (cache is not null) cache[key] = collected;
        return collected;
    }

    private static List<EnclosingFunctionBody> CollectEnclosingFunctionBodies(
        IInvocationOperation select, CancellationToken cancellation)
    {
        List<EnclosingFunctionBody> bodies = [];
        IBlockOperation? currentFunctionBody = null;
        for (var current = select.Parent; current is not null; current = current.Parent)
        {
            cancellation.ThrowIfCancellationRequested();
            if (current is IBlockOperation block)
            {
                currentFunctionBody = block;
                continue;
            }
            if (current is ILocalFunctionOperation && currentFunctionBody is not null)
            {
                bodies.Add(new(currentFunctionBody, OwnerLambda: null));
                currentFunctionBody = null;
                continue;
            }
            if (current is IAnonymousFunctionOperation outerLambda && currentFunctionBody is not null)
            {
                bodies.Add(new(currentFunctionBody, outerLambda));
                currentFunctionBody = null;
            }
        }
        if (currentFunctionBody is not null)
            bodies.Add(new(currentFunctionBody, OwnerLambda: null));
        return bodies;
    }

    private static bool NeedsEnclosingScope(IInvocationOperation select, CancellationToken cancellation)
    {
        if (LinqSequenceFacts.SelectLambda(select) is not { Syntax: var lambda }) return true;
        // A local in the source, including an inline out variable captured by the selector, belongs to the caller.
        foreach (var operation in select.Descendants())
        {
            cancellation.ThrowIfCancellationRequested();
            if (ReferencesExternalState(operation, select, lambda, cancellation))
                return true;
        }
        return false;
    }

    private static bool ReferencesExternalState(
        IOperation operation, IInvocationOperation select, SyntaxNode lambda, CancellationToken cancellation)
    {
        var referencesExternalLocal = operation is ILocalReferenceOperation local &&
            !SelectorFlowSession.IsDeclaredWithin(local.Local, lambda.SyntaxTree, lambda.Span);
        if (referencesExternalLocal) return true;

        var referencesExternalConditionalReceiver = operation is IConditionalAccessInstanceOperation placeholder &&
            RoslynOperationHelpers.ConditionalReceiver(placeholder, cancellation) is { } receiver &&
            !select.Syntax.Span.Contains(receiver.Syntax.Span);
        if (referencesExternalConditionalReceiver) return true;

        var referencesOuterLambdaParameter = operation is IParameterReferenceOperation { Parameter: var parameter } &&
            parameter.ContainingSymbol is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction } &&
            !SelectorFlowSession.IsDeclaredWithin(parameter, lambda.SyntaxTree, lambda.Span);
        return referencesOuterLambdaParameter;
    }
}
