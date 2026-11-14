using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace DRN.Framework.Hosting.DrnProgram;

/// <summary>Controls how a static-file rule writes its response header.</summary>
public enum DrnStaticFileHeaderBehavior
{
    Override,
    IfMissing
}

/// <summary>An exact path and response header configured at application startup.</summary>
public sealed class DrnStaticFileHeaderRule
{
    public DrnStaticFileHeaderRule(PathString path, string headerName, string value,
        DrnStaticFileHeaderBehavior behavior = DrnStaticFileHeaderBehavior.Override)
    {
        if (!IsValidPath(path))
            throw new ArgumentException("Use an absolute local path without a query, fragment, wildcard or control characters.", nameof(path));
        ArgumentException.ThrowIfNullOrWhiteSpace(headerName);
        foreach (var c in headerName.AsSpan())
            if (!char.IsAsciiLetterOrDigit(c) && !"!#$%&'*+-.^_`|~".Contains(c))
                throw new ArgumentException("Use a valid HTTP header name.", nameof(headerName));
        ArgumentNullException.ThrowIfNull(value);
        foreach (var c in value.AsSpan())
            if (c == '\u007f' || c < ' ' && c != '\t')
                throw new ArgumentException("Header values cannot contain control characters other than tabs.", nameof(value));
        if (!Enum.IsDefined(behavior))
            throw new ArgumentOutOfRangeException(nameof(behavior));

        Path = path;
        HeaderName = headerName;
        Value = value;
        Behavior = behavior;
    }

    public PathString Path { get; }
    public string HeaderName { get; }
    public string Value { get; }
    public DrnStaticFileHeaderBehavior Behavior { get; }

    private static bool IsValidPath(PathString path)
    {
        if (!path.HasValue || path.Value.StartsWith("//", StringComparison.Ordinal))
            return false;

        foreach (var c in path.Value.AsSpan())
            if (char.IsControl(c) || "?#*\\".Contains(c))
                return false;

        return true;
    }
}

/// <summary>Startup rules applied after static-file response callbacks. Paths exclude PathBase and the static-file RequestPath.</summary>
public sealed class DrnStaticFileHeaderOptions
{
    private readonly List<DrnStaticFileHeaderRule> _rules =
        [new("/robots.txt", HeaderNames.CacheControl, "no-cache")];

    public DrnStaticFileHeaderOptions() => Rules = _rules.AsReadOnly();

    public IReadOnlyList<DrnStaticFileHeaderRule> Rules { get; }

    internal bool AreHeaderValuesEncodable(Func<string, Encoding?> encodingSelector)
    {
        foreach (var rule in _rules)
        {
            var encoding = (Encoding)(encodingSelector(rule.HeaderName) ?? Encoding.ASCII).Clone();
            // Reject lossy replacement without changing the server's configured encoding.
            encoding.EncoderFallback = EncoderFallback.ExceptionFallback;
            try
            {
                _ = encoding.GetByteCount(rule.Value);
            }
            catch (EncoderFallbackException)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Adds or replaces the rule with the same case-insensitive path and header name.</summary>
    public void Set(DrnStaticFileHeaderRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        for (var index = 0; index < _rules.Count; index++)
            if (Matches(_rules[index], rule.Path, rule.HeaderName))
            {
                _rules[index] = rule;
                return;
            }

        _rules.Add(rule);
    }

    /// <summary>Removes the matching rule, including a framework default. Returns whether a rule was removed.</summary>
    public bool Remove(PathString path, string headerName)
    {
        for (var index = 0; index < _rules.Count; index++)
            if (Matches(_rules[index], path, headerName))
            {
                _rules.RemoveAt(index);
                return true;
            }

        return false;
    }

    private static bool Matches(DrnStaticFileHeaderRule rule, PathString path, string headerName)
        => rule.Path.Equals(path, StringComparison.OrdinalIgnoreCase) &&
           string.Equals(rule.HeaderName, headerName, StringComparison.OrdinalIgnoreCase);
}
