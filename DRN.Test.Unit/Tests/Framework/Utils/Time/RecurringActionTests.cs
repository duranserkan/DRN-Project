using DRN.Framework.Utils.Time;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DRN.Test.Unit.Tests.Framework.Utils.Time;

public class RecurringActionTests
{
    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public void Dedicated_Thread_Should_Reject_Zero_Period_Before_Starting(bool start)
    {
        Action create = () =>
        {
            using var worker = new RecurringAction(() => { }, 0, start, threadName: "DRN.Test.ZeroPeriod");
        };

        create.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("period");
    }

    [Fact]
    public void Timer_Should_Accept_Zero_Period()
    {
        using var worker = new RecurringAction(() => { }, 0, start: false);
    }

    [Fact]
    public async Task Synchronous_Callback_Should_Run_On_The_Timer()
    {
        var invoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var action = new RecurringAction(() =>
        {
            invoked.TrySetResult();
        }, 1000, false);

        action.Start();
        await invoked.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_wakeHandle")]
    private static extern ref AutoResetEvent? GetWakeHandle(RecurringAction action);

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public async Task Callback_And_Listener_Failures_Should_Not_Stop_Recurring_Execution(bool dedicated)
    {
        var reported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var resumed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocations = 0;
        RecurringAction? action = null;
        action = new RecurringAction(() =>
        {
            if (Interlocked.Increment(ref invocations) == 1)
                throw new InvalidOperationException("Callback failed.");
            action!.Stop();
            resumed.TrySetResult();
        }, 10, start: false, threadName: dedicated ? "DRN.Test.FailureRecovery" : null);
        using (action)
        {
            action.OnActionFailed += exception =>
            {
                reported.TrySetResult(exception);
                throw new InvalidOperationException("Listener failed.");
            };
            action.Start();
            var failure = await reported.Task.WaitAsync(TimeSpan.FromSeconds(5));
            failure.Should().BeOfType<InvalidOperationException>().Which.Message.Should().Be("Callback failed.");
            await resumed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Volatile.Read(ref invocations).Should().Be(2);
        }
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public async Task Dedicated_Restart_Should_Preserve_PostCallback_Delay(bool restartDuringCallback)
    {
        const int period = 250;
        var entered = new TaskCompletionSource<Thread>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        var nextInvocation = new TaskCompletionSource<TimeSpan>(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationCount = 0;
        long firstCallbackFinished = 0;
        RecurringAction? action = null;

        void Callback()
        {
            if (Interlocked.Increment(ref invocationCount) == 1)
            {
                entered.TrySetResult(Thread.CurrentThread);
                if (!release.Wait(TimeSpan.FromSeconds(5)))
                    throw new TimeoutException("Callback was not released.");
                Volatile.Write(ref firstCallbackFinished, Stopwatch.GetTimestamp());
            }
            else
            {
                var delay = Stopwatch.GetElapsedTime(Volatile.Read(ref firstCallbackFinished));
                action!.Stop();
                nextInvocation.TrySetResult(delay);
            }
        }

        action = new RecurringAction(Callback, period, start: false, threadName: "DRN.Test.SyncRestartDelay");

        Thread? worker = null;
        try
        {
            // A stop before the first start leaves the same pending wake signal as a stopped callback.
            if (!restartDuringCallback)
                action.Stop();

            action.Start();
            worker = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (restartDuringCallback)
            {
                action.Stop();
                action.Start();
            }

            release.Set();
            var delay = await nextInvocation.Task.WaitAsync(TimeSpan.FromSeconds(5));
            // Allow timer resolution while rejecting an immediately consumed stale wake signal.
            delay.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(period - 20));
            Volatile.Read(ref invocationCount).Should().Be(2);
        }
        finally
        {
            action.Dispose();
            release.Set();
            if (worker != null)
                (await Task.Run(() => worker.Join(TimeSpan.FromSeconds(5)))).Should().BeTrue();
        }
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public async Task Dedicated_Handles_Should_Remain_Alive_Until_Callback_Exits(bool disposeFromCallback)
    {
        var entered = new TaskCompletionSource<Thread>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        RecurringAction? action = null;
        action = new RecurringAction(() =>
        {
            if (disposeFromCallback)
                action!.Dispose();
            entered.TrySetResult(Thread.CurrentThread);
            release.Wait(TimeSpan.FromSeconds(5));
        }, period: 10, start: false, threadName: "DRN.Test.HandleLifetime");

        var wakeHandle = GetWakeHandle(action)!.SafeWaitHandle;
        Thread? worker = null;
        try
        {
            action.Start();
            worker = await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            action.Dispose();
            wakeHandle.IsClosed.Should().BeFalse("the worker still owns an active callback");
        }
        finally
        {
            release.Set();
            action.Dispose();
            if (worker != null)
                (await Task.Run(() => worker.Join(TimeSpan.FromSeconds(5)))).Should().BeTrue();
        }

        wakeHandle.IsClosed.Should().BeTrue("the exiting worker must release its handles");
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public async Task Stop_During_Callback_Should_Prevent_Rescheduling_Until_Restarted(bool dedicated)
    {
        var firstInvocationStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondInvocationStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationCount = 0;
        RecurringAction? action = null;

        void Callback()
        {
            var invocation = Interlocked.Increment(ref invocationCount);
            action!.Stop();

            if (invocation == 1)
                firstInvocationStopped.TrySetResult();
            else if (invocation == 2)
                secondInvocationStopped.TrySetResult();
        }

        var threadName = dedicated ? "DRN.Test.StopRestart" : null;
        action = new RecurringAction(Callback, period: 10, start: false, threadName: threadName);

        using (action)
        {
            action.Start();
            await firstInvocationStopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(250);

            Volatile.Read(ref invocationCount).Should().Be(1);

            action.Start();
            await secondInvocationStopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(250);

            Volatile.Read(ref invocationCount).Should().Be(2);
        }
    }
}
