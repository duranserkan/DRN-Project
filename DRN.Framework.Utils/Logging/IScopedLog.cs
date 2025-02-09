namespace DRN.Framework.Utils.Logging;

public interface IScopedLog
{
    TimeSpan ScopeDuration { get; }
    IReadOnlyDictionary<string, object> Logs { get; }

    IScopedLog WithLoggerName(string name);
    IScopedLog WithTraceIdentifier(string traceIdentifier);
    IScopedLog Add(string key, object value);

    IScopedLog AddProperties<TValue>(string prefix, TValue classObject, params string[] ignoredPropertyNames)
        where TValue : class;

    void AddException(Exception exception, string? message = null);
    void AddWarning(string warningMessage, Exception? exception = null);
    bool HasException { get; }
    bool HasWarning { get; }
    void AddToActions(string action);
    void AddToList(string key, object value);
    long Increase(string key, long by = 1);
    TimeSpan IncreaseTimeSpentOn(string key, TimeSpan by);
}