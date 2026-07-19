using DRN.Framework.Utils.Time;

namespace DRN.Test.Unit.Tests.Framework.Utils.Time;

public class RecurringActionTests
{
    [Fact]
    public async Task Stop_During_Callback_Should_Prevent_Rescheduling_Until_Restarted()
    {
        var firstInvocationStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondInvocationStopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationCount = 0;
        RecurringAction? action = null;

        action = new RecurringAction(() =>
        {
            var invocation = Interlocked.Increment(ref invocationCount);
            action!.Stop();

            if (invocation == 1)
                firstInvocationStopped.TrySetResult();
            else if (invocation == 2)
                secondInvocationStopped.TrySetResult();

            return Task.CompletedTask;
        }, period: 10, start: false);

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
