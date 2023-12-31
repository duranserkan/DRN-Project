namespace DRN.Framework.SharedKernel;

public abstract class DrnException(string message, Exception exception = null!, string? category = "default") : Exception(message, exception)
{
    public string Category { get; } = category ?? "default";
}

public class ValidationException(string message, Exception exception = null!, string? category = null) : DrnException(message, exception, category);

public class NotSavedException(string message, Exception exception = null!, string? category = null) : DrnException(message, exception, category);

public class NotFoundException(string message, Exception exception = null!, string? category = null) : DrnException(message, exception, category);

public class ExpiredException(string message, Exception exception = null!, string? category = null) : DrnException(message, exception, category);

public class ConfigurationException(string message, Exception exception = null!, string? category = null) : DrnException(message, exception, category);