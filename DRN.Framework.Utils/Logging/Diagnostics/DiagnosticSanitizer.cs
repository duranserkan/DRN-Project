using System.Collections;
using System.Buffers;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.Compliance.Classification;
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
    private const string UrlRedacted = "redacted";
    public const string Limited = "[diagnostic capture limited]";
    internal const string Invalid = "[diagnostic field unavailable]";
    private sealed record Member(PropertyInfo Property, bool Ignored, bool Sensitive, bool Exempt)
    {
        public string Name => Property.Name;
        public object? Read(object value) => Property.GetValue(value);
    }
    private static readonly ConcurrentDictionary<Type, Member[]> Members = new();
    private readonly Lock _gate = new();
    private readonly DiagnosticNameRules _rules;
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
            SensitiveFields = options.SensitiveFields.ToArray(),
            SensitiveFieldPaths = options.SensitiveFieldPaths.ToArray(),
            SensitivePathSegments = options.SensitivePathSegments.ToArray()
        };
        _rules = new DiagnosticNameRules(Options);
        _urlRules = Options.SensitivePathSegments.ToArray();
    }

    public static DiagnosticSanitizer FromSettings(IAppSettings settings) =>
        new(settings.Get<DiagnosticLogOptions>("DiagnosticLogging") ?? new DiagnosticLogOptions());

    internal static DiagnosticSanitizer ForDevelopmentDisplay(IAppSettings settings, DiagnosticLogOptions options) =>
        new(options, redact: !settings.IsDevelopmentEnvironment);

    // Explicit ancestry inspection prevents a direct Sensitive(false) from hiding a positive base annotation.
    internal static bool IsClassified(MemberInfo member) =>
        HasClassification(member) || (member switch
        {
            PropertyInfo property => HasClassification(Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType),
            _ => false
        });

    // TODO: Evaluate diagnostic redaction support for ASP.NET Core Identity's [PersonalData] and [ProtectedPersonalData], including inherited attributes and getter suppression.
    private static bool HasClassification(MemberInfo member, bool inherited = false)
    {
        if (member.GetCustomAttributes<DataClassificationAttribute>(false).Any(attribute =>
                (!inherited || attribute.GetType().GetCustomAttribute<AttributeUsageAttribute>(true)?.Inherited != false) &&
                (attribute.Classification != DataClassification.None ||
                 member is not PropertyInfo && attribute is SensitiveAttribute { IsSensitive: false }))) return true;
        if (member is Type type) return type.BaseType != null && HasClassification(type.BaseType, true);
        if (member is not PropertyInfo property || property.GetMethod is not { IsVirtual: true } getter) return false;
        var definition = getter.GetBaseDefinition();
        for (var parent = property.DeclaringType?.BaseType; parent != null; parent = parent.BaseType)
            foreach (var candidate in parent.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                if (candidate.GetMethod?.GetBaseDefinition() == definition && HasClassification(candidate, true)) return true;
        return false;
    }

    internal static bool IsNameExempt(PropertyInfo property) =>
        property.GetCustomAttributes<SensitiveAttribute>(false).Any(attribute =>
            attribute.GetType() == typeof(SensitiveAttribute) && !attribute.IsSensitive);

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

    private T Guard<T>(Func<T> action, T fallback)
    {
        lock (_gate)
        {
            if (_operations++ == 0) _started = Stopwatch.GetTimestamp();
            try { return action(); }
            catch (Exception)
            {
                _status.Add("SanitizationFailed");
                return fallback;
            }
            finally
            {
                if (--_operations == 0) _spentMilliseconds += Stopwatch.GetElapsedTime(_started).TotalMilliseconds;
            }
        }
    }

    private bool WorkAvailable()
    {
        if (_spentMilliseconds + Stopwatch.GetElapsedTime(_started).TotalMilliseconds <= Options.MaxProcessingMilliseconds &&
            _nodes < Options.MaxNodes && _inputBytes <= Options.MaxInputBytes) return true;
        _status.Add("ScopeWorkLimit");
        return false;
    }

    private bool Accept(string value, int? maximum = null)
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
    public string SanitizeKey(string key) => Guard(() =>
    {
        var safe = key.Length <= Options.MaxKeyBytes && Encoding.UTF8.GetByteCount(key) <= Options.MaxKeyBytes &&
            !key.Any(char.IsControl) ? key : Invalid;
        return safe == key && !safe.StartsWith("_diagnostic_", StringComparison.Ordinal)
            ? safe : $"_diagnostic_field_{++_keySequence}";
    }, $"_diagnostic_field_{Interlocked.Increment(ref _keySequence)}");
    public object? SanitizeValue(string fieldPath, object? value) => Capture(fieldPath, value).Value;

    /// <summary>Captures dictionary entries with exact, application-authored key classifications.</summary>
    public object? SanitizeDictionary<TValue>(string path, IReadOnlyDictionary<string, TValue> values,
        params DiagnosticKeyClassification[] classifications) => CaptureDictionary(path, values, classifications).Value;

    internal DiagnosticCaptureResult CaptureDictionary<TValue>(string path, IReadOnlyDictionary<string, TValue> values,
        DiagnosticKeyClassification[] classifications, bool deferRedaction = false) => Guard(() =>
    {
        ArgumentNullException.ThrowIfNull(values);
        ArgumentNullException.ThrowIfNull(classifications);
        if (!WorkAvailable() || ++_nodes > Options.MaxNodes) return new DiagnosticCaptureResult(Limited, []);
        if (classifications.Length > Options.MaxNodes) throw new ArgumentException("Too many dictionary classifications.");
        var rules = new Dictionary<string, DiagnosticDataKind>(StringComparer.Ordinal);
        foreach (var entry in classifications)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Key) || entry.Key.Length > Options.MaxKeyBytes ||
                entry.Key.Any(char.IsControl) || !Enum.IsDefined(entry.Kind) ||
                entry.Reason?.Length > Options.MaxFieldBytes ||
                entry.Kind == DiagnosticDataKind.NonSensitive && string.IsNullOrWhiteSpace(entry.Reason))
                throw new ArgumentException("Invalid dictionary classification.");
            // A positive classification always wins over a false-positive exemption.
            if (!rules.TryGetValue(entry.Key, out var existing) || existing == DiagnosticDataKind.NonSensitive)
                rules[entry.Key] = entry.Kind;
        }
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
            var classified = rules.TryGetValue(item.Key, out var classification);
            if (item.Key.Length > Options.MaxKeyBytes) output[safe] = Limited;
            else if (!deferRedaction && RedactionEnabled && (classified && classification != DiagnosticDataKind.NonSensitive ||
                     item.Value != null && IsClassified(item.Value.GetType()))) { ++_nodes; output[safe] = Redacted; }
            else if (classified && classification == DiagnosticDataKind.NonSensitive && !IsSimpleValue(item.Value) &&
                     !(deferRedaction && item.Value != null && IsClassified(item.Value.GetType()))) { ++_nodes; output[safe] = Invalid; }
            else output[safe] = Project(path + "." + item.Key, item.Value, 1, ancestors, context,
                [path, item.Key], DiagnosticCaptureKind.DictionaryKey, classified, item.Key,
                classified && classification != DiagnosticDataKind.NonSensitive);
        }
        return new DiagnosticCaptureResult(sensitive ? new DiagnosticCapturedValue(output, DiagnosticRedactionKind.Classified) : output,
            context.Decisions.ToArray(), _urlRules);
    }, new DiagnosticCaptureResult(Invalid, []));

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

    private object? CopySafe(object? value, int depth)
    {
        if (!WorkAvailable() || ++_nodes > Options.MaxNodes || depth > Options.MaxDepth) return Limited;
        if (value is DiagnosticCapturedValue captured)
            return captured with { Value = CopySafe(captured.Value, depth) };
        if (value is string text) return Accept(text) ? text : Limited;
        if (value is ScopeEvent scopeEvent) return CaptureEvent(scopeEvent);
        if (value is IDictionary<string, object?> map)
        {
            var result = new Dictionary<string, object?>();
            foreach (var pair in map)
            {
                if (result.Count >= Options.MaxCollectionEntries || !WorkAvailable()) { result["_diagnostic_limit"] = Limited; break; }
                result[UniqueKey(result, pair.Key)] = CopySafe(pair.Value, depth + 1);
            }
            return result;
        }
        if (value is IList list)
        {
            var result = new List<object?>();
            foreach (var item in list)
            {
                if (result.Count >= Options.MaxCollectionEntries || !WorkAvailable()) { result.Add(Limited); break; }
                result.Add(CopySafe(item, depth + 1));
            }
            return result;
        }
        return value;
    }

    // A fresh reader has its own bounded budget. Reading never invokes the original getters.
    internal IReadOnlyDictionary<string, object> MaterializeSnapshot(IReadOnlyDictionary<string, object> snapshot, bool redact) => Guard(() =>
    {
        var output = new Dictionary<string, object>();
        foreach (var pair in snapshot)
        {
            if (output.Count >= Options.MaxNodes || !WorkAvailable()) { output["_diagnostic_limit"] = Limited; break; }
            output[pair.Key] = ReadCaptured(pair.Value, 0, redact)!;
        }
        return (IReadOnlyDictionary<string, object>)output;
    }, new Dictionary<string, object> { ["DiagnosticCapture"] = Invalid });

    private object? ReadCaptured(object? value, int depth, bool redact)
    {
        if (!WorkAvailable() || ++_nodes > Options.MaxNodes || depth > Options.MaxDepth) return Limited;
        if (value is DiagnosticCapturedValue captured)
        {
            if (redact && captured.Kind == DiagnosticRedactionKind.Classified) return Redacted;
            if (redact && captured.Value is string text)
            {
                if (!Accept(text)) return Limited;
                if (captured.SourceOptions is { } sourceOptions)
                {
                    var source = new DiagnosticSanitizer(sourceOptions);
                    text = source.Guard(() => captured.Kind == DiagnosticRedactionKind.Url
                        ? source.Url(text) : source.Pairs(text.TrimStart('?'), redacted: UrlRedacted), Invalid);
                }
                return captured.Kind == DiagnosticRedactionKind.Url ? Url(text) : Pairs(text.TrimStart('?'), redacted: UrlRedacted);
            }
            return ReadCaptured(captured.Value, depth, redact);
        }
        if (value is string scalar) return Accept(scalar) ? scalar : Limited;
        if (value is IReadOnlyDictionary<string, object?> map)
        {
            var output = new Dictionary<string, object?>();
            foreach (var pair in map)
            {
                if (output.Count > Options.MaxCollectionEntries || !WorkAvailable()) { output["_diagnostic_limit"] = Limited; break; }
                output[pair.Key] = ReadCaptured(pair.Value, depth + 1, redact);
            }
            return output;
        }
        if (value is IList list)
        {
            var output = new List<object?>();
            foreach (var item in list)
            {
                if (output.Count > Options.MaxCollectionEntries || !WorkAvailable()) { output.Add(Limited); break; }
                output.Add(ReadCaptured(item, depth + 1, redact));
            }
            return output;
        }
        return value;
    }

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

    private string Url(string value, DiagnosticCaptureContext? context = null)
    {
        if (!WorkAvailable()) return Limited;
        if (value.Length == 0) return value;
        var relative = value.StartsWith('/') || value.StartsWith('?') || value.StartsWith('#');
        if (!Uri.TryCreate(relative ? "https://diagnostic.invalid" + (value.StartsWith('/') ? "" : "/") + value : value,
                UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https" or "ftp" or "file" or "otpauth")) return Invalid;
        var segments = uri.AbsolutePath.Split('/');
        var redactNext = false;
        for (var index = 0; index < segments.Length; index++)
        {
            var decoded = Decode(segments[index]);
            if (!WorkAvailable()) return Limited;
            segments[index] = Uri.EscapeDataString(redactNext ? UrlRedacted : decoded);
            redactNext = RedactionEnabled && _rules.MatchesUrlSegment(decoded);
        }
        var query = uri.Query.Length > 1 ? "?" + Pairs(uri.Query[1..], context, UrlRedacted) : "";
        var fragment = uri.Fragment.Length > 1
            ? uri.Fragment.Contains('=') ? "#" + Pairs(uri.Fragment[1..], context, UrlRedacted) : uri.Fragment
            : "";
        var authority = relative ? "" : uri.GetComponents(RedactionEnabled
            ? UriComponents.SchemeAndServer : UriComponents.SchemeAndServer | UriComponents.UserInfo, UriFormat.UriEscaped);
        return authority + string.Join('/', segments) + query + fragment;
    }

    private string Pairs(string value, DiagnosticCaptureContext? context = null, string redacted = Redacted,
        string sourcePath = "")
    {
        if (!WorkAvailable()) return Limited;
        var result = new List<string>();
        foreach (var part in value.Split('&', Options.MaxCollectionEntries + 1))
        {
            if (result.Count == Options.MaxCollectionEntries || !WorkAvailable()) { result.Add("_diagnostic=" + Limited); break; }
            if (context != null && ++_nodes > Options.MaxNodes) { result.Add("_diagnostic=" + Limited); break; }
            var separator = part.IndexOf('=');
            var key = Decode((separator < 0 ? part : part[..separator]).Replace('+', ' '));
            var text = separator < 0 ? "" : Decode(part[(separator + 1)..].Replace('+', ' '));
            var safeKey = SanitizeKey(key);
            var fieldPath = sourcePath.Length == 0 ? key : sourcePath + "." + key;
            var sensitive = IsSensitive(fieldPath);
            var safeValue = sensitive ? redacted : text;
            // Query and fragment keys have independent name rules, even on an exempt URL property.
            if (context != null && !sensitive) context.Decisions.Add(new(fieldPath, false));
            result.Add(Uri.EscapeDataString(safeKey) + "=" + Uri.EscapeDataString(safeValue));
        }
        return string.Join('&', result);
    }

    private static string Decode(string value)
    {
        for (var round = 0; round < 3 && value.Contains('%'); round++)
        {
            for (var index = 0; index < value.Length; index++)
                if (value[index] == '%' && (index + 2 >= value.Length || !Uri.IsHexDigit(value[index + 1]) || !Uri.IsHexDigit(value[index + 2])))
                    throw new FormatException("Invalid diagnostic URL encoding.");
            value = Uri.UnescapeDataString(value);
        }
        return value.Contains('%') ? Invalid : value;
    }

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
        var sensitive = IsSensitive(path, exempt, namePath) || RedactionEnabled && (classified || value != null && IsClassified(value.GetType()));
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
        if (value is JsonElement element)
        {
            // Bound serialization before GetString can allocate from an externally supplied JSON tree.
            var buffer = new BoundedJsonBuffer(Options.MaxFieldBytes);
            using (var writer = new Utf8JsonWriter(buffer)) { element.WriteTo(writer); writer.Flush(); }
            _inputBytes += buffer.Written;
            return WorkAvailable() ? ProjectJson(path, element, depth, context, segments, kind, exempt, namePath) : Limited;
        }
        if (value is ScopeEvent scopeEvent) return CaptureEvent(scopeEvent);
        if (value is bool or byte or sbyte or short or ushort or int or uint or long or ulong or decimal or Guid or
            DateTime or DateTimeOffset or DateOnly or TimeOnly or TimeSpan || value.GetType().IsEnum) return value;
        if (value is double number) return double.IsFinite(number) ? number : Invalid;
        if (value is float single) return float.IsFinite(single) ? single : Invalid;
        if (value is char character) return character.ToString();
        if (!ancestors.Add(value)) return "[diagnostic cycle]";
        try
        {
            if (value is IDictionary dictionary)
            {
                var output = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (DictionaryEntry item in dictionary)
                {
                    if (output.Count >= Options.MaxCollectionEntries || !WorkAvailable()) { output["_diagnostic_limit"] = Limited; break; }
                    if (item.Key is not string key) { output[$"_diagnostic_field_{++_keySequence}"] = "[unsupported diagnostic key]"; continue; }
                    var safe = UniqueKey(output, key);
                    output[safe] = key.Length > Options.MaxKeyBytes ? Limited : Project(path + "." + key, item.Value, depth + 1, ancestors,
                        context, [.. segments, key], DiagnosticCaptureKind.DictionaryKey, namePath: ChildNamePath(namePath, key));
                }
                return output;
            }
            if (value is IReadOnlyDictionary<string, object?> readOnly)
            {
                var output = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var item in readOnly)
                {
                    if (output.Count >= Options.MaxCollectionEntries || !WorkAvailable()) { output["_diagnostic_limit"] = Limited; break; }
                    var safe = UniqueKey(output, item.Key);
                    output[safe] = item.Key.Length > Options.MaxKeyBytes ? Limited : Project(path + "." + item.Key, item.Value, depth + 1, ancestors,
                        context, [.. segments, item.Key], DiagnosticCaptureKind.DictionaryKey, namePath: ChildNamePath(namePath, item.Key));
                }
                return output;
            }
            if (value is IList list)
            {
                var output = new List<object?>();
                for (var index = 0; index < list.Count && index < Options.MaxCollectionEntries && WorkAvailable(); index++)
                    output.Add(Project(path, list[index], depth + 1, ancestors, context,
                        [.. segments, index.ToString(System.Globalization.CultureInfo.InvariantCulture)], DiagnosticCaptureKind.ListItem,
                        namePath: namePath));
                if (output.Count < list.Count) output.Add(Limited);
                return output;
            }
            if (value is IEnumerable) return "[unsupported lazy diagnostic collection]";
            var typedModel = IsModelType(value.GetType());
            var projected = new Dictionary<string, object?>(StringComparer.Ordinal);
            var members = Members.GetOrAdd(value.GetType(), static type =>
                type.GetProperties(BindingFlag.InstancePublic)
                    .Where(property => property.GetMethod?.IsPublic == true && property.GetIndexParameters().Length == 0)
                    .Select(property => new Member(property, property.IgnoredLog(), IsClassified(property), IsNameExempt(property)))
                    .ToArray());
            foreach (var member in members)
            {
                if (projected.Count >= Options.MaxCollectionEntries || !WorkAvailable()) { projected["_diagnostic_limit"] = Limited; break; }
                var key = UniqueKey(projected, member.Name);
                var memberNamePath = typedModel ? member.Name : ChildNamePath(namePath, member.Name);
                if (member.Ignored) projected[key] = ScopedLogConventions.IgnoredLogValue;
                else if (!context.DeferRedaction && (RedactionEnabled && member.Sensitive || IsSensitive(path + "." + member.Name,
                             member.Exempt || context.Exempts(DiagnosticCaptureKind.Property, [.. segments, member.Name]), memberNamePath))) projected[key] = Redacted;
                else
                {
                    try { projected[key] = Project(path + "." + member.Name, member.Read(value), depth + 1, ancestors,
                        context, [.. segments, member.Name], DiagnosticCaptureKind.Property, member.Exempt, memberNamePath, member.Sensitive); }
                    catch (Exception)
                    {
                        projected[key] = context.DeferRedaction && (member.Sensitive || IsSensitive(path + "." + member.Name,
                            member.Exempt || context.Exempts(DiagnosticCaptureKind.Property, [.. segments, member.Name]), memberNamePath))
                            ? new DiagnosticCapturedValue(Invalid, DiagnosticRedactionKind.Classified) : Invalid;
                        _status.Add("GetterFailed");
                    }
                }
            }
            return projected;
        }
        finally { ancestors.Remove(value); }
    }

    private ScopeEvent CaptureEvent(ScopeEvent value) => new(new EventId(value.Id.Id, SanitizeText(value.Id.Name ?? "")),
        value.Outcome == null ? null : SanitizeText(value.Outcome), value.Reason == null ? null : SanitizeText(value.Reason));

    private string UniqueKey(Dictionary<string, object?> output, string key)
    {
        var safe = key.Length <= Options.MaxKeyBytes && Encoding.UTF8.GetByteCount(key) <= Options.MaxKeyBytes &&
            !key.Any(char.IsControl) ? key : Invalid;
        return safe != key || safe.StartsWith("_diagnostic_", StringComparison.Ordinal) || output.ContainsKey(safe)
            ? $"_diagnostic_field_{++_keySequence}" : safe;
    }

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
                var map = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    if (map.Count >= Options.MaxCollectionEntries || !WorkAvailable()) { map["_diagnostic_limit"] = Limited; break; }
                    map[UniqueKey(map, property.Name)] = ProjectJson(path + "." + property.Name, property.Value, depth + 1,
                        context, [.. segments, property.Name], DiagnosticCaptureKind.JsonProperty,
                        namePath: ChildNamePath(namePath, property.Name));
                }
                return map;
            case JsonValueKind.Array:
                var list = new List<object?>();
                foreach (var item in element.EnumerateArray())
                {
                    if (list.Count >= Options.MaxCollectionEntries || !WorkAvailable()) { list.Add(Limited); break; }
                    list.Add(ProjectJson(path, item, depth + 1, context,
                        [.. segments, list.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)], DiagnosticCaptureKind.ListItem,
                        namePath: namePath));
                }
                return list;
            case JsonValueKind.String: return StringValue(path, element.GetString()!, context);
            case JsonValueKind.Number:
                if (element.TryGetDecimal(out var number)) return number;
                return element.TryGetDouble(out var floating) && double.IsFinite(floating) ? floating : Invalid;
            case JsonValueKind.True: return true;
            case JsonValueKind.False: return false;
            default: return null;
        }
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
