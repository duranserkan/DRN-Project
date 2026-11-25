using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal static class UnstructuredFlowHelper
{
    internal static bool HasUnstructuredJump(IBlockOperation body, CancellationToken cancellation)
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

    internal static IOperation? LoopCondition(ILoopOperation loop, ConventionSymbols symbols)
    {
        if (loop is IForLoopOperation forLoop) return forLoop.Condition;
        if (loop is IWhileLoopOperation whileLoop) return whileLoop.Condition;
        if (loop is not IForEachLoopOperation forEach) return null;

        var source = RoslynOperationHelpers.UnwrapTransparent(forEach.Collection);
        // Mapping and materialization preserve whether the source is empty.
        while (source is IInvocationOperation call &&
               LinqSequenceFacts.IsLengthPreservingSequenceCall(call.TargetMethod, symbols))
            source = RoslynOperationHelpers.UnwrapTransparent(RoslynOperationHelpers.TryGetArgumentValueByOrdinal(call, 0));
        return source;
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

        var walk = new Stack<IOperation>();
        walk.Push(root);
        var found = false;
        while (walk.TryPop(out var current))
        {
            cancellation.ThrowIfCancellationRequested();
            if (current is IAnonymousFunctionOperation or ILocalFunctionOperation) continue;
            if (current is IReturnOperation)
            {
                found = true;
                break;
            }
            if (cache is not null && cache.TryGetValue(current, out var subResult))
            {
                if (subResult)
                {
                    found = true;
                    break;
                }
                continue;
            }
            foreach (var child in current.ChildOperations) walk.Push(child);
        }
        (cache ??= [])[root] = found;
        return found;
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

    private static bool CaseHasDefaultClause(ISwitchCaseOperation section)
    {
        foreach (var clause in section.Clauses)
        {
            if (clause is IDefaultCaseClauseOperation) return true;
        }
        return false;
    }
}
