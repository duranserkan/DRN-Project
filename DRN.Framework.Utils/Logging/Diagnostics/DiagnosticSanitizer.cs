using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Utils.Logging.Diagnostics;

/// <summary>Scope-local structural redaction and bounded capture. Free-text contents are preserved.</summary>
public interface IDiagnosticSanitizer
{
    DiagnosticLogOptions Options { get; }
    string SanitizeText(string? value);
    string SanitizeUrl(string? value);
    string SanitizeBody(string? value, string? contentType, string sourcePath = "");
    object? SanitizeValue(string fieldPath, object? value);
    string SanitizeKey(string key);
    bool IsSensitive(string fieldPath);
    IReadOnlyDictionary<string, object> GetStatus();
}

[Scoped<IDiagnosticSanitizer>]
public sealed class DiagnosticSanitizer : IDiagnosticSanitizer
{
    public const string Redacted = "[redacted]";
    private const string UrlRedacted = DiagnosticUrlSanitizer.UrlRedacted;
    public const string Limited = "[diagnostic capture limited]";
    internal const string Invalid = "[diagnostic field unavailable]";
    private readonly Lock _gate = new();
    private readonly DiagnosticNameRules _rules;
    private readonly DiagnosticUrlSanitizer _urls;
    private readonly DiagnosticCaptureReader _reader;
    private readonly string[] _urlRules;
    private readonly HashSet<string> _status = new(StringComparer.Ordinal);
    private long _inputBytes;
    private int _nodes;
    private int _keySequence;
    private double _spentMilliseconds;
    private long _started;
    private int _operations;

    public DiagnosticLogOptions Options { get; }
    internal bool RedactionEnabled { get; }
    internal string[] UrlRules => _urlRules;

    // Accessed only inside Guard. Snapshot-only sanitizers do not need a projector.
    private DiagnosticValueProjector Projector => field ??= new DiagnosticValueProjector(this);

    public DiagnosticSanitizer(DiagnosticLogOptions options) : this(options, true) { }

    private DiagnosticSanitizer(DiagnosticLogOptions options, bool redact)
    {
        RedactionEnabled = redact;
        Validator.ValidateObject(options, new ValidationContext(options), true);
        Options = options with
        {
            LoadedRules = options.GetRules(),
            LoadedRuleFile = options.RuleFile,
            LoadedRuleAssemblyName = options.RuleAssemblyName,
            SensitiveFields = options.SensitiveFields.ToArray(),
            SensitiveFieldPaths = options.SensitiveFieldPaths.ToArray(),
            SensitivePathSegments = options.SensitivePathSegments.ToArray()
        };
        _rules = new DiagnosticNameRules(Options);
        _urlRules = [.. Options.SensitivePathSegments, .. Options.LoadedRules!.UrlSegments];
        _urls = new DiagnosticUrlSanitizer(this, _rules);
        _reader = new DiagnosticCaptureReader(this);
    }

    public static DiagnosticSanitizer FromSettings(IAppSettings settings) =>
        new(settings.Get<DiagnosticLogOptions>("DiagnosticLogging") ?? new DiagnosticLogOptions());

    internal static DiagnosticSanitizer ForDevelopmentDisplay(IAppSettings settings, DiagnosticLogOptions options) =>
        new(options, redact: !settings.IsDevelopmentEnvironment);

    // TODO: Evaluate diagnostic redaction support for ASP.NET Core Identity's [PersonalData] and [ProtectedPersonalData], including inherited attributes and getter suppression.
    internal static bool IsClassified(MemberInfo member) => DiagnosticTypeMetadata.IsClassified(member);

    internal bool IsSensitive(string fieldPath, bool exempt, string? namePath = null) => RedactionEnabled && (namePath == null
        ? _rules.MatchField(fieldPath, exempt).IsMatch
        : _rules.MatchField(fieldPath, true).IsMatch || !exempt && _rules.MatchField(namePath).IsMatch);

    // Named models own their property names. Anonymous objects and collections retain structural name matching.
    internal static bool IsModelType(Type type) => type is { IsPrimitive: false, IsEnum: false } &&
                                                   type != typeof(decimal) && type != typeof(Guid) && type != typeof(DateTime) &&
                                                   type != typeof(DateTimeOffset) && type != typeof(DateOnly) && type != typeof(TimeOnly) &&
                                                   type != typeof(TimeSpan) && !typeof(Uri).IsAssignableFrom(type) && type != typeof(ScopeEvent) &&
                                                   !typeof(IEnumerable).IsAssignableFrom(type) &&
                                                   !(type.IsDefined(typeof(CompilerGeneratedAttribute), false) && type.Name.Contains("AnonymousType", StringComparison.Ordinal));

    public bool IsSensitive(string fieldPath) => RedactionEnabled && _rules.MatchField(fieldPath).IsMatch;

    public IReadOnlyDictionary<string, object> GetStatus()
    {
        lock (_gate)
            return _status.Count == 0 ? new Dictionary<string, object>() :
                new Dictionary<string, object> { ["DiagnosticCaptureWarnings"] = _status.Order(StringComparer.Ordinal).ToArray() };
    }

    private T Guard<T>(Func<T> action, T fallback) =>
        Guard((action, fallback), static (_, state) => state.action(), static state => state.fallback);

    private T Guard<TState, T>(TState state, Func<DiagnosticSanitizer, TState, T> action, Func<TState, T> fallback)
    {
        lock (_gate)
        {
            if (_operations++ == 0) _started = Stopwatch.GetTimestamp();
            try { return action(this, state); }
            catch (Exception)
            {
                _status.Add("SanitizationFailed");
                return fallback(state);
            }
            finally
            {
                if (--_operations == 0) _spentMilliseconds += Stopwatch.GetElapsedTime(_started).TotalMilliseconds;
            }
        }
    }

    internal bool WorkAvailable()
    {
        if (_spentMilliseconds + Stopwatch.GetElapsedTime(_started).TotalMilliseconds <= Options.MaxProcessingMilliseconds &&
            _nodes < Options.MaxNodes && _inputBytes <= Options.MaxInputBytes) return true;
        _status.Add("ScopeWorkLimit");
        return false;
    }

    // Helpers run inside Guard and charge the same scope budget as model capture.
    internal bool CountNode() => ++_nodes <= Options.MaxNodes;

    internal bool VisitNode(int depth) => WorkAvailable() && CountNode() && depth <= Options.MaxDepth;

    internal void RecordGetterFailure() => Guard(() => _status.Add("GetterFailed"), false);

    internal string NextFieldKey() => $"_diagnostic_field_{++_keySequence}";

    internal bool Accept(string value, int? maximum = null)
    {
        var limit = maximum ?? Options.MaxFieldBytes;
        if (!WorkAvailable()) return false;
        if (value.Length > limit)
        {
            _status.Add("FieldInputLimit");
            return false;
        }
        var bytes = Encoding.UTF8.GetByteCount(value);
        _inputBytes += bytes;
        if (bytes <= limit && _inputBytes <= Options.MaxInputBytes) return true;
        _status.Add(bytes > limit ? "FieldInputLimit" : "ScopeInputLimit");
        return false;
    }

    public string SanitizeText(string? value) => Guard(() => Accept(value ?? "") ? value ?? "" : Limited, Invalid);
    public string SanitizeUrl(string? value) => Guard(() => Accept(value ?? "") ? Url(value ?? "") : Limited, Invalid);
    public string SanitizeKey(string key) => Guard(
        () => IsValidKey(key) ? key : NextFieldKey(),
        $"_diagnostic_field_{Interlocked.Increment(ref _keySequence)}");
    public object? SanitizeValue(string fieldPath, object? value) => Capture(fieldPath, value).Value;

    /// <summary>Captures dictionary entries with exact, application-authored key classifications.</summary>
    public object? SanitizeDictionary<TValue>(string path, IReadOnlyDictionary<string, TValue> values,
        params DiagnosticKeyClassification[] classifications) => CaptureDictionary(path, values, classifications).Value;

    internal DiagnosticCaptureResult CaptureDictionary<TValue>(string path, IReadOnlyDictionary<string, TValue> values,
        DiagnosticKeyClassification[] classifications, bool deferRedaction = false) => Guard(
        () => CaptureDictionaryCore(path, values, classifications, deferRedaction), new DiagnosticCaptureResult(Invalid, []));

    private DiagnosticCaptureResult CaptureDictionaryCore<TValue>(string path, IReadOnlyDictionary<string, TValue> values,
        DiagnosticKeyClassification[] classifications, bool deferRedaction)
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(classifications);
        if (!WorkAvailable() || ++_nodes > Options.MaxNodes) return new DiagnosticCaptureResult(Limited, []);
        var rules = DictionaryClassifications(classifications);
        if (values.IgnoredLog()) return new DiagnosticCaptureResult(ScopedLogConventions.IgnoredLogValue, []);
        var sensitive = IsSensitive(path, false, "") || RedactionEnabled && IsClassified(values.GetType());
        if (sensitive && !deferRedaction) return new DiagnosticCaptureResult(Redacted, []);
        var context = new DiagnosticCaptureContext(null, deferRedaction);
        if (!sensitive) context.Decisions.Add(new(path, false, ""));
        var output = new Dictionary<string, object?>(StringComparer.Ordinal);
        var ancestors = new HashSet<object>(ReferenceEqualityComparer.Instance) { values };
        foreach (var item in values)
        {
            if (output.Count >= Options.MaxCollectionEntries || !WorkAvailable()) { output["_diagnostic_limit"] = Limited; break; }
            var safe = UniqueKey(output, item.Key);
            output[safe] = CaptureClassifiedEntry(path, item.Key, item.Value, rules, ancestors, context);
        }
        return new DiagnosticCaptureResult(sensitive ? new DiagnosticCapturedValue(output, DiagnosticRedactionKind.Classified) : output,
            context.Decisions.ToArray(), _urlRules);
    }

    private Dictionary<string, DiagnosticDataKind> DictionaryClassifications(DiagnosticKeyClassification[] classifications)
    {
        if (classifications.Length > Options.MaxNodes) throw new ArgumentException("Too many dictionary classifications.");
        var rules = new Dictionary<string, DiagnosticDataKind>(StringComparer.Ordinal);
        foreach (var entry in classifications)
        {
            ValidateClassification(entry);
            // A positive classification always wins over a false-positive exemption.
            if (!rules.TryGetValue(entry.Key, out var existing) || existing == DiagnosticDataKind.NonSensitive)
                rules[entry.Key] = entry.Kind;
        }
        return rules;
    }

    private void ValidateClassification(DiagnosticKeyClassification entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Key) || entry.Key.Length > Options.MaxKeyBytes ||
            entry.Key.Any(char.IsControl) || !Enum.IsDefined(entry.Kind) ||
            entry.Reason?.Length > Options.MaxFieldBytes ||
            entry.Kind == DiagnosticDataKind.NonSensitive && string.IsNullOrWhiteSpace(entry.Reason))
            throw new ArgumentException("Invalid dictionary classification.");
    }

    private object? CaptureClassifiedEntry(string path, string key, object? value,
        Dictionary<string, DiagnosticDataKind> rules, HashSet<object> ancestors, DiagnosticCaptureContext context)
    {
        var classified = rules.TryGetValue(key, out var classification);
        if (key.Length > Options.MaxKeyBytes) return Limited;
        var positive = classified && classification != DiagnosticDataKind.NonSensitive;
        if (!context.DeferRedaction && RedactionEnabled && (positive || HasClassifiedType(value)))
        {
            ++_nodes;
            return Redacted;
        }
        if (classified && classification == DiagnosticDataKind.NonSensitive && !IsSimpleValue(value) &&
            !(context.DeferRedaction && HasClassifiedType(value)))
        {
            ++_nodes;
            return Invalid;
        }
        return Projector.Project(path + "." + key, value, 1, ancestors, context,
            [path, key], DiagnosticCaptureKind.DictionaryKey, classified, key, positive);
    }

    internal static bool HasClassifiedType(object? value) => value != null && IsClassified(value.GetType());

    /// <summary>Exempts one supported scalar from built-in name rules, with an application-authored reason.</summary>
    public object? SanitizeValue(string fieldPath, object? value, string exemptionReason) =>
        CaptureWithReason(fieldPath, value, exemptionReason).Value;

    internal DiagnosticCaptureResult CaptureWithReason(string path, object? value, string exemptionReason, bool deferRedaction = false) => Guard(() =>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exemptionReason);
        if (exemptionReason.Length > Options.MaxFieldBytes) throw new ArgumentException("Diagnostic reason exceeds capture limits.");
        if (!IsSimpleValue(value)) throw new ArgumentException("Reason-based exemptions require a supported scalar.", nameof(value));
        return Capture(path, value, exempt: true, deferRedaction: deferRedaction);
    }, new DiagnosticCaptureResult(Invalid, []));

    internal static bool IsSimpleValue(object? value) => value is null or string or char or bool or
        byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal or
        Guid or DateTime or DateTimeOffset or DateOnly or TimeOnly or TimeSpan or Uri || value.GetType().IsEnum;

    /// <summary>Captures a value with application-authored exceptions for exact structural nodes.</summary>
    public object? SanitizeValue(string fieldPath, object? value, DiagnosticCapturePolicy policy) =>
        Capture(fieldPath, value, policy).Value;

    internal DiagnosticCaptureResult Capture(string path, object? value, DiagnosticCapturePolicy? policy = null,
        DiagnosticCaptureKind kind = DiagnosticCaptureKind.NamedEntry, string[]? segments = null, bool exempt = false,
        string? namePath = null, bool deferRedaction = false, bool classified = false) => Guard(() =>
    {
        policy?.Validate(Options);
        var context = new DiagnosticCaptureContext(policy, deferRedaction);
        var result = Projector.Project(path, value, 0, null,
            context, segments ?? [path], kind, exempt, namePath, classified);
        return new DiagnosticCaptureResult(result, context.Decisions.ToArray(), _urlRules);
    }, new DiagnosticCaptureResult(Invalid, []));

    // Only native scoped storage supplies this detached result, never a caller-owned snapshot.
    internal DiagnosticCaptureResult CopyCapture(DiagnosticCaptureResult capture, bool deferRedaction = false) => Guard(() =>
    {
        if (_urlRules.Except(capture.UrlRules ?? [], StringComparer.Ordinal).Any() ||
            capture.Decisions.Any(decision => IsSensitive(decision.Path, decision.Exempt, decision.NamePath)))
            return new DiagnosticCaptureResult(deferRedaction
                ? new DiagnosticCapturedValue(CopySafe(capture.Value, 0), DiagnosticRedactionKind.Classified) : Redacted, []);
        return new DiagnosticCaptureResult(CopySafe(capture.Value, 0), capture.Decisions, _urlRules);
    }, new DiagnosticCaptureResult(Invalid, []));

    internal object? CopySafe(object? value, int depth) => _reader.Copy(value, depth);

    // ScopedLog supplies a fresh sanitizer for each snapshot. Reading never invokes the original getters.
    internal IReadOnlyDictionary<string, object> MaterializeSnapshot(IReadOnlyDictionary<string, object> snapshot, bool redact) =>
        Guard((snapshot, redact), static (owner, state) => owner._reader.Materialize(state.snapshot, state.redact),
            static _ => new Dictionary<string, object> { ["DiagnosticCapture"] = Invalid });

    internal string RedactCapturedText(DiagnosticRedactionKind kind, string text) =>
        kind == DiagnosticRedactionKind.Url ? Url(text) : Pairs(text.TrimStart('?'), redacted: UrlRedacted);

    // Only snapshot-private source workers use this. Destination and capture budgets never reset.
    internal string RedactCapturedTextWithFreshBudget(DiagnosticRedactionKind kind, string text) =>
        Guard((kind, text), static (source, state) =>
        {
            source._inputBytes = 0;
            source._nodes = 0;
            source._spentMilliseconds = 0;
            source._keySequence = 0;
            source._status.Clear();
            return source.RedactCapturedText(state.kind, state.text);
        }, static _ => Invalid);

    public string SanitizeBody(string? value, string? contentType, string sourcePath = "") => Guard(() =>
    {
        value ??= string.Empty;
        if (!Accept(value)) return Limited;
        var media = contentType?.Split(';')[0].Trim().ToLowerInvariant();
        if (media == "application/x-www-form-urlencoded") return Pairs(value, sourcePath: sourcePath);
        if (media?.StartsWith("text/", StringComparison.Ordinal) == true) return value;
        return value.Length == 0 ? "" : "[unsupported diagnostic body]";
    }, Invalid);

    private string Url(string value, DiagnosticCaptureContext? context = null) => _urls.Url(value, context);

    private string Pairs(string value, DiagnosticCaptureContext? context = null, string redacted = Redacted,
        string sourcePath = "") => _urls.Pairs(value, context, redacted, sourcePath);

    internal object StringValue(string path, string value, DiagnosticCaptureContext context)
    {
        if (!Accept(value)) return Limited;
        Span<char> buffer = stackalloc char[512];
        var name = DiagnosticNameRules.Normalize(path, buffer);
        if (name.EndsWith("querystring", StringComparison.Ordinal))
        {
            var sanitized = Pairs(value.TrimStart('?'), context, UrlRedacted);
            if (context.DeferRedaction && sanitized.Contains("_diagnostic=" + Limited, StringComparison.Ordinal)) return Limited;
            return context.DeferRedaction ? new DiagnosticCapturedValue(value, DiagnosticRedactionKind.QueryString, Options) : sanitized;
        }
        if ((name.EndsWith("url", StringComparison.Ordinal) || name.EndsWith("uri", StringComparison.Ordinal)) && value.Length > 0)
            return CaptureUrl(value, context);
        return value;
    }

    internal object CaptureUrl(string value, DiagnosticCaptureContext context)
    {
        var sanitized = Url(value, context);
        if (context.DeferRedaction && sanitized.Contains("_diagnostic=" + Limited, StringComparison.Ordinal)) return Limited;
        return context.DeferRedaction && sanitized is not (Limited or Invalid)
            ? new DiagnosticCapturedValue(value, DiagnosticRedactionKind.Url, Options) : sanitized;
    }

    internal string UniqueKey(Dictionary<string, object?> output, string key)
    {
        return !IsValidKey(key) || output.ContainsKey(key) ? NextFieldKey() : key;
    }

    private bool IsValidKey(string key) => key.Length <= Options.MaxKeyBytes &&
        Encoding.UTF8.GetByteCount(key) <= Options.MaxKeyBytes && !key.Any(char.IsControl) &&
        !key.StartsWith("_diagnostic_", StringComparison.Ordinal);
}
