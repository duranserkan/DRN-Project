using System.Runtime.ExceptionServices;
using System.Text.Json.Serialization;

namespace DRN.Framework.Utils.Http;

public enum HttpStatusClass
{
    Unknown,
    Informational,
    Success,
    Redirection,
    ClientError,
    ServerError
}

internal static class HttpStatusClassifier
{
    internal static HttpStatusClass Classify(int statusCode) => statusCode switch
    {
        >= 100 and <= 199 => HttpStatusClass.Informational,
        >= 200 and <= 299 => HttpStatusClass.Success,
        >= 300 and <= 399 => HttpStatusClass.Redirection,
        >= 400 and <= 499 => HttpStatusClass.ClientError,
        >= 500 and <= 599 => HttpStatusClass.ServerError,
        _ => HttpStatusClass.Unknown
    };
}

public enum HttpFailureKind
{
    Transport,
    Timeout,
    ResponseRead,
    Deserialization
}

public sealed class HttpFailure
{
    internal HttpFailure(HttpFailureKind kind, Exception exception)
    {
        Kind = kind;
        Exception = exception;
        ExceptionType = exception.GetType().FullName ?? exception.GetType().Name;
        Message = exception.Message;
    }

    public HttpFailureKind Kind { get; }
    public string ExceptionType { get; }

    /// <summary>
    /// Gets the exception message for local diagnostics. Redact it before logging or exposing it.
    /// </summary>
    [JsonIgnore]
    public string Message { get; }

    /// <summary>
    /// Gets the original exception for local diagnostics. Do not serialize or log it without redaction.
    /// </summary>
    [JsonIgnore]
    public Exception Exception { get; }

    internal void Throw() => ExceptionDispatchInfo.Capture(Exception).Throw();
}

public sealed class HttpCallResult<TResult> : IDisposable
{
    private readonly HttpResponse<TResult>? _response;
    private readonly int? _httpStatus;

    private HttpCallResult(HttpResponse<TResult>? response, int? httpStatus, HttpFailure? failure)
    {
        _response = response;
        _httpStatus = httpStatus;
        Failure = failure;
    }

    public int? HttpStatus => _httpStatus;
    public HttpStatusClass StatusClass => _httpStatus.HasValue
        ? HttpStatusClassifier.Classify(_httpStatus.Value)
        : HttpStatusClass.Unknown;
    public TResult? Payload => _response is null ? default : _response.Payload;
    public HttpFailure? Failure { get; }
    public bool ResponseReceived => _httpStatus.HasValue;
    public bool IsSuccessStatusCode => StatusClass == HttpStatusClass.Success;
    public bool IsSuccess => IsSuccessStatusCode && Failure is null;

    /// <summary>
    /// Rethrows the captured transport, timeout, response-read, or deserialization exception while preserving its stack.
    /// HTTP status errors without a processing failure are not thrown.
    /// </summary>
    public void ThrowIfFailure() => Failure?.Throw();

    internal static HttpCallResult<TResult> FromResponse(HttpResponse<TResult> response) =>
        new(response, response.HttpStatus, null);

    internal static HttpCallResult<TResult> FromFailure(
        int? httpStatus,
        HttpFailureKind failureKind,
        Exception exception)
    {
        return new HttpCallResult<TResult>(null, httpStatus, new HttpFailure(failureKind, exception));
    }

    /// <summary>
    /// Releases an <see cref="IDisposable"/> payload and the underlying HTTP response, when present.
    /// </summary>
    public void Dispose() => _response?.Dispose();
}
