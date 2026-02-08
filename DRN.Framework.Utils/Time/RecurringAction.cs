using DRN.Framework.Utils.Concurrency;

namespace DRN.Framework.Utils.Time;

public sealed class RecurringAction : IDisposable
{
    private readonly Timer _timer;
    private readonly Func<Task> _actionAsync;
    private readonly int _period;
    private volatile int _isRunning; //0 = false, 1 = true
    private volatile int _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecurringAction"/> class.
    /// </summary>
    /// <param name="actionAsync">The action to be executed repeatedly.</param>
    /// <param name="period">The time, in milliseconds, between the end of one execution and the start of the next.</param>
    /// <param name="start">If set to <c>true</c>, the recurring action starts immediately.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="actionAsync"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="period"/> is negative.</exception>
    public RecurringAction(Func<Task> actionAsync, int period, bool start = true)
    {
        _actionAsync = actionAsync ?? throw new ArgumentNullException(nameof(actionAsync));
        if (period < 0)
            throw new ArgumentOutOfRangeException(nameof(period), "Period must be non-negative.");

        _period = period;
        _timer = new Timer(async void (_) =>
        {
            try
            {
                await TimerCallbackAsync();
            }
            catch (Exception)
            {
                //ignore to prevent the process crash
            }
        }, null, Timeout.Infinite, Timeout.Infinite);

        if (start) Start();
    }

    public event Action<Exception>? OnActionFailed;

    /// <summary>
    /// Starts the recurring action.
    /// </summary>
    /// <exception cref="ObjectDisposedException">Thrown when the instance has already been disposed of.</exception>
    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed == 1, this);

        _timer.Change(0, Timeout.Infinite);
    }

    public void Stop()
    {
        if (_disposed == 1)
            return;

        try
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
        catch (ObjectDisposedException)
        {
            // Ignore if already disposed
        }
    }


    private async Task TimerCallbackAsync()
    {
#pragma warning disable CS0420 // Interlocked provides full memory barrier
        if (!LockUtils.TryClaimLock(ref _isRunning)) return;
#pragma warning restore CS0420

        try
        {
            await _actionAsync();
        }
        catch (Exception ex)
        {
            try
            {
                OnActionFailed?.Invoke(ex);
            }
            catch (Exception)
            {
                //ignore to prevent the process crash
            }
        }
        finally
        {
#pragma warning disable CS0420 // Interlocked provides full memory barrier
            LockUtils.ReleaseLock(ref _isRunning);
#pragma warning restore CS0420 
            
            if (_disposed == 0) //if not disposed
                try
                {
                    _timer.Change(_period, Timeout.Infinite); //reschedule itself
                }
                catch (ObjectDisposedException)
                {
                    // Timer already disposed
                    //https://learn.microsoft.com/en-us/dotnet/api/System.Threading.Timer.Dispose
                }
        }
    }

    public void Dispose()
    {
#pragma warning disable CS0420 // Interlocked provides full memory barrier
        if (!LockUtils.TryClaimLock(ref _disposed)) return;
#pragma warning restore CS0420

        _timer.Dispose();
    }
}