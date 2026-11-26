using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;

namespace DRN.Framework.SharedKernel.Analyzers.Conventions;

internal sealed class SelectorFlowSession
{
    private const int MaxBodyCollections = 128;
    private int _remainingCollections = MaxBodyCollections;
    private readonly Dictionary<IBlockOperation, HashSet<ILocalSymbol>> _externalLocals = [];
    private readonly Dictionary<IBlockOperation, List<MemoizedBodyFlow>> _memo = [];

    internal bool HasBudget => _remainingCollections > 0;

    internal bool TryConsumeCollection()
    {
        if (_remainingCollections <= 0) return false;
        _remainingCollections--;
        return true;
    }

    internal Dictionary<ILocalSymbol, ImmutableArray<LocalBinding>> CaptureIncoming(
        IBlockOperation body, SelectorScope? parentScope, CancellationToken cancellation)
    {
        var result = new Dictionary<ILocalSymbol, ImmutableArray<LocalBinding>>(SymbolEqualityComparer.Default);
        if (parentScope is null) return result;
        var external = GetExternalLocals(body, cancellation);
        if (external.Count == 0) return result;
        for (var scope = parentScope; scope is not null; scope = scope.Parent)
        {
            foreach (var (local, values) in scope.CapturedLocalValues)
            {
                if (external.Contains(local) && !result.ContainsKey(local))
                    result[local] = values;
            }
        }
        return result;
    }

    internal bool TryGetMemoized(
        IBlockOperation body, Dictionary<ILocalSymbol, ImmutableArray<LocalBinding>> incoming,
        out SelectorFlowResult result)
    {
        if (_memo.TryGetValue(body, out var entries))
        {
            for (var i = 0; i < entries.Count; i++)
            {
                if (SameIncomingBindings(entries[i].Incoming, incoming))
                {
                    result = entries[i].Result;
                    return true;
                }
            }
        }
        result = default;
        return false;
    }

    internal void StoreMemoized(
        IBlockOperation body, Dictionary<ILocalSymbol, ImmutableArray<LocalBinding>> incoming,
        SelectorFlowResult result)
    {
        if (!_memo.TryGetValue(body, out var entries))
        {
            entries = [];
            _memo[body] = entries;
        }
        entries.Add(new(incoming, result));
    }

    internal HashSet<ILocalSymbol> GetExternalLocals(IBlockOperation body, CancellationToken cancellation)
    {
        if (_externalLocals.TryGetValue(body, out var cached))
            return cached;
        var result = new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);
        var bodyTree = body.Syntax.SyntaxTree;
        var bodySpan = body.Syntax.Span;
        var walk = new Stack<IOperation>();
        walk.Push(body);
        while (walk.TryPop(out var current))
        {
            cancellation.ThrowIfCancellationRequested();
            var referencedLocal = (current as ILocalReferenceOperation)?.Local;
            var isExternalLocal = referencedLocal is not null && !IsDeclaredWithin(referencedLocal, bodyTree, bodySpan);
            if (isExternalLocal)
                result.Add(referencedLocal!);
            foreach (var child in current.ChildOperations)
                walk.Push(child);
        }
        _externalLocals[body] = result;
        return result;
    }

    internal static bool IsDeclaredWithin(ISymbol symbol, SyntaxTree bodyTree, TextSpan bodySpan)
    {
        foreach (var declaration in symbol.DeclaringSyntaxReferences)
        {
            if (declaration.SyntaxTree == bodyTree && bodySpan.Contains(declaration.Span))
                return true;
        }
        return false;
    }

    private static bool SameIncomingBindings(
        Dictionary<ILocalSymbol, ImmutableArray<LocalBinding>> left,
        Dictionary<ILocalSymbol, ImmutableArray<LocalBinding>> right)
    {
        if (left.Count != right.Count) return false;
        foreach (var (local, leftValues) in left)
        {
            if (!right.TryGetValue(local, out var rightValues) || leftValues.Length != rightValues.Length)
                return false;
            if (!SameBindingSet(leftValues, rightValues))
                return false;
        }
        return true;
    }

    private static bool SameBindingSet(ImmutableArray<LocalBinding> left, ImmutableArray<LocalBinding> right)
    {
        if (SameBindingOrder(left, right))
            return true;
        for (var i = 0; i < left.Length; i++)
        {
            if (!ContainsBinding(right, left[i]))
                return false;
        }
        return true;
    }

    private static bool SameBindingOrder(ImmutableArray<LocalBinding> left, ImmutableArray<LocalBinding> right)
    {
        for (var i = 0; i < left.Length; i++)
        {
            if (!LocalBindingComparer.Instance.Equals(left[i], right[i]))
                return false;
        }
        return true;
    }

    private static bool ContainsBinding(ImmutableArray<LocalBinding> bindings, LocalBinding target)
    {
        for (var i = 0; i < bindings.Length; i++)
        {
            if (LocalBindingComparer.Instance.Equals(bindings[i], target))
                return true;
        }
        return false;
    }
}
