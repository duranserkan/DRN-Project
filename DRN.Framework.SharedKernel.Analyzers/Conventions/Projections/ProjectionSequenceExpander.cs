using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ProjectionSequenceExpander
{
    internal static void ExpandSequenceElement(
        LocalExpansionState state, IOperation sequence, ExpansionCursor cursor)
    {
        if (cursor.Depth >= ProjectionLimits.MaxExpansionDepth)
        {
            ProjectionLocalExpander.ExpandLocalValue(state, sequence, cursor);
            return;
        }
        var pending = new Stack<IOperation>();
        var seen = new HashSet<IOperation>();
        pending.Push(sequence);
        while (pending.TryPop(out var next))
        {
            state.Facts.Cancellation.ThrowIfCancellationRequested();
            var value = RoslynOperationHelpers.UnwrapTransparentNonNull(next);
            if (!seen.Add(value)) continue;
            if (TryExpandSequenceNode(state, value, cursor, pending)) continue;
            ProjectionLocalExpander.ExpandLocalValue(state, value, cursor.NextDepth());
        }
    }

    private static bool TryExpandSequenceNode(
        LocalExpansionState state, IOperation value, ExpansionCursor cursor, Stack<IOperation> pending)
    {
        if (value is ILocalReferenceOperation local && state.Entry.Locals is { } locals &&
            locals.TryGetValues(local, out var bindings))
        {
            ExpandSequenceLocal(state, bindings, cursor, pending);
            return true;
        }
        var selectCall = value as IInvocationOperation;
        var selectLambda = selectCall is not null && LinqSequenceFacts.IsLinqSelect(selectCall.TargetMethod, state.Facts.Symbols)
            ? LinqSequenceFacts.SelectLambda(selectCall)
            : null;
        if (selectLambda is not null)
        {
            ExpandSequenceSelect(state, selectCall!, selectLambda, cursor);
            return true;
        }
        if (value is IConditionalAccessOperation conditionalAccess)
        {
            ProjectionLocalExpander.ExpandConditionValue(state, conditionalAccess.Operation);
            pending.Push(conditionalAccess.WhenNotNull);
            return true;
        }
        if (value is IInvocationOperation wrapper &&
            LinqSequenceFacts.TryGetPreservedSequenceArgumentCount(wrapper.TargetMethod, state.Facts.Symbols, out var count))
        {
            ExpandPreservedSequenceWrapper(state, wrapper, count, pending);
            return true;
        }
        if (value is IArrayCreationOperation { Initializer: { } array })
        {
            ExpandSequenceArray(state, array, cursor);
            return true;
        }
        if (value is ICollectionExpressionOperation collection)
        {
            ExpandSequenceCollection(state, collection, cursor, pending);
            return true;
        }
        return false;
    }

    private static void ExpandSequenceLocal(
        LocalExpansionState state, ImmutableArray<LocalBinding> bindings, ExpansionCursor cursor, Stack<IOperation> pending)
    {
        for (var i = 0; i < bindings.Length; i++)
        {
            var binding = bindings[i];
            var isPlainLocalValue = binding.Kind == LocalBindingKind.Value &&
                binding.TuplePath.IsDefaultOrEmpty &&
                binding.WriteTarget is null;
            if (isPlainLocalValue) pending.Push(binding.Value);
            else ProjectionLocalExpander.ExpandBinding(state, binding, cursor.NextDepth(), localType: null);
        }
    }

    private static void ExpandSequenceSelect(
        LocalExpansionState state, IInvocationOperation call, IAnonymousFunctionOperation lambda, ExpansionCursor cursor)
    {
        var flow = SelectorScopeResolver.GetOrCollect(lambda, state.Entry.Locals, state.Facts, state.SelectorScopes);
        var elementState = state with { Entry = state.Entry with { Locals = flow.Scope } };
        if (flow.ReturnConditions is { } conditions)
        {
            for (var i = 0; i < conditions.Count; i++)
                ProjectionLocalExpander.ExpandConditionValue(elementState, conditions[i]);
        }
        for (var i = 0; i < flow.Returns.Count; i++)
        {
            var returned = flow.Returns[i];
            ExpandReturnedConditions(elementState, returned.Conditions);
            ProjectionLocalExpander.ExpandLocalValue(elementState, returned.Value, cursor.NextDepth());
        }
        if (RoslynOperationHelpers.TryGetArgumentValueByOrdinal(call, 0) is { } source)
            ProjectionLocalExpander.ExpandConditionValue(state, source);
    }

    private static void ExpandReturnedConditions(LocalExpansionState elementState, ImmutableArray<IOperation> conditions)
    {
        if (conditions.IsDefaultOrEmpty) return;
        for (var i = 0; i < conditions.Length; i++)
            ProjectionLocalExpander.ExpandConditionValue(elementState, conditions[i]);
    }

    private static void ExpandPreservedSequenceWrapper(
        LocalExpansionState state, IInvocationOperation wrapper, int count, Stack<IOperation> pending)
    {
        var fallback = LinqSequenceFacts.FallbackElementTypeParameter(wrapper.TargetMethod);
        foreach (var argument in wrapper.Arguments)
        {
            if (LinqSequenceFacts.PreservesSequenceArgument(argument, count, fallback))
                pending.Push(argument.Value);
            else
                ProjectionLocalExpander.ExpandConditionValue(state, argument.Value);
        }
    }

    private static void ExpandSequenceArray(
        LocalExpansionState state, IArrayInitializerOperation array, ExpansionCursor cursor)
    {
        foreach (var element in array.ElementValues)
            ProjectionLocalExpander.ExpandLocalValue(state, element, cursor.NextDepth());
    }

    private static void ExpandSequenceCollection(
        LocalExpansionState state, ICollectionExpressionOperation collection, ExpansionCursor cursor, Stack<IOperation> pending)
    {
        foreach (var element in collection.Elements)
        {
            if (element is ISpreadOperation spread) pending.Push(spread.Operand);
            else ProjectionLocalExpander.ExpandLocalValue(state, element, cursor.NextDepth());
        }
    }
}
