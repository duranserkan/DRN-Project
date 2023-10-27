# DRN.Framework.SharedKernel
DRN.Framework.SharedKernel package is an lightweight package that contains common codes suitable for contract and domain layers. It can be referenced by any projects such as other DRN.Framework packages, projects developed with DRN.Framework.

## AppConstants
```csharp
namespace DRN.Framework.SharedKernel;

public static class AppConstants
{
    private const string GoogleDnsIp = "8.8.4.4";
    public static readonly int ProcessId = Environment.ProcessId;
    public static readonly Guid ApplicationId = Guid.NewGuid();
    public static readonly string ApplicationName = Assembly.GetEntryAssembly()?.GetName().Name ?? "Entry Assembly Not Found";
    public static readonly string TempPath = GetTempPath();
    public static readonly string LocalIpAddress = GetLocalIpAddress();

    private static string GetTempPath()
    {
        var appSpecificTempPath = Path.Combine(Path.GetTempPath(), ApplicationName);
        //Cleans directory in every startup
        if (Directory.Exists(appSpecificTempPath)) Directory.Delete(appSpecificTempPath, true);
        Directory.CreateDirectory(appSpecificTempPath);

        return appSpecificTempPath;
    }

    private static string GetLocalIpAddress()
    {
        //how to get local IP address https://stackoverflow.com/posts/27376368/revisions
        using var dataGramSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
        dataGramSocket.Connect(GoogleDnsIp, 59999);
        var localEndPoint = dataGramSocket.LocalEndPoint as IPEndPoint;
        return localEndPoint?.Address.ToString() ?? string.Empty;
    }
}
```

## Exceptions
Following exceptions are used in DRN.Framework and DRN.Nexus and can be used any project. DRN exceptions contain additional category property so that same exception types can be differentiated with a subcategory.

```csharp
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
```