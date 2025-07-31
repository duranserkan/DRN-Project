using System.Collections.Concurrent;
using System.Text.Json;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Utils.DependencyInjection.Attributes;
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

    private readonly Lock _timeUpdater = new();
    private readonly Lock _counter = new();
    private readonly Lock _list = new();

    internal ConcurrentDictionary<string, object> LogData { get; set; } = new(1, 32);

    public ScopedLog(IAppSettings appSettings)
    {
        Add(nameof(ScopedLog), true);
        Add("App_Name", appSettings.ApplicationName);
        Add("App_InstanceId", AppConstants.AppInstanceId);
        Add("App_NexusId", appSettings.NexusAppSettings.AppId);
        Add("App_NexusInstanceId", appSettings.NexusAppSettings.AppInstanceId);
        Add("App_Environment", appSettings.Environment.ToString());
        Add("App_Environment_MachineName", Environment.MachineName);
        Add(ScopedLogConventions.KeyOfScopeCreatedAt, DateTimeProvider.UtcNow);
    }

    public TimeSpan ScopeDuration => DateTimeProvider.UtcNow - (DateTimeOffset)LogData[ScopedLogConventions.KeyOfScopeCreatedAt];

    public IReadOnlyDictionary<string, object> Logs
    {
        get
        {
            Add(ScopedLogConventions.KeyOfScopeDuration, ScopeDuration);

            return new Dictionary<string, object>(LogData);
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

        if (exception == null) return;
        Add(ScopedLogConventions.KeyOfWarningHasException, true);
        Add(ScopedLogConventions.KeyOfExceptionType, exception.GetType().FullName ?? exception.GetType().Name);
        Add(ScopedLogConventions.KeyOfExceptionMessage, exception.Message);
        Add(ScopedLogConventions.KeyOfExceptionStackTrace, exception.StackTrace ?? string.Empty);

        if (exception.InnerException == null) return;

        Add(ScopedLogConventions.KeyOfInnerExceptionType, exception.InnerException.GetType().FullName ?? exception.InnerException.GetType().Name);
        Add(ScopedLogConventions.KeyOfInnerExceptionMessage, exception.InnerException.Message);
        Add(ScopedLogConventions.KeyOfInnerExceptionStackTrace, exception.InnerException.StackTrace ?? string.Empty);
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

    public IScopedLog AddIfNotNullOrEmpty(string key, string value)
    {
        if (!string.IsNullOrEmpty(value))
            Add(key, value);

        return this;
    }

    public IScopedLog WithLoggerName(string? name) => Add(ScopedLogConventions.KeyOfLoggerName, name ?? "n/a");
    public IScopedLog WithTraceIdentifier(string traceIdentifier) => Add(ScopedLogConventions.KeyOfTraceIdentifier, traceIdentifier);

    public IScopedLog AddProperties<TValue>(string prefix, TValue classObject, params string[] ignoredPropertyNames) where TValue : class
    {
        foreach (var propertyInfo in typeof(TValue).GetProperties())
        {
            var ignored = propertyInfo.IgnoredLog() || ignoredPropertyNames.Contains(propertyInfo.Name);
            var logValue = ignored ? ScopedLogConventions.IgnoredLogValue : propertyInfo.GetValue(classObject);
            var logKey = ScopedLogConventions.PropertyLogKey(prefix, propertyInfo);
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

    //todo add tests
    public long Increase(string key, long by = 1, string prefix = ScopedLogConventions.Stats)
    {
        var counterKey = $"{prefix}{key}";
        lock (_counter)
        {
            var counter = LogData.TryGetValue(counterKey, out var obj) && obj is long i
                ? i
                : 0;

            counter += by;
            Add(counterKey, counter);

            return counter;
        }
    }

    //todo add tests
    public TimeSpan IncreaseTimeSpentOn(string key, TimeSpan by, string prefix = ScopedLogConventions.Stats)
    {
        Increase(ScopedLogConventions.TimeSpentOnCounter(key, prefix: prefix), prefix: string.Empty);
        var updateKey = ScopedLogConventions.TimeSpentOnKey(key, prefix: prefix);
        lock (_timeUpdater)
        {
            var timeSpent = LogData.TryGetValue(updateKey, out var obj) && obj is double durationSeconds
                ? durationSeconds
                : 0;

            timeSpent += by.TotalSeconds;
            Add(updateKey, timeSpent);

            return TimeSpan.FromSeconds(timeSpent);
        }
    }

    //todo add tests
    public ScopeDuration Measure(string key) => new(key, this);

    public override string ToString() => JsonSerializer.Serialize(Logs);
}