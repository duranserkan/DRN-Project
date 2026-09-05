using System.Reflection;

namespace DRN.Framework.Utils.Logging;

public static class ScopedLogConventions
{
    public const string IgnoredLogValue = "ignored";
    public const short StringLimit = 1250;

    public const string Stats = "Stats_";
    public const string KeyOfLoggerName = "LoggerName";
    public const string KeyOfTraceIdentifier = "TraceIdentifier";
    public const string KeyOfTraceId = "TraceId";
    public const string KeyOfCorrelationId = "CorrelationId";
    public const string KeyOfEventId = "EventId";
    public const string KeyOfEventName = "EventName";
    public const string KeyOfEventOutcome = "EventOutcome";
    public const string KeyOfEventReason = "EventReason";
    public const string KeyOfAdditionalEvents = "AdditionalEvents";
    public const string KeyOfScopeCreatedAt = "ScopeCreatedAt";
    public const string KeyOfScopeDuration = "ScopeDuration";
    public const string KeyOfActions = "Actions";
    public const string KeyOfExceptionType = "ExceptionType";
    public const string KeyOfExceptionStackTrace = "ExceptionStackTrace";
    public const string KeyOfExceptionMessage = "ExceptionMessage";
    public const string KeyOfExceptionLogMessage = "ExceptionLogMessage";
    public const string KeyOfExceptionData = "ExceptionData";
    public const string KeyOfInnerExceptionType = "InnerExceptionType";
    public const string KeyOfInnerExceptionStackTrace = "InnerExceptionStackTrace";
    public const string KeyOfInnerExceptionMessage = "InnerExceptionMessage";
    public const string KeyOfWarningMessage = "WarningMessage";
    public const string KeyOfWarningHasException = "WarningHasException";

    public static string TimeSpanKey(string key) => $"{key}_Seconds";
    public static string TimeSpentOnKey(string key, string prefix = Stats) => $"{prefix}TimeSpentOn_{key}_Seconds";
    public static string TimeSpentOnCounter(string key, string prefix = Stats) => $"{prefix}TimeSpentOn_{key}_Counter";
    public static string PropertyLogKey(string prefix, PropertyInfo propertyInfo) => $"{prefix}.{propertyInfo.Name}";
}
