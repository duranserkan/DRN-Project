using System.Collections;
using System.Buffers;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.Logging;

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
    internal static bool IsModelType(Type type) => !type.IsPrimitive && !type.IsEnum &&
        type != typeof(decimal) && type != typeof(Guid) && type != typeof(DateTime) &&
        type != typeof(DateTimeOffset) && type != typeof(DateOnly) && type != typeof(TimeOnly) &&
        type != typeof(TimeSpan) && !typeof(Uri).IsAssignableFrom(type) && type != typeof(JsonElement) && type != typeof(ScopeEvent) &&
        !typeof(IEnumerable).IsAssignableFrom(type) &&
        !(type.IsDefined(typeof(CompilerGeneratedAttribute), false) && type.Name.Contains("AnonymousType", StringComparison.Ordinal));

    private static string? ChildNamePath(string? parent, string name) => parent == null ? null :
        parent.Length == 0 ? name : parent + "." + name;

    public bool IsSensitive(string fieldPath) => RedactionEnabled && _rules.MatchField(fieldPath).IsMatch;

    public IReadOnlyDictionary<string, object> GetStatus()
    {
        lock (_gate)
            return _status.Count == 0 ? new Dictionary<string, object>() :
                new Dictionary<string, object> { ["DiagnosticCaptureWarnings"] = _status.Order(StringComparer.Ordinal).ToArray() };
    }

    internal T Guard<T>(Func<T> action, T fallback) =>
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
        () => IsValidKey(key) ? key : $"_diagnostic_field_{++_keySequence}",
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
        if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || entry.Key.Length > Options.MaxKeyBytes ||
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
        return Project(path + "." + key, value, 1, ancestors, context,
            [path, key], DiagnosticCaptureKind.DictionaryKey, classified, key, positive);
    }

    private static bool HasClassifiedType(object? value) => value != null && IsClassified(value.GetType());

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
        var result = Project(path, value, 0, new HashSet<object>(ReferenceEqualityComparer.Instance),
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

    private object? CopySafe(object? value, int depth) => _reader.Copy(value, depth);

    // ScopedLog supplies a fresh sanitizer for each snapshot. Reading never invokes the original getters.
    internal IReadOnlyDictionary<string, object> MaterializeSnapshot(IReadOnlyDictionary<string, object> snapshot, bool redact) =>
        Guard((snapshot, redact), static (owner, state) => owner._reader.Materialize(state.snapshot, state.redact),
            static _ => (IReadOnlyDictionary<string, object>)new Dictionary<string, object> { ["DiagnosticCapture"] = Invalid });

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
        if (media == "application/json" || media?.EndsWith("+json", StringComparison.Ordinal) == true)
        {
            using var document = JsonDocument.Parse(value, new JsonDocumentOptions { MaxDepth = Options.MaxDepth });
            return JsonSerializer.Serialize(ProjectJson(sourcePath, document.RootElement, 0,
                new DiagnosticCaptureContext(null), [sourcePath], DiagnosticCaptureKind.NamedEntry));
        }
        if (media?.StartsWith("text/", StringComparison.Ordinal) == true) return value;
        return value.Length == 0 ? "" : "[unsupported diagnostic body]";
    }, Invalid);

    private string Url(string value, DiagnosticCaptureContext? context = null) => _urls.Url(value, context);

    private string Pairs(string value, DiagnosticCaptureContext? context = null, string redacted = Redacted,
        string sourcePath = "") => _urls.Pairs(value, context, redacted, sourcePath);

    private object StringValue(string path, string value, DiagnosticCaptureContext context)
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

    private object CaptureUrl(string value, DiagnosticCaptureContext context)
    {
        var sanitized = Url(value, context);
        if (context.DeferRedaction && sanitized.Contains("_diagnostic=" + Limited, StringComparison.Ordinal)) return Limited;
        return context.DeferRedaction && sanitized is not (Limited or Invalid)
            ? new DiagnosticCapturedValue(value, DiagnosticRedactionKind.Url, Options) : sanitized;
    }

    private object? Project(string path, object? value, int depth, HashSet<object> ancestors,
        DiagnosticCaptureContext context, string[] segments, DiagnosticCaptureKind kind, bool exempt = false, string? namePath = null,
        bool classified = false)
    {
        if (!WorkAvailable() || ++_nodes > Options.MaxNodes || depth > Options.MaxDepth) return Limited;
        if (value is DiagnosticCapturedValue)
            return context.DeferRedaction ? CopySafe(value, depth) : Redacted;
        if (value.IgnoredLog()) return ScopedLogConventions.IgnoredLogValue;
        if (kind == DiagnosticCaptureKind.NamedEntry && value != null && IsModelType(value.GetType())) namePath = "";
        exempt |= context.Exempts(kind, segments);
        var sensitive = IsSensitive(path, exempt, namePath) || RedactionEnabled && (classified || HasClassifiedType(value));
        if (sensitive && !context.DeferRedaction) return Redacted;
        if (!sensitive) context.Decisions.Add(new(path, exempt, namePath));
        var result = ProjectValue(path, value, depth, ancestors, context, segments, kind, exempt, namePath);
        return sensitive ? new DiagnosticCapturedValue(result, DiagnosticRedactionKind.Classified) : result;
    }

    private object? ProjectValue(string path, object? value, int depth, HashSet<object> ancestors,
        DiagnosticCaptureContext context, string[] segments, DiagnosticCaptureKind kind, bool exempt, string? namePath)
    {
        if (value == null) return null;
        if (value is string text) return StringValue(path, text, context);
        if (value is Uri uri) return Accept(uri.OriginalString) ? CaptureUrl(uri.OriginalString, context) : Limited;
        if (value is JsonElement element) return CaptureJson(path, element, depth, context, segments, kind, exempt, namePath);
        if (value is ScopeEvent scopeEvent) return CaptureEvent(scopeEvent);
        if (value is bool or byte or sbyte or short or ushort or int or uint or long or ulong or decimal or Guid or
            DateTime or DateTimeOffset or DateOnly or TimeOnly or TimeSpan || value.GetType().IsEnum) return value;
        if (value is double number) return double.IsFinite(number) ? number : Invalid;
        if (value is float single) return float.IsFinite(single) ? single : Invalid;
        if (value is char character) return character.ToString();
        return ProjectComplexValue(path, value, depth, ancestors, context, segments, namePath);
    }

    private object? CaptureJson(string path, JsonElement element, int depth, DiagnosticCaptureContext context,
        string[] segments, DiagnosticCaptureKind kind, bool exempt, string? namePath)
    {
        // Bound serialization before GetString can allocate from an externally supplied JSON tree.
        var buffer = new BoundedJsonBuffer(Options.MaxFieldBytes);
        using (var writer = new Utf8JsonWriter(buffer)) { element.WriteTo(writer); writer.Flush(); }
        _inputBytes += buffer.Written;
        return WorkAvailable() ? ProjectJson(path, element, depth, context, segments, kind, exempt, namePath) : Limited;
    }

    private object ProjectComplexValue(string path, object value, int depth, HashSet<object> ancestors,
        DiagnosticCaptureContext context, string[] segments, string? namePath)
    {
        if (!ancestors.Add(value)) return "[diagnostic cycle]";
        try
        {
            if (value is IDictionary dictionary)
                return ProjectDictionary(path, dictionary, depth, ancestors, context, segments, namePath);
            if (value is IReadOnlyDictionary<string, object?> readOnly)
                return ProjectReadOnlyDictionary(path, readOnly, depth, ancestors, context, segments, namePath);
            if (value is IList list)
                return ProjectList(path, list, depth, ancestors, context, segments, namePath);
            if (value is IEnumerable) return "[unsupported lazy diagnostic collection]";
            return ProjectModel(path, value, depth, ancestors, context, segments, namePath);
        }
        finally { ancestors.Remove(value); }
    }

    private Dictionary<string, object?> ProjectDictionary(string path, IDictionary dictionary, int depth,
        HashSet<object> ancestors, DiagnosticCaptureContext context, string[] segments, string? namePath)
    {
        var output = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (DictionaryEntry item in dictionary)
        {
            if (output.Count >= Options.MaxCollectionEntries || !WorkAvailable()) { output["_diagnostic_limit"] = Limited; break; }
            if (item.Key is not string key) { output[$"_diagnostic_field_{++_keySequence}"] = "[unsupported diagnostic key]"; continue; }
            AddDictionaryEntry(output, key, item.Value, path, depth, ancestors, context, segments, namePath);
        }
        return output;
    }

    private Dictionary<string, object?> ProjectReadOnlyDictionary(string path, IReadOnlyDictionary<string, object?> readOnly,
        int depth, HashSet<object> ancestors, DiagnosticCaptureContext context, string[] segments, string? namePath)
    {
        var output = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var item in readOnly)
        {
            if (output.Count >= Options.MaxCollectionEntries || !WorkAvailable()) { output["_diagnostic_limit"] = Limited; break; }
            AddDictionaryEntry(output, item.Key, item.Value, path, depth, ancestors, context, segments, namePath);
        }
        return output;
    }

    private List<object?> ProjectList(string path, IList list, int depth, HashSet<object> ancestors,
        DiagnosticCaptureContext context, string[] segments, string? namePath)
    {
        var output = new List<object?>();
        for (var index = 0; index < list.Count && index < Options.MaxCollectionEntries && WorkAvailable(); index++)
            output.Add(Project(path, list[index], depth + 1, ancestors, context,
                [.. segments, index.ToString(System.Globalization.CultureInfo.InvariantCulture)], DiagnosticCaptureKind.ListItem,
                namePath: namePath));
        if (output.Count < list.Count) output.Add(Limited);
        return output;
    }

    private void AddDictionaryEntry(Dictionary<string, object?> output, string key, object? item, string path, int depth,
        HashSet<object> ancestors, DiagnosticCaptureContext context, string[] segments, string? namePath)
    {
        var safe = UniqueKey(output, key);
        output[safe] = key.Length > Options.MaxKeyBytes ? Limited : Project(path + "." + key, item, depth + 1, ancestors,
            context, [.. segments, key], DiagnosticCaptureKind.DictionaryKey, namePath: ChildNamePath(namePath, key));
    }

    private Dictionary<string, object?> ProjectModel(string path, object value, int depth, HashSet<object> ancestors,
        DiagnosticCaptureContext context, string[] segments, string? namePath)
    {
        var typedModel = IsModelType(value.GetType());
        var projected = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var member in DiagnosticTypeMetadata.For(value.GetType()).Members)
        {
            if (projected.Count >= Options.MaxCollectionEntries || !WorkAvailable()) { projected["_diagnostic_limit"] = Limited; break; }
            var key = UniqueKey(projected, member.Name);
            var memberNamePath = typedModel ? member.Name : ChildNamePath(namePath, member.Name);
            projected[key] = ProjectMember(path, value, member, depth, ancestors, context, segments, memberNamePath);
        }
        return projected;
    }

    private object? ProjectMember(string path, object value, DiagnosticTypeMetadata.Member member, int depth,
        HashSet<object> ancestors, DiagnosticCaptureContext context, string[] segments, string? memberNamePath)
    {
        if (member.Ignored) return ScopedLogConventions.IgnoredLogValue;
        if (!context.DeferRedaction && RedactionEnabled && member.Sensitive) return Redacted;
        var memberPath = path + "." + member.Name;
        string[] memberSegments = [.. segments, member.Name];
        bool MatchesMemberName() => IsSensitive(memberPath,
            member.Exempt || context.Exempts(DiagnosticCaptureKind.Property, memberSegments), memberNamePath);
        if (!context.DeferRedaction && MatchesMemberName()) return Redacted;
        try
        {
            return Project(memberPath, member.Read(value), depth + 1, ancestors,
                context, memberSegments, DiagnosticCaptureKind.Property, member.Exempt, memberNamePath, member.Sensitive);
        }
        catch (Exception)
        {
            var result = context.DeferRedaction && (member.Sensitive || MatchesMemberName())
                ? (object)new DiagnosticCapturedValue(Invalid, DiagnosticRedactionKind.Classified) : Invalid;
            _status.Add("GetterFailed");
            return result;
        }
    }

    internal ScopeEvent CaptureEvent(ScopeEvent value) => new(new EventId(value.Id.Id, SanitizeText(value.Id.Name ?? "")),
        value.Outcome == null ? null : SanitizeText(value.Outcome), value.Reason == null ? null : SanitizeText(value.Reason));

    internal string UniqueKey(Dictionary<string, object?> output, string key)
    {
        return !IsValidKey(key) || output.ContainsKey(key) ? $"_diagnostic_field_{++_keySequence}" : key;
    }

    private bool IsValidKey(string key) => key.Length <= Options.MaxKeyBytes &&
        Encoding.UTF8.GetByteCount(key) <= Options.MaxKeyBytes && !key.Any(char.IsControl) &&
        !key.StartsWith("_diagnostic_", StringComparison.Ordinal);

    private object? ProjectJson(string path, JsonElement element, int depth, DiagnosticCaptureContext context,
        string[] segments, DiagnosticCaptureKind kind, bool exempt = false, string? namePath = null)
    {
        if (!WorkAvailable() || ++_nodes > Options.MaxNodes || depth > Options.MaxDepth) return Limited;
        exempt |= context.Exempts(kind, segments);
        var sensitive = IsSensitive(path, exempt, namePath);
        if (sensitive && !context.DeferRedaction) return Redacted;
        if (!sensitive) context.Decisions.Add(new(path, exempt, namePath));
        var result = ProjectJsonValue(path, element, depth, context, segments, namePath);
        return sensitive ? new DiagnosticCapturedValue(result, DiagnosticRedactionKind.Classified) : result;
    }

    private object? ProjectJsonValue(string path, JsonElement element, int depth, DiagnosticCaptureContext context,
        string[] segments, string? namePath)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                return ProjectJsonObject(path, element, depth, context, segments, namePath);
            case JsonValueKind.Array:
                return ProjectJsonArray(path, element, depth, context, segments, namePath);
            case JsonValueKind.String: return StringValue(path, element.GetString()!, context);
            case JsonValueKind.Number:
                if (element.TryGetDecimal(out var number)) return number;
                return element.TryGetDouble(out var floating) && double.IsFinite(floating) ? floating : Invalid;
            case JsonValueKind.True: return true;
            case JsonValueKind.False: return false;
            default: return null;
        }
    }

    private Dictionary<string, object?> ProjectJsonObject(string path, JsonElement element, int depth,
        DiagnosticCaptureContext context, string[] segments, string? namePath)
    {
        var map = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (map.Count >= Options.MaxCollectionEntries || !WorkAvailable()) { map["_diagnostic_limit"] = Limited; break; }
            map[UniqueKey(map, property.Name)] = ProjectJson(path + "." + property.Name, property.Value, depth + 1,
                context, [.. segments, property.Name], DiagnosticCaptureKind.JsonProperty,
                namePath: ChildNamePath(namePath, property.Name));
        }
        return map;
    }

    private List<object?> ProjectJsonArray(string path, JsonElement element, int depth,
        DiagnosticCaptureContext context, string[] segments, string? namePath)
    {
        var list = new List<object?>();
        foreach (var item in element.EnumerateArray())
        {
            if (list.Count >= Options.MaxCollectionEntries || !WorkAvailable()) { list.Add(Limited); break; }
            list.Add(ProjectJson(path, item, depth + 1, context,
                [.. segments, list.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)], DiagnosticCaptureKind.ListItem,
                namePath: namePath));
        }
        return list;
    }

    private sealed class BoundedJsonBuffer(int maximum) : IBufferWriter<byte>
    {
        private readonly byte[] _buffer = new byte[maximum];
        public int Written { get; private set; }
        public void Advance(int count)
        {
            if (count < 0 || count > maximum - Written) throw new InvalidOperationException("Diagnostic JSON limit.");
            Written += count;
        }
        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            if (Math.Max(1, sizeHint) > maximum - Written) throw new InvalidOperationException("Diagnostic JSON limit.");
            return _buffer.AsMemory(Written);
        }
        public Span<byte> GetSpan(int sizeHint = 0) => GetMemory(sizeHint).Span;
    }
}
