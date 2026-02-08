using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.Extensions.Hosting;

namespace DRN.Framework.Hosting.Utils;

public interface IAppStartupStatus
{
    /// <summary>
    /// Gets whether the application has started.
    /// </summary>
    bool HasStarted { get; }

    /// <summary>
    /// Waits asynchronously until the application has started.
    /// Returns immediately if the application has already started.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token for graceful shutdown.</param>
    /// <returns>True if the application started; false if cancelled before startup.</returns>
    Task<bool> WaitForStartAsync(CancellationToken cancellationToken = default);
}

[Singleton<IAppStartupStatus>]
public sealed class AppStartupStatus(IHostApplicationLifetime lifetime) : IAppStartupStatus
{
    /// <inheritdoc />
    public bool HasStarted => lifetime.ApplicationStarted.IsCancellationRequested;

    /// <inheritdoc />
    public async Task<bool> WaitForStartAsync(CancellationToken cancellationToken = default)
    {
        if (HasStarted)
            return true;

        var startedTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var startedRegistration = lifetime.ApplicationStarted.Register(() => startedTcs.TrySetResult());
        await using var cancelRegistration = cancellationToken.Register(() => startedTcs.TrySetCanceled(cancellationToken));

        try
        {
            await startedTcs.Task.ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
