using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class UnstructuredFlowHelper
{
    internal static bool HasUnstructuredJump(IOperation body, CancellationToken cancellation)
    {
        var walk = new Stack<IOperation>();
        walk.Push(body);
        while (walk.TryPop(out var current))
        {
            cancellation.ThrowIfCancellationRequested();
            if (current is IAnonymousFunctionOperation or ILocalFunctionOperation) continue;
            if (current is ILabeledOperation or IBranchOperation { BranchKind: BranchKind.GoTo })
                return true;
            foreach (var child in current.ChildOperations) walk.Push(child);
        }
        return false;
    }

    internal static bool IsOutArgumentTarget(ILocalReferenceOperation localRef) =>
        localRef.Parent is IArgumentOperation { Parameter.RefKind: RefKind.Out } ||
        localRef.Parent is IDeclarationExpressionOperation { Parent: IArgumentOperation { Parameter.RefKind: RefKind.Out } };

    internal static ImmutableArray<IOperation> LoopConditions(
        ILoopOperation loop, ConventionSymbols symbols, CancellationToken cancellation)
    {
        if (loop is IForLoopOperation forLoop)
            return forLoop.Condition is { } condition ? [condition] : [];
        if (loop is IWhileLoopOperation whileLoop)
            return whileLoop.Condition is { } condition ? [condition] : [];
        if (loop is not IForEachLoopOperation forEach)
            return [];

        return LinqSequenceFacts.SequenceControllingIteration(forEach.Collection, symbols, cancellation);
    }

    internal static void CollectSwitchConditions(
        ISwitchOperation switchStatement, ISwitchCaseOperation? selected, List<IOperation> destination)
    {
        if (SwitchTestsValue(switchStatement))
            destination.Add(switchStatement.Value);
        // The default section also depends on patterns and guards appearing after it.
        var isDefault = selected is not null && CaseHasDefaultClause(selected);
        foreach (var @case in switchStatement.Cases)
        {
            foreach (var clause in @case.Clauses)
            {
                if (clause is not IPatternCaseClauseOperation patternClause) continue;
                destination.Add(patternClause.Pattern);
                if (patternClause.Guard is { } guard)
                    destination.Add(guard);
            }
            if (!isDefault && ReferenceEquals(@case, selected)) return;
        }
    }

    internal static void CollectSwitchExpressionConditions(
        ISwitchExpressionOperation switchExpression, ISwitchExpressionArmOperation? selected, List<IOperation> destination)
    {
        if (!ProjectionOriginAnalyzer.AreAllArmsBindingOnly(switchExpression.Arms))
            destination.Add(switchExpression.Value);
        foreach (var arm in switchExpression.Arms)
        {
            if (!ProjectionOriginAnalyzer.IsBindingOnlyPattern(arm.Pattern))
                destination.Add(arm.Pattern);
            if (arm.Guard is { } guard)
                destination.Add(guard);
            if (ReferenceEquals(arm, selected)) return;
        }
    }

    internal static bool CanDoWhileReachCondition(IWhileLoopOperation loop, CancellationToken cancellation) =>
        LoopBodyCompletionAnalyzer.CanDoWhileReachCondition(loop, cancellation);

    internal static bool SwitchContainsReturn(
        ISwitchOperation switchStatement, ref Dictionary<IOperation, bool>? cache, CancellationToken cancellation)
    {
        foreach (var @case in switchStatement.Cases)
        {
            foreach (var operation in @case.Body)
            {
                if (ContainsReturn(operation, ref cache, cancellation))
                    return true;
            }
        }
        return false;
    }

    internal static bool ContainsReturn(
        IOperation root, ref Dictionary<IOperation, bool>? cache, CancellationToken cancellation)
    {
        if (cache is not null && cache.TryGetValue(root, out var cached))
            return cached;

        var found = ContainsReturnCore(root, cache, cancellation);
        (cache ??= [])[root] = found;
        return found;
    }

    private static bool ContainsReturnCore(
        IOperation root, Dictionary<IOperation, bool>? cache, CancellationToken cancellation)
    {
        var walk = new Stack<IOperation>();
        walk.Push(root);
        while (walk.TryPop(out var current))
        {
            cancellation.ThrowIfCancellationRequested();
            if (IsNestedFunction(current)) continue;
            if (current is IReturnOperation) return true;
            if (TryGetCachedReturn(cache, current, out var subResult))
            {
                if (subResult) return true;
                continue;
            }
            foreach (var child in current.ChildOperations) walk.Push(child);
        }
        return false;
    }

    private static bool IsNestedFunction(IOperation operation) =>
        operation is IAnonymousFunctionOperation or ILocalFunctionOperation;

    private static bool TryGetCachedReturn(
        Dictionary<IOperation, bool>? cache, IOperation operation, out bool result)
    {
        if (cache is not null)
            return cache.TryGetValue(operation, out result);
        result = false;
        return false;
    }

    private static bool SwitchTestsValue(ISwitchOperation switchStatement)
    {
        foreach (var section in switchStatement.Cases)
        {
            foreach (var clause in section.Clauses)
            {
                if (clause is IDefaultCaseClauseOperation) continue;
                if (clause is IPatternCaseClauseOperation pattern &&
                    ProjectionOriginAnalyzer.IsBindingOnlyPattern(pattern.Pattern))
                    continue;
                return true;
            }
        }
        return false;
    }

    [SuppressMessage("SonarQube", "S3267", Justification = "Scanning the immutable clauses array directly avoids boxing ImmutableArray and allocating a predicate delegate.")]
    internal static bool CaseHasDefaultClause(ISwitchCaseOperation section)
    {
        foreach (var clause in section.Clauses)
        {
            if (clause is IDefaultCaseClauseOperation) return true;
        }
        return false;
    }
}
