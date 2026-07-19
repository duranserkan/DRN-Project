using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Utils.Cancellation;

/// <summary>
/// Owns the explicit root plus stable keyed child scopes for the current dependency-injection service scope.
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

    /// <summary>Gets the stable, terminal child scope associated with <paramref name="key"/>.</summary>
    /// <param name="key">A valid type-owned key defined by the cancellation group.</param>
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
    private readonly Dictionary<CancellationScopeKey, CancellationScope> _keyedScopes = [];
    private readonly Lock _lock = new();
    private bool _isDisposed;

    /// <inheritdoc />
    public ICancellationScope Root => _root;

    /// <inheritdoc />
    public ICancellationScope GetOrCreateScope(CancellationScopeKey key)
    {
        key.Validate(nameof(key));

        lock (_lock)
        {
            ThrowIfDisposedUnderLock();
            if (_keyedScopes.TryGetValue(key, out var existing))
                return existing;

            var scope = new CancellationScope();
            try
            {
                scope.Merge(_root.Token);
                _keyedScopes.Add(key, scope);
                return scope;
            }
            catch
            {
                scope.ReleaseResources();
                throw;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        CancellationScope[] children;
        lock (_lock)
        {
            if (_isDisposed) return;

            _isDisposed = true;
            children = _keyedScopes.Values.ToArray();
            _keyedScopes.Clear();
        }

        // If disposal is reentered from a root callback, child cleanup waits until all root callbacks finish.
        _root.ReleaseResources(() => DisposeChildren(children));
    }

    private void ThrowIfDisposedUnderLock()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    private static void DisposeChildren(IEnumerable<CancellationScope> children)
    {
        foreach (var child in children)
            child.ReleaseResources();
    }
}
