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

    internal static bool CanDoWhileReachCondition(IWhileLoopOperation loop, CancellationToken cancellation)
    {
        var completion = AnalyzeLoopBodyCompletion(loop.Body, loop, switchExit: null, cancellation, depth: 0);
        return completion.CanFallThrough || completion.CanContinue;
    }

    private readonly record struct LoopCompletion(bool CanFallThrough, bool CanContinue, bool BrokeEnclosingSwitch);

    private static LoopCompletion AnalyzeLoopBodyCompletion(
        IOperation operation, ILoopOperation ownerLoop, ILabelSymbol? switchExit, CancellationToken cancellation, int depth)
    {
        cancellation.ThrowIfCancellationRequested();
        if (depth >= ProjectionLimits.MaxExpansionDepth)
            return new(CanFallThrough: true, CanContinue: true, BrokeEnclosingSwitch: false);

        switch (operation)
        {
            case IBlockOperation block:
                return AnalyzeStatementSequenceCompletion(block.Operations, ownerLoop, switchExit, cancellation, depth + 1);
            case IReturnOperation or IThrowOperation:
                return new(false, false, false);
            case IBranchOperation branch:
                return AnalyzeBranchCompletion(branch, ownerLoop, switchExit);
            case IConditionalOperation conditional:
            {
                var whenTrue = AnalyzeLoopBodyCompletion(conditional.WhenTrue, ownerLoop, switchExit, cancellation, depth + 1);
                if (conditional.WhenFalse is not { } whenFalse)
                    return new(true, whenTrue.CanContinue, whenTrue.BrokeEnclosingSwitch);
                var falseResult = AnalyzeLoopBodyCompletion(whenFalse, ownerLoop, switchExit, cancellation, depth + 1);
                return new(
                    whenTrue.CanFallThrough || falseResult.CanFallThrough,
                    whenTrue.CanContinue || falseResult.CanContinue,
                    whenTrue.BrokeEnclosingSwitch || falseResult.BrokeEnclosingSwitch);
            }
            case ISwitchOperation switchOp:
                return AnalyzeSwitchCompletion(switchOp, ownerLoop, cancellation, depth + 1);
            case IUsingOperation usingOp:
                return AnalyzeLoopBodyCompletion(usingOp.Body, ownerLoop, switchExit, cancellation, depth + 1);
            case ILockOperation lockOp:
                return AnalyzeLoopBodyCompletion(lockOp.Body, ownerLoop, switchExit, cancellation, depth + 1);
            case ITryOperation tryOp:
                return AnalyzeTryCompletion(tryOp, ownerLoop, switchExit, cancellation, depth + 1);
            case ILoopOperation nestedLoop:
                return HasUnstructuredJump(nestedLoop, cancellation)
                    ? new(true, true, false)
                    : new(true, false, false);
            case ILabeledOperation:
                return new(true, true, false);
            default:
                return new(true, false, false);
        }
    }

    private static LoopCompletion AnalyzeStatementSequenceCompletion(
        ImmutableArray<IOperation> statements, ILoopOperation ownerLoop, ILabelSymbol? switchExit,
        CancellationToken cancellation, int depth)
    {
        var canContinue = false;
        var brokeSwitch = false;
        for (var i = 0; i < statements.Length; i++)
        {
            var step = AnalyzeLoopBodyCompletion(statements[i], ownerLoop, switchExit, cancellation, depth);
            canContinue |= step.CanContinue;
            brokeSwitch |= step.BrokeEnclosingSwitch;
            if (!step.CanFallThrough)
                return new(false, canContinue, brokeSwitch);
        }
        return new(true, canContinue, brokeSwitch);
    }

    private static LoopCompletion AnalyzeBranchCompletion(
        IBranchOperation branch, ILoopOperation ownerLoop, ILabelSymbol? switchExit) => branch.BranchKind switch
    {
        BranchKind.Continue when SymbolEqualityComparer.Default.Equals(branch.Target, ownerLoop.ContinueLabel) =>
            new(false, true, false),
        BranchKind.Break when SymbolEqualityComparer.Default.Equals(branch.Target, ownerLoop.ExitLabel) =>
            new(false, false, false),
        BranchKind.Break when switchExit is not null && SymbolEqualityComparer.Default.Equals(branch.Target, switchExit) =>
            new(false, false, true),
        BranchKind.GoTo => new(true, true, false),
        _ => new(false, false, false)
    };

    private static LoopCompletion AnalyzeSwitchCompletion(
        ISwitchOperation switchOp, ILoopOperation ownerLoop, CancellationToken cancellation, int depth)
    {
        var hasDefault = false;
        var canFallThrough = false;
        var canContinue = false;
        foreach (var section in switchOp.Cases)
        {
            if (CaseHasDefaultClause(section)) hasDefault = true;
            var sectionResult = AnalyzeStatementSequenceCompletion(
                section.Body, ownerLoop, switchOp.ExitLabel, cancellation, depth);
            canFallThrough |= sectionResult.CanFallThrough || sectionResult.BrokeEnclosingSwitch;
            canContinue |= sectionResult.CanContinue;
        }
        return new(!hasDefault || canFallThrough, canContinue, false);
    }

    private static LoopCompletion AnalyzeTryCompletion(
        ITryOperation tryOp, ILoopOperation ownerLoop, ILabelSymbol? switchExit, CancellationToken cancellation, int depth)
    {
        if (tryOp.Finally is { } @finally)
        {
            var finallyResult = AnalyzeLoopBodyCompletion(@finally, ownerLoop, switchExit, cancellation, depth);
            if (!finallyResult.CanFallThrough)
                return finallyResult;
        }
        var bodyResult = AnalyzeLoopBodyCompletion(tryOp.Body, ownerLoop, switchExit, cancellation, depth);
        var canFallThrough = bodyResult.CanFallThrough;
        var canContinue = bodyResult.CanContinue;
        var brokeSwitch = bodyResult.BrokeEnclosingSwitch;
        foreach (var clause in tryOp.Catches)
        {
            var catchResult = AnalyzeLoopBodyCompletion(clause.Handler, ownerLoop, switchExit, cancellation, depth);
            canFallThrough |= catchResult.CanFallThrough;
            canContinue |= catchResult.CanContinue;
            brokeSwitch |= catchResult.BrokeEnclosingSwitch;
        }
        return new(canFallThrough, canContinue, brokeSwitch);
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
