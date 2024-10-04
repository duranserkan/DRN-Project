using DRN.Framework.Utils.Extensions;

namespace DRN.Framework.Utils.Scope;

public class ScopeData
{
    private readonly Dictionary<string, bool> _flags = new();
    private readonly Dictionary<string, object> _parameters = new();

    public IReadOnlyDictionary<string, bool> Flags => _flags;
    public IReadOnlyDictionary<string, object> Parameters => _parameters;

    public bool IsFlagEnabled(string flag) => _flags.TryGetValue(flag, out var value) && value;
    public TValue GetParameter<TValue>(string key, TValue defaultValue = default!) => _parameters.GetAndCastValueOrDefault(key, defaultValue);

    public void SetParameterAsFlag(string flag, string stringValue, bool defaultValue = false)
        => _flags[flag] = bool.TryParse(stringValue, out var value) ? value : defaultValue;

    public void SetParameter<TValue>(string key, string stringValue, TValue defaultValue = default!) where TValue : IParsable<TValue>
        => _parameters[key] = stringValue.TryParse<TValue>(out var result) ? result! : defaultValue;
}