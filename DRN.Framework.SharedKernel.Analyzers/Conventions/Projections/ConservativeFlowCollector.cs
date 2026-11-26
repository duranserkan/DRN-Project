using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal readonly record struct ConservativeWriteCollector(
    SyntaxTree BodyTree,
    TextSpan BodySpan,
    Dictionary<ILocalSymbol, List<LocalBinding>> OuterWrites,
    Dictionary<ILocalSymbol, List<LocalBinding>> InnerWrites)
{
    internal bool IsDeclaredInBody(ILocalSymbol local) =>
        SelectorFlowSession.IsDeclaredWithin(local, BodyTree, BodySpan);

    internal void AddBinding(ILocalSymbol local, LocalBinding binding)
    {
        var target = IsDeclaredInBody(local) ? InnerWrites : OuterWrites;
        if (!target.TryGetValue(local, out var list))
        {
            list = [];
            target[local] = list;
        }
        SelectorFlowEnvironment.AddUnique(list, binding);
    }

    internal void AddWriteTarget(IOperation target, IOperation value)
    {
        if (target is ILocalReferenceOperation targetLocal)
            AddBinding(targetLocal.Local, new(value));
        else if (SelectorFlowCollector.TryGetMutatedLocal(target) is { } mutated)
            AddBinding(mutated.Local, new(value, WriteTarget: target));
    }
}

internal static class ConservativeFlowCollector
{
    internal static SelectorFlowResult Collect(
        IBlockOperation body, SelectorScope? parentScope, CancellationToken cancellation)
    {
        var writes = new ConservativeWriteCollector(
            body.Syntax.SyntaxTree,
            body.Syntax.Span,
            new(SymbolEqualityComparer.Default),
            new(SymbolEqualityComparer.Default));
        var innerRefs = new List<ILocalReferenceOperation>();
        var walk = new Stack<IOperation>();
        walk.Push(body);
        while (walk.TryPop(out var current))
        {
            cancellation.ThrowIfCancellationRequested();
            if (current is ILocalReferenceOperation localRef && IsInnerReadReference(localRef, writes))
                innerRefs.Add(localRef);
            CollectOperation(current, writes);
            foreach (var child in current.ChildOperations)
                walk.Push(child);
        }

        var scope = BuildScope(innerRefs, writes.InnerWrites, parentScope);
        var outerExitValues = writes.OuterWrites.Count > 0 ? SelectorFlowEnvironment.ToImmutableMap(writes.OuterWrites) : null;
        return new([], ReturnConditions: null, scope, outerExitValues);
    }

    private static bool IsInnerReadReference(ILocalReferenceOperation localRef, ConservativeWriteCollector writes) =>
        !localRef.IsDeclaration &&
        !UnstructuredFlowHelper.IsOutArgumentTarget(localRef) &&
        writes.IsDeclaredInBody(localRef.Local);

    private static void CollectOperation(IOperation current, ConservativeWriteCollector writes)
    {
        switch (current)
        {
            case IVariableDeclaratorOperation declarator:
            {
                var init = declarator.Initializer?.Value ??
                    (declarator.Parent as IVariableDeclarationOperation)?.Initializer?.Value;
                if (init is not null)
                    writes.AddBinding(declarator.Symbol, new(init));
                return;
            }
            case IDeconstructionAssignmentOperation deconstruction:
                CollectDeconstruction(deconstruction.Target, deconstruction.Value, writes);
                return;
            case IAssignmentOperation assignment:
                writes.AddWriteTarget(assignment.Target, assignment.Value);
                return;
            case IInvocationOperation call:
                CollectInvocation(call, writes);
                return;
        }
    }

    private static void CollectInvocation(IInvocationOperation call, ConservativeWriteCollector writes)
    {
        foreach (var argument in call.Arguments)
        {
            if (argument.Parameter?.RefKind is not (RefKind.Out or RefKind.Ref)) continue;
            var target = argument.Value is IDeclarationExpressionOperation decl ? decl.Expression : argument.Value;
            writes.AddWriteTarget(target, call);
        }
        var mutatesReceiverCollection = call.Instance is not null &&
            call.Arguments.Length > 0 &&
            CollectionMutationFacts.CanStoreArgument(call.TargetMethod);
        if (mutatesReceiverCollection && SelectorFlowCollector.TryGetMutatedLocal(call.Instance!) is { } receiverLocal)
        {
            writes.AddBinding(receiverLocal.Local,
                new(call, Kind: LocalBindingKind.ReceiverMutation, WriteTarget: call.Instance));
        }
    }

    private static void CollectDeconstruction(
        IOperation target, IOperation value, ConservativeWriteCollector writes)
    {
        if (target is IDeclarationExpressionOperation declaration) target = declaration.Expression;
        if (target is ITupleOperation tuple)
        {
            for (var i = 0; i < tuple.Elements.Length; i++)
                CollectDeconstruction(tuple.Elements[i], value, writes);
            return;
        }
        writes.AddWriteTarget(target, value);
    }

    private static SelectorScope? BuildScope(
        List<ILocalReferenceOperation> innerRefs,
        Dictionary<ILocalSymbol, List<LocalBinding>> innerWrites,
        SelectorScope? parentScope)
    {
        if (innerWrites.Count == 0 || innerRefs.Count == 0) return null;
        var referenceValues = new Dictionary<ILocalReferenceOperation, ImmutableArray<LocalBinding>>();
        for (var i = 0; i < innerRefs.Count; i++)
        {
            var reference = innerRefs[i];
            if (innerWrites.TryGetValue(reference.Local, out var bindings))
                referenceValues[reference] = [.. bindings];
        }
        return referenceValues.Count > 0
            ? new SelectorScope(referenceValues, SelectorScope.EmptyCaptured, SelectorScope.EmptyLambdaCaptures, parentScope)
            : null;
    }
}
