using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;

namespace DRN.Framework.CompilerTools;

// Owned by one compilation or generation pass. Values must be immutable, context-free facts.
internal sealed class SymbolFactCache<TSymbol, TValue>(int capacity = 1024) where TSymbol : class, ISymbol
{
    private readonly ConcurrentDictionary<TSymbol, TValue> _values =
        new(concurrencyLevel: -1, capacity: capacity, comparer: SymbolEqualityComparer.Default);

    internal TValue GetOrAdd(TSymbol symbol, Func<TSymbol, CancellationToken, TValue> calculate,
        CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (_values.TryGetValue(symbol, out var value)) return value;

        // Concurrent misses may calculate twice. Do not retain exceptions or canceled calculations.
        value = calculate(symbol, cancellation);
        cancellation.ThrowIfCancellationRequested();
        return _values.GetOrAdd(symbol, value);
    }
}
