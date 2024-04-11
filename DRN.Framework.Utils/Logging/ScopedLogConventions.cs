using System.Reflection;

namespace DRN.Framework.Utils.Logging;

public static class ScopedLogConventions
{
    public const string IgnoredLogValue = "ignored";
    public const short StringLimit = 1250;

    public const string KeyOfLoggerName = "LoggerName";
    public const string KeyOfScopeCreatedAt = "ScopeCreatedAt";
    public const string KeyOfScopeDuration = "ScopeDuration";
    public const string KeyOfActions = "Actions";
    public const string KeyOfExceptionType = "ExceptionType";
    public const string KeyOfExceptionStackTrace = "ExceptionStackTrace";
    public const string KeyOfExceptionMessage = "ExceptionMessage";
    public const string KeyOfWarningMessage = "WarningMessage";

    public static string TimeSpanKey(string key) => $"{key}_Seconds";
    public static string TimeSpentOnKey(string key) => $"TimeSpentOn_{key}_Seconds";
    public static string TimeSpentOnCounter(string key) => $"TimeSpentOn_{key}_Counter";
    public static string PropertyLogKeyKey(string prefix, PropertyInfo propertyInfo) => $"{prefix}.{propertyInfo.Name}";
}