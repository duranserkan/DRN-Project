using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Numbers;
using DRN.Framework.Utils.Time;

namespace DRN.Test.Unit.Tests.Framework.Utils.Ids;

/// <summary>
/// Verifies the epoch addressability claims in paper/paper-peerj.md § Epoch and Extensibility (Table 6).
/// All values are absolute — computed from 2^30 and 2^31 seconds against the 2025-01-01 epoch.
/// </summary>
public class PaperEpochAddressabilityTests
{
    // Epoch configuration from EpochTimeUtils
    private static readonly DateTimeOffset Epoch2025 = EpochTimeUtils.Epoch2025; // 2025-01-01T00:00:00Z

    // Bit layout constants — actual SKID layout: 1 (sign) + 32 (timestamp) + 7 (appId) + 6 (instanceId) + 18 (sequence) = 64
    private const int TimestampBits = 32;
    private const int AppIdBits = 7;
    private const int InstanceIdBits = 6;
    private const int SequenceBits = 18;
    private const int TicksPerSecond = 4; // 250ms precision → 4 ticks per second

    // Derived tick boundaries
    private const long TicksPerHalf = 1L << TimestampBits;         // 2^32 ticks per half-epoch
    private const uint MaxTimestamp = (uint)(TicksPerHalf - 1);    // 2^32 - 1: max 32-bit timestamp value

    // Absolute duration constants (seconds)
    // 2^32 ticks / 4 ticks/s = 2^30 seconds per half-epoch; sign bit doubles to 2^31 seconds per epoch
    private const long SecondsPerHalf = (1L << TimestampBits) / TicksPerSecond;  // 2^30 = 1,073,741,824
    private const long SecondsPerEpoch = SecondsPerHalf * 2;                     // 2^31 = 2,147,483,648
    private const int TotalEpochs = 256; // 8-bit epoch index (SKEID byte 0)

    // Gregorian used by DateTimeOffset
    private const double SecondsPerGregorianYear = 365.2425 * 24 * 3600; // 31,556,952
    // Actual year use for long term epoch calculations
    private const double SecondsPerSunYear = 365.2422 * 24 * 3600; // 31,556,926

    // Absolute boundary years (computed from DateTimeOffset arithmetic)
    // Half: 2025-01-01 + 2^30 seconds = 2059-01-10 → year 2059
    // Epoch 0x00: 2025-01-01 to 2093-01-19 → years 2025 to 2093
    // Epoch 0x01: 2093-01-19 to 2161-02-07 → years 2093 to 2161
    // Epoch 0xFF: January 7, 19378 to January 26, 19446 (cannot represent in DateTimeOffset)
    private const int HalfEpochBoundaryYear = 2_059;
    private const int Epoch0StartYear = 2_025;
    private const int Epoch0EndYear = 2_093;
    private const int Epoch1EndYear = 2_161;
    private const int EpochFfStartYear = 19_378;
    private const int EpochFfEndYear = 19_446;
    private const int TotalCoverageYears = 17_421;

    [Fact]
    public void Half_Epoch_Should_End_In_Year_2059()
    {
        // 2^30 seconds from 2025-01-01T00:00:00Z
        var halfEnd = Epoch2025.AddSeconds(SecondsPerHalf);

        halfEnd.Year.Should().Be(HalfEpochBoundaryYear, $"2025-01-01 + 2^30 seconds ({SecondsPerHalf:N0}s) = {halfEnd:yyyy-MM-dd}");
    }

    [Fact]
    public void Epoch_0x00_Should_Span_2025_To_2093()
    {
        var epoch0Start = Epoch2025;
        var epoch0End = Epoch2025.AddSeconds(SecondsPerEpoch);

        epoch0Start.Year.Should().Be(2025);
        epoch0Start.Month.Should().Be(1);
        epoch0Start.Day.Should().Be(1);
        
        epoch0End.Year.Should().Be(Epoch0EndYear);
        epoch0End.Month.Should().Be(1);
        epoch0End.Day.Should().Be(19);
        
        epoch0Start.Should().Be(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Epoch_0x01_Should_Span_2093_To_2161()
    {
        var epoch1Start = Epoch2025.AddSeconds(SecondsPerEpoch);
        var epoch1End = Epoch2025.AddSeconds(SecondsPerEpoch * 2);
        
        epoch1Start.Year.Should().Be(Epoch0EndYear);
        epoch1Start.Month.Should().Be(1);
        epoch1Start.Day.Should().Be(19);
        
        epoch1End.Month.Should().Be(2);
        epoch1End.Day.Should().Be(7);
        epoch1End.Year.Should().Be(Epoch1EndYear);
    }

    [Fact]
    public void Epoch_0xFF_Should_Start_At_Year_19378_And_End_At_19446()
    {
        // DateTimeOffset cannot represent year 19,378 — compute via Gregorian year arithmetic
        var yearsPerEpoch = SecondsPerEpoch / SecondsPerSunYear;
        var epochFfStart = 2025 + 255 * yearsPerEpoch;
        var epochFfEnd = 2025 + 256 * yearsPerEpoch;
        var totalCoverage = epochFfEnd - 2025;
        
        ((int)epochFfStart).Should().Be(EpochFfStartYear,  "epoch 0xFF start year");
        ((int)epochFfEnd).Should().Be(EpochFfEndYear,  "epoch 0xFF end year");
        ((int)totalCoverage).Should().Be(TotalCoverageYears);
    }

    [Fact]
    public void Total_Coverage_Should_Be_Approximately_17421_Years()
    {
        var yearsPerEpoch = SecondsPerEpoch / SecondsPerGregorianYear;
        var totalYears = TotalEpochs * yearsPerEpoch;

        ((int)totalYears).Should().Be(TotalCoverageYears);
    }

    [Fact]
    public void Sign_Bit_Should_Preserve_Monotonic_Sort_Order()
    {
        // First half (sign=1): negative longs — covers seconds 0 to 2^30-1
        // Second half (sign=0): positive longs — covers the next 2^30 seconds
        var earlyTimestamp = 31_536_000u; // ~1 year from epoch

        var builderEarly = NumberBuilder.GetLong();
        builderEarly.SetResidueValue(earlyTimestamp);
        builderEarly.TryAdd(1u, AppIdBits);
        builderEarly.TryAdd(1u, InstanceIdBits);
        builderEarly.TryAdd(1u, SequenceBits);
        var earlySkid = builderEarly.GetValue();

        var builderLate = NumberBuilder.GetLong();
        builderLate.MakePositive();
        builderLate.SetResidueValue(earlyTimestamp);
        builderLate.TryAdd(1u, AppIdBits);
        builderLate.TryAdd(1u, InstanceIdBits);
        builderLate.TryAdd(1u, SequenceBits);
        var lateSkid = builderLate.GetValue();

        earlySkid.Should().BeNegative("first epoch half produces negative SKIDs");
        lateSkid.Should().BePositive("second epoch half produces positive SKIDs");
        earlySkid.Should().BeLessThan(lateSkid, "negative (first half) sorts before positive (second half) in signed comparison");
    }

    [Fact]
    public void Max_Timestamp_Should_Fit_In_32_Bits()
    {
        MaxTimestamp.Should().Be(4_294_967_295U);

        var builder = NumberBuilder.GetLong();
        builder.SetResidueValue(MaxTimestamp);
        var skid = builder.GetValue();

        var parser = NumberParser.Get(skid);
        var recovered = parser.ReadResidueValue();
        recovered.Should().Be(MaxTimestamp, "max 32-bit timestamp must round-trip through NumberBuilder/NumberParser");
    }

    [Fact]
    public void Max_Epoch_Ticks_Should_Be_2_33_Minus_1()
    {
        // Validates the guard constant used in SourceKnownIdUtils.Generate()
        var maxEpochSeconds = (SourceKnownIdUtils.MaxEpochTicks + 1) / TicksPerSecond; // 2^31 seconds

        SourceKnownIdUtils.MaxEpochTicks.Should().Be(8_589_934_591L, "2^33 - 1 covers the full ~68-year epoch");
        maxEpochSeconds.Should().Be(SecondsPerEpoch, "max epoch ticks / 4 ticks/s = 2^31 seconds = SecondsPerEpoch");
    }

    [Fact]
    public void Epoch_Half_Masking_Should_Produce_Correct_Sign_And_Monotonic_Order()
    {
        // Exercises the exact masking and sign-bit logic from SourceKnownIdUtils.Generate():
        //   isSecondHalf = elapsedTicks >= TicksPerHalf
        //   storedTimestamp = (uint)(elapsedTicks & MaxTimestamp)
        //   if (isSecondHalf) builder.MakePositive()

        const byte appId = 1;
        const byte appInstanceId = 1;
        const uint sequenceId = 1;

        // Boundary timestamps: first tick, last tick of first half, first tick of second half, last tick of second half
        long[] boundaryTicks = [0L, MaxTimestamp, TicksPerHalf, SourceKnownIdUtils.MaxEpochTicks];

        var skids = new long[boundaryTicks.Length];
        for (var i = 0; i < boundaryTicks.Length; i++)
        {
            var elapsedTicks = boundaryTicks[i];
            var isSecondHalf = elapsedTicks >= TicksPerHalf;
            var storedTimestamp = (uint)(elapsedTicks & MaxTimestamp);

            var builder = NumberBuilder.GetLong();
            builder.SetResidueValue(storedTimestamp);
            if (isSecondHalf)
                builder.MakePositive();
            builder.TryAdd(appId, AppIdBits);
            builder.TryAdd(appInstanceId, InstanceIdBits);
            builder.TryAdd(sequenceId, SequenceBits);
            skids[i] = builder.GetValue();
        }

        // First half SKIDs are negative
        skids[0].Should().BeNegative("tick 0 is in the first half → negative SKID");
        skids[1].Should().BeNegative("tick 2^32-1 is the last tick of the first half → negative SKID");

        // Second half SKIDs are positive
        skids[2].Should().BePositive("tick 2^32 is the first tick of the second half → positive SKID");
        skids[3].Should().BePositive("tick 2^33-1 is the last tick of the second half → positive SKID");

        // Monotonic sort order across all boundary timestamps
        for (var i = 1; i < skids.Length; i++)
            skids[i].Should().BeGreaterThan(skids[i - 1],  $"SKID at tick {boundaryTicks[i]} must sort after SKID at tick {boundaryTicks[i - 1]}");

        // Round-trip: stored timestamp must be recoverable from all SKIDs
        for (var i = 0; i < skids.Length; i++)
        {
            var parser = NumberParser.Get(skids[i]);
            _ = parser.Read(AppIdBits);
            _ = parser.Read(InstanceIdBits);
            _ = parser.Read(SequenceBits);
            var recoveredTimestamp = parser.ReadResidueValue();

            var expectedStoredTimestamp = (uint)(boundaryTicks[i] & MaxTimestamp);
            recoveredTimestamp.Should().Be(expectedStoredTimestamp, $"stored timestamp for tick {boundaryTicks[i]} must round-trip");
        }
    }
}
