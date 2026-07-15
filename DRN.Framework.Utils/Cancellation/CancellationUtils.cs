using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Utils.Cancellation;

/// <summary>
/// Owns the explicit root plus stable named child scopes for the current dependency-injection service scope.
/// </summary>
/// <remarks>
/// Use <see cref="Root"/> only for cancel-all behavior and <see cref="GetOrCreateScope"/> for a component or workflow group.
/// Use a caller-owned linked <see cref="CancellationTokenSource"/> for instance-specific or operation-specific cancellation.
/// Root cancellation propagates to every existing child, and children created after root cancellation are immediately canceled.
/// Child cancellation never propagates to the root or sibling scopes.
/// The utility owns and disposes every returned child scope; callers must not dispose them.
/// </remarks>
public interface ICancellationUtils : IDisposable
{
    /// <summary>Gets the stable, explicit cancel-all scope for the current dependency-injection service scope.</summary>
    ICancellationScope Root { get; }

    /// <summary>Gets the stable, terminal named child scope associated with <paramref name="key"/>.</summary>
    /// <param name="key">A valid owner-type-based key defined by the component that owns the cancellation group.</param>
    /// <returns>The one shared child scope associated with <paramref name="key"/> in this parent service scope.</returns>
    /// <exception cref="ArgumentException"><paramref name="key"/> is the invalid default value.</exception>
    /// <exception cref="ObjectDisposedException">This utility has been disposed.</exception>
    ICancellationScope GetOrCreateScope(CancellationScopeKey key);
}

/// <inheritdoc />
[Scoped<ICancellationUtils>]
public sealed class CancellationUtils : ICancellationUtils
{
    private readonly CancellationScope _root = new();
    private readonly CancellationToken _rootToken;
    private readonly Dictionary<CancellationScopeKey, CancellationScope> _namedScopes = [];
    private readonly Lock _lock = new();
    private bool _isDisposed;

    /// <summary>Initializes a cancellation owner with one stable root scope.</summary>
    public CancellationUtils()
    {
        _rootToken = _root.Token;
    }

    /// <inheritdoc />
    public ICancellationScope Root => _root;

    /// <inheritdoc />
    public ICancellationScope GetOrCreateScope(CancellationScopeKey key)
    {
        key.Validate(nameof(key));

        lock (_lock)
        {
            ThrowIfDisposedUnderLock();
            if (_namedScopes.TryGetValue(key, out var existing))
                return existing;
        }

        var candidate = CreateRootLinkedScope();

        CancellationScope selected;
        var candidateWasAdded = false;
        try
        {
            lock (_lock)
            {
                ThrowIfDisposedUnderLock();
                if (_namedScopes.TryGetValue(key, out var existing))
                {
                    selected = existing;
                }
                else
                {
                    _namedScopes.Add(key, candidate);
                    selected = candidate;
                    candidateWasAdded = true;
                }
            }
        }
        catch
        {
            candidate.Dispose();
            throw;
        }

        if (!candidateWasAdded)
            candidate.Dispose();
        return selected;
    }

    /// <summary>Disposes every child scope and then the root scope.</summary>
    public void Dispose()
    {
        CancellationScope[] children;
        lock (_lock)
        {
            if (_isDisposed) return;

            _isDisposed = true;
            children = _namedScopes.Values.ToArray();
            _namedScopes.Clear();
        }

        // If disposal is reentered from a root callback, child cleanup waits until all root callbacks finish.
        _root.Dispose(() => DisposeChildren(children));
    }

    private CancellationScope CreateRootLinkedScope()
    {
        var candidate = new CancellationScope();
        try
        {
            // Root cancellation can invoke this callback synchronously, so linking occurs without the parent lock.
            candidate.Merge(_rootToken);
            return candidate;
        }
        catch
        {
            candidate.Dispose();
            throw;
        }
    }

    private void ThrowIfDisposedUnderLock()
    {
        if (_isDisposed)
            throw new ObjectDisposedException(nameof(CancellationUtils));
    }

    private static void DisposeChildren(IEnumerable<CancellationScope> children)
    {
        foreach (var child in children)
            child.Dispose();
    }
}
