using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal sealed class SelectorFlowCollector
{
    private readonly record struct ExpressionWalkEntry(
        IOperation Operation,
        bool Unconditional,
        bool IsExit = false);

    private readonly record struct UnstructuredWalkEntry(
        IOperation Operation,
        FlowState? Before = null,
        IOperation? SingleCondition = null,
        ISwitchOperation? SwitchOwner = null,
        ISwitchCaseOperation? SwitchSection = null);

    private readonly ConventionFacts _facts;
    private readonly ConventionSymbols _symbols;
    private readonly CancellationToken _cancellation;
    private readonly IBlockOperation _body;
    private readonly List<IOperation> _returns = [];
    private readonly Dictionary<ILocalReferenceOperation, ImmutableArray<LocalBinding>> _referenceValues = [];
    private readonly Dictionary<ILocalSymbol, List<LocalBinding>> _captured = new(SymbolEqualityComparer.Default);
    private readonly List<IOperation> _childScratch = [];
    private readonly Stack<IOperation> _sideEffectScratch = [];

    private List<IOperation>? _returnConditions;
    private Dictionary<IAnonymousFunctionOperation, Dictionary<ILocalSymbol, List<LocalBinding>>>? _lambdaCaptured;
    private Dictionary<IOperation, List<IAnonymousFunctionOperation>>? _materializedLambdas;
    private HashSet<ILocalSymbol>? _outerWrites;
    private Dictionary<ILocalSymbol, List<LocalBinding>>? _outerExitValues;
    private Dictionary<IOperation, bool>? _containsReturnCache;
    private QueryMaterializerResolver? _materializerResolver;

    private SelectorFlowCollector(IBlockOperation body, ConventionFacts facts)
    {
        _body = body;
        _facts = facts;
        _symbols = facts.Symbols;
        _cancellation = facts.Cancellation;
    }

    internal static SelectorFlowResult Collect(IBlockOperation body, SelectorScope? parentScope, ConventionFacts facts)
    {
        var unstructured = UnstructuredFlowHelper.HasUnstructuredJump(body, facts.Cancellation);
        var collector = new SelectorFlowCollector(body, facts);
        collector.AnalyzeBody(SelectorFlowEnvironment.CreateInitialState(parentScope), unstructured);
        if (collector._outerExitValues is { Count: > 0 })
        {
            // A later element can read a captured value left by an earlier invocation.
            var initial = SelectorFlowEnvironment.CreateInitialState(parentScope);
            SelectorFlowEnvironment.MergeBranchEnv(initial.Env, collector._outerExitValues);
            collector = new SelectorFlowCollector(body, facts);
            collector.AnalyzeBody(initial, unstructured);
        }
        var hasScopeData = collector._referenceValues.Count > 0 ||
            collector._captured.Count > 0 ||
            collector._lambdaCaptured is { Count: > 0 };
        var scope = hasScopeData ? collector.BuildScope(parentScope) : parentScope;
        return new(collector._returns, collector._returnConditions, scope);
    }

    internal static SelectorScope? SeedEnclosingSelectorScopes(
        IOperation root, ConventionFacts facts, Dictionary<IAnonymousFunctionOperation, SelectorFlowResult>? cache)
    {
        List<IAnonymousFunctionOperation>? lambdas = null;
        for (var current = root.Parent; current is not null; current = current.Parent)
        {
            facts.Cancellation.ThrowIfCancellationRequested();
            if (current is IAnonymousFunctionOperation lambda &&
                LinqSequenceFacts.FindEnclosingSelectCall(lambda, facts.Symbols) is not null)
            {
                (lambdas ??= []).Add(lambda);
            }
        }
        if (lambdas is null) return null;
        SelectorScope? scope = null;
        for (var i = lambdas.Count - 1; i >= 0; i--)
            scope = GetOrCollect(lambdas[i], scope, facts, cache).Scope;
        return scope;
    }

    internal static SelectorFlowResult GetOrCollect(
        IAnonymousFunctionOperation lambda, SelectorScope? parentScope,
        ConventionFacts facts, Dictionary<IAnonymousFunctionOperation, SelectorFlowResult>? cache)
    {
        if (cache is not null && cache.TryGetValue(lambda, out var cached))
            return cached;
        var effectiveParent = parentScope?.ForChild(lambda);
        var collected = Collect(lambda.Body, effectiveParent, facts);
        if (cache is not null) cache[lambda] = collected;
        return collected;
    }

    private void AnalyzeBody(FlowState state, bool unstructured)
    {
        if (unstructured) AnalyzeUnstructuredStatement(_body, state);
        else AnalyzeStatement(_body, state);
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
        foreach (var statement in block.Operations)
        {
            _cancellation.ThrowIfCancellationRequested();
            if (AnalyzeStatement(statement, state, depth + 1)) return true;
        }
        return false;
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
            AnalyzeExpression(assignment.Value, state, valueUnconditional, depth);
            var replacesLocal = allowReplace && assignment is ISimpleAssignmentOperation;
            RecordLocalValue(targetLocal.Local, new(assignment.Value), state, replace: replacesLocal);
            return;
        }

        AnalyzeIndexerKeyExpressions(assignment.Target, state, depth);
        AnalyzeExpression(assignment.Value, state, valueUnconditional, depth);
        if (TryGetMutatedLocal(assignment.Target) is not { } mutated) return;

        var replacesMember = TryReplacePriorDirectMemberWrites(assignment, mutated.Local, allowReplace, state);
        RecordLocalValue(mutated.Local,
            new(assignment.Value, WriteTarget: assignment.Target, ReplacesMember: replacesMember),
            state, replace: false);
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
            AnalyzeExpression(value, state, unconditional: true);
            _returns.Add(value);
            RecordOuterExitValues(state);
        }
        return true;
    }

    private bool AnalyzeThrow(IThrowOperation thrown, FlowState state)
    {
        if (thrown.Exception is { } exception) AnalyzeExpression(exception, state, unconditional: true);
        return true;
    }

    private bool AnalyzeConditionalStatement(IConditionalOperation conditional, FlowState state, int depth)
    {
        AnalyzeExpression(conditional.Condition, state, unconditional: true);
        var returnsBefore = _returns.Count;
        var alternate = SelectorFlowEnvironment.Clone(state);
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
        if (_returns.Count > returnsBefore) AddReturnCondition(conditional.Condition);
        return MergeBranch(state, targetExits, alternate, alternateExits, conditional.Condition);
    }

    private void AddReturnCondition(IOperation condition) =>
        (_returnConditions ??= []).Add(condition);

    private bool MergeBranch(FlowState target, bool targetExits, FlowState alternate, bool alternateExits, IOperation guard)
    {
        if (targetExits && alternateExits) return true;
        if (targetExits)
        {
            SelectorFlowEnvironment.Replace(target, alternate);
            return false;
        }
        if (alternateExits) return false;
        var writes = SelectorFlowEnvironment.CollectBranchDiffs(target.Env, alternate.Env);
        SelectorFlowEnvironment.MergeBranchEnv(target.Env, alternate.Env);
        target.ActiveLambdas.UnionWith(alternate.ActiveLambdas);
        if (writes is not null) AttachGuard(target, guard, writes);
        return false;
    }

    private void AttachGuard(FlowState state, IOperation guard, List<(ILocalSymbol Local, IOperation? Target)> writes)
    {
        for (var i = 0; i < writes.Count; i++)
        {
            var (local, writeTarget) = writes[i];
            RecordLocalValue(local, new(guard, Kind: LocalBindingKind.Condition, WriteTarget: writeTarget), state, replace: false);
        }
    }

    private void AttachSwitchGuards(
        FlowState state, ISwitchOperation owner, ISwitchCaseOperation section,
        List<(ILocalSymbol Local, IOperation? Target)>? writes)
    {
        if (writes is null) return;
        var conditions = new List<IOperation>();
        UnstructuredFlowHelper.CollectSwitchConditions(owner, section, conditions);
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
                var diffs = SelectorFlowEnvironment.CollectBranchDiffs(before.Env, state.Env);
                if (entry.SingleCondition is { } condition && diffs is not null)
                    AttachGuard(state, condition, diffs);
                else if (entry.SwitchOwner is { } owner && entry.SwitchSection is { } section)
                    AttachSwitchGuards(state, owner, section, diffs);
                continue;
            }
            var current = entry.Operation;
            if (current is IAnonymousFunctionOperation or ILocalFunctionOperation) continue;
            PushGoverningConditionSentinel(current, state, walk);
            RecordUnstructuredAssignment(current, state);

            RoslynOperationHelpers.CopyChildrenTo(current, _childScratch);
            for (var i = _childScratch.Count - 1; i >= 0; i--) walk.Push(new(_childScratch[i]));
            _childScratch.Clear();
        }
    }

    private void PushGoverningConditionSentinel(
        IOperation current, FlowState state, Stack<UnstructuredWalkEntry> walk)
    {
        switch (current.Parent)
        {
            case ISwitchOperation owner when current is ISwitchCaseOperation section:
                walk.Push(new(current, SelectorFlowEnvironment.Clone(state), SwitchOwner: owner, SwitchSection: section));
                return;
            case IConditionalOperation conditional when !ReferenceEquals(current, conditional.Condition):
                walk.Push(new(current, SelectorFlowEnvironment.Clone(state), SingleCondition: conditional.Condition));
                return;
            case IBinaryOperation binary when IsShortCircuitBinary(binary) && ReferenceEquals(current, binary.RightOperand):
                walk.Push(new(current, SelectorFlowEnvironment.Clone(state), SingleCondition: binary.LeftOperand));
                return;
            case ILoopOperation loop when ReferenceEquals(current, loop.Body) &&
                UnstructuredFlowHelper.LoopCondition(loop, _symbols) is { } condition:
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
                _returns.Add(returned);
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
        if (current is ILoopOperation loop && loop is not IWhileLoopOperation { ConditionIsTop: false })
        {
            if (UnstructuredFlowHelper.LoopCondition(loop, _symbols) is { } condition &&
                UnstructuredFlowHelper.ContainsReturn(loop.Body, ref _containsReturnCache, _cancellation))
                AddReturnCondition(condition);
            return;
        }
        if (current is ICatchClauseOperation { Filter: { } filter } clause)
        {
            if (UnstructuredFlowHelper.ContainsReturn(clause.Handler, ref _containsReturnCache, _cancellation))
                AddReturnCondition(filter);
            return;
        }
        if (current is IConditionalOperation conditional)
        {
            var hasReturnInBranch = UnstructuredFlowHelper.ContainsReturn(conditional.WhenTrue, ref _containsReturnCache, _cancellation) ||
                (conditional.WhenFalse is { } whenFalse &&
                 UnstructuredFlowHelper.ContainsReturn(whenFalse, ref _containsReturnCache, _cancellation));
            if (hasReturnInBranch) AddReturnCondition(conditional.Condition);
            return;
        }
        if (current is ISwitchOperation switchStatement &&
            UnstructuredFlowHelper.SwitchContainsReturn(switchStatement, ref _containsReturnCache, _cancellation))
            UnstructuredFlowHelper.CollectSwitchConditions(switchStatement, selected: null, _returnConditions ??= []);
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
                if (mutatesFlow) return true;
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
        for (IOperation? current = target; current is not null;)
        {
            if (current is IPropertyReferenceOperation property)
            {
                if (property.Property.IsIndexer)
                    foreach (var arg in property.Arguments) AnalyzeExpression(arg.Value, state, depth: depth);
                current = property.Instance;
            }
            else if (current is IFieldReferenceOperation field)
            {
                current = field.Instance;
            }
            else if (current is IArrayElementReferenceOperation element)
            {
                foreach (var index in element.Indices) AnalyzeExpression(index, state, depth: depth);
                current = element.ArrayReference;
            }
            else
            {
                return;
            }
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

    private static ILocalReferenceOperation? TryGetMutatedLocal(IOperation target)
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
        var select = LinqSequenceFacts.FindEnclosingSelectCall(childLambda, _symbols);
        var resolver = select is null ? null : (_materializerResolver ??= new(_body, _symbols, _cancellation));
        var materializers = resolver?.FindMaterializers(select!);
        if (materializers is not { Count: > 0 })
        {
            CaptureDeferredLambda(childLambda, state);
            return;
        }
        var map = _materializedLambdas ??= [];
        foreach (var materializer in materializers)
        {
            if (!map.TryGetValue(materializer, out var list))
            {
                list = [];
                map[materializer] = list;
            }
            list.Add(childLambda);
        }
    }

    private void CaptureDeferredLambda(IAnonymousFunctionOperation lambda, FlowState state)
    {
        CaptureCurrentValues(lambda, state);
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
            CaptureCurrentValues(finished[i], state);
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

    private bool IsSelectorLocal(ILocalSymbol local)
    {
        var bodyTree = _body.Syntax.SyntaxTree;
        var bodySpan = _body.Syntax.Span;
        foreach (var declaration in local.DeclaringSyntaxReferences)
        {
            if (declaration.SyntaxTree == bodyTree && bodySpan.Contains(declaration.Span))
                return true;
        }
        return false;
    }
}
