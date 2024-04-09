using System.Collections.Concurrent;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Utils.Logging;

[IgnoreLog]
[Scoped<IScopedLog>]
public class ScopedLog : IScopedLog
{
    private static string GetSafeString(string text) => text[..(text.Length > ScopedLogConventions.StringLimit
        ? ScopedLogConventions.StringLimit
        : text.Length)];

    private readonly object _timeUpdater = new();
    private readonly object _counter = new();
    private readonly object _list = new();

    private ConcurrentDictionary<string, object> LogData { get; } = new(1, 32);

    public ScopedLog()
    {
        Upsert(ScopedLogConventions.KeyOfScopeCreatedAt, DateTimeOffset.UtcNow);
        Upsert(nameof(ScopedLog), true);
        Upsert(nameof(AppConstants.ApplicationName), AppConstants.ApplicationName);
        Upsert(nameof(Environment.MachineName), Environment.MachineName);
    }

    public TimeSpan ScopeDuration => DateTimeOffset.UtcNow - (DateTimeOffset)LogData[ScopedLogConventions.KeyOfScopeCreatedAt];

    public IReadOnlyDictionary<string, object> Logs
    {
        get
        {
            Upsert(ScopedLogConventions.KeyOfScopeDuration, ScopeDuration);

            return new SortedDictionary<string, object>(LogData);
        }
    }

    public bool HasException { get; private set; }
    public bool HasWarning { get; private set; }

    public void UpsertException(Exception exception)
    {
        HasException = true;
        Upsert(ScopedLogConventions.KeyOfExceptionType, exception.GetType().FullName ?? string.Empty);
        Upsert(ScopedLogConventions.KeyOfExceptionMessage, exception.Message);
        Upsert(ScopedLogConventions.KeyOfExceptionStackTrace, exception.StackTrace ?? string.Empty);
    }

    public void UpsertWarning(string warningMessage)
    {
        HasWarning = true;
        Upsert(ScopedLogConventions.KeyOfWarningMessage, warningMessage);
    }

    public void Upsert(string key, object value)
    {
        if (value.IgnoredLog())
            LogData[key] = ScopedLogConventions.IgnoredLogValue;
        else if (value is string text)
            LogData[key] = GetSafeString(text);
        else if (value is TimeSpan time)
            LogData[ScopedLogConventions.TimeSpanKey(key)] = time.TotalSeconds;
        else
            LogData[key] = value;
    }

    public void UpsertProperties<TValue>(string prefix, TValue classObject, params string[] ignoredPropertyNames) where TValue : class
    {
        foreach (var propertyInfo in typeof(TValue).GetProperties())
        {
            var ignored = propertyInfo.IgnoredLog() || ignoredPropertyNames.Contains(propertyInfo.Name);
            var logValue = ignored ? ScopedLogConventions.IgnoredLogValue : propertyInfo.GetValue(classObject);
            var logKey = ScopedLogConventions.PropertyLogKeyKey(prefix, propertyInfo);
            Upsert(logKey, logValue ?? string.Empty);
        }
    }

    public void AddToActions(string action) => AddToList(ScopedLogConventions.KeyOfActions, action);

    public void AddToList(string key, object value)
    {
        if (value is string text) value = GetSafeString(text);

        lock (_list)
        {
            if (LogData.TryGetValue(key, out var obj) && obj is List<object> list)
                list.Add(value);
            else
                Upsert(key, new List<object>(8) { value });
        }
    }

    public long Increase(string key, long by = 1)
    {
        lock (_counter)
        {
            var counter = LogData.TryGetValue(key, out var obj)
                ? obj is long i
                    ? i
                    : 0
                : 0;

            counter += by;
            Upsert(key, counter);

            return counter;
        }
    }

    public TimeSpan IncreaseTimeSpentOn(string key, TimeSpan by)
    {
        Increase(ScopedLogConventions.TimeSpentOnCounter(key));
        var updateKey = ScopedLogConventions.TimeSpentOnKey(key);
        lock (_timeUpdater)
        {
            var timeSpent = LogData.TryGetValue(updateKey, out var obj)
                ? obj is double durationSeconds
                    ? durationSeconds
                    : 0
                : 0;

            timeSpent += by.TotalSeconds;
            Upsert(ScopedLogConventions.TimeSpentOnKey(key), timeSpent);

            return TimeSpan.FromSeconds(timeSpent);
        }
    }
}