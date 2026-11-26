using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal readonly record struct QueryMaterializationPlan(
    Dictionary<IOperation, ImmutableArray<IOperation>>? Materializers,
    bool HasUnknownUse);

internal readonly record struct QueryUse(
    IOperation Operation,
    IOperation? Condition = null);

// DRNDTO003 scope freeze: fix only real reported bugs (see ResponseProjectionAnalysis).
internal sealed class QueryMaterializerResolver(
    IBlockOperation body,
    ConventionSymbols symbols,
    CancellationToken cancellation)
{
    private Dictionary<ILocalSymbol, List<ILocalReferenceOperation>>? _localUses;

    internal static bool IsMaterializingOperation(IOperation operation, ConventionSymbols symbols) => operation switch
    {
        ISpreadOperation { Parent: ICollectionExpressionOperation } => true,
        IObjectCreationOperation creation => LinqSequenceFacts.IsMaterializingCollectionCreation(creation, symbols),
        _ => false
    };

    internal QueryMaterializationPlan FindMaterializers(IInvocationOperation queryCall, IAnonymousFunctionOperation lambda)
    {
        var iteration = LinqSequenceFacts.LambdaIterationConditions(queryCall, lambda, symbols, cancellation);
        if (RoslynOperationHelpers.TryGetArgumentByOrdinal(queryCall, 0) is { } firstArg &&
            LinqSequenceFacts.IsMaterializingLinqCall(queryCall.TargetMethod, firstArg, symbols))
            return new(new() { [queryCall] = iteration }, HasUnknownUse: false);
        Dictionary<IOperation, ImmutableArray<IOperation>>? materializers = null;
        var hasUnknownUse = false;
        var pending = new Stack<QueryUse>();
        var visited = new HashSet<QueryUse>();
        pending.Push(new(queryCall));
        while (pending.TryPop(out var use))
        {
            cancellation.ThrowIfCancellationRequested();
            if (!visited.Add(use)) continue;
            if (!StepQueryUse(use, iteration, pending, ref materializers))
                hasUnknownUse = true;
        }
        return new(materializers, hasUnknownUse);
    }

    private bool StepQueryUse(
        QueryUse use, ImmutableArray<IOperation> iteration, Stack<QueryUse> pending,
        ref Dictionary<IOperation, ImmutableArray<IOperation>>? materializers)
    {
        var current = use.Operation;
        var parent = current.Parent;
        if (RoslynOperationHelpers.ConditionalInstance(current, cancellation) is { } instance)
        {
            pending.Push(use with { Operation = instance });
            return true;
        }
        if (parent is not null && TryForwardParentUse(use, current, parent, pending))
            return true;
        if (TryGetAssignedQueryLocal(current, parent) is { } assignedLocal)
            return TryFollowQueryLocal(assignedLocal, parent!, use.Condition, pending);
        if (Materializer(parent) is { } materializer)
        {
            RecordMaterializer(materializer, use.Condition, iteration, ref materializers);
            return true;
        }
        return parent is IArgumentOperation { Parent: IInvocationOperation { Instance: null } consumer } argument &&
               TryHandleArgumentConsumer(use, consumer, argument, pending);
    }

    private static bool TryForwardParentUse(
        QueryUse use, IOperation current, IOperation parent, Stack<QueryUse> pending)
    {
        if (!RoslynOperationHelpers.ForwardsOperandValue(parent, current))
            return false;
        pending.Push(use with { Operation = parent });
        // Carry each guard independently so converging aliases do not multiply paths.
        if (use.Condition is null && SelectionCondition(parent, current) is { } guard)
            pending.Push(new(parent, guard));
        return true;
    }

    private static void RecordMaterializer(
        IOperation materializer, IOperation? condition, ImmutableArray<IOperation> iteration,
        ref Dictionary<IOperation, ImmutableArray<IOperation>>? materializers)
    {
        materializers ??= [];
        if (!materializers.TryGetValue(materializer, out var conditions)) conditions = iteration;
        if (condition is not null && !conditions.Contains(condition)) conditions = conditions.Add(condition);
        materializers[materializer] = conditions;
    }

    private bool TryHandleArgumentConsumer(
        QueryUse use, IInvocationOperation consumer, IArgumentOperation argument, Stack<QueryUse> pending)
    {
        if (LinqSequenceFacts.IsTryGetNonEnumeratedCount(consumer.TargetMethod, argument, symbols))
            return true;
        if (!LinqSequenceFacts.PreservesSequenceFlow(consumer.TargetMethod, argument, symbols))
            return false;
        pending.Push(use with { Operation = consumer });
        return true;
    }

    private static IOperation? SelectionCondition(IOperation parent, IOperation value) => parent switch
    {
        IConditionalOperation conditional => conditional.Condition,
        ICoalesceOperation coalesce when ReferenceEquals(value, coalesce.WhenNull) => coalesce.Value,
        IConditionalAccessOperation access => access.Operation,
        _ => null
    };

    private static ILocalSymbol? TryGetAssignedQueryLocal(IOperation value, IOperation? parent) => parent switch
    {
        IVariableInitializerOperation { Parent: IVariableDeclaratorOperation declarator } => declarator.Symbol,
        ISimpleAssignmentOperation { Target: ILocalReferenceOperation target } assignment
            when ReferenceEquals(assignment.Value, value) && assignment.Parent is IExpressionStatementOperation => target.Local,
        _ => null
    };

    private IOperation? Materializer(IOperation? parent) => parent switch
    {
        ISpreadOperation { Parent: ICollectionExpressionOperation } spread => spread,
        IArgumentOperation { Parent: IObjectCreationOperation creation }
            when LinqSequenceFacts.IsMaterializingCollectionCreation(creation, symbols) => creation,
        IArgumentOperation { Parent: IInvocationOperation { Instance: null } call } argument
            when LinqSequenceFacts.IsMaterializingLinqCall(call.TargetMethod, argument, symbols) => call,
        _ => null
    };

    private bool TryFollowQueryLocal(ILocalSymbol local, IOperation write, IOperation? condition, Stack<QueryUse> pending)
    {
        if (!IsDeclaredInBody(local)) return false;
        var usesByLocal = GetOrBuildLocalUses();
        if (!usesByLocal.TryGetValue(local, out var uses)) return true;

        var allKnown = true;
        for (var i = 0; i < uses.Count; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            var use = uses[i];
            if (ReferenceEquals(use.Parent, write)) continue;
            if (IsWriteOrEscapingClosureUse(use))
            {
                allKnown = false;
                continue;
            }
            pending.Push(new(use, condition));
        }
        return allKnown;
    }

    private bool IsWriteOrEscapingClosureUse(ILocalReferenceOperation use)
    {
        // A write or an escape to another closure leaves the enumeration time unknown.
        var isRewritten = use.Parent is IAssignmentOperation assignment && ReferenceEquals(assignment.Target, use);
        var isByRefArgument = use.Parent is IArgumentOperation { Parameter.RefKind: RefKind.Ref or RefKind.Out };
        if (isRewritten || isByRefArgument) return true;
        for (var ancestor = use.Parent; ancestor is not null && !ReferenceEquals(ancestor, body); ancestor = ancestor.Parent)
        {
            if (ancestor is IAnonymousFunctionOperation or ILocalFunctionOperation)
                return true;
        }
        return false;
    }

    private bool IsDeclaredInBody(ILocalSymbol local)
    {
        var bodyTree = body.Syntax.SyntaxTree;
        var bodySpan = body.Syntax.Span;
        foreach (var declaration in local.DeclaringSyntaxReferences)
        {
            if (declaration.SyntaxTree == bodyTree && bodySpan.Contains(declaration.Span))
                return true;
        }
        return false;
    }

    private Dictionary<ILocalSymbol, List<ILocalReferenceOperation>> GetOrBuildLocalUses()
    {
        if (_localUses is not null) return _localUses;
        var index = new Dictionary<ILocalSymbol, List<ILocalReferenceOperation>>(SymbolEqualityComparer.Default);
        var pending = new Stack<IOperation>();
        pending.Push(body);
        while (pending.TryPop(out var current))
        {
            cancellation.ThrowIfCancellationRequested();
            if (current is ILocalReferenceOperation localRef)
            {
                if (!index.TryGetValue(localRef.Local, out var list))
                {
                    list = [];
                    index[localRef.Local] = list;
                }
                list.Add(localRef);
            }
            foreach (var child in current.ChildOperations)
                pending.Push(child);
        }
        _localUses = index;
        return index;
    }
}
