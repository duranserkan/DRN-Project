namespace DRN.Framework.SharedKernel;

public abstract class DrnException : Exception
{
    public string Category { get; }

    public DrnException(string message, Exception exception = null!, string category = "default") : base(message, exception)
    {
        Category = category;
    }
}

public class ValidationException : DrnException
{
    public ValidationException(string message, string category, Exception exception = null!) : base(message, exception, category)
    {
    }

    public ValidationException(string message, Exception exception = null!) : base(message, exception)
    {
    }
}

public class NotSavedException : DrnException
{
    public NotSavedException(string message, string category, Exception exception = null!) : base(message, exception, category)
    {
    }

    public NotSavedException(string message, Exception exception = null!) : base(message, exception)
    {
    }
}

public class NotFoundException : DrnException
{
    public NotFoundException(string message, string category, Exception exception = null!) : base(message, exception, category)
    {
    }

    public NotFoundException(string message, Exception exception = null!) : base(message, exception)
    {
    }
}

public class ExpiredException : DrnException
{
    public ExpiredException(string message, string category, Exception exception = null!) : base(message, exception, category)
    {
    }

    public ExpiredException(string message, Exception exception = null!) : base(message, exception)
    {
    }
}

public class ConfigurationException : DrnException
{
    public ConfigurationException(string message, string category, Exception exception = null!) : base(message, exception, category)
    {
    }

    public ConfigurationException(string message, Exception exception = null!) : base(message, exception)
    {
    }
}