namespace DRN.Framework.Utils.Scope;

/// <summary>
/// Stores caller-owned ambient flags and parameters with case-insensitive keys.
/// </summary>
public class ScopeData
{
    private readonly Dictionary<string, bool> _flags = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object?> _parameters = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, bool> Flags => _flags;
    public IReadOnlyDictionary<string, object?> Parameters => _parameters;

    public bool IsFlagEnabled(string flag) => _flags.TryGetValue(flag, out var value) && value;
    public TValue? GetParameter<TValue>(string key, TValue? defaultValue = default) =>
        _parameters.TryGetValue(key, out var value) && value is TValue typedValue ? typedValue : defaultValue;

    public void SetFlag(string flag, bool value) => _flags[flag] = value;
    public void SetParameter<TValue>(string key, TValue value) => _parameters[key] = value;

    // TODO: Add explicit GetOrSet adapters for Headers, QueryString, Form, Cookie, Path, Items, and TempData.
    // Define trust and precedence before managing or unifying values from those boundaries.
}
