using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ReturnedSequenceMatcher
{
    internal static bool IsReturnedSequence(
        IOperation sequence, ConventionFacts facts, ref Dictionary<IBlockOperation, SelectorFlowResult>? bodyFlows)
    {
        var current = sequence;
        while (current.Parent is { } parent)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (parent is IReturnOperation)
                return IsMethodOrLocalFunctionReturn(parent);
            if (RoslynOperationHelpers.ConditionalInstance(current, facts.Cancellation) is { } instance)
            {
                current = instance;
                continue;
            }
            if (PreservesSequenceParent(current, parent, facts.Symbols, out var next))
            {
                current = next;
                continue;
            }
            if (!IsLocalSequenceBinding(current, parent)) return false;
            if (FindEnclosingFunctionBody(parent, facts.Cancellation) is not { } body) return false;
            bodyFlows ??= [];
            if (!bodyFlows.TryGetValue(body, out var flow))
            {
                flow = SelectorFlowCollector.Collect(body, parentScope: null, facts);
                bodyFlows[body] = flow;
            }
            return ReturnsReachOperation(flow, sequence, facts);
        }
        return false;
    }

    private static bool IsMethodOrLocalFunctionReturn(IOperation returnOperation)
    {
        for (var owner = returnOperation.Parent; owner is not null; owner = owner.Parent)
        {
            if (owner is IAnonymousFunctionOperation) return false;
            if (owner is ILocalFunctionOperation) return true;
        }
        return true;
    }

    private static bool PreservesSequenceParent(
        IOperation child, IOperation parent, ConventionSymbols symbols, out IOperation next)
    {
        next = parent;
        if (parent is ICollectionExpressionOperation or IArrayInitializerOperation ||
            RoslynOperationHelpers.ForwardsOperandValue(parent, child))
            return true;
        if (parent is IArrayCreationOperation array)
            return ReferenceEquals(child, array.Initializer);
        if (parent is ISpreadOperation spread)
            return !spread.ElementConversion.IsUserDefined;
        if (parent is ISwitchExpressionArmOperation arm && ReferenceEquals(child, arm.Value) &&
            arm.Parent is ISwitchExpressionOperation switchExpression)
        {
            next = switchExpression;
            return true;
        }
        if (parent is IArgumentOperation { Parent: IInvocationOperation consumer } argument &&
            LinqSequenceFacts.TryGetPreservedSequenceArgumentCount(consumer.TargetMethod, symbols, out var count))
        {
            var fallback = LinqSequenceFacts.FallbackElementTypeParameter(consumer.TargetMethod);
            if (LinqSequenceFacts.PreservesSequenceArgument(argument, count, fallback))
            {
                next = consumer;
                return true;
            }
        }
        return false;
    }

    private static bool IsLocalSequenceBinding(IOperation value, IOperation parent) => parent switch
    {
        IVariableInitializerOperation { Parent: IVariableDeclaratorOperation or IVariableDeclarationOperation } => true,
        ISimpleAssignmentOperation { Target: ILocalReferenceOperation } simple => ReferenceEquals(simple.Value, value),
        ICoalesceAssignmentOperation { Target: ILocalReferenceOperation } coalesce => ReferenceEquals(coalesce.Value, value),
        _ => false
    };

    private static IBlockOperation? FindEnclosingFunctionBody(IOperation operation, CancellationToken cancellation)
    {
        IBlockOperation? body = null;
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            cancellation.ThrowIfCancellationRequested();
            if (current is IBlockOperation block)
            {
                body = block;
                continue;
            }
            if (current is IAnonymousFunctionOperation) return null;
            if (current is ILocalFunctionOperation) return body;
        }
        return body;
    }

    private static bool ReturnsReachOperation(SelectorFlowResult flow, IOperation target, ConventionFacts facts)
    {
        if (flow.Returns.Count == 0) return false;
        var pending = new Stack<IOperation>();
        var visited = new HashSet<IOperation>();
        for (var i = 0; i < flow.Returns.Count; i++) pending.Push(flow.Returns[i].Value);
        while (pending.TryPop(out var current))
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (ReferenceEquals(current, target)) return true;
            if (!visited.Add(current)) continue;
            PushReturnedSequencePredecessors(current, flow.Scope, facts, pending);
        }
        return false;
    }

    private static void PushReturnedSequencePredecessors(
        IOperation current, SelectorScope? scope, ConventionFacts facts, Stack<IOperation> pending)
    {
        if (current is IConditionalAccessInstanceOperation placeholder)
        {
            if (RoslynOperationHelpers.ConditionalReceiver(placeholder, facts.Cancellation) is { } receiver)
                pending.Push(receiver);
            return;
        }
        if (current is ILocalReferenceOperation local)
        {
            if (scope is null || !scope.ReferenceValues.TryGetValue(local, out var bindings)) return;
            foreach (var binding in bindings)
                if (binding.Kind == LocalBindingKind.Value && binding.WriteTarget is null && binding.TuplePath.IsDefaultOrEmpty)
                    pending.Push(binding.Value);
            return;
        }
        // Follow the same value edges in both directions. Arguments and switch arms wrap the value.
        foreach (var child in current.ChildOperations)
        {
            var value = child switch
            {
                IArgumentOperation argument => argument.Value,
                ISwitchExpressionArmOperation arm => arm.Value,
                _ => child
            };
            if (value.Parent is { } parent &&
                PreservesSequenceParent(value, parent, facts.Symbols, out var next) && ReferenceEquals(next, current))
                pending.Push(value);
        }
    }
}
