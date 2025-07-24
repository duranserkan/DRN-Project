namespace DRN.Framework.SharedKernel;

/// <summary>
/// DrnExceptions are handled by scope handler and can be used to short circuit the processing pipeline
/// </summary>
public abstract class DrnException(string message, Exception? ex, string? category, short? status = null)
    : Exception(message, ex)
{
    public const string DefaultCategory = "default";
    public string Category { get; } = category ?? DefaultCategory;
    public short Status { get; } = status ?? 500;
    public new IDictionary<string, object> Data { get; } = new Dictionary<string, object>();
}

/// <summary>
/// Scope handler returns 400 when thrown
/// </summary>
public class ValidationException(string message, Exception? ex = null, string? category = null)
    : DrnException(message, ex, category, 400);

/// <summary>
/// Scope handler returns 401 when thrown
/// </summary>
public class UnauthorizedException(string message, Exception? ex = null, string? category = null)
    : DrnException(message, ex, category, 401);

/// <summary>
/// Scope handler returns 403 when thrown
/// </summary>
public class ForbiddenException(string message, Exception? ex = null, string? category = null)
    : DrnException(message, ex, category, 403);

/// <summary>
/// Scope handler returns 404 when thrown
/// </summary>
public class NotFoundException(string message, Exception? ex = null, string? category = null)
    : DrnException(message, ex, category, 404);

/// <summary>
/// Scope handler returns 409 when thrown
/// </summary>
public class ConflictException(string message, Exception? ex = null, string? category = null)
    : DrnException(message, ex, category, 409);

/// <summary>
/// Scope handler returns 410 when thrown
/// </summary>
public class ExpiredException(string message, Exception? ex = null, string? category = null)
    : DrnException(message, ex, category, 410);

/// <summary>
/// Scope handler returns 500 when thrown
/// </summary>
public class ConfigurationException(string message, Exception? ex = null, string? category = null)
    : DrnException(message, ex, category, 500);

/// <summary>
/// Scope handler returns 422 when thrown
/// </summary>
public class UnprocessableEntityException(string message, Exception? ex = null, string? category = null)
    : DrnException(message, ex, category, 422);

/// <summary>
/// To abort requests that doesn't even deserve a result
/// </summary>
public class MaliciousRequestException(string message, Exception? ex = null, string? category = null)
    : DrnException(message, ex, category, short.MaxValue);

public static class ExceptionFor
{
    private const string Default = DrnException.DefaultCategory;

    /// <summary>
    /// Scope handler returns 400 when thrown
    /// </summary>
    public static ValidationException Validation(string message, Exception exception = null!, string? category = Default)
        => new(message, exception, category);

    /// <summary>
    /// Scope handler returns 401 when thrown
    /// </summary>
    public static UnauthorizedException Unauthorized(string message, Exception exception = null!, string? category = Default)
        => new(message, exception, category);

    /// <summary>
    /// Scope handler returns 403 when thrown
    /// </summary>
    public static ForbiddenException Forbidden(string message, Exception? exception = null, string? category = Default)
        => new(message, exception, category);

    /// <summary>
    /// Scope handler returns 404 when thrown
    /// </summary>
    public static NotFoundException NotFound(string message, Exception exception = null!, string? category = Default)
        => new(message, exception, category);

    /// <summary>
    /// Scope handler returns 409 when thrown
    /// </summary>
    public static ConflictException Conflict(string message, Exception exception = null!, string? category = Default)
        => new(message, exception, category);

    /// <summary>
    /// Scope handler returns 410 when thrown
    /// </summary>
    public static ExpiredException Expired(string message, Exception exception = null!, string? category = Default)
        => new(message, exception, category);

    /// <summary>
    /// Scope handler returns 500 when thrown
    /// </summary>
    public static ConfigurationException Configuration(string message, Exception? ex = null, string? category = Default)
        => new(message, ex, category);

    /// <summary>
    /// Scope handler returns 422 when thrown
    /// </summary>
    public static UnprocessableEntityException UnprocessableEntity(string message, Exception exception = null!, string? category = Default)
        => new(message, exception, category);

    /// <summary>
    /// To abort requests that doesn't even deserve a result
    /// </summary>
    public static MaliciousRequestException MaliciousRequest(string message, Exception exception = null!, string? category = Default)
        => new(message, exception, category);
}