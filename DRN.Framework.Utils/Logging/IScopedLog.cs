using System.Runtime.CompilerServices;

namespace DRN.Framework.Utils.Logging;

public interface IScopedLog
{
    TimeSpan ScopeDuration { get; }
    IReadOnlyDictionary<string, object> Logs { get; }

    IScopedLog WithLoggerName(string name);
    IScopedLog WithTraceIdentifier(string traceIdentifier);
    IScopedLog Add(string key, object value);
    IScopedLog AddIfNotNullOrEmpty(string key, string value);

    IScopedLog AddProperties<TValue>(string prefix, TValue classObject, params string[] ignoredPropertyNames)
        where TValue : class;

    void AddException(Exception exception, string? message = null);
    void AddWarning(string warningMessage, Exception? exception = null);
    bool HasException { get; }
    bool HasWarning { get; }
    void AddToActions(string action);
    void AddToList(string key, object value);
    long Increase(string key, long by = 1, string prefix = ScopedLogConventions.Stats);
    TimeSpan IncreaseTimeSpentOn(string key, TimeSpan by, string prefix = ScopedLogConventions.Stats);
    ScopeDuration Measure(string key);
    ScopeDuration Measure(object callerObject, [CallerMemberName] string? caller = null);
}