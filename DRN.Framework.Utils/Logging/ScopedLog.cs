using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Settings;
using DRN.Framework.Utils.Time;

namespace DRN.Framework.Utils.Logging;

[IgnoreLog]
[Scoped<IScopedLog>]
public class ScopedLog : IScopedLog
{
    private static string GetSafeString(string text) => text[..(text.Length > ScopedLogConventions.StringLimit
        ? ScopedLogConventions.StringLimit
        : text.Length)];

    private readonly record struct LogProperty(PropertyInfo Property, bool Ignored);
    private static class PropertyCache<TValue> where TValue : class
    {
        internal static readonly LogProperty[] Properties = typeof(TValue)
            .GetProperties(BindingFlag.InstancePublic)
            .Where(property => property.GetMethod is { IsPublic: true } && property.GetIndexParameters().Length == 0)
            .Select(property => new LogProperty(property, property.IgnoredLog())).ToArray();
    }

    // Duration, correlation, trace, and four primary-event fields.
    private const int SnapshotMetadataFieldCount = 7;
    private static readonly string ScopeDurationKey = ScopedLogConventions.TimeSpanKey(ScopedLogConventions.KeyOfScopeDuration);
    private static readonly object TrueValue = true;
    private static readonly object AppInstanceIdValue = AppConstants.AppInstanceId;

    // Mutable metrics stay private; snapshots materialize detached primitive values.
    private sealed class CounterValue(long value)
    {
        public long Value = value;
    }

    private sealed class DurationValue(double value)
    {
        public double Value = value;
    }

    private static object CloneLogValue(object value)
    {
        switch (value)
        {
            case IReadOnlyDictionary<string, object> readOnlyDictionary:
                return readOnlyDictionary.ToDictionary(pair => pair.Key, pair => pair.Value);
            case IDictionary dictionary:
            {
                var clone = new Dictionary<object, object?>(dictionary.Count);
                foreach (DictionaryEntry entry in dictionary)
                    clone[entry.Key] = entry.Value;

                return clone;
            }
            case List<object> list:
                return new List<object>(list);
            case IList<object> objectList:
                return objectList.ToList();
            case IEnumerable<object> objectEnumerable:
                return objectEnumerable.ToList();
            default:
                return value;
        }
    }

    private readonly Lock _sync = new();
    private Dictionary<(string Prefix, string Key), string>? _counterKeys;
    private Dictionary<(string Prefix, string Key), (string Counter, string Duration)>? _durationKeys;
    private Dictionary<(Type Type, string? Caller), string>? _measurementKeys;
    private Dictionary<(Type Type, string Prefix), string[]>? _propertyKeys;
    private ScopeEvent? _event;

    public string? TraceId { get; } = Activity.Current is { IdFormat: ActivityIdFormat.W3C, TraceId: var traceId } && traceId != default
        ? traceId.ToString()
        : null;
    public string CorrelationId { get; } = Guid.NewGuid().ToString("N");
    public ScopeEvent? Event => Volatile.Read(ref _event);
    public int? EventId => Event?.Id.Id;
    public string? EventName => Event?.Id.Name;
    public string? EventOutcome => Event?.Outcome;
    public string? EventReason => Event?.Reason;

    public IScopedLog WithEvent(ScopeEvent scopeEvent)
    {
        ArgumentNullException.ThrowIfNull(scopeEvent);
        if (Interlocked.CompareExchange(ref _event, scopeEvent, null) != null)
            AddToList(ScopedLogConventions.KeyOfAdditionalEvents, scopeEvent);
        return this;
    }

    private Dictionary<string, object> LogData { get; } = new(32, StringComparer.Ordinal);

    public ScopedLog(IAppSettings appSettings)
    {
        Add(nameof(ScopedLog), TrueValue);
        Add("App_Name", appSettings.ApplicationName);
        Add("App_InstanceId", AppInstanceIdValue);
        Add("App_NexusId", appSettings.NexusAppSettings.AppId);
        Add("App_NexusInstanceId", appSettings.NexusAppSettings.AppInstanceId);
        Add("App_Environment", appSettings.Environment.ToString());
        Add("App_Environment_MachineName", Environment.MachineName);
        Add(ScopedLogConventions.KeyOfScopeCreatedAt, DateTimeProvider.UtcNow);
    }

    public TimeSpan ScopeDuration
    {
        get
        {
            lock (_sync)
                return DateTimeProvider.UtcNow - (DateTimeOffset)LogData[ScopedLogConventions.KeyOfScopeCreatedAt];
        }
    }

    private SortedList<string, object> SnapshotLogData()
    {
        lock (_sync)
            return SnapshotLogDataLocked();
    }

    // Caller holds _sync. Sorted insertion appends without shifting existing entries.
    private SortedList<string, object> SnapshotLogDataLocked(int additionalCapacity = 0)
    {
        var snapshot = new SortedList<string, object>(checked(LogData.Count + additionalCapacity), StringComparer.Ordinal);
        var keys = LogData.Keys.ToArray();
        Array.Sort(keys, snapshot.Comparer);

        foreach (var key in keys)
            snapshot.Add(key, LogData[key]);

        for (var index = 0; index < snapshot.Count; index++)
        {
            var value = snapshot.GetValueAtIndex(index);
            snapshot.SetValueAtIndex(index, value switch
            {
                CounterValue counter => counter.Value,
                DurationValue duration => duration.Value,
                List<object> list => new List<object>(list),
                _ => value
            });
        }

        return snapshot;
    }

    public IReadOnlyDictionary<string, object> GetLogs()
    {
        lock (_sync)
        {
            var snapshot = SnapshotLogDataLocked(SnapshotMetadataFieldCount);
            snapshot[ScopeDurationKey] = (DateTimeProvider.UtcNow - (DateTimeOffset)LogData[ScopedLogConventions.KeyOfScopeCreatedAt]).TotalSeconds;
            snapshot[ScopedLogConventions.KeyOfCorrelationId] = CorrelationId;
            if (TraceId != null)
                snapshot[ScopedLogConventions.KeyOfTraceId] = TraceId;
            else
                snapshot.Remove(ScopedLogConventions.KeyOfTraceId);

            var scopeEvent = Event;
            if (scopeEvent is null)
                return snapshot;

            snapshot[ScopedLogConventions.KeyOfEventId] = scopeEvent.Id.Id;
            snapshot[ScopedLogConventions.KeyOfEventName] = scopeEvent.Id.Name ?? string.Empty;
            snapshot[ScopedLogConventions.KeyOfEventOutcome] = scopeEvent.Outcome ?? string.Empty;
            snapshot[ScopedLogConventions.KeyOfEventReason] = scopeEvent.Reason ?? string.Empty;
            return snapshot;
        }
    }

    public bool HasException { get; private set; }
    public bool HasWarning { get; private set; }

    public void AddException(Exception exception, string? message = null)
    {
        HasException = true;

        if (!string.IsNullOrWhiteSpace(message))
            Add(ScopedLogConventions.KeyOfExceptionLogMessage, message);

        Add(ScopedLogConventions.KeyOfExceptionType, exception.GetType().FullName ?? exception.GetType().Name);
        Add(ScopedLogConventions.KeyOfExceptionMessage, exception.Message);
        Add(ScopedLogConventions.KeyOfExceptionStackTrace, exception.StackTrace ?? string.Empty);

        if (exception is DrnException drnException)
            foreach (var kvp in drnException.Data)
                Add($"{ScopedLogConventions.KeyOfExceptionData}_{kvp.Key}", kvp.Value);

        if (exception.InnerException == null) return;

        Add(ScopedLogConventions.KeyOfInnerExceptionType, exception.InnerException.GetType().FullName ?? exception.InnerException.GetType().Name);
        Add(ScopedLogConventions.KeyOfInnerExceptionMessage, exception.InnerException.Message);
        Add(ScopedLogConventions.KeyOfInnerExceptionStackTrace, exception.InnerException.StackTrace ?? string.Empty);
    }

    public void AddWarning(string warningMessage, Exception? exception = null)
    {
        HasWarning = true;
        Add(ScopedLogConventions.KeyOfWarningMessage, warningMessage);

        if (exception == null)
            return;
        Add(ScopedLogConventions.KeyOfWarningHasException, true);
        Add(ScopedLogConventions.KeyOfExceptionType, exception.GetType().FullName ?? exception.GetType().Name);
        Add(ScopedLogConventions.KeyOfExceptionMessage, exception.Message);
        Add(ScopedLogConventions.KeyOfExceptionStackTrace, exception.StackTrace ?? string.Empty);

        if (exception.InnerException == null)
            return;

        Add(ScopedLogConventions.KeyOfInnerExceptionType, exception.InnerException.GetType().FullName ?? exception.InnerException.GetType().Name);
        Add(ScopedLogConventions.KeyOfInnerExceptionMessage, exception.InnerException.Message);
        Add(ScopedLogConventions.KeyOfInnerExceptionStackTrace, exception.InnerException.StackTrace ?? string.Empty);
    }

    public IScopedLog Add(string key, object value)
    {
        if (value.IgnoredLog())
            value = ScopedLogConventions.IgnoredLogValue;
        else if (value is string text)
            value = GetSafeString(text);
        else if (value is TimeSpan time)
        {
            key = ScopedLogConventions.TimeSpanKey(key);
            value = time.TotalSeconds;
        }

        lock (_sync)
            LogData[key] = value;

        return this;
    }

    public IScopedLog AddIfNotNullOrEmpty(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
            Add(key, value);

        return this;
    }

    public IScopedLog CopyFrom(IScopedLog source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (ReferenceEquals(this, source)) return this;

        var sourceIsScopedLog = source is ScopedLog;
        var sourceLogs = source is ScopedLog scopedLog ? scopedLog.SnapshotLogData() : source.GetLogs();
        foreach (var (key, value) in sourceLogs)
        {
            if (key == ScopedLogConventions.KeyOfAdditionalEvents && value is IEnumerable<object> events)
                foreach (var additionalEvent in sourceIsScopedLog && value is List<object> ? events : events.ToArray())
                    AddToList(key, additionalEvent);
            else
            {
                var copy = sourceIsScopedLog && value is List<object> ? value : CloneLogValue(value);
                lock (_sync)
                    LogData[key] = copy;
            }
        }

        HasException |= source.HasException;
        HasWarning |= source.HasWarning;
        if (source.Event is { } scopeEvent)
            WithEvent(scopeEvent);

        return this;
    }

    public IScopedLog WithLoggerName(string? name) => Add(ScopedLogConventions.KeyOfLoggerName, name ?? "n/a");
    public IScopedLog WithTraceIdentifier(string traceIdentifier) => Add(ScopedLogConventions.KeyOfTraceIdentifier, traceIdentifier);

    public IScopedLog AddProperties<TValue>(string prefix, TValue classObject, params string[] ignoredPropertyNames) where TValue : class
    {
        var properties = PropertyCache<TValue>.Properties;
        string[] keys;
        lock (_sync)
        {
            _propertyKeys ??= new();
            var identity = (typeof(TValue), prefix);
            if (!_propertyKeys.TryGetValue(identity, out keys!))
            {
                keys = new string[properties.Length];
                for (var index = 0; index < properties.Length; index++)
                    keys[index] = ScopedLogConventions.PropertyLogKey(prefix, properties[index].Property);
                _propertyKeys.Add(identity, keys);
            }
        }

        // User getters execute outside the storage lock and may call back into logging.
        for (var index = 0; index < properties.Length; index++)
        {
            var (propertyInfo, ignoredByAttribute) = properties[index];
            var ignored = ignoredByAttribute || ignoredPropertyNames.Contains(propertyInfo.Name);
            var logValue = ignored ? ScopedLogConventions.IgnoredLogValue : propertyInfo.GetValue(classObject);
            Add(keys[index], logValue ?? string.Empty);
        }

        return this;
    }

    public void AddToActions(string action) => AddToList(ScopedLogConventions.KeyOfActions, action);

    public void AddToList(string key, object value)
    {
        if (value is string text)
            value = GetSafeString(text);

        lock (_sync)
        {
            if (LogData.TryGetValue(key, out var obj) && obj is List<object> list)
                list.Add(value);
            else
                Add(key, new List<object>(8) { value });
        }
    }

    public long Increase(string key, long by = 1, string prefix = ScopedLogConventions.Stats)
    {
        lock (_sync)
        {
            string counterKey;
            if (string.IsNullOrEmpty(prefix))
                counterKey = key;
            else
            {
                _counterKeys ??= new();
                if (!_counterKeys.TryGetValue((prefix, key), out counterKey!))
                {
                    counterKey = string.Concat(prefix, key);
                    _counterKeys.Add((prefix, key), counterKey);
                }
            }

            return IncreaseLocked(counterKey, by);
        }
    }

    // Caller holds _sync and supplies the fully composed key.
    private long IncreaseLocked(string counterKey, long by)
    {
        if (LogData.TryGetValue(counterKey, out var obj) && obj is CounterValue counter)
            return counter.Value += by;

        var value = (obj is long existing ? existing : 0) + by;
        LogData[counterKey] = new CounterValue(value);
        return value;
    }

    public TimeSpan IncreaseTimeSpentOn(string key, TimeSpan by, string prefix = ScopedLogConventions.Stats)
    {
        lock (_sync)
        {
            _durationKeys ??= new();
            if (!_durationKeys.TryGetValue((prefix, key), out var keys))
            {
                keys = (ScopedLogConventions.TimeSpentOnCounter(key, prefix), ScopedLogConventions.TimeSpentOnKey(key, prefix));
                _durationKeys.Add((prefix, key), keys);
            }

            IncreaseLocked(keys.Counter, 1);
            double timeSpent;
            if (LogData.TryGetValue(keys.Duration, out var obj) && obj is DurationValue duration)
                timeSpent = duration.Value += by.TotalSeconds;
            else
            {
                timeSpent = (obj is double existing ? existing : 0) + by.TotalSeconds;
                LogData[keys.Duration] = new DurationValue(timeSpent);
            }
            return TimeSpan.FromSeconds(timeSpent);
        }
    }

    public ScopeDuration Measure(string key) => new(key, this);
    public ScopeDuration Measure(object callerObject, string? caller = null)
    {
        string key;
        lock (_sync)
        {
            _measurementKeys ??= new();
            var identity = (callerObject.GetType(), caller);
            if (_measurementKeys.TryGetValue(identity, out key!))
                return Measure(key);

            key = $"{identity.Item1.FullName}.{caller}";
            _measurementKeys.Add(identity, key);
        }

        return Measure(key);
    }

    public override string ToString() => JsonSerializer.Serialize(GetLogs());
}
