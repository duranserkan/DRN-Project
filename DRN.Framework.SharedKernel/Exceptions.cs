namespace DRN.Framework.SharedKernel;

public class ValidationException : Exception
{
    public ValidationException(string message, Exception exception = null!) : base(message, exception)
    {
    }
}
public class NotSavedException : Exception
{
    public NotSavedException(string message, Exception exception = null!) : base(message, exception)
    {
    }
}
public class NotFoundException : Exception
{
    public NotFoundException(string message, Exception exception = null!) : base(message, exception)
    {
    }
}
public class ExpiredException : Exception
{
    public ExpiredException(string message, Exception exception = null!) : base(message, exception)
    {
    }
}
public class ConfigurationException : Exception
{
    public ConfigurationException(string message, Exception exception = null!) : base(message, exception)
    {
    }
}
