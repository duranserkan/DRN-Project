using System.Collections.Concurrent;
using System.Text.Json;
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
        Add(ScopedLogConventions.KeyOfScopeCreatedAt, DateTimeOffset.UtcNow);
        Add(nameof(ScopedLog), true);
        Add(nameof(AppConstants.ApplicationName), AppConstants.ApplicationName);
        Add(nameof(AppConstants.ApplicationId), AppConstants.ApplicationId);
        Add(nameof(Environment.MachineName), Environment.MachineName);
    }

    public TimeSpan ScopeDuration => DateTimeOffset.UtcNow - (DateTimeOffset)LogData[ScopedLogConventions.KeyOfScopeCreatedAt];

    public IReadOnlyDictionary<string, object> Logs
    {
        get
        {
            Add(ScopedLogConventions.KeyOfScopeDuration, ScopeDuration);

            return new SortedDictionary<string, object>(LogData);
        }
    }

    public bool HasException { get; private set; }
    public bool HasWarning { get; private set; }

    public void AddException(Exception exception)
    {
        HasException = true;
        Add(ScopedLogConventions.KeyOfExceptionType, exception.GetType().FullName ?? string.Empty);
        Add(ScopedLogConventions.KeyOfExceptionMessage, exception.Message);
        Add(ScopedLogConventions.KeyOfExceptionStackTrace, exception.StackTrace ?? string.Empty);
    }

    public void AddWarning(string warningMessage)
    {
        HasWarning = true;
        Add(ScopedLogConventions.KeyOfWarningMessage, warningMessage);
    }

    public IScopedLog Add(string key, object value)
    {
        if (value.IgnoredLog())
            LogData[key] = ScopedLogConventions.IgnoredLogValue;
        else if (value is string text)
            LogData[key] = GetSafeString(text);
        else if (value is TimeSpan time)
            LogData[ScopedLogConventions.TimeSpanKey(key)] = time.TotalSeconds;
        else
            LogData[key] = value;

        return this;
    }

    public IScopedLog WithLoggerName(string? name)
    {
        Add(ScopedLogConventions.KeyOfLoggerName, name ?? "n/a");

        return this;
    }

    public IScopedLog AddProperties<TValue>(string prefix, TValue classObject, params string[] ignoredPropertyNames) where TValue : class
    {
        foreach (var propertyInfo in typeof(TValue).GetProperties())
        {
            var ignored = propertyInfo.IgnoredLog() || ignoredPropertyNames.Contains(propertyInfo.Name);
            var logValue = ignored ? ScopedLogConventions.IgnoredLogValue : propertyInfo.GetValue(classObject);
            var logKey = ScopedLogConventions.PropertyLogKeyKey(prefix, propertyInfo);
            Add(logKey, logValue ?? string.Empty);
        }

        return this;
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
                Add(key, new List<object>(8) { value });
        }
    }

    public long Increase(string key, long by = 1)
    {
        lock (_counter)
        {
            var counter = LogData.TryGetValue(key, out var obj) && obj is long i
                ? i
                : 0;

            counter += by;
            Add(key, counter);

            return counter;
        }
    }

    public TimeSpan IncreaseTimeSpentOn(string key, TimeSpan by)
    {
        Increase(ScopedLogConventions.TimeSpentOnCounter(key));
        var updateKey = ScopedLogConventions.TimeSpentOnKey(key);
        lock (_timeUpdater)
        {
            var timeSpent = LogData.TryGetValue(updateKey, out var obj) && obj is double durationSeconds
                ? durationSeconds
                : 0;

            timeSpent += by.TotalSeconds;
            Add(ScopedLogConventions.TimeSpentOnKey(key), timeSpent);

            return TimeSpan.FromSeconds(timeSpent);
        }
    }

    public override string ToString()
    {
        return JsonSerializer.Serialize(Logs);
    }
}