namespace DRN.Framework.Utils.Time;

/// <summary>
/// Executes an asynchronous callback periodically using <see cref="PeriodicTimer"/> without overlapping executions.
/// Supports cancellation token propagation and graceful teardown via <see cref="IAsyncDisposable"/>.
/// </summary>
public sealed class RecurringActionAsync : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task> _actionAsync;
    private readonly TimeSpan _period;
    private readonly TimeSpan? _executionTimeout;
    private readonly Lock _scheduleLock = new();
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private int _isStarted; // 0 = stopped, 1 = started
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecurringActionAsync"/> class.
    /// </summary>
    /// <param name="actionAsync">The asynchronous action to execute periodically, receiving a <see cref="CancellationToken"/>.</param>
    /// <param name="period">The interval between ticks, truncated to whole milliseconds in the range 1 through 4,294,967,294.</param>
    /// <param name="start">Whether to start the action immediately (defaults to <c>true</c>).</param>
    /// <param name="executionTimeout">Optional delay before requesting cancellation. The callback must observe its token and finish before another iteration can run.</param>
    public RecurringActionAsync(
        Func<CancellationToken, Task> actionAsync,
        TimeSpan period,
        bool start = true,
        TimeSpan? executionTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(actionAsync);
        // Match PeriodicTimer's whole-millisecond range before any background work starts.
        var periodMilliseconds = (long)period.TotalMilliseconds;
        if (periodMilliseconds is < 1 or > uint.MaxValue - 1L)
            throw new ArgumentOutOfRangeException(nameof(period), period,
                "Period must represent between 1 and 4,294,967,294 whole milliseconds.");
        if (executionTimeout.HasValue)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(executionTimeout.Value, TimeSpan.Zero, nameof(executionTimeout));
            // CancelAfter truncates to whole milliseconds, including positive sub-millisecond delays.
            if ((long)executionTimeout.Value.TotalMilliseconds > uint.MaxValue - 1L)
                throw new ArgumentOutOfRangeException(nameof(executionTimeout), executionTimeout,
                    "Execution timeout must not exceed 4,294,967,294 whole milliseconds.");
        }

        _actionAsync = actionAsync;
        _period = period;
        _executionTimeout = executionTimeout;

        if (start) Start();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RecurringActionAsync"/> class.
    /// </summary>
    /// <param name="actionAsync">The asynchronous action to execute periodically, receiving a <see cref="CancellationToken"/>.</param>
    /// <param name="periodMilliseconds">The interval in milliseconds between ticks.</param>
    /// <param name="start">Whether to start the action immediately (defaults to <c>true</c>).</param>
    /// <param name="executionTimeout">Optional delay before requesting cancellation. The callback must observe its token and finish before another iteration can run.</param>
    public RecurringActionAsync(
        Func<CancellationToken, Task> actionAsync,
        int periodMilliseconds,
        bool start = true,
        TimeSpan? executionTimeout = null)
        : this(actionAsync, TimeSpan.FromMilliseconds(periodMilliseconds), start, executionTimeout)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RecurringActionAsync"/> class.
    /// </summary>
    /// <param name="actionAsync">The asynchronous action to execute periodically.</param>
    /// <param name="period">The interval between ticks, truncated to whole milliseconds in the range 1 through 4,294,967,294.</param>
    /// <param name="start">Whether to start the action immediately (defaults to <c>true</c>).</param>
    public RecurringActionAsync(
        Func<Task> actionAsync,
        TimeSpan period,
        bool start = true)
        : this(Adapt(actionAsync), period, start)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RecurringActionAsync"/> class.
    /// </summary>
    /// <param name="actionAsync">The asynchronous action to execute periodically.</param>
    /// <param name="periodMilliseconds">The interval in milliseconds between ticks.</param>
    /// <param name="start">Whether to start the action immediately (defaults to <c>true</c>).</param>
    public RecurringActionAsync(
        Func<Task> actionAsync,
        int periodMilliseconds,
        bool start = true)
        : this(actionAsync, TimeSpan.FromMilliseconds(periodMilliseconds), start)
    {
    }

    private static Func<CancellationToken, Task> Adapt(Func<Task> actionAsync)
    {
        ArgumentNullException.ThrowIfNull(actionAsync);
        return _ => actionAsync();
    }

    /// <summary>
    /// Raised for callback failures, including TimeoutException when a callback throws OperationCanceledException after its timeout.
    /// </summary>
    public event Action<Exception>? OnActionFailed;

    /// <summary>
    /// Starts recurring execution if not already started.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown if the instance is disposed.</exception>
    public void Start()
    {
        lock (_scheduleLock)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_isStarted == 1)
                return;

            Volatile.Write(ref _isStarted, 1);
            _cts = new CancellationTokenSource();
            _loopTask = RunLoopAsync(_cts.Token, _loopTask is { IsCompletedSuccessfully: true } ? null : _loopTask);
        }
    }

    /// <summary>
    /// Requests cancellation and stops recurring scheduling without waiting for the active callback to finish.
    /// </summary>
    public void Stop()
    {
        CancellationTokenSource? ctsToCancel;
        lock (_scheduleLock)
        {
            if (_isStarted == 0 || _disposed)
                return;

            Volatile.Write(ref _isStarted, 0);
            ctsToCancel = _cts;
            _cts = null;
        }

        using (ctsToCancel)
            ctsToCancel?.Cancel();
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken, Task? previousLoop)
    {
        // Avoid executing callback work inline in Start().
        await Task.Yield();

        // A stopped callback may still be finishing. Preserve its lifetime across restarts
        // so the newest loop also represents all outstanding work for async disposal.
        if (previousLoop != null)
            await previousLoop.ConfigureAwait(false);
        previousLoop = null;

        if (cancellationToken.IsCancellationRequested)
            return;

        using var timer = new PeriodicTimer(_period);
        try
        {
            // Execute the initial iteration immediately upon start
            if (!cancellationToken.IsCancellationRequested && Volatile.Read(ref _isStarted) == 1)
                await ExecuteIterationAsync(cancellationToken).ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested && await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                if (Volatile.Read(ref _isStarted) == 0 || cancellationToken.IsCancellationRequested)
                    break;

                await ExecuteIterationAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Expected on cancellation
        }
    }

    private async Task ExecuteIterationAsync(CancellationToken cancellationToken)
    {
        // Iterations are awaited serially; restarted loops await the previous loop.
        CancellationTokenSource? linkedCts = null;
        try
        {
            var effectiveToken = cancellationToken;
            if (_executionTimeout.HasValue)
            {
                linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linkedCts.CancelAfter(_executionTimeout.Value);
                effectiveToken = linkedCts.Token;
            }

            await _actionAsync(effectiveToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (linkedCts?.IsCancellationRequested == true && !cancellationToken.IsCancellationRequested)
        {
            ReportFailure(new TimeoutException($"RecurringActionAsync execution exceeded the configured timeout of {_executionTimeout!.Value}."));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Clean cancellation
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
        }
        finally
        {
            linkedCts?.Dispose();
        }
    }

    private void ReportFailure(Exception exception)
    {
        try
        {
            OnActionFailed?.Invoke(exception);
        }
        catch (Exception)
        {
            // Listener failures must not stop recurring execution.
        }
    }

    /// <summary>
    /// Asynchronously stops recurring scheduling and awaits active callbacks, including after earlier disposal calls.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        CancellationTokenSource? ctsToCancel;
        Task? loopToAwait;

        lock (_scheduleLock)
        {
            _disposed = true;

            Volatile.Write(ref _isStarted, 0);
            ctsToCancel = _cts;
            _cts = null;
            // Retain the completion barrier so every async disposer awaits outstanding work.
            loopToAwait = _loopTask;
        }

        using var cancellationSource = ctsToCancel;
        try
        {
            if (ctsToCancel != null)
                await ctsToCancel.CancelAsync();
        }
        finally
        {
            if (loopToAwait != null)
                await loopToAwait.ConfigureAwait(false);
        }
    }
}
