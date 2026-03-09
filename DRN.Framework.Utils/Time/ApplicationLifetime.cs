namespace DRN.Framework.Utils.Time;

/// <summary>
/// Provides a static shutdown hook for critical static utilities that cannot access DI.
/// The hosting layer registers a shutdown action (typically <c>IHostApplicationLifetime.StopApplication</c>)
/// during application bootstrap.
/// </summary>
public static class ApplicationLifetime
{
    internal static Action? ShutdownAction;

    /// <summary>
    /// Requests graceful application shutdown by invoking the registered <see cref="ShutdownAction"/>.
    /// No-op if no action has been registered.
    /// </summary>
    internal static void RequestShutdown()
    {
        try
        {
            ShutdownAction?.Invoke();
        }
        catch (ObjectDisposedException)
        {
            // Host already disposed during shutdown — safe to ignore
        }
    }
}
