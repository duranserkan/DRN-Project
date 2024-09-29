using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Logging;

namespace DRN.Framework.Utils.Scope;

public class ScopeContext
{
    private ScopeContext()
    {
    }

    private static readonly AsyncLocal<ScopeContext> Local = new();
    public static ScopeContext Value => Local.Value ??= new ScopeContext();

    public string TraceId { get; private set; } = null!;
    public string? UserId => ScopedUser.Id;

    public IScopedLog ScopedLog { get; private set; } = null!;
    public IScopedUser ScopedUser { get; private set; } = null!;

    private readonly Dictionary<string, bool> _flags = new();
    public IReadOnlyDictionary<string, bool> Flags => _flags;

    private readonly Dictionary<string, object> _parameters = new();
    public IReadOnlyDictionary<string, object> Parameters => _parameters;

    public bool IsFlagEnabled(string flag) => _flags.TryGetValue(flag, out var value) && value;
    public TValue GetParameter<TValue>(string key, TValue defaultValue = default!) => _parameters.GetAndCastValueOrDefault(key, defaultValue);

    public void SetParameterAsFlag(string flag, string stringValue, bool defaultValue = false)
        => _flags[flag] = bool.TryParse(stringValue, out var value) ? value : defaultValue;

    public void SetParameter<TValue>(string key, string stringValue, TValue defaultValue = default!) where TValue : IParsable<TValue>
        => _parameters[key] = stringValue.TryParse<TValue>(out var result) ? result! : defaultValue;


    public bool IsClaimFlagEnabled(string flag, string? issuer = null, bool defaultValue = false)
    {
        if (_flags.TryGetValue(flag, out var value)) return value;

        AddClaimValueToFlags(flag, issuer, defaultValue);

        return _flags.TryGetValue(flag, out value) && value;
    }

    public TValue GetClaimParameter<TValue>(string key, string? issuer = null, TValue defaultValue = default!) where TValue : IParsable<TValue>
    {
        if (_parameters.TryGetValue(key, out var value))
            return value is TValue tValue ? tValue : defaultValue;

        AddClaimValueToParameters(key, issuer, defaultValue);

        return _parameters.TryGetValue(key, out value)
            ? value is TValue ttValue ? ttValue : defaultValue
            : defaultValue;
    }

    public void AddClaimValueToFlags(string claim, string? issuer = null, bool defaultValue = false)
    {
        var value = ScopedUser.GetClaimValue(claim, issuer, defaultValue.ToString());
        SetParameterAsFlag(claim, value);
    }

    public void AddClaimValueToParameters<TValue>(string claim, string? issuer = null, TValue defaultValue = default!) where TValue : IParsable<TValue>
    {
        var value = ScopedUser.GetClaimValue(claim, issuer, defaultValue.ToString() ?? string.Empty);
        SetParameter<TValue>(claim, value);
    }

    public static void Initialize(string traceId, IScopedLog scopedLog, IScopedUser scopedUser)
    {
        var context = Value;
        context.TraceId = traceId;
        context.ScopedLog = scopedLog;
        context.ScopedUser = scopedUser;
    }
}