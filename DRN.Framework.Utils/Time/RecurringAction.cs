using DRN.Framework.Utils.Concurrency;

namespace DRN.Framework.Utils.Time;

public sealed class RecurringAction : IDisposable
{
    private readonly Timer? _timer;
    private readonly ManualResetEventSlim? _startGate;
    private readonly AutoResetEvent? _wakeHandle;
    private readonly Action _action;
    private readonly int _period;
    // Keeps the atomic started state and Timer.Change calls in the same ordering domain.
    private readonly Lock _scheduleLock = new();
    private int _isRunning; //0 = false, 1 = true
    private int _isStarted; //0 = stopped, 1 = started
    private int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecurringAction"/> class for synchronous actions.
    /// </summary>
    /// <param name="action">The synchronous action to execute repeatedly. Use <see cref="RecurringActionAsync"/> for async callbacks, not async void.</param>
    /// <param name="period">The time, in milliseconds, between the end of one execution and the start of the next.</param>
    /// <param name="start">If set to <c>true</c>, the recurring action starts immediately.</param>
    /// <param name="threadName">When specified, runs on a dedicated unpooled background thread rather than the ThreadPool.</param>
    /// <param name="priority">The scheduling priority of the dedicated thread (defaults to <see cref="ThreadPriority.Highest"/>).</param>
    public RecurringAction(
        Action action,
        int period,
        bool start = true,
        string? threadName = null,
        ThreadPriority priority = ThreadPriority.Highest)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (period < 0)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be non-negative.");

        _action = action;
        _period = period;

        if (threadName != null)
        {
            _startGate = new ManualResetEventSlim(start);
            _wakeHandle = new AutoResetEvent(false);
            var thread = new Thread(DedicatedThreadLoop)
            {
                Name = threadName,
                IsBackground = true,
                Priority = priority
            };

            if (start)
                _isStarted = 1;

            thread.Start();
        }
        else
        {
            _timer = new Timer(_ => TimerCallback(), null, Timeout.Infinite, Timeout.Infinite);

            if (start) Start();
        }
    }

    public event Action<Exception>? OnActionFailed;

    /// <summary>
    /// Starts the recurring action. In dedicated-thread mode, restarting during an active callback
    /// preserves the configured period after that callback finishes.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the instance has already been disposed of.</exception>
    public void Start()
    {
        lock (_scheduleLock)
        {
            ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

            // Clear the previous stop signal before a finishing callback can observe the restart.
            if (_timer == null && Volatile.Read(ref _isStarted) == 0)
                _wakeHandle!.Reset();

            Volatile.Write(ref _isStarted, 1);
            if (_timer != null)
            {
                _timer.Change(0, Timeout.Infinite);
            }
            else
            {
                _startGate?.Set();
            }
        }
    }

    /// <summary>
    /// Stops recurring scheduling. An active callback is allowed to finish without scheduling another invocation.
    /// </summary>
    public void Stop()
    {
        lock (_scheduleLock)
        {
            Volatile.Write(ref _isStarted, 0);
            if (Volatile.Read(ref _disposed) == 1)
                return;

            if (_timer != null)
            {
                _timer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            else
            {
                _startGate?.Reset();
                _wakeHandle?.Set();
            }
        }
    }

    private void DedicatedThreadLoop()
    {
        try
        {
            while (Volatile.Read(ref _disposed) == 0)
            {
                _startGate!.Wait();

                if (Volatile.Read(ref _disposed) != 0) break;

                ExecuteAction();

                if (Volatile.Read(ref _disposed) != 0) break;

                if (Volatile.Read(ref _isStarted) == 1)
                {
                    _wakeHandle!.WaitOne(_period);
                }
            }
        }
        finally
        {
            // Only the exiting worker releases handles, after lifecycle callers have
            // finished signaling them. Dispose never waits on a callback holding this lock.
            lock (_scheduleLock)
            {
                _startGate!.Dispose();
                _wakeHandle!.Dispose();
            }
        }
    }

    private void ExecuteAction()
    {
        try
        {
            _action();
        }
        catch (Exception ex)
        {
            ReportFailure(ex);
        }
    }

    private void TimerCallback()
    {
        if (!LockUtils.TryClaimLock(ref _isRunning)) return;

        try
        {
            ExecuteAction();
        }
        finally
        {
            LockUtils.ReleaseLock(ref _isRunning);
            
            lock (_scheduleLock)
            {
                if (Volatile.Read(ref _disposed) == 0 && Volatile.Read(ref _isStarted) == 1)
                    _timer!.Change(_period, Timeout.Infinite);
            }
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

    public void Dispose()
    {
        lock (_scheduleLock)
        {
            if (_disposed == 1) return;
            Volatile.Write(ref _disposed, 1);

            Volatile.Write(ref _isStarted, 0);

            if (_timer != null)
            {
                _timer.Dispose();
            }
            else
            {
                _startGate?.Set();
                _wakeHandle?.Set();
            }
        }
    }
}
