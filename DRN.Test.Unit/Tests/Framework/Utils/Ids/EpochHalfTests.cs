using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Numbers;
using DRN.Framework.Utils.Time;

namespace DRN.Test.Unit.Tests.Framework.Utils.Ids;

/// <summary>
/// Verifies epoch-half masking and sign-bit logic in <see cref="EpochTimeUtils.ConvertToSourceKnownIdTimeStamp(DateTimeOffset, DateTimeOffset)"/>
/// and <see cref="SourceKnownIdUtils.ParseId"/>.
/// </summary>
public class EpochHalfTests
{
    private static readonly DateTimeOffset Epoch = EpochTimeUtils.Epoch2025;

    private const int AppIdBits = 7;
    private const int InstanceIdBits = 6;
    private const int SequenceBits = 18;
    // 250ms precision → 4 ticks per second
    private const int TicksPerSecond = 4;

    [Fact]
    public void ConvertToSourceKnownIdTimeStamp_First_Half_Should_Produce_Negative_Value()
    {
        // 1 year from epoch = 31,536,000 seconds → 126,144,000 ticks (well within first half)
        var dateTime = Epoch.AddSeconds(31_536_000);
        var result = EpochTimeUtils.ConvertToSourceKnownIdTimeStamp(dateTime, Epoch);

        result.Should().BeNegative("first epoch half (ticks < 2^32) must produce a negative timestamp value");
    }

    [Fact]
    public void ConvertToSourceKnownIdTimeStamp_Second_Half_Should_Produce_Positive_Value()
    {
        // One tick past the half boundary: TicksPerHalf + 1 → storedTimestamp = 1, sign bit cleared → positive
        // (At exactly TicksPerHalf, storedTimestamp wraps to 0, producing 0L which is non-negative but not positive)
        var secondsPastHalf = (SourceKnownIdUtils.TicksPerHalf + 1.0) / TicksPerSecond;
        var dateTime = Epoch.AddSeconds(secondsPastHalf);

        var result = EpochTimeUtils.ConvertToSourceKnownIdTimeStamp(dateTime, Epoch);

        result.Should().BePositive("second epoch half (ticks > 2^32) must produce a positive timestamp value");
    }

    [Fact]
    public void ConvertToSourceKnownIdTimeStamp_Half_Boundary_Produces_Monotonic_Order()
    {
        // Just before half boundary (last tick of first half)
        var justBeforeHalf = Epoch.AddSeconds((SourceKnownIdUtils.TicksPerHalf - 1.0) / TicksPerSecond);
        // Exactly at half boundary (first tick of second half)
        var atHalf = Epoch.AddSeconds((double)SourceKnownIdUtils.TicksPerHalf / TicksPerSecond);

        var beforeResult = EpochTimeUtils.ConvertToSourceKnownIdTimeStamp(justBeforeHalf, Epoch);
        var atResult = EpochTimeUtils.ConvertToSourceKnownIdTimeStamp(atHalf, Epoch);

        beforeResult.Should().BeNegative("last tick of first half → negative");
        // At exactly TicksPerHalf, storedTimestamp wraps to 0 and MakePositive clears the sign bit → result is 0L
        atResult.Should().BeGreaterThanOrEqualTo(0, "first tick of second half → non-negative (0 at exact boundary)");
        atResult.Should().BeGreaterThan(beforeResult, "second half must sort after first half for monotonic ordering");
    }

    [Fact]
    public void ConvertToSourceKnownIdTimeStamp_Epoch_Start_Should_Produce_Minimum_Negative()
    {
        // Tick 0 — epoch start
        var result = EpochTimeUtils.ConvertToSourceKnownIdTimeStamp(Epoch, Epoch);

        result.Should().BeNegative("tick 0 is first half → negative");
    }
    

    [Fact]
    public void ParseId_First_Half_Should_Recover_Correct_DateTime()
    {
        // 100 seconds from epoch → 400 ticks (first half)
        var expectedDateTime = Epoch.AddSeconds(100);
        var elapsedTicks = 400L; // 100 * 4

        var skid = BuildSkid(elapsedTicks, appId: 1, appInstanceId: 1, sequenceId: 1);
        var parsed = SourceKnownIdUtils.ParseId(skid, Epoch);

        parsed.AppId.Should().Be(1);
        parsed.AppInstanceId.Should().Be(1);
        parsed.InstanceId.Should().Be(1);
        // DateTime recovery: truncated to 250ms tick precision
        parsed.CreatedAt.Should().Be(expectedDateTime, "first-half datetime must round-trip correctly");
    }

    [Fact]
    public void ParseId_Second_Half_Should_Recover_Correct_DateTime()
    {
        // TicksPerHalf (2^32) ticks → exactly second half start
        var elapsedTicks = SourceKnownIdUtils.TicksPerHalf;
        var expectedSeconds = elapsedTicks / TicksPerSecond;
        var expectedDateTime = Epoch.AddSeconds(expectedSeconds);

        var skid = BuildSkid(elapsedTicks, appId: 5, appInstanceId: 3, sequenceId: 42);
        var parsed = SourceKnownIdUtils.ParseId(skid, Epoch);

        parsed.AppId.Should().Be(5);
        parsed.AppInstanceId.Should().Be(3);
        parsed.InstanceId.Should().Be(42u);
        parsed.CreatedAt.Should().Be(expectedDateTime, "second-half datetime must round-trip correctly");
    }

    [Fact]
    public void ParseId_Last_Tick_Of_First_Half_Should_Recover_Correctly()
    {
        // 2^32 - 1 ticks — last first-half tick
        var elapsedTicks = SourceKnownIdUtils.TicksPerHalf - 1;
        var expectedSeconds = elapsedTicks / TicksPerSecond;
        var expectedDateTime = Epoch.AddSeconds(expectedSeconds);
        // TicksPerSecond = 4, so 250ms remainder is floored
        var remainderTicks250ms = elapsedTicks % TicksPerSecond;
        expectedDateTime = expectedDateTime.Add(TimeSpan.FromMilliseconds(remainderTicks250ms * 250));

        var skid = BuildSkid(elapsedTicks, appId: 1, appInstanceId: 1, sequenceId: 0);
        var parsed = SourceKnownIdUtils.ParseId(skid, Epoch);

        skid.Should().BeNegative("last tick of first half → negative SKID");
        parsed.CreatedAt.Should().Be(expectedDateTime, "last first-half tick must round-trip correctly");
    }

    [Fact]
    public void ParseId_Last_Tick_Of_Second_Half_Should_Recover_Correctly()
    {
        // MaxEpochTicks (2^33 - 1) — last second-half tick
        var elapsedTicks = SourceKnownIdUtils.MaxEpochTicks;
        var expectedSeconds = elapsedTicks / TicksPerSecond;
        var remainderTicks250ms = elapsedTicks % TicksPerSecond;
        var expectedDateTime = Epoch.AddSeconds(expectedSeconds)
            .Add(TimeSpan.FromMilliseconds(remainderTicks250ms * 250));

        var skid = BuildSkid(elapsedTicks, appId: 127, appInstanceId: 63, sequenceId: 262_143);
        var parsed = SourceKnownIdUtils.ParseId(skid, Epoch);

        skid.Should().BePositive("last tick of second half → positive SKID");
        parsed.AppId.Should().Be(127);
        parsed.AppInstanceId.Should().Be(63);
        parsed.InstanceId.Should().Be(262_143u);
        parsed.CreatedAt.Should().Be(expectedDateTime, "last second-half tick must round-trip correctly");
    }

    [Fact]
    public void ParseId_All_Boundary_Ticks_Should_Preserve_Monotonic_DateTime_Order()
    {
        long[] boundaryTicks =
        [
            0L,                                     // first tick of first half
            SourceKnownIdUtils.TicksPerHalf - 1,    // last tick of first half
            SourceKnownIdUtils.TicksPerHalf,         // first tick of second half
            SourceKnownIdUtils.MaxEpochTicks          // last tick of second half
        ];

        var parsedDates = new DateTimeOffset[boundaryTicks.Length];
        for (var i = 0; i < boundaryTicks.Length; i++)
        {
            var skid = BuildSkid(boundaryTicks[i], appId: 1, appInstanceId: 1, sequenceId: 1);
            parsedDates[i] = SourceKnownIdUtils.ParseId(skid, Epoch).CreatedAt;
        }

        for (var i = 1; i < parsedDates.Length; i++)
            parsedDates[i].Should().BeAfter(parsedDates[i - 1],
                $"parsed datetime at tick {boundaryTicks[i]} must be after tick {boundaryTicks[i - 1]}");
    }
    
    /// <summary>
    /// Builds a SKID from raw elapsed ticks and topology fields, replicating the exact logic
    /// from <see cref="SourceKnownIdUtils.Generate{TEntity}"/>.
    /// </summary>
    private static long BuildSkid(long elapsedTicks, byte appId, byte appInstanceId, uint sequenceId)
    {
        var isSecondHalf = elapsedTicks >= SourceKnownIdUtils.TicksPerHalf;
        var storedTimestamp = (uint)(elapsedTicks & uint.MaxValue);

        var builder = NumberBuilder.GetLong();
        builder.SetResidueValue(storedTimestamp);
        if (isSecondHalf)
            builder.MakePositive();
        builder.TryAdd(appId, AppIdBits);
        builder.TryAdd(appInstanceId, InstanceIdBits);
        builder.TryAdd(sequenceId, SequenceBits);

        return builder.GetValue();
    }
}
