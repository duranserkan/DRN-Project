using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class ReturnedSequenceMatcher
{
    internal static bool IsReturnedSequence(
        IOperation sequence, ConventionFacts facts, ref Dictionary<IBlockOperation, SelectorFlowResult>? bodyFlows)
    {
        var current = sequence;
        for (var parent = current.Parent; parent is not null; current = parent, parent = parent.Parent)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (parent is IReturnOperation)
                return IsMethodOrLocalFunctionReturn(parent);
            if (PreservesSequenceParent(current, parent, facts.Symbols, out var next))
            {
                parent = next;
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
        if (parent is IParenthesizedOperation or IConversionOperation { Conversion.IsUserDefined: false } or
            ICollectionExpressionOperation or IArrayInitializerOperation)
            return true;
        if (parent is IArrayCreationOperation array)
            return ReferenceEquals(child, array.Initializer);
        if (parent is IConditionalAccessOperation conditionalAccess)
            return ReferenceEquals(child, conditionalAccess.WhenNotNull);
        if (parent is ISpreadOperation spread)
            return !spread.ElementConversion.IsUserDefined;
        if (parent is ICoalesceOperation coalesce)
            return ReferenceEquals(child, coalesce.WhenNull) || !coalesce.ValueConversion.IsUserDefined;
        if (parent is IConditionalOperation conditional)
            return ReferenceEquals(child, conditional.WhenTrue) || ReferenceEquals(child, conditional.WhenFalse);
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
            PushReturnedSequencePredecessors(current, flow.Scope, facts.Symbols, pending);
        }
        return false;
    }

    private static void PushReturnedSequencePredecessors(
        IOperation current, SelectorScope? scope, ConventionSymbols symbols, Stack<IOperation> pending)
    {
        switch (current)
        {
            case IParenthesizedOperation parenthesized:
                pending.Push(parenthesized.Operand);
                return;
            case IConversionOperation { Conversion.IsUserDefined: false } conversion:
                pending.Push(conversion.Operand);
                return;
            case ISpreadOperation spread when !spread.ElementConversion.IsUserDefined:
                pending.Push(spread.Operand);
                return;
            case ICollectionExpressionOperation collection:
                foreach (var element in collection.Elements) pending.Push(element);
                return;
            case IArrayCreationOperation { Initializer: { } arrayInitializer }:
                pending.Push(arrayInitializer);
                return;
            case IArrayInitializerOperation arrayInitializer:
                foreach (var element in arrayInitializer.ElementValues) pending.Push(element);
                return;
            case IConditionalAccessOperation conditionalAccess:
                pending.Push(conditionalAccess.WhenNotNull);
                return;
            case IConditionalOperation conditional:
                pending.Push(conditional.WhenTrue);
                if (conditional.WhenFalse is { } whenFalse) pending.Push(whenFalse);
                return;
            case ICoalesceOperation coalesce:
                if (!coalesce.ValueConversion.IsUserDefined) pending.Push(coalesce.Value);
                pending.Push(coalesce.WhenNull);
                return;
            case ISwitchExpressionOperation switchExpression:
                foreach (var arm in switchExpression.Arms) pending.Push(arm.Value);
                return;
            case IInvocationOperation call when
                LinqSequenceFacts.TryGetPreservedSequenceArgumentCount(call.TargetMethod, symbols, out var count):
                var fallback = LinqSequenceFacts.FallbackElementTypeParameter(call.TargetMethod);
                foreach (var argument in call.Arguments)
                {
                    if (LinqSequenceFacts.PreservesSequenceArgument(argument, count, fallback))
                        pending.Push(argument.Value);
                }
                return;
            case ILocalReferenceOperation localRef when scope is not null &&
                scope.ReferenceValues.TryGetValue(localRef, out var bindings):
                for (var i = 0; i < bindings.Length; i++)
                {
                    var binding = bindings[i];
                    var isDirectLocalValue = binding.Kind == LocalBindingKind.Value &&
                        binding.WriteTarget is null &&
                        binding.TuplePath.IsDefaultOrEmpty;
                    if (isDirectLocalValue) pending.Push(binding.Value);
                }
                return;
        }
    }
}
