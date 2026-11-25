using Microsoft.CodeAnalysis.Operations;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal readonly record struct QueryMaterializationPlan(
    HashSet<IOperation>? Materializers,
    bool HasUnknownUse);

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

    internal QueryMaterializationPlan FindMaterializers(IInvocationOperation queryCall)
    {
        if (RoslynOperationHelpers.TryGetArgumentByOrdinal(queryCall, 0) is { } firstArg &&
            LinqSequenceFacts.IsMaterializingLinqCall(queryCall.TargetMethod, firstArg, symbols))
            return new([queryCall], HasUnknownUse: false);
        HashSet<IOperation>? materializers = null;
        var hasUnknownUse = false;
        var pending = new Stack<IOperation>();
        var visited = new HashSet<IOperation>();
        pending.Push(queryCall);
        while (pending.TryPop(out var current))
        {
            cancellation.ThrowIfCancellationRequested();
            if (!visited.Add(current)) continue;
            var parent = ClimbTransparentParents(ref current);
            if (TryGetAssignedQueryLocal(current, parent) is { } assignedLocal)
            {
                if (!TryFollowQueryLocal(assignedLocal, parent!, pending))
                    hasUnknownUse = true;
                continue;
            }
            if (!TryAdvanceMaterializerSearch(parent, ref materializers, pending))
                hasUnknownUse = true;
        }
        return new(materializers, hasUnknownUse);
    }

    private static IOperation? ClimbTransparentParents(ref IOperation current)
    {
        var parent = current.Parent;
        while (parent is IParenthesizedOperation or IConversionOperation { Conversion.IsUserDefined: false } ||
               (parent is IConditionalAccessOperation conditionalAccess && ReferenceEquals(current, conditionalAccess.WhenNotNull)))
        {
            current = parent;
            parent = parent.Parent;
        }
        return parent;
    }

    private static ILocalSymbol? TryGetAssignedQueryLocal(IOperation value, IOperation? parent) => parent switch
    {
        IVariableInitializerOperation { Parent: IVariableDeclaratorOperation declarator } => declarator.Symbol,
        ISimpleAssignmentOperation { Target: ILocalReferenceOperation target } assignment
            when ReferenceEquals(assignment.Value, value) && assignment.Parent is IExpressionStatementOperation => target.Local,
        _ => null
    };

    private bool TryAdvanceMaterializerSearch(
        IOperation? parent, ref HashSet<IOperation>? materializers, Stack<IOperation> pending)
    {
        if (parent is ISpreadOperation { Parent: ICollectionExpressionOperation } spread)
        {
            (materializers ??= []).Add(spread);
            return true;
        }
        if (parent is not IArgumentOperation argument)
            return false;
        if (argument.Parent is IObjectCreationOperation creation &&
            LinqSequenceFacts.IsMaterializingCollectionCreation(creation, symbols))
        {
            (materializers ??= []).Add(creation);
            return true;
        }
        if (argument.Parent is not IInvocationOperation { Instance: null } outerCall)
            return false;
        if (LinqSequenceFacts.IsTryGetNonEnumeratedCount(outerCall.TargetMethod, argument, symbols))
            return true;
        if (LinqSequenceFacts.IsMaterializingLinqCall(outerCall.TargetMethod, argument, symbols))
        {
            (materializers ??= []).Add(outerCall);
            return true;
        }
        if (LinqSequenceFacts.PreservesSequenceFlow(outerCall.TargetMethod, argument, symbols))
        {
            pending.Push(outerCall);
            return true;
        }
        return false;
    }

    private bool TryFollowQueryLocal(ILocalSymbol local, IOperation write, Stack<IOperation> pending)
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
            pending.Push(use);
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
