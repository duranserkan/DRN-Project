namespace DRN.Framework.Utils.Http;

public class HttpResponse(int httpStatus)
{
    public int HttpStatus { get; } = httpStatus;
    public HttpStatusClass StatusClass { get; } = HttpStatusClassifier.Classify(httpStatus);
    public bool IsSuccessStatusCode => StatusClass == HttpStatusClass.Success;
}

public sealed class HttpResponse<TResult>(int httpStatus, TResult? payload) : HttpResponse(httpStatus), IDisposable
{
    private IDisposable? _owner;
    private int _disposed;

    internal HttpResponse(int httpStatus, TResult? payload, IDisposable owner) : this(httpStatus, payload)
        => _owner = owner;

    public TResult? Payload { get; } = payload;

    /// <summary>
    /// Releases an <see cref="IDisposable"/> payload and the underlying HTTP response, when present.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        var owner = Interlocked.Exchange(ref _owner, null);
        var disposablePayload = Payload as IDisposable;
        try
        {
            disposablePayload?.Dispose();
        }
        finally
        {
            if (!ReferenceEquals(owner, disposablePayload))
                owner?.Dispose();
        }
    }
}
