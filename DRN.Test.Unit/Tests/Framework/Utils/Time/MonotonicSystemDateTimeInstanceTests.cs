using DRN.Framework.Utils.Time;
using NSubstitute.Extensions;
using Xunit.Abstractions;

namespace DRN.Test.Unit.Tests.Framework.Utils.Time;

public class MonotonicDateTimeProviderInstanceTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInlineUnit(-2)]
    [DataInlineUnit(2)]
    public void ISystemDateTime_Should_Return_Disrupted_When_Enabled(int disruption, ISystemDateTimeProvider system)
    {
        var disrupt = false;
        var disruptedDateTime = DateTimeOffset.UtcNow;
        system.ReturnsForAll(callInfo => disrupt ? disruptedDateTime : DateTimeOffset.UtcNow);

        var now = system.UtcNow;
        now.Should().NotBe(disruptedDateTime);

        disrupt = true;
        now = system.UtcNow;
        now.Should().Be(disruptedDateTime);

        disruptedDateTime = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(disruption);
        now = system.UtcNow;
        now.Should().Be(disruptedDateTime);

        disrupt = false;
        now = system.UtcNow;
        now.Should().NotBe(disruptedDateTime);
    }

    [Theory]
    [DataInlineUnit(2)]
    [DataInlineUnit(-2)]
    public async Task MonotonicSystemDateTimeInstance_Should_Set_Shutdown_Requested_True(int disruption, ISystemDateTimeProvider system)
    {
        var disrupt = false;
        var disruptedDateTime = DateTimeOffset.UtcNow;
        system.ReturnsForAll(callInfo => disrupt ? disruptedDateTime : DateTimeOffset.UtcNow);

        var correctedDrifts = new List<DriftInfo>();
        var checkedDrifts = new List<DriftInfo>();
        var instance = new MonotonicDateTimeProviderInstance(system, 1);
        instance.OnDriftChecked += driftInfo => checkedDrifts.Add(driftInfo);
        instance.OnDriftCorrected += driftInfo => correctedDrifts.Add(driftInfo);

        await WaitUntilCycleChange(checkedDrifts);

        correctedDrifts.Count.Should().Be(0);
        instance.IsShutdownRequested.Should().BeFalse();

        disruptedDateTime = DateTimeOffset.UtcNow + TimeSpan.FromMinutes(disruption);
        disrupt = true;

        await WaitUntilCycleChange(checkedDrifts);

        instance.IsShutdownRequested.Should().BeTrue();
        correctedDrifts.Count.Should().Be(0);
    }

    [Theory]
    [DataInlineUnit(2)]
    [DataInlineUnit(-2)]
    public async Task MonotonicSystemDateTimeInstance_Should_Trigger_OnDriftCorrected(int disruption, ISystemDateTimeProvider system)
    {
        var disrupt = false;
        var disruptedDateTime = DateTimeOffset.UtcNow;
        system.ReturnsForAll(callInfo => disrupt ? disruptedDateTime : DateTimeOffset.UtcNow);

        var correctedDrifts = new List<DriftInfo>();
        var checkedDrifts = new List<DriftInfo>();
        var instance = new MonotonicDateTimeProviderInstance(system, 1);
        instance.OnDriftChecked += driftInfo => checkedDrifts.Add(driftInfo);
        instance.OnDriftCorrected += driftInfo => correctedDrifts.Add(driftInfo);

        await WaitUntilCycleChange(checkedDrifts);
        correctedDrifts.Count.Should().Be(0);

        disruptedDateTime = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(disruption);
        disrupt = true;

        await WaitUntilCycleChange(checkedDrifts);
        correctedDrifts.Count.Should().BeGreaterThan(0);

        disrupt = false;
        await WaitUntilCycleChange(checkedDrifts);
        correctedDrifts.Clear();

        for (var i = 0; i < 10; i++)
            await WaitUntilCycleChange(checkedDrifts);

        correctedDrifts.Count.Should().Be(0);
    }

    private static async Task WaitUntilCycleChange(List<DriftInfo> checkedDrifts)
    {
        var checkCount = checkedDrifts.Count;
        while (checkedDrifts.Count == checkCount)
            await Task.Delay(1);
    }
}