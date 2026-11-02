using System.Collections;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Settings;
using DRN.Framework.Utils.Time;
using DRN.Framework.Utils.Logging.Diagnostics;

namespace DRN.Framework.Utils.Logging;

[IgnoreLog]
[Scoped<IScopedLog>]
public class ScopedLog : IScopedLog
{
    // Duration, correlation, and trace. Events are attached after diagnostic bounds.
    private const int SnapshotMetadataFieldCount = 3;
    // Leave room for scope metadata and limit markers added when snapshots are materialized.
    private const int SnapshotReserveBytes = 2048;
    private const int MetricValueReserveBytes = 40;
    private static readonly string ScopeDurationKey = ScopedLogConventions.TimeSpanKey(ScopedLogConventions.KeyOfScopeDuration);
    private static readonly object TrueValue = true;
    private static readonly object AppInstanceIdValue = AppConstants.AppInstanceId;

    private enum EntryKind { Reservation, Diagnostic, Counter, Duration }

    // EntryKind selects the active numeric representation.
    [StructLayout(LayoutKind.Explicit)]
    private struct MetricValue
    {
        [FieldOffset(0)] public long Counter;
        [FieldOffset(0)] public double Duration;
    }

    // Metrics remain unboxed in storage. Reservations never appear in snapshots or entry counts.
    private record struct LogEntry(EntryKind Kind, int Bytes, object? Value,
        DiagnosticCaptureMetadata Capture = default, MetricValue Metric = default)
    {
        internal readonly bool HasValue => Kind != EntryKind.Reservation;
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
                return objectEnumerable.Select(CloneLogValue).ToList();
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
    private List<ScopeEvent>? _additionalEvents;
    private long _omittedEventCount;
    private readonly Dictionary<string, LogEntry> _entries;
    private int _entryCount;
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
        lock (_sync)
        {
            if (_event == null)
                Volatile.Write(ref _event, scopeEvent);
            else AddAdditionalEventLocked(scopeEvent);
        }
        return this;
    }

    // Caller holds _sync so retention and omission accounting are atomic.
    private void AddAdditionalEventLocked(ScopeEvent scopeEvent)
    {
        if ((_additionalEvents?.Count ?? 0) < ScopeEventSnapshot.MaxAdditionalEvents)
            (_additionalEvents ??= []).Add(scopeEvent);
        else RecordOmittedEventsLocked(1);
    }

    private void RecordOmittedEventsLocked(long count) =>
        _omittedEventCount += Math.Min(count, long.MaxValue - _omittedEventCount);

    private void CopyEvents(ScopeEventSnapshot events)
    {
        lock (_sync)
        {
            if (events.Primary is { } primary)
            {
                if (_event == null) Volatile.Write(ref _event, primary);
                else AddAdditionalEventLocked(primary);
            }
            if (events.Additional is { Length: > 0 } additional)
                foreach (var scopeEvent in additional) AddAdditionalEventLocked(scopeEvent);
            RecordOmittedEventsLocked(events.OmittedCount);
        }
    }

    public ScopedLog(IAppSettings appSettings) : this(appSettings, DiagnosticSanitizer.FromSettings(appSettings)) { }

    public ScopedLog(IAppSettings appSettings, IDiagnosticSanitizer sanitizer)
    {
        if (sanitizer is DiagnosticSanitizer { RedactionEnabled: false })
            throw new ArgumentException("The display sanitizer cannot be used for scoped log exports.", nameof(sanitizer));
        _isDevelopment = appSettings.IsDevelopmentEnvironment;
        Sanitizer = sanitizer;
        _entries = new Dictionary<string, LogEntry>(Math.Clamp(sanitizer.Options.MaxNodes, 0, 32), StringComparer.Ordinal);
        Add(nameof(ScopedLog), TrueValue);
        Add("App_Name", appSettings.ApplicationName);
        Add("App_InstanceId", AppInstanceIdValue);
        Add("App_NexusId", appSettings.NexusAppSettings.AppId);
        Add("App_NexusInstanceId", appSettings.NexusAppSettings.AppInstanceId);
        Add("App_Environment", appSettings.Environment.ToString());
        Add("App_Environment_MachineName", Environment.MachineName);
        Add(ScopedLogConventions.KeyOfScopeCreatedAt, DateTimeProvider.UtcNow);
    }

    public TimeSpan ScopeDuration => DateTimeProvider.UtcNow - _createdAt;

    // Caller holds _sync. Sorted insertion appends without shifting existing entries.
    private SortedList<string, object> SnapshotLogDataLocked(int additionalCapacity = 0, bool cloneValues = true)
    {
        var snapshot = new SortedList<string, object>(checked(_entryCount + additionalCapacity), StringComparer.Ordinal);
        var keys = new string[_entryCount];
        var index = 0;
        foreach (var pair in _entries)
            if (pair.Value.HasValue) keys[index++] = pair.Key;
        Array.Sort(keys, snapshot.Comparer);

        foreach (var key in keys)
        {
            var entry = _entries[key];
            snapshot.Add(key, entry.Kind switch
            {
                EntryKind.Counter => entry.Metric.Counter,
                EntryKind.Duration => entry.Metric.Duration,
                _ => cloneValues ? CloneLogValue(entry.Value!) : entry.Value!
            });
        }

        return snapshot;
    }

    // Caller holds _sync and supplies the detached snapshot from the same lock.
    private Dictionary<string, DiagnosticCaptureResult> SnapshotCapturesLocked(IReadOnlyDictionary<string, object> snapshot)
    {
        var captures = new Dictionary<string, DiagnosticCaptureResult>(StringComparer.Ordinal);
        foreach (var pair in _entries)
            if (pair.Value.Capture is { HasValue: true } capture)
                captures.Add(pair.Key, capture.Snapshot(snapshot[pair.Key]));
        return captures;
    }

    public IReadOnlyDictionary<string, object> GetLogs() => GetLogs(redact: true);

    /// <summary>Returns detached bounded diagnostics with retained events. Unredacted diagnostic reads require Development.</summary>
    public IReadOnlyDictionary<string, object> GetLogs(bool redact)
    {
        var output = GetDiagnosticLogs(redact, out var events);
        events.WriteTo(output);
        return output;
    }

    internal Dictionary<string, object> GetDiagnosticLogs(bool redact, out ScopeEventSnapshot events)
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

            events = SnapshotEventsLocked();
            // Native materialization creates the detached tree while private lists are stable.
            // It only reads captured values and never invokes the original model getters.
            output = reader?.MaterializeSnapshot(snapshot, redact) ?? snapshot;
        }
        return DiagnosticLogFormatter.BoundSnapshot(output, Sanitizer.Options.MaxScopeBytes);
    }

    private ScopeEventSnapshot SnapshotEventsLocked() => new(_event, _additionalEvents?.ToArray(), _omittedEventCount);

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

    private ScopedLog AddCore(string key, object? value, DiagnosticCapturePolicy? policy, string? exemptionReason = null)
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

    internal void StoreCapture(string key, DiagnosticCaptureResult capture) =>
        Store(key, capture.Value ?? string.Empty, capture);

    internal void Store(string key, object value, DiagnosticCaptureResult? capture = null)
    {
        using var sizing = DiagnosticJsonSize.Rent();
        var size = sizing.MeasureEntry(key, value);
        lock (_sync)
            StoreLocked(key, value, size, capture);
    }

    // Caller holds _sync and supplies the measured JSON entry size.
    private void StoreLocked(string key, object value, int size, DiagnosticCaptureResult? capture)
    {
        var previous = _entries.GetValueOrDefault(key);
        if ((_entryCount >= Sanitizer.Options.MaxNodes && !previous.HasValue) ||
            _storedBytes - previous.Bytes + size > Sanitizer.Options.MaxScopeBytes - SnapshotReserveBytes)
        {
            _captureLimited = true;
            if (previous.HasValue)
            {
                _entries.Remove(key);
                _entryCount--;
                _storedBytes -= previous.Bytes;
            }
            return;
        }
        DiagnosticCaptureMetadata metadata = capture != null && ReferenceEquals(capture.Value, value)
            ? new DiagnosticCaptureMetadata(capture) : default;
        _entries[key] = new LogEntry(EntryKind.Diagnostic, size, value, metadata);
        if (!previous.HasValue) _entryCount++;
        _storedBytes += size - previous.Bytes;
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

    private ScopedLog CopyFromCore(IScopedLog source)
    {
        Dictionary<string, DiagnosticCaptureResult>? captures = null;
        IReadOnlyDictionary<string, object> sourceLogs;
        ScopeEventSnapshot events;
        if (source is ScopedLog scopedLog && Sanitizer is DiagnosticSanitizer)
        {
            lock (scopedLog._sync)
            {
                // Stored values are detached and never exposed. Snapshot and provenance are bound under one lock.
                sourceLogs = scopedLog.SnapshotLogDataLocked();
                captures = scopedLog.SnapshotCapturesLocked(sourceLogs);
                events = scopedLog.SnapshotEventsLocked();
            }
        }
        else if (source is ScopedLog nativeSource)
            sourceLogs = nativeSource.GetDiagnosticLogs(redact: true, out events);
        else
        {
            var diagnostics = new Dictionary<string, object>(source.GetLogs(), StringComparer.Ordinal);
            events = ScopeEventSnapshot.TakeFrom(source.Event, diagnostics);
            sourceLogs = diagnostics;
        }
        if (sourceLogs.Count > Sanitizer.Options.MaxNodes) _captureLimited = true;
        foreach (var (key, value) in sourceLogs.Take(Sanitizer.Options.MaxNodes))
            CopyEntry(key, value, captures);

        HasException |= source.HasException;
        HasWarning |= source.HasWarning;
        CopyEvents(events);
        return this;
    }

    private void CopyEntry(string key, object value, Dictionary<string, DiagnosticCaptureResult>? captures)
    {
        if (key is ScopedLogConventions.KeyOfCorrelationId or ScopedLogConventions.KeyOfTraceId or
            ScopedLogConventions.KeyOfEventId or ScopedLogConventions.KeyOfEventName or
            ScopedLogConventions.KeyOfEventOutcome or ScopedLogConventions.KeyOfEventReason) return;
        if (Sanitizer is DiagnosticSanitizer native && captures?.TryGetValue(key, out var capture) == true)
            StoreCapture(Sanitizer.SanitizeKey(key), native.CopyCapture(capture, deferRedaction: true));
        else Add(key, value);
    }

    public IScopedLog WithLoggerName(string? name) => Add(ScopedLogConventions.KeyOfLoggerName, name ?? "n/a");
    public IScopedLog WithTraceIdentifier(string traceIdentifier) => Add(ScopedLogConventions.KeyOfTraceIdentifier, traceIdentifier);

    public IScopedLog AddProperties<TValue>(string prefix, TValue classObject, params string[] ignoredPropertyNames) where TValue : class
        => AddPropertiesGuarded(prefix, classObject, null, ignoredPropertyNames);

    /// <summary>Captures properties with explicit exceptions for the root and individual structural nodes.</summary>
    public IScopedLog AddPropertiesWithPolicy<TValue>(string prefix, TValue classObject, DiagnosticCapturePolicy policy,
        params string[] ignoredPropertyNames) where TValue : class => AddPropertiesGuarded(prefix, classObject, policy, ignoredPropertyNames);

    private ScopedLog AddPropertiesGuarded<TValue>(string prefix, TValue classObject, DiagnosticCapturePolicy? policy,
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

    private ScopedLog AddPropertiesCore<TValue>(string prefix, TValue classObject, DiagnosticCapturePolicy? policy,
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
        var properties = DiagnosticPropertyCapture.PropertiesFor<TValue>(classObject.GetType());
        if (properties.Length > Sanitizer.Options.MaxCollectionEntries) _captureLimited = true;
        var keys = GetPropertyKeys<TValue>(prefix, properties);
        var root = new DiagnosticNameDecision(prefix, rootExempt, rootNamePath);
        var capture = new DiagnosticPropertyCapture(this, policy, ignoredPropertyNames, root);

        // User getters execute outside the storage lock and may call back into logging.
        for (var index = 0; index < keys.Length; index++)
            capture.Add(classObject, keys[index], properties[index]);

        return this;
    }

    private string[] GetPropertyKeys<TValue>(string prefix, DiagnosticTypeMetadata.Member[] properties) where TValue : class
    {
        lock (_sync)
        {
            _propertyKeys ??= new();
            var identity = (typeof(TValue), prefix);
            if (!_propertyKeys.TryGetValue(identity, out var keys))
            {
                keys = new string[Math.Min(properties.Length, Sanitizer.Options.MaxCollectionEntries)];
                for (var index = 0; index < keys.Length; index++)
                    keys[index] = ScopedLogConventions.PropertyLogKey(prefix, properties[index].Property);
                if (_propertyKeys.Count < Sanitizer.Options.MaxNodes) _propertyKeys.Add(identity, keys);
            }
            return keys;
        }
    }

    public void AddToActions(string action) => AddToList(ScopedLogConventions.KeyOfActions, action);

    public void AddToList(string key, object value)
    {
        if (key == ScopedLogConventions.KeyOfAdditionalEvents && value is ScopeEvent scopeEvent)
        {
            lock (_sync) AddAdditionalEventLocked(scopeEvent);
            return;
        }
        var capture = Sanitizer is DiagnosticSanitizer native ? native.Capture(key, value, deferRedaction: true) : null;
        value = (capture != null ? capture.Value : Sanitizer.SanitizeValue(key, value)) ?? string.Empty;
        key = Sanitizer.SanitizeKey(key);
        using var sizing = DiagnosticJsonSize.Rent();
        var itemSize = sizing.Measure(value) + 1;

        lock (_sync)
        {
            if (_entries.TryGetValue(key, out var entry) && entry.Value is List<object> list)
            {
                if (list.Count >= Sanitizer.Options.MaxCollectionEntries || _storedBytes + itemSize > Sanitizer.Options.MaxScopeBytes - SnapshotReserveBytes)
                { _captureLimited = true; return; }
                list.Add(value);
                DiagnosticCaptureMetadata metadata = capture != null && entry.Capture is { HasValue: true } previous
                    ? previous.Append(capture) : default;
                _entries[key] = entry with { Bytes = entry.Bytes + itemSize, Capture = metadata };
                _storedBytes += itemSize;
            }
            else
            {
                var items = new List<object>(8) { value };
                var entrySize = sizing.MeasureSingleItemListEntry(key, itemSize - 1);
                StoreLocked(key, items, entrySize, capture == null ? null : capture with { Value = items });
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
            if (_counterKeys.Count >= Sanitizer.Options.MaxNodes) { _captureLimited = true; return 0; }
            if (safe.Length > 0 && !ReserveMetric(safe)) safe = "";
            _counterKeys[(prefix, key)] = safe;
            return safe.Length == 0 ? 0 : IncreaseLocked(safe, by, prefix, key);
        }
    }

    private bool ReserveMetric(string key)
    {
        if (_entries.TryGetValue(key, out var entry) && entry.HasValue) return true;
        using var sizing = DiagnosticJsonSize.Rent();
        var size = sizing.Measure(key) + MetricValueReserveBytes;
        if (_entryCount >= Sanitizer.Options.MaxNodes || _storedBytes + size > Sanitizer.Options.MaxScopeBytes - SnapshotReserveBytes)
        { _captureLimited = true; return false; }
        _storedBytes += size;
        _entries[key] = new LogEntry(EntryKind.Reservation, size, null);
        return true;
    }

    // Caller holds _sync and supplies the fully composed key.
    private long IncreaseLocked(string counterKey, long by, string prefix, string name)
    {
        // No dictionary mutations or callbacks occur while the reference is used.
        ref var stored = ref CollectionsMarshal.GetValueRefOrNullRef(_entries, counterKey);
        if (!Unsafe.IsNullRef(ref stored) && stored.Kind == EntryKind.Counter)
        {
            var counter = stored.Metric.Counter + by;
            stored.Metric = new MetricValue { Counter = counter };
            return counter;
        }

        var entry = _entries.GetValueOrDefault(counterKey);
        var value = (entry.Value is long existing ? existing : 0) + by;
        StoreMetricLocked(counterKey, new LogEntry(EntryKind.Counter, entry.Bytes, null, Metric: new MetricValue { Counter = value }),
            entry, prefix, name);
        return value;
    }

    private void StoreMetricLocked(string key, LogEntry entry, LogEntry previous, string prefix, string name)
    {
        // CopyFrom binds metadata to the detached metric scalar under the same lock.
        DiagnosticCaptureMetadata metadata = default;
        if (Sanitizer is DiagnosticSanitizer)
        {
            var path = prefix + name;
            DiagnosticNameDecision[] decisions = string.Equals(path, key, StringComparison.Ordinal)
                ? [new(path, true)] : [new(path, true), new(key, true)];
            metadata = new DiagnosticCaptureMetadata(decisions, Sanitizer.Options.SensitivePathSegments);
        }
        _entries[key] = entry with { Capture = metadata };
        if (!previous.HasValue) _entryCount++;
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
            if (_durationKeys.Count >= Sanitizer.Options.MaxNodes) { _captureLimited = true; return TimeSpan.Zero; }
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
        // Acquire after the counter update, which may insert into the dictionary.
        ref var stored = ref CollectionsMarshal.GetValueRefOrNullRef(_entries, keys.Duration);
        if (!Unsafe.IsNullRef(ref stored) && stored.Kind == EntryKind.Duration)
        {
            timeSpent = stored.Metric.Duration + by.TotalSeconds;
            stored.Metric = new MetricValue { Duration = timeSpent };
        }
        else
        {
            var entry = _entries.GetValueOrDefault(keys.Duration);
            timeSpent = (entry.Value is double existing ? existing : 0) + by.TotalSeconds;
            StoreMetricLocked(keys.Duration, new LogEntry(EntryKind.Duration, entry.Bytes, null, Metric: new MetricValue { Duration = timeSpent }),
                entry, prefix, name);
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
