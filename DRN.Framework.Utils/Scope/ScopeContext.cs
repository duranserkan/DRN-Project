using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Utils.Scope;

public class ScopeContext
{
    private bool _initialized;

    private ScopeContext()
    {
    }

    private static readonly AsyncLocal<ScopeContext> Local = new();
    public static ScopeContext Value => Local.Value ??= new ScopeContext();

    private string Trace { get; set; } = null!;
    private IScopedLog ScopedLog { get; set; } = null!;
    private IScopedUser ScopedUser { get; set; } = null!;
    private ScopeData ScopeData { get; } = new();
    private IServiceProvider ServiceProvider { get; set; } = null!;
    private IAppSettings AppSettings { get; set; } = null!;

    public ScopeSummary GetScopeSummary() => new(ScopedLog, ScopedUser, ScopeData);

    public static string TraceId => Value.Trace;
    public static ScopeData Data => Value.ScopeData;
    public static IScopedLog Log => Value.ScopedLog;
    public static IScopedUser User => Value.ScopedUser;
    public static string? UserId => User.Id;
    public static bool Authenticated => User.Authenticated;
    public static bool MFACompleted => MfaFor.MfaCompleted;

    public static IServiceProvider Services => Value.ServiceProvider;
    public static IAppSettings Settings => Value.AppSettings;

    public static bool IsUserInRole(string role)
    {
        if (Data.Roles.TryGetValue(role, out var value))
            return value;

        AddRoleExistanceToRoles(role);

        return Data.IsRoleExists(role);
    }

    private static void AddRoleExistanceToRoles(string role)
    {
        var value = User.IsInRole(role);
        Data.SetParameterAsRole(role, value);
    }

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

    internal static void Initialize(string traceId, IScopedLog scopedLog, IScopedUser scopedUser, IAppSettings settings, IServiceProvider serviceProvider)
    {
        var context = Value;
        if (context._initialized)
            return;

        context.Trace = traceId;
        context.ScopedLog = scopedLog;
        context.ScopedUser = scopedUser;
        context.ServiceProvider = serviceProvider;
        context.AppSettings = settings;
        context._initialized = true;
    }
}

public record ScopeSummary(IScopedLog Log, IScopedUser User, ScopeData ScopeData);