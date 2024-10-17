using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Logging;

namespace DRN.Framework.Utils.Scope;

public class ScopeContext
{
    private bool _initialized = false;

    private ScopeContext()
    {
    }

    private static readonly AsyncLocal<ScopeContext> Local = new();
    public static ScopeContext Value => Local.Value ??= new ScopeContext();

    public string TraceId { get; private set; } = null!;
    public ScopeData ScopeData { get; } = new();
    public IScopedLog ScopedLog { get; private set; } = null!;
    public IScopedUser ScopedUser { get; private set; } = null!;

    public static ScopeData Data => Value.ScopeData;
    public static IScopedLog Log => Value.ScopedLog;
    public static IScopedUser User => Value.ScopedUser;
    public static string? UserId => User.Id;
    public static bool Authenticated => User.Authenticated;

    public static bool IsClaimFlagEnabled(string flag, string? issuer = null, bool defaultValue = false)
    {
        if (Data.Flags.TryGetValue(flag, out var value))
            return value;

        AddClaimValueToFlags(flag, issuer, defaultValue);

        return Data.IsFlagEnabled(flag);
    }

    private static void AddClaimValueToFlags(string claim, string? issuer = null, bool defaultValue = false)
    {
        var value = User.GetClaimValue(claim, issuer, defaultValue.ToString());
        Data.SetParameterAsFlag(claim, value);
    }

    public static bool HasClaimValue<TValue>(string key, TValue expectedValue, string? issuer = null) where TValue : IParsable<TValue>
    {
        var value = GetClaimParameter<TValue>(key, issuer);

        return expectedValue.Equals(value);
    }

    public static TValue? GetClaimParameter<TValue>(string key, string? issuer = null, TValue? defaultValue = default) where TValue : IParsable<TValue>
    {
        if (Data.Parameters.TryGetValue(key, out var value))
            return value is TValue tValue ? tValue : defaultValue;

        AddClaimValueToParameters(key, issuer, defaultValue);

        return Data.GetParameter(key, defaultValue);
    }

    private static void AddClaimValueToParameters<TValue>(string claim, string? issuer = null, TValue? defaultValue = default) where TValue : IParsable<TValue>
    {
        var value = User.GetClaimValue(claim, issuer, defaultValue?.ToString() ?? string.Empty);
        Data.SetParameter<TValue>(claim, value);
    }

    public static void Initialize(string traceId, IScopedLog scopedLog, IScopedUser scopedUser)
    {
        var context = Value;

        if (context._initialized) return;

        context.TraceId = traceId;
        context.ScopedLog = scopedLog;
        context.ScopedUser = scopedUser;
        context._initialized = true;
    }
}