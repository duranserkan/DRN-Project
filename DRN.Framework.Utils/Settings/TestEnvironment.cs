namespace DRN.Framework.Utils.Settings;

public static class TestEnvironment
{
    private static readonly AsyncLocal<bool?> OverrideDrnTestContextEnabled = new();
    private static volatile bool _drnTestContextEnabled;

    public const string TestContextAddress = "http://localhost";

    public static bool DrnTestContextEnabled
    {
        get => OverrideDrnTestContextEnabled.Value ?? _drnTestContextEnabled;
        internal set => _drnTestContextEnabled = value;
    }

    internal static IDisposable SetTestContextEnabledScope(bool enabled)
    {
        var previous = OverrideDrnTestContextEnabled.Value;
        OverrideDrnTestContextEnabled.Value = enabled;
        return new ScopeDisposable(() => OverrideDrnTestContextEnabled.Value = previous);
    }

    private sealed class ScopeDisposable(Action onDispose) : IDisposable
    {
        private Action? _onDispose = onDispose;

        public void Dispose() => Interlocked.Exchange(ref _onDispose, null)?.Invoke();
    }
}