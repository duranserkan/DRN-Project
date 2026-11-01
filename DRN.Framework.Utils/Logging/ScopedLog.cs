using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Settings;
using DRN.Framework.Utils.Time;
using DRN.Framework.Utils.Logging.Diagnostics;

namespace DRN.Framework.Utils.Logging;

[IgnoreLog]
[Scoped<IScopedLog>]
public class ScopedLog : IScopedLog
{
    private static class PropertyCache<TValue> where TValue : class
    {
        private static readonly PropertyInfo[] Properties = typeof(TValue)
            .GetProperties(BindingFlag.InstancePublic)
            .Where(property => property.GetMethod is { IsPublic: true } && property.GetIndexParameters().Length == 0)
            .ToArray();
        private static readonly ConcurrentDictionary<Type, DiagnosticTypeMetadata.Member[]> RuntimeProperties = new();

        internal static DiagnosticTypeMetadata.Member[] For(Type runtimeType) => RuntimeProperties.GetOrAdd(runtimeType, static type =>
            Properties.Select(property => DiagnosticTypeMetadata.ForProperty(property, type)).ToArray());
    }

    // Duration, correlation, trace, and four primary-event fields.
    private const int SnapshotMetadataFieldCount = 7;
    // Leave room for scope metadata and limit markers added when snapshots are materialized.
    private const int SnapshotReserveBytes = 2048;
    private const int MetricValueReserveBytes = 40;
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
            case DiagnosticCapturedValue captured:
                return captured with { Value = captured.Value == null ? null : CloneLogValue(captured.Value) };
            case IReadOnlyDictionary<string, object> readOnlyDictionary:
                return readOnlyDictionary.ToDictionary(pair => pair.Key, pair => pair.Value == null ? null! : CloneLogValue(pair.Value));
            case IDictionary dictionary:
            {
                var clone = new Dictionary<object, object?>(dictionary.Count);
                foreach (DictionaryEntry entry in dictionary)
                    clone[entry.Key] = entry.Value == null ? null : CloneLogValue(entry.Value);

                return clone;
            }
            case IEnumerable<object> objectEnumerable:
                return objectEnumerable.Select(item => item == null ? null! : CloneLogValue(item)).ToList();
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
    private readonly Dictionary<string, int> _entryBytes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DiagnosticCaptureResult> _captures = new(StringComparer.Ordinal);
    private int _storedBytes;
    private volatile bool _captureLimited;
    private readonly DateTimeOffset _createdAt = DateTimeProvider.UtcNow;
    internal IDiagnosticSanitizer Sanitizer { get; }
    private readonly bool _isDevelopment;

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
        scopeEvent = Sanitizer.SanitizeValue("Event", scopeEvent) as ScopeEvent
                     ?? new ScopeEvent(new Microsoft.Extensions.Logging.EventId(scopeEvent.Id.Id), Reason: DiagnosticSanitizer.Limited);
        using var sizing = DiagnosticJsonSize.Rent();
        var size = sizing.Measure(scopeEvent);
        lock (_sync)
        {
            if (_event == null)
            {
                if (size + _storedBytes > Sanitizer.Options.MaxScopeBytes - SnapshotReserveBytes)
                {
                    scopeEvent = new ScopeEvent(new Microsoft.Extensions.Logging.EventId(scopeEvent.Id.Id, DiagnosticSanitizer.Limited), Reason: DiagnosticSanitizer.Limited);
                    size = sizing.Measure(scopeEvent);
                    _captureLimited = true;
                }
                _storedBytes += size;
                Volatile.Write(ref _event, scopeEvent);
                return this;
            }
        }
        AddToList(ScopedLogConventions.KeyOfAdditionalEvents, scopeEvent);
        return this;
    }

    private Dictionary<string, object> LogData { get; } = new(32, StringComparer.Ordinal);

    public ScopedLog(IAppSettings appSettings) : this(appSettings, DiagnosticSanitizer.FromSettings(appSettings)) { }

    public ScopedLog(IAppSettings appSettings, IDiagnosticSanitizer sanitizer)
    {
        if (sanitizer is DiagnosticSanitizer { RedactionEnabled: false })
            throw new ArgumentException("The display sanitizer cannot be used for scoped log exports.", nameof(sanitizer));
        _isDevelopment = appSettings.IsDevelopmentEnvironment;
        Sanitizer = sanitizer;
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
                return DateTimeProvider.UtcNow - _createdAt;
        }
    }

    // Caller holds _sync. Sorted insertion appends without shifting existing entries.
    private SortedList<string, object> SnapshotLogDataLocked(int additionalCapacity = 0, bool cloneValues = true)
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
                _ => cloneValues ? CloneLogValue(value) : value
            });
        }

        return snapshot;
    }

    public IReadOnlyDictionary<string, object> GetLogs() => GetLogs(redact: true);

    /// <summary>Returns a detached bounded snapshot. Unredacted reads are allowed only in Development.</summary>
    public IReadOnlyDictionary<string, object> GetLogs(bool redact)
    {
        if (!redact && !_isDevelopment)
            throw new InvalidOperationException("Unredacted diagnostic snapshots are only available in Development.");
        var status = Sanitizer.GetStatus();
        var reader = Sanitizer is DiagnosticSanitizer ? new DiagnosticSanitizer(Sanitizer.Options) : null;
        IReadOnlyDictionary<string, object> output;
        lock (_sync)
        {
            var snapshot = SnapshotLogDataLocked(SnapshotMetadataFieldCount, cloneValues: reader == null);
            snapshot[ScopeDurationKey] = (DateTimeProvider.UtcNow - _createdAt).TotalSeconds;
            snapshot[ScopedLogConventions.KeyOfCorrelationId] = CorrelationId;
            if (TraceId != null)
                snapshot[ScopedLogConventions.KeyOfTraceId] = TraceId;
            else
                snapshot.Remove(ScopedLogConventions.KeyOfTraceId);
            foreach (var pair in status) snapshot[pair.Key] = pair.Value;
            if (_captureLimited) snapshot["DiagnosticScopeLimit"] = true;

            var scopeEvent = Event;
            if (scopeEvent != null)
            {
                snapshot[ScopedLogConventions.KeyOfEventId] = scopeEvent.Id.Id;
                snapshot[ScopedLogConventions.KeyOfEventName] = scopeEvent.Id.Name ?? string.Empty;
                snapshot[ScopedLogConventions.KeyOfEventOutcome] = scopeEvent.Outcome ?? string.Empty;
                snapshot[ScopedLogConventions.KeyOfEventReason] = scopeEvent.Reason ?? string.Empty;
            }
            // Native materialization creates the detached tree while private lists are stable.
            // It only reads captured values and never invokes the original model getters.
            output = reader?.MaterializeSnapshot(snapshot, redact) ?? snapshot;
        }
        return DiagnosticLogFormatter.BoundSnapshot(output, Sanitizer.Options.MaxScopeBytes);
    }

    public bool HasException { get; private set; }
    public bool HasWarning { get; private set; }

    public void AddException(Exception exception, string? message = null)
    {
        try { AddExceptionCore(exception, message); }
        catch (Exception) { _captureLimited = true; }
    }

    private void AddExceptionCore(Exception exception, string? message)
    {
        HasException = true;

        if (!string.IsNullOrWhiteSpace(message))
            Add(ScopedLogConventions.KeyOfExceptionLogMessage, message);

        Add(ScopedLogConventions.KeyOfExceptionType, exception.GetType().FullName ?? exception.GetType().Name);
        Add(ScopedLogConventions.KeyOfExceptionMessage, exception.Message);
        Add(ScopedLogConventions.KeyOfExceptionStackTrace, exception.StackTrace ?? string.Empty);

        if (exception is DrnException drnException)
        {
            if (drnException.Data.Count > Sanitizer.Options.MaxCollectionEntries) _captureLimited = true;
            foreach (var kvp in drnException.Data.Take(Sanitizer.Options.MaxCollectionEntries))
                Add(kvp.Key.Length > Sanitizer.Options.MaxKeyBytes ? kvp.Key : $"{ScopedLogConventions.KeyOfExceptionData}_{kvp.Key}", kvp.Value);
        }

        if (exception.InnerException == null) return;

        Add(ScopedLogConventions.KeyOfInnerExceptionType, exception.InnerException.GetType().FullName ?? exception.InnerException.GetType().Name);
        Add(ScopedLogConventions.KeyOfInnerExceptionMessage, exception.InnerException.Message);
        Add(ScopedLogConventions.KeyOfInnerExceptionStackTrace, exception.InnerException.StackTrace ?? string.Empty);
    }

    public void AddWarning(string warningMessage, Exception? exception = null)
    {
        try { AddWarningCore(warningMessage, exception); }
        catch (Exception) { _captureLimited = true; }
    }

    private void AddWarningCore(string warningMessage, Exception? exception)
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

    public IScopedLog Add(string key, object value) => AddCore(key, value, null);

    /// <summary>Captures a dictionary with classifications for exact keys at this level.</summary>
    public IScopedLog AddDictionary<TValue>(string key, IReadOnlyDictionary<string, TValue> values,
        params DiagnosticKeyClassification[] classifications)
    {
        var safeKey = Sanitizer.SanitizeKey(key);
        if (Sanitizer is DiagnosticSanitizer native) StoreCapture(safeKey, native.CaptureDictionary(key, values, classifications, deferRedaction: true));
        else Store(safeKey, DiagnosticSanitizer.Invalid);
        return this;
    }

    /// <summary>Exempts one supported scalar from built-in name rules, with an application-authored reason.</summary>
    public IScopedLog Add(string key, object? value, string exemptionReason) => AddCore(key, value, null, exemptionReason ?? string.Empty);

    /// <summary>Captures a named entry using explicit, exact application-authored name exceptions.</summary>
    public IScopedLog AddWithPolicy(string key, object value, DiagnosticCapturePolicy policy) => AddCore(key, value, policy);

    private IScopedLog AddCore(string key, object? value, DiagnosticCapturePolicy? policy, string? exemptionReason = null)
    {
        if (value is TimeSpan time && key.Length <= Sanitizer.Options.MaxKeyBytes)
        {
            key = ScopedLogConventions.TimeSpanKey(key);
            value = time.TotalSeconds;
        }
        var safeKey = Sanitizer.SanitizeKey(key);
        if (Sanitizer is DiagnosticSanitizer native)
            StoreCapture(safeKey, exemptionReason == null ? native.Capture(key, value, policy, deferRedaction: true) :
                native.CaptureWithReason(key, value, exemptionReason, deferRedaction: true));
        else
            Store(safeKey, Sanitizer.SanitizeValue(key, exemptionReason != null && !DiagnosticSanitizer.IsSimpleValue(value)
                ? DiagnosticSanitizer.Invalid : value) ?? string.Empty);

        return this;
    }

    private void StoreCapture(string key, DiagnosticCaptureResult capture) =>
        Store(key, capture.Value ?? string.Empty, capture);

    private void Store(string key, object value, DiagnosticCaptureResult? capture = null)
    {
        using var sizing = DiagnosticJsonSize.Rent();
        var size = sizing.MeasureEntry(key, value);
        lock (_sync)
        {
            var previousSize = _entryBytes.GetValueOrDefault(key);
            if ((!LogData.ContainsKey(key) && LogData.Count >= Sanitizer.Options.MaxNodes) ||
                _storedBytes - previousSize + size > Sanitizer.Options.MaxScopeBytes - SnapshotReserveBytes)
            {
                _captureLimited = true;
                if (LogData.Remove(key))
                {
                    _captures.Remove(key);
                    _entryBytes.Remove(key);
                    _storedBytes -= previousSize;
                }
                return;
            }
            LogData[key] = value;
            _captures.Remove(key);
            if (capture != null && ReferenceEquals(capture.Value, value)) _captures[key] = capture;
            _entryBytes[key] = size;
            _storedBytes += size - previousSize;
        }
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
        try { return CopyFromCore(source); }
        catch (Exception) { _captureLimited = true; return this; }
    }

    private IScopedLog CopyFromCore(IScopedLog source)
    {
        Dictionary<string, DiagnosticCaptureResult>? captures = null;
        IReadOnlyDictionary<string, object> sourceLogs;
        if (source is ScopedLog scopedLog && Sanitizer is DiagnosticSanitizer)
        {
            lock (scopedLog._sync)
            {
                sourceLogs = scopedLog.SnapshotLogDataLocked();
                // Stored values are detached and never exposed. Snapshot and provenance are bound under one lock.
                captures = scopedLog._captures.ToDictionary(pair => pair.Key,
                    pair => pair.Value with { Value = sourceLogs[pair.Key] }, StringComparer.Ordinal);
            }
        }
        else sourceLogs = source.GetLogs();
        if (sourceLogs.Count > Sanitizer.Options.MaxNodes) _captureLimited = true;
        foreach (var (key, value) in sourceLogs.Take(Sanitizer.Options.MaxNodes))
        {
            if (key == ScopedLogConventions.KeyOfAdditionalEvents && value is IList<object> events)
                foreach (var additionalEvent in events.Take(Sanitizer.Options.MaxCollectionEntries))
                    AddToList(key, additionalEvent);
            else
            {
                if (key is ScopedLogConventions.KeyOfCorrelationId or ScopedLogConventions.KeyOfTraceId or
                    ScopedLogConventions.KeyOfEventId or ScopedLogConventions.KeyOfEventName or
                    ScopedLogConventions.KeyOfEventOutcome or ScopedLogConventions.KeyOfEventReason) continue;
                if (Sanitizer is DiagnosticSanitizer native && captures?.TryGetValue(key, out var capture) == true)
                    StoreCapture(Sanitizer.SanitizeKey(key), native.CopyCapture(capture, deferRedaction: true));
                else Add(key, value);
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
        => AddPropertiesGuarded(prefix, classObject, null, ignoredPropertyNames);

    /// <summary>Captures properties with explicit exceptions for the root and individual structural nodes.</summary>
    public IScopedLog AddPropertiesWithPolicy<TValue>(string prefix, TValue classObject, DiagnosticCapturePolicy policy,
        params string[] ignoredPropertyNames) where TValue : class => AddPropertiesGuarded(prefix, classObject, policy, ignoredPropertyNames);

    private IScopedLog AddPropertiesGuarded<TValue>(string prefix, TValue classObject, DiagnosticCapturePolicy? policy,
        string[] ignoredPropertyNames) where TValue : class
    {
        try { return AddPropertiesCore(prefix, classObject, policy, ignoredPropertyNames); }
        catch (Exception)
        {
            _captureLimited = true;
            Store(Sanitizer.SanitizeKey(prefix), "[diagnostic field unavailable]");
            return this;
        }
    }

    private IScopedLog AddPropertiesCore<TValue>(string prefix, TValue classObject, DiagnosticCapturePolicy? policy,
        string[] ignoredPropertyNames) where TValue : class
    {
        policy?.Validate(Sanitizer.Options);
        var native = Sanitizer as DiagnosticSanitizer;
        var typedModel = DiagnosticSanitizer.IsModelType(classObject.GetType());
        var rootNamePath = typedModel ? "" : null;
        var rootExempt = native != null && policy?.Exempts(DiagnosticCaptureKind.ObjectRoot, [prefix]) == true;
        if (prefix.Length > Sanitizer.Options.MaxKeyBytes) { Add("DiagnosticPropertyPrefix", DiagnosticSanitizer.Limited); return this; }
        if (classObject.IgnoredLog()) { Store(Sanitizer.SanitizeKey(prefix), ScopedLogConventions.IgnoredLogValue); return this; }
        if (DiagnosticSanitizer.IsClassified(classObject.GetType()) ||
            (native?.IsSensitive(prefix, rootExempt, rootNamePath) ?? Sanitizer.IsSensitive(prefix)))
        {
            if (native == null) Store(Sanitizer.SanitizeKey(prefix), DiagnosticSanitizer.Redacted);
            else StoreCapture(Sanitizer.SanitizeKey(prefix), native.Capture(prefix, classObject, policy,
                DiagnosticCaptureKind.ObjectRoot, [prefix], rootExempt, rootNamePath, deferRedaction: true));
            return this;
        }
        var properties = PropertyCache<TValue>.For(classObject.GetType());
        if (properties.Length > Sanitizer.Options.MaxCollectionEntries) _captureLimited = true;
        string[] keys;
        lock (_sync)
        {
            _propertyKeys ??= new();
            var identity = (typeof(TValue), prefix);
            if (!_propertyKeys.TryGetValue(identity, out keys!))
            {
                keys = new string[Math.Min(properties.Length, Sanitizer.Options.MaxCollectionEntries)];
                for (var index = 0; index < keys.Length; index++)
                    keys[index] = ScopedLogConventions.PropertyLogKey(prefix, properties[index].Property);
                if (_propertyKeys.Count < Sanitizer.Options.MaxNodes) _propertyKeys.Add(identity, keys);
            }
        }

        // User getters execute outside the storage lock and may call back into logging.
        for (var index = 0; index < keys.Length; index++)
        {
            var (propertyInfo, ignoredByAttribute, sensitive, exempt) = properties[index];
            var namePath = typedModel ? propertyInfo.Name : null;
            var segments = new[] { prefix, propertyInfo.Name };
            exempt |= native != null && policy?.Exempts(DiagnosticCaptureKind.Property, segments) == true;
            var ignored = ignoredByAttribute || ignoredPropertyNames.Contains(propertyInfo.Name);
            if (ignored) Store(Sanitizer.SanitizeKey(keys[index]), ScopedLogConventions.IgnoredLogValue);
            else if (native == null && (sensitive || Sanitizer.IsSensitive(keys[index])))
                Store(Sanitizer.SanitizeKey(keys[index]), DiagnosticSanitizer.Redacted);
            else
            {
                try
                {
                    var value = propertyInfo.GetValue(classObject) ?? string.Empty;
                    if (native == null) Add(keys[index], value);
                    else
                    {
                        var key = keys[index];
                        if (value is TimeSpan time && key.Length <= Sanitizer.Options.MaxKeyBytes)
                        {
                            key = ScopedLogConventions.TimeSpanKey(key);
                            if (namePath != null) namePath = ScopedLogConventions.TimeSpanKey(namePath);
                            value = time.TotalSeconds;
                        }
                        var capture = native.Capture(key, value, policy, DiagnosticCaptureKind.Property, segments, exempt, namePath,
                            deferRedaction: true, classified: sensitive);
                        capture = capture with { Decisions = [new(prefix, rootExempt, rootNamePath), .. capture.Decisions] };
                        StoreCapture(Sanitizer.SanitizeKey(key), capture);
                    }
                }
                catch (Exception)
                {
                    const string unavailable = "[diagnostic getter unavailable]";
                    if (native == null) Store(Sanitizer.SanitizeKey(keys[index]), unavailable);
                    else StoreCapture(Sanitizer.SanitizeKey(keys[index]), native.Capture(keys[index], unavailable, policy,
                        DiagnosticCaptureKind.Property, segments, exempt, namePath, deferRedaction: true, classified: sensitive));
                }
            }
        }

        return this;
    }

    public void AddToActions(string action) => AddToList(ScopedLogConventions.KeyOfActions, action);

    public void AddToList(string key, object value)
    {
        var capture = Sanitizer is DiagnosticSanitizer native ? native.Capture(key, value, deferRedaction: true) : null;
        value = (capture != null ? capture.Value : Sanitizer.SanitizeValue(key, value)) ?? string.Empty;
        key = Sanitizer.SanitizeKey(key);
        using var sizing = DiagnosticJsonSize.Rent();
        var itemSize = sizing.Measure(value) + 1;

        lock (_sync)
        {
            if (LogData.TryGetValue(key, out var obj) && obj is List<object> list)
            {
                if (list.Count >= Sanitizer.Options.MaxCollectionEntries || _storedBytes + itemSize > Sanitizer.Options.MaxScopeBytes - SnapshotReserveBytes)
                { _captureLimited = true; return; }
                list.Add(value);
                // Only URL rules applied to every item can authorize a native copy of the list.
                if (capture != null && _captures.TryGetValue(key, out var previous))
                    _captures[key] = new DiagnosticCaptureResult(list, [.. previous.Decisions, .. capture.Decisions],
                        (previous.UrlRules ?? []).Intersect(capture.UrlRules ?? [], StringComparer.Ordinal).ToArray());
                else _captures.Remove(key);
                _storedBytes += itemSize;
                _entryBytes[key] = _entryBytes.GetValueOrDefault(key) + itemSize;
            }
            else
            {
                var items = new List<object>(8) { value };
                Store(key, items, capture == null ? null : capture with { Value = items });
            }
        }
    }

    public long Increase(string key, long by = 1, string prefix = ScopedLogConventions.Stats)
    {
        if (key.Length + prefix.Length > Sanitizer.Options.MaxKeyBytes) { _captureLimited = true; return 0; }
        lock (_sync)
        {
            _counterKeys ??= new();
            if (_counterKeys.TryGetValue((prefix, key), out var cached)) return cached.Length == 0 ? 0 : IncreaseLocked(cached, by, prefix, key);
            if (_counterKeys.Count >= Sanitizer.Options.MaxNodes) { _captureLimited = true; return 0; }
        }
        var combined = string.Concat(prefix, key);
        var safe = IsMetricSensitive(combined) ? "" : Sanitizer.SanitizeKey(combined);
        lock (_sync)
        {
            if (_counterKeys.TryGetValue((prefix, key), out var cached)) return cached.Length == 0 ? 0 : IncreaseLocked(cached, by, prefix, key);
            if (_counterKeys.Count >= Sanitizer.Options.MaxNodes && !_counterKeys.ContainsKey((prefix, key))) { _captureLimited = true; return 0; }
            if (safe.Length > 0 && !ReserveMetric(safe)) safe = "";
            _counterKeys[(prefix, key)] = safe;
            return safe.Length == 0 ? 0 : IncreaseLocked(safe, by, prefix, key);
        }
    }

    private bool ReserveMetric(string key)
    {
        if (LogData.ContainsKey(key)) return true;
        using var sizing = DiagnosticJsonSize.Rent();
        var size = sizing.Measure(key) + MetricValueReserveBytes;
        if (LogData.Count >= Sanitizer.Options.MaxNodes || _storedBytes + size > Sanitizer.Options.MaxScopeBytes - SnapshotReserveBytes)
        { _captureLimited = true; return false; }
        _storedBytes += size;
        _entryBytes[key] = size;
        return true;
    }

    // Caller holds _sync and supplies the fully composed key.
    private long IncreaseLocked(string counterKey, long by, string prefix, string name)
    {
        if (LogData.TryGetValue(counterKey, out var obj) && obj is CounterValue counter)
            return counter.Value += by;

        var value = (obj is long existing ? existing : 0) + by;
        LogData[counterKey] = new CounterValue(value);
        CaptureMetric(counterKey, prefix, name);
        return value;
    }

    private void CaptureMetric(string key, string prefix, string name)
    {
        _captures.Remove(key);
        if (Sanitizer is DiagnosticSanitizer)
            // CopyFrom replaces the private metric value with its detached scalar under the same lock.
            _captures[key] = new DiagnosticCaptureResult(LogData[key],
                [new(prefix + name, true), new(key, true)], Sanitizer.Options.SensitivePathSegments);
    }

    public TimeSpan IncreaseTimeSpentOn(string key, TimeSpan by, string prefix = ScopedLogConventions.Stats)
    {
        if (key.Length + prefix.Length > Sanitizer.Options.MaxKeyBytes) { _captureLimited = true; return TimeSpan.Zero; }
        (string Counter, string Duration) keys;
        lock (_sync)
        {
            _durationKeys ??= new();
            if (_durationKeys.TryGetValue((prefix, key), out keys)) return IncreaseDurationLocked(keys, by, prefix, key);
            if (_durationKeys.Count >= Sanitizer.Options.MaxNodes) { _captureLimited = true; return TimeSpan.Zero; }
        }
        keys = IsMetricSensitive(prefix + key) ? ("", "") :
            (Sanitizer.SanitizeKey(ScopedLogConventions.TimeSpentOnCounter(key, prefix)), Sanitizer.SanitizeKey(ScopedLogConventions.TimeSpentOnKey(key, prefix)));
        lock (_sync)
        {
            if (_durationKeys.TryGetValue((prefix, key), out var cached)) return IncreaseDurationLocked(cached, by, prefix, key);
            if (_durationKeys.Count >= Sanitizer.Options.MaxNodes && !_durationKeys.ContainsKey((prefix, key))) { _captureLimited = true; return TimeSpan.Zero; }
            if (keys.Counter.Length > 0 && (!ReserveMetric(keys.Counter) || !ReserveMetric(keys.Duration))) keys = ("", "");
            _durationKeys[(prefix, key)] = keys;
            return IncreaseDurationLocked(keys, by, prefix, key);
        }
    }

    private bool IsMetricSensitive(string key) => Sanitizer is DiagnosticSanitizer native
        ? native.IsSensitive(key, exempt: true) : Sanitizer.IsSensitive(key);

    private TimeSpan IncreaseDurationLocked((string Counter, string Duration) keys, TimeSpan by, string prefix, string name)
    {
        if (keys.Counter.Length == 0) return TimeSpan.Zero;
        IncreaseLocked(keys.Counter, 1, prefix, name);
        double timeSpent;
        if (LogData.TryGetValue(keys.Duration, out var obj) && obj is DurationValue duration)
            timeSpent = duration.Value += by.TotalSeconds;
        else
        {
            timeSpent = (obj is double existing ? existing : 0) + by.TotalSeconds;
            LogData[keys.Duration] = new DurationValue(timeSpent);
            CaptureMetric(keys.Duration, prefix, name);
        }
        return TimeSpan.FromSeconds(timeSpent);
    }

    public ScopeDuration Measure(string key) => new(key, this);
    public ScopeDuration Measure(object callerObject, string? caller = null)
    {
        if (caller?.Length > Sanitizer.Options.MaxKeyBytes) return Measure("DiagnosticMeasurementLimited");
        string key;
        lock (_sync)
        {
            _measurementKeys ??= new();
            var identity = (callerObject.GetType(), caller);
            if (_measurementKeys.TryGetValue(identity, out key!))
                return Measure(key);

            key = $"{identity.Item1.FullName}.{caller}";
            if (_measurementKeys.Count < Sanitizer.Options.MaxNodes) _measurementKeys.Add(identity, key);
        }

        return Measure(key);
    }

    public override string ToString() => JsonSerializer.Serialize(GetLogs());
}
