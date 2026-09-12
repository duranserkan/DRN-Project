using System.Diagnostics.CodeAnalysis;
using DRN.Framework.Utils.Time;

namespace DRN.Test.Unit.Tests.Framework.Utils.Time;

[SuppressMessage("ReSharper", "AccessToModifiedClosure")]
public class RecurringActionAsyncTests
{
    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public async Task Every_Async_Disposal_Should_Join_The_Active_Callback(bool stopFirst)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cancellationObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocations = 0;
        var cancellations = 0;
        var worker = new RecurringActionAsync(async ct =>
        {
            await using var registration = ct.Register(() =>
            {
                Interlocked.Increment(ref cancellations);
                cancellationObserved.TrySetResult();
            });
            Interlocked.Increment(ref invocations);
            started.TrySetResult();
            await release.Task;
        }, 10);

        Task? first = null;
        Task? second = null;
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (stopFirst)
                worker.Stop();
            first = worker.DisposeAsync().AsTask();
            second = worker.DisposeAsync().AsTask();
            await cancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(5));
            first.IsCompleted.Should().BeFalse("the callback has not finished");
            second.IsCompleted.Should().BeFalse("every async disposer must await the callback");
            Volatile.Read(ref cancellations).Should().Be(1);
        }
        finally
        {
            release.TrySetResult();
            await Task.WhenAll(first ?? worker.DisposeAsync().AsTask(), second ?? Task.CompletedTask)
                .WaitAsync(TimeSpan.FromSeconds(5));
        }

        Volatile.Read(ref invocations).Should().Be(1);
        await worker.DisposeAsync();
        Volatile.Read(ref cancellations).Should().Be(1);
        var restart = worker.Start;
        restart.Should().Throw<ObjectDisposedException>();
    }

    [Theory]
    [DataInlineUnit(-1L)]
    [DataInlineUnit(0L)]
    [DataInlineUnit(uint.MaxValue * TimeSpan.TicksPerMillisecond)]
    [DataInlineUnit(long.MaxValue)]
    public async Task Unsupported_Timeouts_Should_Throw_Before_Scheduling(long ticks)
    {
        foreach (var start in new[] { false, true })
        foreach (var milliseconds in new[] { false, true })
        {
            var invoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Func<CancellationToken, Task> callback = _ =>
            {
                invoked.TrySetResult();
                return Task.CompletedTask;
            };
            RecurringActionAsync? worker = null;
            Action create = () => worker = milliseconds
                ? new RecurringActionAsync(callback, 10, start, TimeSpan.FromTicks(ticks))
                : new RecurringActionAsync(callback, TimeSpan.FromMilliseconds(10), start, TimeSpan.FromTicks(ticks));
            try
            {
                create.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("executionTimeout");
                invoked.Task.IsCompleted.Should().BeFalse();
            }
            finally
            {
                if (worker != null) await worker.DisposeAsync();
            }
        }
    }

    [Theory]
    [DataInlineUnit(1L)]
    [DataInlineUnit(((long)uint.MaxValue - 1) * TimeSpan.TicksPerMillisecond)]
    [DataInlineUnit(uint.MaxValue * TimeSpan.TicksPerMillisecond - 1)]
    public async Task Supported_Timeout_Boundaries_Should_Reach_Callback(long ticks)
    {
        foreach (var milliseconds in new[] { false, true })
        {
            var invoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Func<CancellationToken, Task> callback = _ =>
            {
                invoked.TrySetResult();
                return Task.CompletedTask;
            };
            await using var worker = milliseconds
                ? new RecurringActionAsync(callback, 10, start: false, executionTimeout: TimeSpan.FromTicks(ticks))
                : new RecurringActionAsync(callback, TimeSpan.FromMilliseconds(10), start: false, executionTimeout: TimeSpan.FromTicks(ticks));
            worker.Start();
            await invoked.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Theory]
    [DataInlineUnit(-TimeSpan.TicksPerMillisecond)]
    [DataInlineUnit(0L)]
    [DataInlineUnit(TimeSpan.TicksPerMillisecond / 2)]
    [DataInlineUnit(TimeSpan.TicksPerMillisecond - 1)]
    [DataInlineUnit(uint.MaxValue * TimeSpan.TicksPerMillisecond)]
    [DataInlineUnit(long.MaxValue)]
    public async Task Unsupported_Periods_Should_Throw_Synchronously_For_Both_Callback_Forms(long ticks)
    {
        var period = TimeSpan.FromTicks(ticks);
        foreach (var start in new[] { false, true })
        foreach (var tokenAware in new[] { false, true })
        {
            RecurringActionAsync? worker = null;
            Action create = () => worker = tokenAware
                ? new RecurringActionAsync(_ => Task.CompletedTask, period, start)
                : new RecurringActionAsync(() => Task.CompletedTask, period, start);
            try
            {
                create.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("period");
            }
            finally
            {
                if (worker != null) await worker.DisposeAsync();
            }
        }
    }

    [Theory]
    [DataInlineUnit(TimeSpan.TicksPerMillisecond)]
    [DataInlineUnit(((long)uint.MaxValue - 1) * TimeSpan.TicksPerMillisecond)]
    [DataInlineUnit(uint.MaxValue * TimeSpan.TicksPerMillisecond - 1)]
    public async Task Supported_Period_Boundaries_Should_Execute_For_Both_Callback_Forms(long ticks)
    {
        var period = TimeSpan.FromTicks(ticks);
        foreach (var tokenAware in new[] { false, true })
        {
            var invoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            Task Callback()
            {
                invoked.TrySetResult();
                return Task.CompletedTask;
            }

            await using var worker = tokenAware
                ? new RecurringActionAsync(_ => Callback(), period, start: false)
                : new RecurringActionAsync((Func<Task>)Callback, period, start: false);

            worker.Start();
            await invoked.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public async Task Tokenless_Constructors_Should_Validate_Before_Starting_And_Execute_Without_Timeout(bool milliseconds)
    {
        var invoked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Func<Task> callback = () =>
        {
            invoked.TrySetResult();
            return Task.CompletedTask;
        };

        RecurringActionAsync Create(Func<Task> action) => milliseconds
            ? new RecurringActionAsync(action, 10)
            : new RecurringActionAsync(action, TimeSpan.FromMilliseconds(10));

        var invalidCallback = () => Create(null!);
        invalidCallback.Should().Throw<ArgumentNullException>().WithParameterName("actionAsync");
        invoked.Task.IsCompleted.Should().BeFalse();

        await using var worker = Create(callback);
        await invoked.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [DataInlineUnit("running")]
    [DataInlineUnit("stopped")]
    [DataInlineUnit("restarted")]
    public async Task DisposeAsync_Should_Cancel_And_Await_Callback_Across_Lifecycle_States(string state)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finished = false;
        var invocations = 0;
        var callbackToken = CancellationToken.None;
        var action = new RecurringActionAsync(async ct =>
        {
            Interlocked.Increment(ref invocations);
            callbackToken = ct;
            started.TrySetResult();
            // Model asynchronous cleanup which must finish even after cancellation.
            await release.Task;
            finished = true;
        }, periodMilliseconds: 10);

        Task? disposal = null;
        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5), callbackToken);
            if (state != "running")
                action.Stop();
            if (state == "restarted")
            {
                action.Start();
                action.Stop();
                action.Start();
            }

            disposal = action.DisposeAsync().AsTask();
            callbackToken.IsCancellationRequested.Should().BeTrue();
            disposal.IsCompleted.Should().BeFalse("the original callback has not finished");
            Volatile.Read(ref invocations).Should().Be(1);
        }
        finally
        {
            release.TrySetResult();
            await (disposal ?? action.DisposeAsync().AsTask()).WaitAsync(TimeSpan.FromSeconds(5));
        }

        finished.Should().BeTrue();
    }

    [Fact]
    public async Task Stop_Should_Halt_Rescheduling_Until_Restarted()
    {
        var firstInvocationStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondInvocationStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationCount = 0;
        RecurringActionAsync? action = null;

        action = new RecurringActionAsync(async _ =>
        {
            var invocation = Interlocked.Increment(ref invocationCount);
            action!.Stop();

            if (invocation == 1)
                firstInvocationStopped.TrySetResult();
            else if (invocation == 2)
                secondInvocationStopped.TrySetResult();

            await Task.Yield();
        }, periodMilliseconds: 10, start: false);

        await using (action)
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

    [Fact]
    public async Task OnActionFailed_Should_Be_Raised_On_Exception()
    {
        var exceptionCaptured = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);

        await using var action = new RecurringActionAsync(_ => throw new InvalidOperationException("Test async error"), periodMilliseconds: 10, start: false);

        action.OnActionFailed += ex => exceptionCaptured.TrySetResult(ex);
        action.Start();

        var captured = await exceptionCaptured.Task.WaitAsync(TimeSpan.FromSeconds(5));
        captured.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Be("Test async error");
    }

    [Fact]
    public async Task ExecutionTimeout_Should_Cancel_Long_Running_Action_And_Raise_TimeoutException()
    {
        var timeoutCaptured = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondTickExecuted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tickCount = 0;

        await using var action = new RecurringActionAsync(async ct =>
        {
            var tick = Interlocked.Increment(ref tickCount);
            if (tick == 1)
            {
                // Delay longer than executionTimeout so cancellation is triggered
                await Task.Delay(500, ct);
            }
            else if (tick == 2)
            {
                secondTickExecuted.TrySetResult();
            }
        }, periodMilliseconds: 20, start: false, executionTimeout: TimeSpan.FromMilliseconds(50));

        action.OnActionFailed += ex =>
        {
            if (ex is TimeoutException)
                timeoutCaptured.TrySetResult(ex);
        };

        action.Start();

        var captured = await timeoutCaptured.Task.WaitAsync(TimeSpan.FromSeconds(5));
        captured.Should().BeOfType<TimeoutException>()
            .Which.Message.Should().Contain("RecurringActionAsync execution exceeded the configured timeout");

        await secondTickExecuted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Volatile.Read(ref tickCount).Should().BeGreaterThanOrEqualTo(2);
    }
}
