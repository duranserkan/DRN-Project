using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using DRN.Framework.SharedKernel;

namespace DRN.Framework.Utils.Scope;

/// <summary>Public error defaults for the current scope. Do not store private diagnostic data here.</summary>
[StackTraceHidden]
public sealed class ScopeProblem
{
    public string? Type { get; set; }
    public string? Title { get; set; }
    public string? Detail { get; set; }
    public string? Instance { get; set; }
    public Dictionary<string, object?> Extensions { get; } = new();

    /// <summary>Throws a validation exception without changing public error defaults.</summary>
    [DoesNotReturn]
    public void Validation(string message, Exception? exception = null, string? category = DrnException.DefaultCategory)
        => throw ExceptionFor.Validation(message, exception, category);

    /// <summary>Throws an unauthorized exception without changing public error defaults.</summary>
    [DoesNotReturn]
    public void Unauthorized(string message, Exception? exception = null, string? category = DrnException.DefaultCategory)
        => throw ExceptionFor.Unauthorized(message, exception, category);

    /// <summary>Throws a forbidden exception without changing public error defaults.</summary>
    [DoesNotReturn]
    public void Forbidden(string message, Exception? exception = null, string? category = DrnException.DefaultCategory)
        => throw ExceptionFor.Forbidden(message, exception, category);

    /// <summary>Throws a not-found exception without changing public error defaults.</summary>
    [DoesNotReturn]
    public void NotFound(string message, Exception? exception = null, string? category = DrnException.DefaultCategory)
        => throw ExceptionFor.NotFound(message, exception, category);

    /// <summary>Throws a conflict without copying diagnostic data into public error defaults.</summary>
    /// <exception cref="ConflictException">Always thrown. DRN Hosting maps it to HTTP 409.</exception>
    [DoesNotReturn]
    public void Conflict(string message, Exception? exception = null, string? category = DrnException.DefaultCategory)
        => throw ExceptionFor.Conflict(message, exception, category);

    /// <summary>Throws an expired exception without changing public error defaults.</summary>
    [DoesNotReturn]
    public void Expired(string message, Exception? exception = null, string? category = DrnException.DefaultCategory)
        => throw ExceptionFor.Expired(message, exception, category);

    /// <summary>Throws a configuration exception without changing public error defaults.</summary>
    [DoesNotReturn]
    public void Configuration(string message, Exception? exception = null, string? category = DrnException.DefaultCategory)
        => throw ExceptionFor.Configuration(message, exception, category);

    /// <summary>Throws an unprocessable-entity exception without changing public error defaults.</summary>
    [DoesNotReturn]
    public void UnprocessableEntity(string message, Exception? exception = null, string? category = DrnException.DefaultCategory)
        => throw ExceptionFor.UnprocessableEntity(message, exception, category);

    /// <summary>Throws a malicious-request exception to abort processing without changing public error defaults.</summary>
    [DoesNotReturn]
    public void MaliciousRequest(string message, Exception? exception = null, string? category = DrnException.DefaultCategory)
        => throw ExceptionFor.MaliciousRequest(message, exception, category);

    /// <summary>Throws a jackpot exception without changing public error defaults.</summary>
    [DoesNotReturn]
    public void Jackpot(string message, Exception? exception = null, string? category = DrnException.DefaultCategory)
        => throw ExceptionFor.Jackpot(message, exception, category);
}
