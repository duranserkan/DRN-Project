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
    public IScopedLog ScopedLog { get; private set; } = null!;
    public IScopedUser ScopedUser { get; private set; } = null!;

    public string? UserId => ScopedUser.Id;
    public Dictionary<string, bool> Flags { get; } = new();
    public Dictionary<string, object> Parameters { get; } = new();

    public string GetClaimValue(string claim, string defaultValue = "") => ScopedUser.FindClaimGroup(claim)?.Value ?? defaultValue;

    public bool IsFlagEnabled(string flag) => Flags.TryGetValue(flag, out var value) && value;

    public T GetParameterValue<T>(string parameter, T defaultValue = default!) => Parameters.GetAndCastValueOrDefault(parameter, defaultValue);

    public void SetParameterAsFlag(string parameter, string stringValue, bool defaultValue = false) => Flags[parameter]
        = bool.TryParse(stringValue, out var value) ? value : defaultValue;

    public void SetParameterAsIntValue(string parameter, string stringValue, int defaultValue = 0) => Parameters[parameter]
        = int.TryParse(stringValue, out var value) ? value : defaultValue;

    public void AddClaimValueToFlags(string claim, bool defaultValue = false)
    {
        var claimValue = GetClaimValue(claim, defaultValue.ToString());
        SetParameterAsFlag(claim, claimValue);
    }

    public void AddClaimValueToParametersAsInt(string claim, int defaultValue = 0)
    {
        var claimValue = GetClaimValue(claim, defaultValue.ToString());
        SetParameterAsIntValue(claim, claimValue);
    }

    public static void Initialize(string traceId, IScopedLog scopedLog, IScopedUser scopedUser)
    {
        var context = Value;
        context.TraceId = traceId;
        context.ScopedLog = scopedLog;
        context.ScopedUser = scopedUser;
    }
}