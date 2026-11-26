using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

// DRNDTO003 scope freeze: fix only real reported bugs (see ResponseProjectionAnalysis).
internal sealed class SelectorFlowCollector
{
    private readonly ConventionFacts _facts;
    private readonly ConventionSymbols _symbols;
    private readonly CancellationToken _cancellation;
    private readonly IBlockOperation _body;
    private readonly SelectorScope? _parentScope;
    private readonly SelectorFlowSession _session;
    private readonly List<RecordedReturn> _returns = [];
    private readonly Dictionary<ILocalReferenceOperation, ImmutableArray<LocalBinding>> _referenceValues = [];
    private readonly Dictionary<ILocalSymbol, List<LocalBinding>> _captured = new(SymbolEqualityComparer.Default);
    private readonly List<IOperation> _childScratch = [];
    private readonly Stack<IOperation> _sideEffectScratch = [];

    private List<IOperation>? _returnConditions;
    private List<IOperation>? _unstructuredReturnConditions;
    private List<BranchGuard>? _activeBranchGuards;
    private Dictionary<IAnonymousFunctionOperation, Dictionary<ILocalSymbol, List<LocalBinding>>>? _lambdaCaptured;
    private Dictionary<IOperation, List<MaterializedLambda>>? _materializedLambdas;
    private Dictionary<ILocalSymbol, List<LocalBinding>>? _deferredLinqWrites;
    private HashSet<ILocalSymbol>? _outerWrites;
    private Dictionary<ILocalSymbol, List<LocalBinding>>? _outerExitValues;
    private Dictionary<IOperation, bool>? _containsReturnCache;
    private QueryMaterializerResolver? _materializerResolver;

    private SelectorFlowCollector(
        IBlockOperation body, SelectorScope? parentScope, ConventionFacts facts, SelectorFlowSession session)
    {
        _body = body;
        _parentScope = parentScope;
        _facts = facts;
        _symbols = facts.Symbols;
        _cancellation = facts.Cancellation;
        _session = session;
    }

    internal static SelectorFlowResult Collect(IBlockOperation body, SelectorScope? parentScope, ConventionFacts facts)
    {
        var session = new SelectorFlowSession();
        session.TryConsumeCollection();
        return CollectCore(body, parentScope, facts, session);
    }

    internal static SelectorScope? ResolveEnclosingBodyScope(
        IInvocationOperation select, ConventionFacts facts,
        Dictionary<IBlockOperation, SelectorFlowResult>? bodyFlows) =>
        SelectorScopeResolver.ResolveEnclosingBodyScope(select, facts, bodyFlows);

    private static SelectorFlowResult CollectCore(
        IBlockOperation body, SelectorScope? parentScope, ConventionFacts facts, SelectorFlowSession session)
    {
        var unstructured = UnstructuredFlowHelper.HasUnstructuredJump(body, facts.Cancellation);
        var collector = new SelectorFlowCollector(body, parentScope, facts, session);
        collector.AnalyzeBody(SelectorFlowEnvironment.CreateInitialState(parentScope), unstructured);

        Dictionary<ILocalSymbol, List<LocalBinding>>? mergedOuterExit = null;
        const int maxCrossIterationPasses = 8;
        for (var pass = 1; pass < maxCrossIterationPasses && collector._outerExitValues is { Count: > 0 }; pass++)
        {
            if (!MergeOuterExitValues(ref mergedOuterExit, collector._outerExitValues))
                break;
            if (!session.TryConsumeCollection())
                break;
            // A later element can read a captured value left by an earlier invocation.
            var initial = SelectorFlowEnvironment.CreateInitialState(parentScope);
            SelectorFlowEnvironment.MergeBranchEnv(initial.Env, mergedOuterExit!);
            collector = new SelectorFlowCollector(body, parentScope, facts, session);
            collector.AnalyzeBody(initial, unstructured);
        }
        if (collector._outerExitValues is { Count: > 0 })
            MergeOuterExitValues(ref mergedOuterExit, collector._outerExitValues);

        var hasScopeData = collector._referenceValues.Count > 0 ||
            collector._captured.Count > 0 ||
            collector._lambdaCaptured is { Count: > 0 };
        var scope = hasScopeData ? collector.BuildScope(parentScope) : parentScope;
        var outerExitValues = mergedOuterExit is { Count: > 0 }
            ? SelectorFlowEnvironment.ToImmutableMap(mergedOuterExit)
            : null;
        var returns = collector.BuildReturns();
        return new(returns, collector._returnConditions, scope, outerExitValues);
    }

    private static SelectorFlowResult CollectChild(
        IBlockOperation body, SelectorScope? parentScope, ConventionFacts facts, SelectorFlowSession session)
    {
        var incoming = session.CaptureIncoming(body, parentScope, facts.Cancellation);
        if (session.TryGetMemoized(body, incoming, out var cached))
            return cached;
        var result = session.TryConsumeCollection()
            ? CollectCore(body, parentScope, facts, session)
            : ConservativeFlowCollector.Collect(body, parentScope, facts.Cancellation);
        session.StoreMemoized(body, incoming, result);
        return result;
    }

    private static bool MergeOuterExitValues(
        ref Dictionary<ILocalSymbol, List<LocalBinding>>? target,
        Dictionary<ILocalSymbol, List<LocalBinding>> source)
    {
        var grew = false;
        target ??= new(SymbolEqualityComparer.Default);
        foreach (var (local, values) in source)
        {
            if (!target.TryGetValue(local, out var existing))
            {
                target[local] = [.. values];
                if (values.Count > 0) grew = true;
                continue;
            }
            for (var i = 0; i < values.Count; i++)
            {
                if (SelectorFlowEnvironment.ContainsBinding(existing, values[i])) continue;
                existing.Add(values[i]);
                grew = true;
            }
        }
        return grew;
    }

    private void AnalyzeBody(FlowState state, bool unstructured)
    {
        var exited = unstructured
            ? AnalyzeUnstructuredStatement(_body, state)
            : AnalyzeStatement(_body, state);
        if (!exited) RecordOuterExitValues(state);
    }

    private List<SelectorReturn> BuildReturns()
    {
        var result = new List<SelectorReturn>(_returns.Count);
        var hasSingleReturnedLocal = HasSingleReturnedLocal();
        for (var i = 0; i < _returns.Count; i++)
        {
            var entry = _returns[i];
            result.Add(new(entry.Value, BuildReturnGuards(entry, hasSingleReturnedLocal)));
        }
        return result;
    }

    private bool HasSingleReturnedLocal()
    {
        if (_returns.Count == 0 || _returns[0].ReturnedLocal is not { } single)
            return false;
        for (var i = 1; i < _returns.Count; i++)
        {
            if (!SymbolEqualityComparer.Default.Equals(_returns[i].ReturnedLocal, single))
                return false;
        }
        return true;
    }

    private ImmutableArray<IOperation> BuildReturnGuards(RecordedReturn entry, bool hasSingleReturnedLocal)
    {
        ImmutableArray<IOperation>.Builder? builder = null;
        if (!entry.Guards.IsDefaultOrEmpty)
        {
            for (var j = 0; j < entry.Guards.Length; j++)
            {
                var (condition, localExistedUnreplaced) = entry.Guards[j];
                if (localExistedUnreplaced && hasSingleReturnedLocal)
                    continue;
                (builder ??= ImmutableArray.CreateBuilder<IOperation>()).Add(condition);
                AddReturnCondition(condition);
            }
        }
        if (_unstructuredReturnConditions is { Count: > 0 } && !hasSingleReturnedLocal)
        {
            builder ??= ImmutableArray.CreateBuilder<IOperation>(_unstructuredReturnConditions.Count);
            for (var j = 0; j < _unstructuredReturnConditions.Count; j++)
                builder.Add(_unstructuredReturnConditions[j]);
        }
        return builder is null ? [] : builder.ToImmutable();
    }

    private void RecordOuterExitValues(FlowState state)
    {
        if (_outerWrites is null) return;
        foreach (var local in _outerWrites)
        {
            if (state.Env.TryGetValue(local, out var values))
                SelectorFlowEnvironment.MergeLocalBindings(_outerExitValues ??= new(SymbolEqualityComparer.Default), local, values);
        }
    }

    private SelectorScope BuildScope(SelectorScope? parentScope)
    {
        var captured = _captured.Count == 0
            ? SelectorScope.EmptyCaptured
            : SelectorFlowEnvironment.ToImmutableMap(_captured);
        Dictionary<IAnonymousFunctionOperation, Dictionary<ILocalSymbol, ImmutableArray<LocalBinding>>> lambdaCaptured;
        if (_lambdaCaptured is not { Count: > 0 })
        {
            lambdaCaptured = SelectorScope.EmptyLambdaCaptures;
        }
        else
        {
            lambdaCaptured = new(_lambdaCaptured.Count);
            foreach (var (lambda, map) in _lambdaCaptured)
                lambdaCaptured[lambda] = SelectorFlowEnvironment.ToImmutableMap(map);
        }
        return new SelectorScope(_referenceValues, captured, lambdaCaptured, parentScope);
    }

    private bool AnalyzeStatement(IOperation statement, FlowState state, int depth = 0)
    {
        _cancellation.ThrowIfCancellationRequested();
        if (depth >= ProjectionLimits.MaxExpansionDepth) return AnalyzeUnstructuredStatement(statement, state);
        return statement switch
        {
            IBlockOperation block => AnalyzeBlock(block, state, depth),
            IVariableDeclarationGroupOperation group => AnalyzeDeclarationGroup(group, state),
            IUsingDeclarationOperation usingDeclaration => AnalyzeDeclarationGroup(usingDeclaration.DeclarationGroup, state),
            IExpressionStatementOperation expressionStatement => AnalyzeExpressionStatement(expressionStatement, state),
            IReturnOperation returned => AnalyzeReturn(returned, state),
            IThrowOperation thrown => AnalyzeThrow(thrown, state),
            IConditionalOperation conditional => AnalyzeConditionalStatement(conditional, state, depth),
            ILocalFunctionOperation => false,
            _ => AnalyzeUnstructuredStatement(statement, state)
        };
    }

    private bool AnalyzeBlock(IBlockOperation block, FlowState state, int depth)
    {
        var guardCount = _activeBranchGuards?.Count ?? 0;
        foreach (var statement in block.Operations)
        {
            _cancellation.ThrowIfCancellationRequested();
            if (AnalyzeStatement(statement, state, depth + 1))
            {
                TrimBranchGuards(guardCount);
                return true;
            }
        }
        ApplyAndTrimContinuationGuards(guardCount, state);
        return false;
    }

    private int PushBranchGuard(FlowState before, IOperation condition)
    {
        var list = _activeBranchGuards ??= [];
        var index = list.Count;
        list.Add(new(before, condition));
        return index;
    }

    private void TrimBranchGuards(int count)
    {
        if (_activeBranchGuards is { Count: > 0 } list && list.Count > count)
            list.RemoveRange(count, list.Count - count);
    }

    private void ApplyAndTrimContinuationGuards(int startIndex, FlowState state)
    {
        if (_activeBranchGuards is null || _activeBranchGuards.Count <= startIndex) return;
        for (var i = _activeBranchGuards.Count - 1; i >= startIndex; i--)
        {
            var guard = _activeBranchGuards[i];
            var diffs = SelectorFlowEnvironment.CollectBranchDiffs(guard.Before.Env, state.Env);
            if (diffs is not null) AttachGuard(state, guard.Condition, diffs);
        }
        _activeBranchGuards.RemoveRange(startIndex, _activeBranchGuards.Count - startIndex);
    }

    private bool AnalyzeDeclarationGroup(IVariableDeclarationGroupOperation group, FlowState state)
    {
        foreach (var declaration in group.Declarations)
        {
            foreach (var declarator in declaration.Declarators)
            {
                var initial = declarator.Initializer?.Value ?? declaration.Initializer?.Value;
                if (initial is not null) AnalyzeExpression(initial, state, unconditional: true);
                RecordLocalValue(declarator.Symbol, initial is null ? null : new LocalBinding(initial), state, replace: true);
            }
        }
        return false;
    }

    private bool AnalyzeExpressionStatement(IExpressionStatementOperation statement, FlowState state)
    {
        if (statement.Operation is IAssignmentOperation assignment)
        {
            AnalyzeAssignment(assignment, state, allowReplace: true);
            return false;
        }
        AnalyzeExpression(statement.Operation, state, unconditional: true);
        return false;
    }

    private void AnalyzeAssignment(IAssignmentOperation assignment, FlowState state, bool allowReplace, int depth = 0)
    {
        var valueUnconditional = allowReplace && assignment is not ICoalesceAssignmentOperation;
        if (assignment is IDeconstructionAssignmentOperation deconstruction)
        {
            AnalyzeExpression(deconstruction.Value, state, valueUnconditional, depth);
            BindDeconstructionTarget(deconstruction.Target, deconstruction.Value, state, allowReplace, default);
            return;
        }
        if (assignment.Target is ILocalReferenceOperation targetLocal)
        {
            if (assignment is not ISimpleAssignmentOperation)
                SnapshotReference(targetLocal, state.Env);
            AnalyzeAssignmentValue(assignment, state, valueUnconditional, depth);
            var replacesLocal = allowReplace && assignment is ISimpleAssignmentOperation;
            RecordLocalValue(targetLocal.Local, new(assignment.Value), state, replace: replacesLocal);
            return;
        }

        if (assignment is ICoalesceAssignmentOperation)
            AnalyzeExpression(assignment.Target, state, allowReplace, depth);
        else
            AnalyzeIndexerKeyExpressions(assignment.Target, state, depth);
        AnalyzeAssignmentValue(assignment, state, valueUnconditional, depth);
        if (TryGetMutatedLocal(assignment.Target) is not { } mutated) return;

        var replacesMember = TryReplacePriorDirectMemberWrites(assignment, mutated.Local, allowReplace, state);
        RecordLocalValue(mutated.Local,
            new(assignment.Value, WriteTarget: assignment.Target, ReplacesMember: replacesMember),
            state, replace: false);
    }

    private void AnalyzeAssignmentValue(
        IAssignmentOperation assignment, FlowState state, bool valueUnconditional, int depth)
    {
        if (assignment is ICoalesceAssignmentOperation && HasFlowSideEffects(assignment.Value))
        {
            var whenNull = SelectorFlowEnvironment.Clone(state);
            AnalyzeExpression(assignment.Value, whenNull, unconditional: false, depth);
            MergeBranch(state, targetExits: false, whenNull, alternateExits: false, assignment.Target);
            return;
        }
        AnalyzeExpression(assignment.Value, state, valueUnconditional, depth);
    }

    private bool TryReplacePriorDirectMemberWrites(
        IAssignmentOperation assignment, ILocalSymbol mutatedLocal, bool allowReplace, FlowState state)
    {
        if (!allowReplace || assignment is not ISimpleAssignmentOperation) return false;
        if (PropertyAccessFacts.DirectWrittenProperty(assignment.Target) is not { } writtenProperty)
            return false;

        var hasExistingBindings = state.Env.TryGetValue(mutatedLocal, out var previous);
        var knownReceiver = hasExistingBindings
            ? PropertyAccessFacts.KnownLocalReceiverType(CollectionsMarshal.AsSpan(previous!))
            : null;
        var dispatchedWritten = PropertyAccessFacts.DispatchedProperty(writtenProperty, knownReceiver, _cancellation);
        // Only direct auto-property setters can erase earlier writes without risking side effects on sibling state.
        if (!_facts.ProjectionFacts.CanGuaranteeDirectSetter(dispatchedWritten, knownReceiver))
            return false;

        if (previous is not null)
            RemoveMatchingMemberWrites(previous, dispatchedWritten, knownReceiver);
        return true;
    }

    private void RemoveMatchingMemberWrites(
        List<LocalBinding> previous, IPropertySymbol dispatchedWritten, INamedTypeSymbol? knownReceiver)
    {
        for (var i = previous.Count - 1; i >= 0; i--)
        {
            if (WritesMember(previous[i].WriteTarget, dispatchedWritten, knownReceiver))
                previous.RemoveAt(i);
        }
    }

    private bool WritesMember(IOperation? target, IPropertySymbol dispatchedWritten, INamedTypeSymbol? knownReceiver)
    {
        for (var current = RoslynOperationHelpers.UnwrapTransparent(target);
             current is not null;
             current = CollectionMutationFacts.MutationReceiver(current))
        {
            if (current is not IPropertyReferenceOperation member ||
                RoslynOperationHelpers.UnwrapTransparent(member.Instance) is not ILocalReferenceOperation)
                continue;
            var candidate = PropertyAccessFacts.DispatchedProperty(member.Property, knownReceiver, _cancellation);
            if (PropertyAccessFacts.SameOrMatchingProperty(candidate, dispatchedWritten))
                return true;
        }
        return false;
    }

    private static bool IsShortCircuitBinary(IBinaryOperation binary) =>
        binary.OperatorKind is BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr;

    private bool AnalyzeReturn(IReturnOperation returned, FlowState state)
    {
        if (returned.ReturnedValue is { } value)
        {
            var returnedLocal = (RoslynOperationHelpers.UnwrapTransparent(value) as ILocalReferenceOperation)?.Local;
            var snapshotState = state;
            ImmutableArray<ReturnBranchGuard> guards = default;
            if (_activeBranchGuards is { Count: > 0 })
            {
                snapshotState = SelectorFlowEnvironment.Clone(state);
                guards = CollectReturnBranchGuards(returnedLocal, snapshotState);
            }
            AnalyzeExpression(value, snapshotState, unconditional: true);
            _returns.Add(new(value, returnedLocal, guards));
            RecordOuterExitValues(snapshotState);
        }
        return true;
    }

    private ImmutableArray<ReturnBranchGuard> CollectReturnBranchGuards(
        ILocalSymbol? returnedLocal, FlowState snapshotState)
    {
        var activeGuards = _activeBranchGuards!;
        var builder = ImmutableArray.CreateBuilder<ReturnBranchGuard>(activeGuards.Count);
        for (var i = activeGuards.Count - 1; i >= 0; i--)
        {
            var guard = activeGuards[i];
            var diffs = SelectorFlowEnvironment.CollectBranchDiffs(guard.Before.Env, snapshotState.Env);
            var localExistedUnreplaced = returnedLocal is not null &&
                guard.Before.Env.ContainsKey(returnedLocal) &&
                !HasWholesaleLocalDiff(diffs, returnedLocal);
            if (diffs is not null)
            {
                AttachGuard(snapshotState, guard.Condition, diffs);
                if (HasAnyLocalDiff(diffs, returnedLocal))
                    AddReturnCondition(guard.Condition);
            }
            builder.Add(new(guard.Condition, localExistedUnreplaced));
        }
        return builder.MoveToImmutable();
    }

    private static bool HasAnyLocalDiff(List<BranchWriteDiff> diffs, ILocalSymbol? local)
    {
        if (local is null) return false;
        for (var i = 0; i < diffs.Count; i++)
        {
            if (SymbolEqualityComparer.Default.Equals(diffs[i].Local, local))
                return true;
        }
        return false;
    }

    private static bool HasWholesaleLocalDiff(List<BranchWriteDiff>? diffs, ILocalSymbol local)
    {
        if (diffs is null) return false;
        for (var i = 0; i < diffs.Count; i++)
        {
            if (diffs[i].Target is null && SymbolEqualityComparer.Default.Equals(diffs[i].Local, local))
                return true;
        }
        return false;
    }

    private bool AnalyzeThrow(IThrowOperation thrown, FlowState state)
    {
        if (_activeBranchGuards is not null)
            foreach (var guard in _activeBranchGuards) AddReturnCondition(guard.Condition);
        if (thrown.Exception is { } exception) AnalyzeExpression(exception, state, unconditional: true);
        return true;
    }

    private bool AnalyzeConditionalStatement(IConditionalOperation conditional, FlowState state, int depth)
    {
        AnalyzeExpression(conditional.Condition, state, unconditional: true);
        var beforeBranch = SelectorFlowEnvironment.Clone(state);
        var alternate = SelectorFlowEnvironment.Clone(state);
        var guardIndex = PushBranchGuard(beforeBranch, conditional.Condition);
        bool targetExits;
        bool alternateExits;
        if (conditional.WhenFalse is { } whenFalse)
        {
            targetExits = AnalyzeStatement(conditional.WhenTrue, state, depth + 1);
            alternateExits = AnalyzeStatement(whenFalse, alternate, depth + 1);
        }
        else
        {
            targetExits = false;
            alternateExits = AnalyzeStatement(conditional.WhenTrue, alternate, depth + 1);
        }
        TrimBranchGuards(guardIndex);
        var exited = MergeBranch(state, targetExits, alternate, alternateExits, conditional.Condition, beforeBranch);
        if (!exited && targetExits != alternateExits)
            PushBranchGuard(SelectorFlowEnvironment.Clone(state), conditional.Condition);
        return exited;
    }

    private void AddReturnCondition(IOperation condition, bool unstructured = false)
    {
        if (!(_returnConditions ??= []).Contains(condition)) _returnConditions.Add(condition);
        if (unstructured) (_unstructuredReturnConditions ??= []).Add(condition);
    }

    private bool MergeBranch(
        FlowState target, bool targetExits, FlowState alternate, bool alternateExits,
        IOperation guard, FlowState? beforeBranch = null)
    {
        if (targetExits && alternateExits) return true;
        if (targetExits)
        {
            var writesWhenAlternateSurvives = beforeBranch is { } before
                ? SelectorFlowEnvironment.CollectBranchDiffs(before.Env, alternate.Env)
                : null;
            SelectorFlowEnvironment.Replace(target, alternate);
            if (writesWhenAlternateSurvives is not null) AttachGuard(target, guard, writesWhenAlternateSurvives);
            return false;
        }
        if (alternateExits)
        {
            var writesWhenTargetSurvives = beforeBranch is { } before
                ? SelectorFlowEnvironment.CollectBranchDiffs(before.Env, target.Env)
                : null;
            if (writesWhenTargetSurvives is not null) AttachGuard(target, guard, writesWhenTargetSurvives);
            return false;
        }
        var writes = SelectorFlowEnvironment.CollectBranchDiffs(target.Env, alternate.Env);
        SelectorFlowEnvironment.MergeBranchEnv(target.Env, alternate.Env);
        target.ActiveLambdas.UnionWith(alternate.ActiveLambdas);
        if (writes is not null) AttachGuard(target, guard, writes);
        return false;
    }

    private void AttachGuard(FlowState state, IOperation guard, List<BranchWriteDiff> writes)
    {
        for (var i = 0; i < writes.Count; i++)
        {
            var (local, writeTarget) = writes[i];
            RecordLocalValue(local, new(guard, Kind: LocalBindingKind.Condition, WriteTarget: writeTarget), state, replace: false);
        }
    }

    private void AttachSwitchGuards(
        FlowState state, ISwitchOperation owner, ISwitchCaseOperation section,
        List<BranchWriteDiff>? writes)
    {
        if (writes is null) return;
        var conditions = new List<IOperation>();
        UnstructuredFlowHelper.CollectSwitchConditions(owner, section, conditions);
        for (var i = 0; i < conditions.Count; i++) AttachGuard(state, conditions[i], writes);
    }

    private void AttachSwitchExpressionGuards(
        FlowState state, ISwitchExpressionOperation owner, ISwitchExpressionArmOperation arm,
        List<BranchWriteDiff>? writes)
    {
        if (writes is null) return;
        var conditions = new List<IOperation>();
        UnstructuredFlowHelper.CollectSwitchExpressionConditions(owner, arm, conditions);
        for (var i = 0; i < conditions.Count; i++) AttachGuard(state, conditions[i], writes);
    }

    private bool AnalyzeUnstructuredStatement(IOperation statement, FlowState state)
    {
        PreCollectUnstructuredAssignments(statement, state);
        WalkUnstructuredRegion(statement, state);
        return false;
    }

    private void PreCollectUnstructuredAssignments(IOperation root, FlowState state)
    {
        var walk = new Stack<UnstructuredWalkEntry>();
        walk.Push(new(root));
        while (walk.TryPop(out var entry))
        {
            _cancellation.ThrowIfCancellationRequested();
            if (entry.Before is { } before)
            {
                ApplyUnstructuredGuardSentinel(entry, before, state);
                continue;
            }
            var current = entry.Operation;
            if (current is IAnonymousFunctionOperation childLambda)
            {
                ApplyUnstructuredMaterializedLambdaOuterWrites(childLambda, state);
                continue;
            }
            if (current is ILocalFunctionOperation) continue;
            PushGoverningConditionSentinel(current, state, walk);
            RecordUnstructuredAssignment(current, state);

            RoslynOperationHelpers.CopyChildrenTo(current, _childScratch);
            for (var i = _childScratch.Count - 1; i >= 0; i--) walk.Push(new(_childScratch[i]));
            _childScratch.Clear();
        }
    }

    private void ApplyUnstructuredGuardSentinel(UnstructuredWalkEntry entry, FlowState before, FlowState state)
    {
        var diffs = SelectorFlowEnvironment.CollectBranchDiffs(before.Env, state.Env);
        if (entry.SingleCondition is { } condition && diffs is not null)
            AttachGuard(state, condition, diffs);
        else if (entry.SwitchOwner is { } owner && entry.SwitchSection is { } section)
            AttachSwitchGuards(state, owner, section, diffs);
        else if (entry.SwitchExpressionOwner is { } exprOwner && entry.SwitchExpressionArm is { } arm)
            AttachSwitchExpressionGuards(state, exprOwner, arm, diffs);
    }

    private void PushGoverningConditionSentinel(
        IOperation current, FlowState state, Stack<UnstructuredWalkEntry> walk)
    {
        switch (current.Parent)
        {
            case ISwitchOperation owner when current is ISwitchCaseOperation section:
                walk.Push(new(current, SelectorFlowEnvironment.Clone(state), SwitchOwner: owner, SwitchSection: section));
                return;
            case ISwitchExpressionOperation owner when current is ISwitchExpressionArmOperation arm:
                walk.Push(new(current, SelectorFlowEnvironment.Clone(state), SwitchExpressionOwner: owner, SwitchExpressionArm: arm));
                return;
            case IConditionalOperation conditional when !ReferenceEquals(current, conditional.Condition):
                walk.Push(new(current, SelectorFlowEnvironment.Clone(state), SingleCondition: conditional.Condition));
                return;
            case IBinaryOperation binary when IsShortCircuitBinary(binary) && ReferenceEquals(current, binary.RightOperand):
                walk.Push(new(current, SelectorFlowEnvironment.Clone(state), SingleCondition: binary.LeftOperand));
                return;
            case ICoalesceOperation coalesce when ReferenceEquals(current, coalesce.WhenNull):
                walk.Push(new(current, SelectorFlowEnvironment.Clone(state), SingleCondition: coalesce.Value));
                return;
            case IConditionalAccessOperation conditionalAccess when ReferenceEquals(current, conditionalAccess.WhenNotNull):
                walk.Push(new(current, SelectorFlowEnvironment.Clone(state), SingleCondition: conditionalAccess.Operation));
                return;
            case ICoalesceAssignmentOperation coalesceAssign when ReferenceEquals(current, coalesceAssign.Value):
                walk.Push(new(current, SelectorFlowEnvironment.Clone(state), SingleCondition: coalesceAssign.Target));
                return;
            case ILoopOperation loop when ReferenceEquals(current, loop.Body) &&
                UnstructuredFlowHelper.LoopCondition(loop, _symbols, _cancellation) is { } condition:
                walk.Push(new(current, SelectorFlowEnvironment.Clone(state), SingleCondition: condition));
                return;
            case ICatchClauseOperation { Filter: { } filter } clause when ReferenceEquals(current, clause.Handler):
                walk.Push(new(current, SelectorFlowEnvironment.Clone(state), SingleCondition: filter));
                return;
        }
    }

    private void RecordUnstructuredAssignment(IOperation current, FlowState state)
    {
        if (current is IForEachLoopOperation loop)
        {
            BindDeconstructionTarget(loop.LoopControlVariable, loop.Collection, state, false, default, LocalBindingKind.SequenceElement);
            return;
        }
        if (current is IVariableDeclaratorOperation declarator)
        {
            var initial = declarator.Initializer?.Value ??
                          (declarator.Parent as IVariableDeclarationOperation)?.Initializer?.Value;
            RecordLocalValue(declarator.Symbol, initial is null ? null : new LocalBinding(initial), state, replace: false);
            return;
        }
        if (current is IDeconstructionAssignmentOperation deconstruction)
        {
            BindDeconstructionTarget(deconstruction.Target, deconstruction.Value, state, replace: false, default);
            return;
        }
        if (current is IAssignmentOperation assignment)
        {
            RecordUnstructuredAssignmentTarget(assignment, state);
            return;
        }
        if (current is IInvocationOperation call)
        {
            RecordOutArguments(call, state, replaceOut: false);
            RecordReceiverMutation(call, state);
            return;
        }
        RecordPatternLocal(current, state);
    }

    private void RecordUnstructuredAssignmentTarget(IAssignmentOperation assignment, FlowState state)
    {
        if (assignment.Target is ILocalReferenceOperation targetLocal)
        {
            RecordLocalValue(targetLocal.Local, new(assignment.Value), state, replace: false);
            return;
        }
        if (TryGetMutatedLocal(assignment.Target) is { } mutated)
            RecordLocalValue(mutated.Local, new(assignment.Value, WriteTarget: assignment.Target), state, replace: false);
    }

    private void WalkUnstructuredRegion(IOperation root, FlowState state)
    {
        var walk = new Stack<IOperation>();
        walk.Push(root);
        while (walk.TryPop(out var current))
        {
            _cancellation.ThrowIfCancellationRequested();
            if (current is IAnonymousFunctionOperation childLambda)
            {
                CaptureDeferredLambda(childLambda, state);
                continue;
            }
            if (current is ILocalFunctionOperation) continue;
            if (current is IReturnOperation { ReturnedValue: { } returned })
            {
                var returnedLocal = (RoslynOperationHelpers.UnwrapTransparent(returned) as ILocalReferenceOperation)?.Local;
                _returns.Add(new(returned, returnedLocal, default));
                RecordOuterExitValues(state);
            }
            if (current is ILocalReferenceOperation localRef && !UnstructuredFlowHelper.IsOutArgumentTarget(localRef))
                SnapshotReference(localRef, state.Env);
            RecordUnstructuredReturnCondition(current);
            CaptureAtMaterialization(current, state);
            RoslynOperationHelpers.PushChildrenReversed(current, _childScratch, walk);
        }
    }

    private void RecordUnstructuredReturnCondition(IOperation current)
    {
        switch (current)
        {
            case ILoopOperation loop:
                RecordLoopReturnCondition(loop);
                return;
            case ICatchClauseOperation { Filter: { } filter } clause:
                if (UnstructuredFlowHelper.ContainsReturn(clause.Handler, ref _containsReturnCache, _cancellation))
                    AddReturnCondition(filter, unstructured: true);
                return;
            case IConditionalOperation conditional:
                if (ConditionalContainsReturn(conditional))
                    AddReturnCondition(conditional.Condition, unstructured: true);
                return;
            case ISwitchOperation switchStatement:
                if (UnstructuredFlowHelper.SwitchContainsReturn(switchStatement, ref _containsReturnCache, _cancellation))
                    RecordSwitchReturnConditions(switchStatement);
                return;
        }
    }

    private void RecordLoopReturnCondition(ILoopOperation loop)
    {
        if (UnstructuredFlowHelper.LoopCondition(loop, _symbols, _cancellation) is not { } condition ||
            !UnstructuredFlowHelper.ContainsReturn(loop.Body, ref _containsReturnCache, _cancellation))
            return;
        if (loop is IWhileLoopOperation { ConditionIsTop: false } doWhile &&
            !UnstructuredFlowHelper.CanDoWhileReachCondition(doWhile, _cancellation))
            return;
        AddReturnCondition(condition, unstructured: true);
    }

    private bool ConditionalContainsReturn(IConditionalOperation conditional) =>
        UnstructuredFlowHelper.ContainsReturn(conditional.WhenTrue, ref _containsReturnCache, _cancellation) ||
        (conditional.WhenFalse is { } whenFalse &&
         UnstructuredFlowHelper.ContainsReturn(whenFalse, ref _containsReturnCache, _cancellation));

    private void RecordSwitchReturnConditions(ISwitchOperation switchStatement)
    {
        var switchConditions = new List<IOperation>();
        UnstructuredFlowHelper.CollectSwitchConditions(switchStatement, selected: null, switchConditions);
        for (var i = 0; i < switchConditions.Count; i++)
            AddReturnCondition(switchConditions[i], unstructured: true);
    }

    private void AnalyzeExpression(IOperation root, FlowState state, bool unconditional = false, int depth = 0)
    {
        if (depth >= ProjectionLimits.MaxExpansionDepth)
        {
            AnalyzeUnstructuredStatement(root, state);
            return;
        }
        WalkExpression(root, state, unconditional, depth + 1);
    }

    private void WalkExpression(IOperation root, FlowState state, bool unconditional, int depth)
    {
        var walk = new Stack<ExpressionWalkEntry>();
        walk.Push(new(root, unconditional));
        while (walk.TryPop(out var current))
        {
            _cancellation.ThrowIfCancellationRequested();
            if (current.IsExit)
            {
                CaptureAtMaterialization(current.Operation, state);
                continue;
            }
            if (TryHandleSpecialExpression(current, state, depth)) continue;

            var operation = current.Operation;
            if (operation is ILocalReferenceOperation localRef)
                SnapshotReference(localRef, state.Env);
            if (operation is IVariableDeclaratorOperation declarator)
            {
                var initial = declarator.Initializer?.Value is { } init ? new LocalBinding(init) : (LocalBinding?)null;
                RecordLocalValue(declarator.Symbol, initial, state, replace: false);
            }
            RecordPatternLocal(operation, state);
            if (QueryMaterializerResolver.IsMaterializingOperation(operation, _symbols))
                walk.Push(new(operation, false, IsExit: true));
            PushExpressionChildren(operation, current.Unconditional, walk);
        }
    }

    private bool TryHandleSpecialExpression(ExpressionWalkEntry current, FlowState state, int depth)
    {
        var operation = current.Operation;
        switch (operation)
        {
            case IAnonymousFunctionOperation childLambda:
                CaptureLambdaEnvironment(childLambda, state);
                return true;
            case ILocalFunctionOperation:
                return true;
            case IConditionalOperation conditional:
                AnalyzeConditionalExpression(conditional, state, current.Unconditional, depth);
                return true;
            case IBinaryOperation binary when IsShortCircuitBinary(binary):
                AnalyzeShortCircuitBinary(binary, state, current.Unconditional, depth);
                return true;
            case ICoalesceOperation coalesce when HasFlowSideEffects(coalesce.WhenNull):
                AnalyzeCoalesceExpression(coalesce, state, current.Unconditional, depth);
                return true;
            case IConditionalAccessOperation conditionalAccess when HasFlowSideEffects(conditionalAccess.WhenNotNull):
                AnalyzeConditionalAccessExpression(conditionalAccess, state, current.Unconditional, depth);
                return true;
            case ISwitchExpressionOperation switchExpression when HasSwitchExpressionSideEffects(switchExpression):
                AnalyzeSwitchExpression(switchExpression, state, current.Unconditional, depth);
                return true;
            case IAssignmentOperation assignment:
                AnalyzeAssignment(assignment, state, allowReplace: current.Unconditional, depth);
                return true;
            case IInvocationOperation call:
                AnalyzeCall(call, state, current.Unconditional, depth);
                return true;
            default:
                return false;
        }
    }

    private void AnalyzeCoalesceExpression(
        ICoalesceOperation coalesce, FlowState state, bool unconditional, int depth)
    {
        AnalyzeExpression(coalesce.Value, state, unconditional, depth);
        var whenNull = SelectorFlowEnvironment.Clone(state);
        AnalyzeExpression(coalesce.WhenNull, whenNull, unconditional, depth);
        MergeBranch(state, targetExits: false, whenNull, alternateExits: false, coalesce.Value);
    }

    private void AnalyzeConditionalAccessExpression(
        IConditionalAccessOperation conditionalAccess, FlowState state, bool unconditional, int depth)
    {
        AnalyzeExpression(conditionalAccess.Operation, state, unconditional, depth);
        var whenNotNull = SelectorFlowEnvironment.Clone(state);
        AnalyzeExpression(conditionalAccess.WhenNotNull, whenNotNull, unconditional, depth);
        MergeBranch(state, targetExits: false, whenNotNull, alternateExits: false, conditionalAccess.Operation);
    }

    private bool HasSwitchExpressionSideEffects(ISwitchExpressionOperation switchExpression)
    {
        for (var i = 0; i < switchExpression.Arms.Length; i++)
        {
            var arm = switchExpression.Arms[i];
            if (HasFlowSideEffects(arm.Value) || (arm.Guard is { } guard && HasFlowSideEffects(guard)))
                return true;
        }
        return false;
    }

    private void AnalyzeSwitchExpression(
        ISwitchExpressionOperation switchExpression, FlowState state, bool unconditional, int depth)
    {
        AnalyzeExpression(switchExpression.Value, state, unconditional, depth);
        var beforeSwitch = SelectorFlowEnvironment.Clone(state);
        var fallthroughState = SelectorFlowEnvironment.Clone(beforeSwitch);
        var conditions = new List<IOperation>();
        if (!ProjectionOriginAnalyzer.AreAllArmsBindingOnly(switchExpression.Arms))
            conditions.Add(switchExpression.Value);

        for (var i = 0; i < switchExpression.Arms.Length; i++)
        {
            var arm = switchExpression.Arms[i];
            var armState = SelectorFlowEnvironment.Clone(fallthroughState);
            AnalyzeExpression(arm.Pattern, armState, unconditional: false, depth);
            if (!ProjectionOriginAnalyzer.IsBindingOnlyPattern(arm.Pattern))
                conditions.Add(arm.Pattern);
            if (arm.Guard is { } guard)
                AnalyzeSwitchArmGuard(guard, fallthroughState, armState, conditions, depth);
            AnalyzeExpression(arm.Value, armState, unconditional, depth);
            var writes = SelectorFlowEnvironment.CollectBranchDiffs(beforeSwitch.Env, armState.Env);
            SelectorFlowEnvironment.MergeBranchEnv(state.Env, armState.Env);
            state.ActiveLambdas.UnionWith(armState.ActiveLambdas);
            AttachSwitchConditions(state, conditions, writes);
        }
    }

    private void AnalyzeSwitchArmGuard(
        IOperation guard, FlowState fallthroughState, FlowState armState, List<IOperation> conditions, int depth)
    {
        AnalyzeExpression(guard, armState, unconditional: false, depth);
        // Side effects from a failed guard remain visible to subsequent arms.
        var guardWrites = SelectorFlowEnvironment.CollectBranchDiffs(fallthroughState.Env, armState.Env);
        SelectorFlowEnvironment.MergeBranchEnv(fallthroughState.Env, armState.Env);
        fallthroughState.ActiveLambdas.UnionWith(armState.ActiveLambdas);
        AttachSwitchConditions(fallthroughState, conditions, guardWrites);
        AttachSwitchConditions(armState, conditions, guardWrites);
        conditions.Add(guard);
    }

    private void AttachSwitchConditions(
        FlowState state, List<IOperation> conditions, List<BranchWriteDiff>? writes)
    {
        if (writes is null) return;
        for (var j = 0; j < conditions.Count; j++)
            AttachGuard(state, conditions[j], writes);
    }

    private void AnalyzeConditionalExpression(
        IConditionalOperation conditional, FlowState state, bool unconditional, int depth)
    {
        AnalyzeExpression(conditional.Condition, state, unconditional, depth);
        var hasBranchEffects = HasFlowSideEffects(conditional.WhenTrue) ||
            (conditional.WhenFalse is { } checkFalse && HasFlowSideEffects(checkFalse));
        if (!hasBranchEffects)
        {
            AnalyzeExpression(conditional.WhenTrue, state, unconditional: false, depth);
            if (conditional.WhenFalse is { } pureFalse)
                AnalyzeExpression(pureFalse, state, unconditional: false, depth);
            return;
        }

        var alternate = SelectorFlowEnvironment.Clone(state);
        if (conditional.WhenFalse is { } whenFalse)
        {
            AnalyzeExpression(conditional.WhenTrue, state, unconditional, depth);
            AnalyzeExpression(whenFalse, alternate, unconditional, depth);
        }
        else
        {
            AnalyzeExpression(conditional.WhenTrue, alternate, unconditional, depth);
        }
        MergeBranch(state, targetExits: false, alternate, alternateExits: false, conditional.Condition);
    }

    private void AnalyzeShortCircuitBinary(
        IBinaryOperation binary, FlowState state, bool unconditional, int depth)
    {
        AnalyzeExpression(binary.LeftOperand, state, unconditional, depth);
        if (!HasFlowSideEffects(binary.RightOperand))
        {
            AnalyzeExpression(binary.RightOperand, state, unconditional: false, depth);
            return;
        }
        var right = SelectorFlowEnvironment.Clone(state);
        AnalyzeExpression(binary.RightOperand, right, unconditional, depth);
        MergeBranch(state, targetExits: false, right, alternateExits: false, binary.LeftOperand);
    }

    private bool HasFlowSideEffects(IOperation root)
    {
        _sideEffectScratch.Clear();
        _sideEffectScratch.Push(root);
        try
        {
            while (_sideEffectScratch.TryPop(out var current))
            {
                _cancellation.ThrowIfCancellationRequested();
                if (current is ILocalFunctionOperation) continue;
                var mutatesFlow = current is IAssignmentOperation or
                    IVariableDeclaratorOperation or
                    IAnonymousFunctionOperation or
                    IDeclarationPatternOperation { DeclaredSymbol: ILocalSymbol } or
                    IRecursivePatternOperation { DeclaredSymbol: ILocalSymbol };
                if (mutatesFlow || _materializedLambdas?.ContainsKey(current) == true) return true;
                if (current is IInvocationOperation call && HasMutatingCallEffect(call)) return true;
                foreach (var child in current.ChildOperations) _sideEffectScratch.Push(child);
            }
            return false;
        }
        finally
        {
            _sideEffectScratch.Clear();
        }
    }

    [SuppressMessage("SonarQube", "S3267", Justification = "Scanning Arguments directly avoids boxing ImmutableArray and allocating a predicate delegate in the hot side-effect check.")]
    private static bool HasMutatingCallEffect(IInvocationOperation call)
    {
        if (call.Instance is not null && call.Arguments.Length > 0 &&
            CollectionMutationFacts.CanStoreArgument(call.TargetMethod))
            return true;
        foreach (var argument in call.Arguments)
        {
            if (argument.Parameter?.RefKind is RefKind.Out or RefKind.Ref) return true;
        }
        return false;
    }

    private void AnalyzeCall(IInvocationOperation call, FlowState state, bool unconditional, int depth)
    {
        if (call.Instance is { } instance) AnalyzeExpression(instance, state, unconditional, depth);
        foreach (var argument in call.Arguments)
        {
            if (argument.Parameter?.RefKind == RefKind.Out)
                AnalyzeIndexerKeyExpressions(argument.Value, state, depth);
            else
                AnalyzeExpression(argument.Value, state, unconditional, depth);
        }
        RecordOutArguments(call, state, replaceOut: unconditional);
        RecordReceiverMutation(call, state);
        CaptureAtMaterialization(call, state);
    }

    private void PushExpressionChildren(IOperation operation, bool unconditional, Stack<ExpressionWalkEntry> walk)
    {
        switch (operation)
        {
            case ICoalesceOperation coalesce:
                walk.Push(new(coalesce.WhenNull, false));
                walk.Push(new(coalesce.Value, unconditional));
                return;
            case IConditionalAccessOperation conditionalAccess:
                walk.Push(new(conditionalAccess.WhenNotNull, false));
                walk.Push(new(conditionalAccess.Operation, unconditional));
                return;
            case ISwitchExpressionOperation switchExpression:
                for (var i = switchExpression.Arms.Length - 1; i >= 0; i--)
                    walk.Push(new(switchExpression.Arms[i], false));
                walk.Push(new(switchExpression.Value, unconditional));
                return;
            case IIsPatternOperation isPattern:
                walk.Push(new(isPattern.Pattern, false));
                walk.Push(new(isPattern.Value, unconditional));
                return;
            default:
                RoslynOperationHelpers.CopyChildrenTo(operation, _childScratch);
                for (var i = _childScratch.Count - 1; i >= 0; i--)
                    walk.Push(new(_childScratch[i], unconditional));
                _childScratch.Clear();
                return;
        }
    }

    private void AnalyzeIndexerKeyExpressions(IOperation target, FlowState state, int depth = 0)
    {
        IOperation? current = target;
        while (current is not null)
            current = StepIndexerKeyExpression(current, state, depth);
    }

    private IOperation? StepIndexerKeyExpression(IOperation current, FlowState state, int depth)
    {
        switch (current)
        {
            case IPropertyReferenceOperation { Property.IsIndexer: true } indexer:
                foreach (var arg in indexer.Arguments) AnalyzeExpression(arg.Value, state, depth: depth);
                return indexer.Instance;
            case IPropertyReferenceOperation property:
                return property.Instance;
            case IFieldReferenceOperation field:
                return field.Instance;
            case IArrayElementReferenceOperation element:
                foreach (var index in element.Indices) AnalyzeExpression(index, state, depth: depth);
                return element.ArrayReference;
            default:
                return null;
        }
    }

    private void BindDeconstructionTarget(
        IOperation target, IOperation value, FlowState state,
        bool replace, ImmutableArray<int> tuplePath, LocalBindingKind kind = LocalBindingKind.Value)
    {
        if (target is IDeclarationExpressionOperation declaration) target = declaration.Expression;
        var unwrappedValue = RoslynOperationHelpers.UnwrapTransparentNonNull(value);
        if (target is IVariableDeclarationGroupOperation group)
        {
            foreach (var declarationGroup in group.Declarations)
                foreach (var variable in declarationGroup.Declarators)
                    RecordLocalValue(variable.Symbol, new(value, tuplePath, kind), state, replace);
            return;
        }
        if (target is IVariableDeclaratorOperation declarator)
        {
            RecordLocalValue(declarator.Symbol, new(value, tuplePath, kind), state, replace);
            return;
        }
        if (target is ITupleOperation targetTuple)
        {
            BindTupleDeconstruction(targetTuple, value, unwrappedValue, state, replace, tuplePath, kind);
            return;
        }
        if (target is ILocalReferenceOperation localRef)
        {
            RecordLocalValue(localRef.Local, new(value, tuplePath, kind), state, replace);
            return;
        }
        if (TryGetMutatedLocal(target) is { } mutated)
            RecordLocalValue(mutated.Local, new(value, tuplePath, kind, target), state, replace: false);
    }

    private void BindTupleDeconstruction(
        ITupleOperation targetTuple, IOperation value, IOperation unwrappedValue,
        FlowState state, bool replace, ImmutableArray<int> tuplePath, LocalBindingKind kind)
    {
        var matchesDirectValueTuple = kind == LocalBindingKind.Value &&
            tuplePath.IsDefaultOrEmpty &&
            unwrappedValue is ITupleOperation valueTuple &&
            targetTuple.Elements.Length == valueTuple.Elements.Length;
        if (matchesDirectValueTuple)
        {
            var sourceTuple = (ITupleOperation)unwrappedValue;
            for (var i = 0; i < targetTuple.Elements.Length; i++)
                BindDeconstructionTarget(targetTuple.Elements[i], sourceTuple.Elements[i], state, replace, default);
            return;
        }
        for (var i = 0; i < targetTuple.Elements.Length; i++)
        {
            var nextPath = tuplePath.IsDefault ? [i] : tuplePath.Add(i);
            BindDeconstructionTarget(targetTuple.Elements[i], value, state, replace, nextPath, kind);
        }
    }

    private void RecordOutArguments(IInvocationOperation call, FlowState state, bool replaceOut)
    {
        foreach (var argument in call.Arguments)
        {
            if (argument.Parameter?.RefKind is not (RefKind.Out or RefKind.Ref)) continue;
            var target = argument.Value is IDeclarationExpressionOperation decl ? decl.Expression : argument.Value;
            if (target is ILocalReferenceOperation outLocal)
            {
                var replacesLocal = replaceOut && argument.Parameter.RefKind == RefKind.Out;
                RecordLocalValue(outLocal.Local, new(call), state, replace: replacesLocal);
            }
            else if (TryGetMutatedLocal(target) is { } mutated)
            {
                RecordLocalValue(mutated.Local, new(call, WriteTarget: target), state, replace: false);
            }
        }
    }

    private void RecordPatternLocal(IOperation current, FlowState state)
    {
        var local = current switch
        {
            IDeclarationPatternOperation declaration => declaration.DeclaredSymbol as ILocalSymbol,
            IRecursivePatternOperation recursive => recursive.DeclaredSymbol as ILocalSymbol,
            _ => null
        };
        if (local is null) return;
        RecordLocalValue(local, ProjectionOriginAnalyzer.PatternContainerMatchedValue(current), state, replace: false);
    }

    internal static ILocalReferenceOperation? TryGetMutatedLocal(IOperation target)
    {
        var current = target;
        for (var depth = 0; current is not null && depth < 16; depth++)
        {
            current = RoslynOperationHelpers.UnwrapTransparent(current);
            if (current is ILocalReferenceOperation localRef) return localRef;
            current = current is null ? null : CollectionMutationFacts.MutationReceiver(current);
        }
        return null;
    }

    private void CaptureLambdaEnvironment(IAnonymousFunctionOperation childLambda, FlowState state)
    {
        var linqCall = LinqSequenceFacts.FindEnclosingLinqCall(childLambda, _symbols);
        if (linqCall is null)
        {
            CaptureDeferredLambda(childLambda, state);
            return;
        }
        if (_session.GetExternalLocals(childLambda.Body, _cancellation).Count == 0)
            return;
        var resolver = _materializerResolver ??= new(_body, _symbols, _cancellation);
        var plan = resolver.FindMaterializers(linqCall, childLambda);
        if (plan.Materializers is { Count: > 0 })
        {
            var map = _materializedLambdas ??= [];
            foreach (var (materializer, conditions) in plan.Materializers)
            {
                if (!map.TryGetValue(materializer, out var list))
                {
                    list = [];
                    map[materializer] = list;
                }
                list.Add(new(childLambda, conditions));
            }
        }
        if (plan.HasUnknownUse || plan.Materializers is not { Count: > 0 })
        {
            CaptureDeferredLambda(childLambda, state);
            if (plan.HasUnknownUse)
                PropagateDeferredLinqLambdaOuterWrites(childLambda, state);
        }
    }

    private void CaptureDeferredLambda(IAnonymousFunctionOperation lambda, FlowState state)
    {
        CaptureCurrentValues(lambda, state);
        if (_deferredLinqWrites is { Count: > 0 } && _lambdaCaptured is not null &&
            _lambdaCaptured.TryGetValue(lambda, out var snapshot))
            SelectorFlowEnvironment.MergeBranchEnv(snapshot, _deferredLinqWrites);
        state.ActiveLambdas.Add(lambda);
    }

    private void CaptureCurrentValues(IAnonymousFunctionOperation lambda, FlowState state)
    {
        var map = _lambdaCaptured ??= [];
        if (!map.TryGetValue(lambda, out var snapshot))
        {
            map[lambda] = snapshot = new(SymbolEqualityComparer.Default);
            SelectorFlowEnvironment.MergeInto(snapshot, state.Env);
            return;
        }
        SelectorFlowEnvironment.MergeBranchEnv(snapshot, state.Env);
    }

    private void CaptureAtMaterialization(IOperation materializer, FlowState state)
    {
        if (_materializedLambdas is not { Count: > 0 } ||
            !_materializedLambdas.Remove(materializer, out var finished))
            return;
        for (var i = 0; i < finished.Count; i++)
            CaptureCurrentValues(finished[i].Lambda, state);
        ApplyMaterializedLambdaOuterWrites(finished, state);
    }

    private void ApplyMaterializedLambdaOuterWrites(List<MaterializedLambda> finished, FlowState state)
    {
        // A single materialized lambda already converged in its own Collect call; only multi-lambda pipelines need sibling passes.
        var maxPasses = finished.Count <= 1 ? 1 : Math.Min(finished.Count + 1, 4);
        for (var pass = 0; pass < maxPasses; pass++)
        {
            if (pass > 0 && !_session.HasBudget) break;
            if (!RunMaterializedLambdaPass(finished, state)) break;
            for (var i = 0; i < finished.Count; i++)
                CaptureCurrentValues(finished[i].Lambda, state);
        }
    }

    private bool RunMaterializedLambdaPass(List<MaterializedLambda> finished, FlowState state)
    {
        var tempScope = BuildCurrentParentScope(state);
        var changed = false;
        for (var i = 0; i < finished.Count; i++)
        {
            var callback = finished[i];
            var childResult = CollectChild(callback.Lambda.Body, tempScope, _facts, _session);
            if (childResult.OuterExitValues is not { Count: > 0 }) continue;
            MergeChildReferenceValues(childResult.Scope);
            if (JoinOuterExitValuesIntoState(childResult.OuterExitValues, state, callback))
                changed = true;
        }
        return changed;
    }

    private void ApplyUnstructuredMaterializedLambdaOuterWrites(IAnonymousFunctionOperation childLambda, FlowState state)
    {
        var linqCall = LinqSequenceFacts.FindEnclosingLinqCall(childLambda, _symbols);
        if (linqCall is null || _session.GetExternalLocals(childLambda.Body, _cancellation).Count == 0) return;
        var resolver = _materializerResolver ??= new(_body, _symbols, _cancellation);
        if (resolver.FindMaterializers(linqCall, childLambda).Materializers is not { Count: > 0 } materializers) return;
        var childResult = CollectChild(childLambda.Body, BuildCurrentParentScope(state), _facts, _session);
        if (childResult.OuterExitValues is not { Count: > 0 }) return;
        MergeChildReferenceValues(childResult.Scope);
        var callback = new MaterializedLambda(childLambda, [.. materializers.Values.SelectMany(conditions => conditions).Distinct()]);
        JoinOuterExitValuesIntoState(childResult.OuterExitValues, state, callback);
    }

    private void PropagateDeferredLinqLambdaOuterWrites(IAnonymousFunctionOperation childLambda, FlowState state)
    {
        var childResult = CollectChild(childLambda.Body, BuildCurrentParentScope(state), _facts, _session);
        if (childResult.OuterExitValues is not { Count: > 0 }) return;
        MergeChildReferenceValues(childResult.Scope);
        foreach (var (local, bindings) in childResult.OuterExitValues)
        {
            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                var effective = binding.ReplacesMember ? binding with { ReplacesMember = false } : binding;
                // Record for deferred sibling lambdas without mutating parent state before enumeration.
                var deferred = _deferredLinqWrites ??= new(SymbolEqualityComparer.Default);
                if (!deferred.TryGetValue(local, out var deferredList))
                {
                    deferredList = [];
                    deferred[local] = deferredList;
                }
                SelectorFlowEnvironment.AddUnique(deferredList, effective);
                foreach (var active in state.ActiveLambdas)
                    AddLambdaCapture(active, local, effective);
                if (!_captured.TryGetValue(local, out var capturedList))
                {
                    if (!IsSelectorLocal(local)) (_outerWrites ??= new(SymbolEqualityComparer.Default)).Add(local);
                    capturedList = [];
                    _captured[local] = capturedList;
                }
                SelectorFlowEnvironment.AddUnique(capturedList, effective);
            }
        }
    }

    private SelectorScope BuildCurrentParentScope(FlowState state) =>
        new(_referenceValues, SelectorFlowEnvironment.ToImmutableMap(state.Env),
            SelectorScope.EmptyLambdaCaptures, _parentScope);

    private bool JoinOuterExitValuesIntoState(
        Dictionary<ILocalSymbol, ImmutableArray<LocalBinding>> outerExitValues, FlowState state, MaterializedLambda callback)
    {
        var changed = false;
        foreach (var (local, bindings) in outerExitValues)
        {
            for (var i = 0; i < bindings.Length; i++)
            {
                var binding = bindings[i];
                var joined = binding.ReplacesMember ? binding with { ReplacesMember = false } : binding;
                var newBinding = !state.Env.TryGetValue(local, out var existing) || !SelectorFlowEnvironment.ContainsBinding(existing, joined);
                changed |= newBinding;
                // Sequence may be empty, so join with incoming bindings instead of replacing.
                RecordLocalValue(local, joined, state, replace: false);
                // A later enumeration can repeat this write under a different guard.
                var isUnchangedExternalBinding = !newBinding &&
                    !callback.Lambda.Syntax.Span.Contains((joined.WriteTarget ?? joined.Value).Syntax.Span);
                if (callback.Conditions.IsEmpty || isUnchangedExternalBinding)
                    continue;
                foreach (var condition in callback.Conditions)
                {
                    var guard = new LocalBinding(condition, Kind: LocalBindingKind.Condition, WriteTarget: joined.WriteTarget);
                    if (SelectorFlowEnvironment.ContainsBinding(state.Env[local], guard)) continue;
                    RecordLocalValue(local, guard, state, replace: false);
                    changed = true;
                }
            }
        }
        return changed;
    }

    private void MergeChildReferenceValues(SelectorScope? childScope)
    {
        if (childScope is null ||
            childScope.ReferenceValues.Count == 0 ||
            ReferenceEquals(childScope.ReferenceValues, _referenceValues))
            return;
        foreach (var (reference, bindings) in childScope.ReferenceValues)
        {
            if (!_referenceValues.TryGetValue(reference, out var existing))
            {
                _referenceValues[reference] = bindings;
                continue;
            }
            var merged = new List<LocalBinding>(existing.Length + bindings.Length);
            for (var i = 0; i < existing.Length; i++) merged.Add(existing[i]);
            for (var i = 0; i < bindings.Length; i++) SelectorFlowEnvironment.AddUnique(merged, bindings[i]);
            if (merged.Count > existing.Length)
                _referenceValues[reference] = [.. merged];
        }
    }

    private void AddLambdaCapture(IAnonymousFunctionOperation lambda, ILocalSymbol local, LocalBinding binding)
    {
        if (_lambdaCaptured is null || !_lambdaCaptured.TryGetValue(lambda, out var snapshot)) return;
        if (!snapshot.TryGetValue(local, out var list))
        {
            list = [];
            snapshot[local] = list;
        }
        SelectorFlowEnvironment.AddUnique(list, binding);
    }

    private void SnapshotReference(ILocalReferenceOperation reference, Dictionary<ILocalSymbol, List<LocalBinding>> env)
    {
        if (!reference.IsDeclaration && env.TryGetValue(reference.Local, out var values))
            _referenceValues[reference] = [.. values];
    }

    private void RecordReceiverMutation(IInvocationOperation call, FlowState state)
    {
        var storesIntoReceiver = call.Instance is not null &&
            call.Arguments.Length > 0 &&
            CollectionMutationFacts.CanStoreArgument(call.TargetMethod);
        if (storesIntoReceiver && TryGetMutatedLocal(call.Instance!) is { } mutated)
            RecordLocalValue(mutated.Local, new(call, Kind: LocalBindingKind.ReceiverMutation, WriteTarget: call.Instance), state, replace: false);
    }

    private void RecordLocalValue(ILocalSymbol local, LocalBinding? value, FlowState state, bool replace)
    {
        if (!_captured.TryGetValue(local, out var capturedList))
        {
            if (!IsSelectorLocal(local)) (_outerWrites ??= new(SymbolEqualityComparer.Default)).Add(local);
            capturedList = [];
            _captured[local] = capturedList;
        }
        if (value is { } binding)
        {
            var capturedBinding = binding.ReplacesMember ? binding with { ReplacesMember = false } : binding;
            SelectorFlowEnvironment.AddUnique(capturedList, capturedBinding);
            foreach (var lambda in state.ActiveLambdas) AddLambdaCapture(lambda, local, capturedBinding);
        }
        if (replace || !state.Env.TryGetValue(local, out var list))
        {
            list = [];
            state.Env[local] = list;
        }
        if (value is { } envBinding) SelectorFlowEnvironment.AddUnique(list, envBinding);
    }

    private bool IsSelectorLocal(ILocalSymbol local) =>
        SelectorFlowSession.IsDeclaredWithin(local, _body.Syntax.SyntaxTree, _body.Syntax.Span);
}
