using DRN.Framework.Utils.Numbers;
using DRN.Framework.Utils.Time;

namespace DRN.Test.Unit.Tests.Framework.Utils.Ids;

/// <summary>
/// Verifies the epoch addressability claims in paper/paper-peerj.md § Epoch and Extensibility (Table 4).
/// All values are absolute — computed from 2^30 and 2^31 seconds against the 2025-01-01 epoch.
/// </summary>
public class PaperEpochAddressabilityTests
{
    // Epoch configuration from EpochTimeUtils
    private static readonly DateTimeOffset Epoch2025 = EpochTimeUtils.Epoch2025; // 2025-01-01T00:00:00Z

    // Bit layout constants
    private const int TimestampBits = 30;
    private const int SignBitCount = 1;

    // Absolute duration constants (seconds)
    private const long SecondsPerHalf = 1L << TimestampBits; // 2^30 = 1,073,741,824
    private const long SecondsPerEpoch = 1L << (TimestampBits + SignBitCount); // 2^31 = 2,147,483,648
    private const int TotalEpochs = 256; // 8-bit epoch index (SKEID byte 5)

    // Absolute boundary years (computed from DateTimeOffset arithmetic)
    // Half: 2025-01-01 + 2^30 seconds = 2059-01-10 → year 2059
    // Epoch 0x00: 2025-01-01 to 2093-01-19 → years 2025 to 2093
    // Epoch 0x01: 2093-01-19 to 2161-02-07 → years 2093 to 2161
    // Epoch 0xFF: year 19,377 to year 19,445 (cannot represent in DateTimeOffset)
    // Total coverage: 17,420 years
    private const int HalfEpochBoundaryYear = 2059;
    private const int Epoch0EndYear = 2093;
    private const int Epoch1EndYear = 2161;
    private const int EpochFfStartYear = 19_377;
    private const int EpochFfEndYear = 19_445;
    private const int TotalCoverageYears = 17_420;

    // Julian year in seconds (IAU standard: exactly 365.25 days)
    private const double SecondsPerJulianYear = 365.25 * 24 * 3600; // 31,557,600

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
        epoch0End.Year.Should().Be(Epoch0EndYear, $"2025-01-01 + 2^31 seconds ({SecondsPerEpoch:N0}s) = {epoch0End:yyyy-MM-dd}");
        epoch0Start.Should().Be(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Epoch_0x01_Should_Span_2093_To_2161()
    {
        var epoch1Start = Epoch2025.AddSeconds(SecondsPerEpoch);
        var epoch1End = Epoch2025.AddSeconds(SecondsPerEpoch * 2);

        epoch1Start.Year.Should().Be(Epoch0EndYear);
        epoch1End.Year.Should().Be(Epoch1EndYear, $"2025-01-01 + 2 × 2^31 seconds = {epoch1End:yyyy-MM-dd}");
    }

    [Fact]
    public void Epoch_0xFF_Should_Start_At_Year_19377_And_End_At_19445()
    {
        // DateTimeOffset cannot represent year 19,377 — compute via Julian year arithmetic
        var yearsPerEpoch = SecondsPerEpoch / SecondsPerJulianYear;
        var epochFfStart = (int)(2025 + 255 * yearsPerEpoch);
        var epochFfEnd = (int)(2025 + 256 * yearsPerEpoch);

        epochFfStart.Should().Be(EpochFfStartYear, "epoch 0xFF start year");
        epochFfEnd.Should().Be(EpochFfEndYear, "epoch 0xFF end year");
    }

    [Fact]
    public void Total_Coverage_Should_Be_17420_Years()
    {
        var yearsPerEpoch = SecondsPerEpoch / SecondsPerJulianYear;
        var totalYears = (int)(TotalEpochs * yearsPerEpoch);

        totalYears.Should().Be(TotalCoverageYears,
            $"256 epochs × {yearsPerEpoch:F2} years/epoch = {TotalEpochs * yearsPerEpoch:F2} years");
    }

    [Fact]
    public void Sign_Bit_Should_Preserve_Monotonic_Sort_Order()
    {
        // First half (sign=1): negative longs — covers seconds 0 to 2^30-1
        // Second half (sign=0): positive longs — covers the next 2^30 seconds
        var earlyTimestamp = 31_536_000u; // ~1 year from epoch

        var builderEarly = NumberBuilder.GetLong();
        builderEarly.SetResidueValue(earlyTimestamp);
        builderEarly.TryAdd(1u, 7);
        builderEarly.TryAdd(1u, 6);
        builderEarly.TryAdd(1u, 20);
        var earlySkid = builderEarly.GetValue();

        var builderLate = NumberBuilder.GetLong();
        builderLate.MakePositive();
        builderLate.SetResidueValue(earlyTimestamp);
        builderLate.TryAdd(1u, 7);
        builderLate.TryAdd(1u, 6);
        builderLate.TryAdd(1u, 20);
        var lateSkid = builderLate.GetValue();

        earlySkid.Should().BeNegative("first epoch half produces negative SKIDs");
        lateSkid.Should().BePositive("second epoch half produces positive SKIDs");
        earlySkid.Should().BeLessThan(lateSkid,
            "negative (first half) sorts before positive (second half) in signed comparison");
    }

    [Fact]
    public void Max_Timestamp_Should_Fit_In_30_Bits()
    {
        var maxTimestamp = (uint)((1L << TimestampBits) - 1); // 1,073,741,823
        maxTimestamp.Should().Be(1_073_741_823u);

        var builder = NumberBuilder.GetLong();
        builder.SetResidueValue(maxTimestamp);
        var skid = builder.GetValue();

        var parser = NumberParser.Get(skid);
        var recovered = parser.ReadResidueValue();
        recovered.Should().Be(maxTimestamp,
            "max 30-bit timestamp must round-trip through NumberBuilder/NumberParser");
    }
}