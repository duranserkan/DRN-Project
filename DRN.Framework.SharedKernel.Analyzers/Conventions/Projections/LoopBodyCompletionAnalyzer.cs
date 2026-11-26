using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal readonly record struct LoopCompletion(
    bool CanFallThrough,
    bool CanContinue,
    bool BrokeEnclosingSwitch);

internal static class LoopBodyCompletionAnalyzer
{
    internal static bool CanDoWhileReachCondition(IWhileLoopOperation loop, CancellationToken cancellation)
    {
        var completion = AnalyzeOperation(loop.Body, loop, switchExit: null, depth: 0, cancellation);
        return completion.CanFallThrough || completion.CanContinue;
    }

    private static LoopCompletion AnalyzeOperation(
        IOperation operation, ILoopOperation ownerLoop, ILabelSymbol? switchExit, int depth, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (depth >= ProjectionLimits.MaxExpansionDepth)
            return new(CanFallThrough: true, CanContinue: true, BrokeEnclosingSwitch: false);

        return operation switch
        {
            IBlockOperation block => AnalyzeStatementSequence(block.Operations, ownerLoop, switchExit, depth + 1, cancellation),
            IReturnOperation or IThrowOperation => new(false, false, false),
            IBranchOperation branch => AnalyzeBranch(branch, ownerLoop, switchExit),
            IConditionalOperation conditional => AnalyzeConditional(conditional, ownerLoop, switchExit, depth + 1, cancellation),
            ISwitchOperation switchOp => AnalyzeSwitch(switchOp, ownerLoop, depth + 1, cancellation),
            IUsingOperation usingOp => AnalyzeOperation(usingOp.Body, ownerLoop, switchExit, depth + 1, cancellation),
            ILockOperation lockOp => AnalyzeOperation(lockOp.Body, ownerLoop, switchExit, depth + 1, cancellation),
            ITryOperation tryOp => AnalyzeTry(tryOp, ownerLoop, switchExit, depth + 1, cancellation),
            ILoopOperation nestedLoop => UnstructuredFlowHelper.HasUnstructuredJump(nestedLoop, cancellation)
                ? new(true, true, false)
                : new(true, false, false),
            ILabeledOperation => new(true, true, false),
            _ => new(true, false, false)
        };
    }

    private static LoopCompletion AnalyzeConditional(
        IConditionalOperation conditional, ILoopOperation ownerLoop, ILabelSymbol? switchExit, int depth, CancellationToken cancellation)
    {
        var whenTrue = AnalyzeOperation(conditional.WhenTrue, ownerLoop, switchExit, depth, cancellation);
        if (conditional.WhenFalse is not { } whenFalse)
            return new(true, whenTrue.CanContinue, whenTrue.BrokeEnclosingSwitch);
        var falseResult = AnalyzeOperation(whenFalse, ownerLoop, switchExit, depth, cancellation);
        return new(
            whenTrue.CanFallThrough || falseResult.CanFallThrough,
            whenTrue.CanContinue || falseResult.CanContinue,
            whenTrue.BrokeEnclosingSwitch || falseResult.BrokeEnclosingSwitch);
    }

    private static LoopCompletion AnalyzeStatementSequence(
        ImmutableArray<IOperation> statements, ILoopOperation ownerLoop, ILabelSymbol? switchExit,
        int depth, CancellationToken cancellation)
    {
        var canContinue = false;
        var brokeSwitch = false;
        for (var i = 0; i < statements.Length; i++)
        {
            var step = AnalyzeOperation(statements[i], ownerLoop, switchExit, depth, cancellation);
            canContinue |= step.CanContinue;
            brokeSwitch |= step.BrokeEnclosingSwitch;
            if (!step.CanFallThrough)
                return new(false, canContinue, brokeSwitch);
        }
        return new(true, canContinue, brokeSwitch);
    }

    private static LoopCompletion AnalyzeBranch(
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

    private static LoopCompletion AnalyzeSwitch(
        ISwitchOperation switchOp, ILoopOperation ownerLoop, int depth, CancellationToken cancellation)
    {
        var hasDefault = false;
        var canFallThrough = false;
        var canContinue = false;
        foreach (var section in switchOp.Cases)
        {
            if (UnstructuredFlowHelper.CaseHasDefaultClause(section)) hasDefault = true;
            var sectionResult = AnalyzeStatementSequence(
                section.Body, ownerLoop, switchOp.ExitLabel, depth, cancellation);
            canFallThrough |= sectionResult.CanFallThrough || sectionResult.BrokeEnclosingSwitch;
            canContinue |= sectionResult.CanContinue;
        }
        return new(!hasDefault || canFallThrough, canContinue, false);
    }

    private static LoopCompletion AnalyzeTry(
        ITryOperation tryOp, ILoopOperation ownerLoop, ILabelSymbol? switchExit, int depth, CancellationToken cancellation)
    {
        if (tryOp.Finally is { } @finally)
        {
            var finallyResult = AnalyzeOperation(@finally, ownerLoop, switchExit, depth, cancellation);
            if (!finallyResult.CanFallThrough)
                return finallyResult;
        }
        var bodyResult = AnalyzeOperation(tryOp.Body, ownerLoop, switchExit, depth, cancellation);
        var canFallThrough = bodyResult.CanFallThrough;
        var canContinue = bodyResult.CanContinue;
        var brokeSwitch = bodyResult.BrokeEnclosingSwitch;
        foreach (var clause in tryOp.Catches)
        {
            var catchResult = AnalyzeOperation(clause.Handler, ownerLoop, switchExit, depth, cancellation);
            canFallThrough |= catchResult.CanFallThrough;
            canContinue |= catchResult.CanContinue;
            brokeSwitch |= catchResult.BrokeEnclosingSwitch;
        }
        return new(canFallThrough, canContinue, brokeSwitch);
    }
}
