using System.Runtime.CompilerServices;

namespace DRN.Framework.Utils.Logging;

public interface IScopedLog
{
    TimeSpan ScopeDuration { get; }
    /// <summary>W3C trace captured at scope creation, or null when no W3C activity exists.</summary>
    string? TraceId { get; }
    /// <summary>Stable scope correlation independent of distributed tracing.</summary>
    string CorrelationId { get; }
    ScopeEvent? Event { get; }
    int? EventId { get; }
    string? EventName { get; }
    string? EventOutcome { get; }
    string? EventReason { get; }
    IReadOnlyDictionary<string, object> GetLogs();

    /// <summary>Sets the first event as primary. Later events are retained separately without replacing it.</summary>
    IScopedLog WithEvent(ScopeEvent scopeEvent);

    IScopedLog WithLoggerName(string name);
    IScopedLog WithTraceIdentifier(string traceIdentifier);
    IScopedLog Add(string key, object value);
    IScopedLog AddIfNotNullOrEmpty(string key, string value);
    IScopedLog CopyFrom(IScopedLog source);

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
