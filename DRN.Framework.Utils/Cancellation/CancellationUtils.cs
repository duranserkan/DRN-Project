using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Utils.Cancellation;

public interface ICancellationUtils : IDisposable
{
    CancellationToken Token { get; }
    bool IsCancellationRequested { get; }
    void Cancel();
    void Merge(CancellationToken other);
}

[Scoped<ICancellationUtils>]
public sealed class CancellationUtils : ICancellationUtils
{
    //intentionally made private to not leak control of inner CTS
    private CancellationTokenSource _source = new();
    private bool _isDisposed;
    private readonly Lock _lock = new();

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
            if (_isDisposed)
                return;
            if (_source.IsCancellationRequested) return;
            _source.Cancel();
        }
    }

    public void Merge(CancellationToken other)
    {
        lock (_lock)
        {
            if (_isDisposed)
                return;
            var oldSource = _source;
            _source = CancellationTokenSource.CreateLinkedTokenSource(_source.Token, other);
            oldSource.Dispose(); // dispose of the old source to prevent leaks
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            if (_isDisposed) return;
            _source.Dispose();
            _isDisposed = true;
        }
    }
}