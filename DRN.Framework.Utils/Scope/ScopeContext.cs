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
    public IScopedLog ScopedLog { get; private set; } = null!;
    public IScopedUser ScopedUser { get; private set; } = null!;

    public Dictionary<string, bool> Flags { get; } = new();
    public Dictionary<string, object> Parameters { get; } = new();

    public bool IsFlagEnabled(string flag) => Flags.TryGetValue(flag, out var value) && value;


    public static void Initialize(string traceId, IScopedLog scopedLog, IScopedUser scopedUser)
    {
        var context = Value;
        context.TraceId = traceId;
        context.ScopedLog = scopedLog;
        context.ScopedUser = scopedUser;
    }
}