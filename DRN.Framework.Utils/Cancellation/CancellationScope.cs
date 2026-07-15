namespace DRN.Framework.Utils.Cancellation;

/// <summary>Represents one stable, terminal cancellation scope.</summary>
/// <remarks>
/// A scope keeps the same token for its complete lifetime. Merged tokens and manual cancellation cancel that token once;
/// a canceled scope is never reset or replaced. Named child scopes are owned by <see cref="ICancellationUtils"/> and shared by key.
/// </remarks>
public interface ICancellationScope
{
    /// <summary>Gets the stable token for this scope.</summary>
    CancellationToken Token { get; }

    /// <summary>Gets whether cancellation has been requested for this scope.</summary>
    bool IsCancellationRequested { get; }

    /// <summary>Merges a token into this scope so its cancellation requests cancellation of this scope.</summary>
    /// <param name="token">
    /// The external token to observe. <see cref="CancellationToken.None"/>, this scope's own token, and repeated tokens are ignored.
    /// </param>
    /// <remarks>
    /// An already-canceled token requests cancellation synchronously. The external token source remains owned by its caller.
    /// </remarks>
    void Merge(CancellationToken token);

    /// <summary>Requests cancellation of this scope.</summary>
    void Cancel();
}

internal sealed class CancellationScope : ICancellationScope
{
    private readonly CancellationTokenSource _source = new();
    private readonly HashSet<CancellationToken> _mergedTokens = [];
    private readonly List<CancellationTokenRegistration> _registrations = [];
    private readonly Lock _lock = new();
    private Action? _beforeScopeResources;
    private bool _isCancellationInProgress;
    private bool _disposeAfterCancellation;
    private bool _isDisposed;

    public CancellationToken Token
    {
        get
        {
            lock (_lock)
                return _source.Token;
        }
    }

    public bool IsCancellationRequested
    {
        get
        {
            lock (_lock)
                return _source.IsCancellationRequested;
        }
    }

    public void Cancel()
    {
        lock (_lock)
        {
            if (!TryBeginCancellationUnderLock()) return;
        }

        try
        {
            _source.Cancel();
        }
        finally
        {
            ScopeResources? resources;
            CancellationTokenRegistration[] registrations;
            lock (_lock)
                resources = FinishCancellationUnderLock(out registrations);

            if (resources is not null)
                DisposeResources(resources);
            else
                Unregister(registrations);
        }
    }

    public void Merge(CancellationToken token)
    {
        lock (_lock)
        {
            if (!CanMergeUnderLock(token))
                return;

            _mergedTokens.Add(token);
        }

        CancellationTokenRegistration registration;
        try
        {
            registration = token.UnsafeRegister(static state => ((CancellationScope)state!).Cancel(), this);
        }
        catch
        {
            lock (_lock)
                _mergedTokens.Remove(token);
            throw;
        }

        bool shouldDisposeRegistration;
        lock (_lock)
        {
            shouldDisposeRegistration = !CanAcceptCancellationUnderLock();
            if (shouldDisposeRegistration)
                _mergedTokens.Remove(token);
            else
                _registrations.Add(registration);
        }

        if (shouldDisposeRegistration)
            registration.Dispose();
    }

    internal void Dispose() => Dispose(null);

    internal void Dispose(Action? beforeScopeResources)
    {
        ScopeResources resources;
        lock (_lock)
        {
            if (_isDisposed) return;

            _isDisposed = true;
            _beforeScopeResources = beforeScopeResources;
            if (_isCancellationInProgress)
            {
                _disposeAfterCancellation = true;
                return;
            }

            resources = TakeResourcesUnderLock();
        }

        DisposeResources(resources);
    }

    private bool TryBeginCancellationUnderLock()
    {
        if (!CanAcceptCancellationUnderLock())
            return false;

        _isCancellationInProgress = true;
        return true;
    }

    private ScopeResources? FinishCancellationUnderLock(out CancellationTokenRegistration[] registrations)
    {
        _isCancellationInProgress = false;
        if (!_disposeAfterCancellation)
        {
            registrations = TakeRegistrationsUnderLock();
            return null;
        }

        _disposeAfterCancellation = false;
        registrations = [];

        return TakeResourcesUnderLock();
    }

    private bool CanMergeUnderLock(CancellationToken token)
    {
        if (!CanAcceptCancellationUnderLock()) return false;
        if (!token.CanBeCanceled) return false;
        if (token == _source.Token) return false;

        return !_mergedTokens.Contains(token);
    }

    private bool CanAcceptCancellationUnderLock()
    {
        if (_isDisposed) return false;
        if (_isCancellationInProgress) return false;

        return !_source.IsCancellationRequested;
    }

    private ScopeResources TakeResourcesUnderLock()
    {
        var resources = new ScopeResources(_beforeScopeResources, TakeRegistrationsUnderLock());
        _beforeScopeResources = null;

        return resources;
    }

    private CancellationTokenRegistration[] TakeRegistrationsUnderLock()
    {
        var registrations = _registrations.ToArray();
        _registrations.Clear();
        _mergedTokens.Clear();

        return registrations;
    }

    private static void Unregister(IEnumerable<CancellationTokenRegistration> registrations)
    {
        foreach (var registration in registrations)
            registration.Unregister();
    }

    private void DisposeResources(ScopeResources resources)
    {
        try
        {
            resources.BeforeScopeResources?.Invoke();
        }
        finally
        {
            try
            {
                foreach (var registration in resources.Registrations)
                    registration.Dispose();
            }
            finally
            {
                lock (_lock)
                    _source.Dispose();
            }
        }
    }

    private sealed record ScopeResources(Action? BeforeScopeResources, CancellationTokenRegistration[] Registrations);
}
