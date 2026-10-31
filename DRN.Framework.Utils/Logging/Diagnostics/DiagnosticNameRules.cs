using System.Buffers;
using DRN.Framework.SharedKernel.Diagnostics;

namespace DRN.Framework.Utils.Logging.Diagnostics;

internal enum DiagnosticRuleType { None, Secret, Personal, FieldPath, UrlSegment, Limit }
internal readonly record struct DiagnosticRuleResult(DiagnosticRuleType Type)
{
    public bool IsMatch => Type != DiagnosticRuleType.None;
}

internal sealed class DiagnosticNameRules
{
    private readonly string[] _additional;
    private readonly string[] _additionalPaths;
    private readonly string[] _urlSegments;
    private readonly int _maxKeyBytes;
    private readonly int _capacity;
    private readonly Dictionary<string, (DiagnosticRuleType BuiltIn, DiagnosticRuleType Additional)> _cache = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    public DiagnosticNameRules(DiagnosticLogOptions options)
    {
        _maxKeyBytes = options.MaxKeyBytes;
        _capacity = options.MaxNodes;
        _additional = NormalizeRules(options.SensitiveFields);
        _additionalPaths = NormalizeRules(options.SensitiveFieldPaths);
        _urlSegments = NormalizeRules(options.SensitivePathSegments);
    }

    public DiagnosticRuleResult MatchField(string value, bool exempt = false)
    {
        // Hard path limits and configured rules cannot be waived by an exemption.
        if (value.Length > _maxKeyBytes) return new(DiagnosticRuleType.Limit);
        lock (_gate)
        {
            if (!_cache.TryGetValue(value, out var result))
            {
                Span<char> buffer = stackalloc char[512];
                var name = Normalize(value, buffer);
                var builtIn = DiagnosticRuleCatalog.Matches(name, DiagnosticRuleCatalog.SecretFields) ? DiagnosticRuleType.Secret :
                    DiagnosticRuleCatalog.Matches(name, DiagnosticRuleCatalog.PersonalFields) ? DiagnosticRuleType.Personal :
                    DiagnosticRuleCatalog.Matches(name, DiagnosticRuleCatalog.FieldPaths) ? DiagnosticRuleType.FieldPath : DiagnosticRuleType.None;
                var additional = DiagnosticRuleCatalog.Matches(name, _additional) ? DiagnosticRuleType.Secret :
                    DiagnosticRuleCatalog.Matches(name, _additionalPaths) ? DiagnosticRuleType.FieldPath : DiagnosticRuleType.None;
                result = (builtIn, additional);
                if (_cache.Count < _capacity) _cache.Add(value, result);
            }
            return new(!exempt && result.BuiltIn != DiagnosticRuleType.None ? result.BuiltIn : result.Additional);
        }
    }

    public bool MatchesUrlSegment(ReadOnlySpan<char> value)
    {
        char[]? rented = null;
        Span<char> buffer = value.Length <= 512 ? stackalloc char[512] : (rented = ArrayPool<char>.Shared.Rent(value.Length));
        try
        {
            var name = Normalize(value, buffer);
            return DiagnosticRuleCatalog.Matches(name, DiagnosticRuleCatalog.UrlSegments) ||
                   DiagnosticRuleCatalog.Matches(name, _urlSegments);
        }
        finally { if (rented != null) ArrayPool<char>.Shared.Return(rented, clearArray: true); }
    }

    internal static ReadOnlySpan<char> Normalize(ReadOnlySpan<char> value, Span<char> buffer) =>
        DiagnosticRuleCatalog.Normalize(value, buffer);

    private static string[] NormalizeRules(IEnumerable<string> values) => values.Select(DiagnosticRuleCatalog.Normalize)
        .Where(value => value.Length != 0).ToArray();
}
