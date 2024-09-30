using DRN.Framework.Utils.Auth;
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

    public ScopeData Data { get; } = new();
    public IScopedLog ScopedLog { get; private set; } = null!;
    public IScopedUser ScopedUser { get; private set; } = null!;


    public bool IsClaimFlagEnabled(string flag, string? issuer = null, bool defaultValue = false)
    {
        if (Data.Flags.TryGetValue(flag, out var value))
            return value;

        AddClaimValueToFlags(flag, issuer, defaultValue);

        return Data.IsFlagEnabled(flag);
    }

    private void AddClaimValueToFlags(string claim, string? issuer = null, bool defaultValue = false)
    {
        var value = ScopedUser.GetClaimValue(claim, issuer, defaultValue.ToString());
        Data.SetParameterAsFlag(claim, value);
    }

    public TValue GetClaimParameter<TValue>(string key, string? issuer = null, TValue defaultValue = default!) where TValue : IParsable<TValue>
    {
        if (Data.Parameters.TryGetValue(key, out var value))
            return value is TValue tValue ? tValue : defaultValue;

        AddClaimValueToParameters(key, issuer, defaultValue);

        return Data.GetParameter(key, defaultValue);
    }

    private void AddClaimValueToParameters<TValue>(string claim, string? issuer = null, TValue defaultValue = default!) where TValue : IParsable<TValue>
    {
        var value = ScopedUser.GetClaimValue(claim, issuer, defaultValue.ToString() ?? string.Empty);
        Data.SetParameter<TValue>(claim, value);
    }

    public static void Initialize(string traceId, IScopedLog scopedLog, IScopedUser scopedUser)
    {
        var context = Value;
        context.TraceId = traceId;
        context.ScopedLog = scopedLog;
        context.ScopedUser = scopedUser;
    }
}