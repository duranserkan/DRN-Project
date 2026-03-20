using DRN.Framework.Utils.Numbers;
using DRN.Framework.Utils.Time;

namespace DRN.Test.Unit.Tests.Framework.Utils.Ids;

/// <summary>
/// Verifies the epoch addressability claims in paper/paper-peerj.md § Epoch and Extensibility (Table 4).
/// All values are absolute — computed from 2^31 and 2^32 seconds against the 2025-01-01 epoch.
/// </summary>
public class PaperEpochAddressabilityTests
{
    // Epoch configuration from EpochTimeUtils
    private static readonly DateTimeOffset Epoch2025 = EpochTimeUtils.Epoch2025; // 2025-01-01T00:00:00Z

    // Bit layout constants
    private const int TimestampBits = 31;
    private const int SignBitCount = 1;

    // Absolute duration constants (seconds)
    private const long SecondsPerHalf = 1L << TimestampBits; // 2^31 = 2,147,483,648
    private const long SecondsPerEpoch = 1L << (TimestampBits + SignBitCount); // 2^32 = 4,294,967,296
    private const int TotalEpochs = 256; // 8-bit epoch index (SKEID byte 5)

    // Absolute boundary years (computed from DateTimeOffset arithmetic)
    // Half: 2025-01-01 + 2^31 seconds = 2093-01-19 → year 2093
    // Epoch 0x00: 2025-01-01 to 2161-02-07 → years 2025 to 2161
    // Epoch 0x01: 2161-02-07 to 2297-03-16 → years 2161 to 2297
    // Epoch 0xFF: year 36,730 to year 36,866 (cannot represent in DateTimeOffset)
    // Total coverage: 34,841 years
    private const int HalfEpochBoundaryYear = 2093;
    private const int Epoch0EndYear = 2161;
    private const int Epoch1EndYear = 2297;
    private const int EpochFfStartYear = 36_730;
    private const int EpochFfEndYear = 36_866;
    private const int TotalCoverageYears = 34_841;

    // Julian year in seconds (IAU standard: exactly 365.25 days)
    private const double SecondsPerJulianYear = 365.25 * 24 * 3600; // 31,557,600

    [Fact]
    public void Half_Epoch_Should_End_In_Year_2093()
    {
        // 2^31 seconds from 2025-01-01T00:00:00Z
        var halfEnd = Epoch2025.AddSeconds(SecondsPerHalf);

        halfEnd.Year.Should().Be(HalfEpochBoundaryYear, $"2025-01-01 + 2^31 seconds ({SecondsPerHalf:N0}s) = {halfEnd:yyyy-MM-dd}");
    }

    [Fact]
    public void Epoch_0x00_Should_Span_2025_To_2161()
    {
        var epoch0Start = Epoch2025;
        var epoch0End = Epoch2025.AddSeconds(SecondsPerEpoch);

        epoch0Start.Year.Should().Be(2025);
        epoch0End.Year.Should().Be(Epoch0EndYear, $"2025-01-01 + 2^32 seconds ({SecondsPerEpoch:N0}s) = {epoch0End:yyyy-MM-dd}");
        epoch0Start.Should().Be(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Epoch_0x01_Should_Span_2161_To_2297()
    {
        var epoch1Start = Epoch2025.AddSeconds(SecondsPerEpoch);
        var epoch1End = Epoch2025.AddSeconds(SecondsPerEpoch * 2);

        epoch1Start.Year.Should().Be(Epoch0EndYear);
        epoch1End.Year.Should().Be(Epoch1EndYear, $"2025-01-01 + 2 × 2^32 seconds = {epoch1End:yyyy-MM-dd}");
    }

    [Fact]
    public void Epoch_0xFF_Should_Start_At_Year_36730_And_End_At_36866()
    {
        // DateTimeOffset cannot represent year 36,730 — compute via Julian year arithmetic
        var yearsPerEpoch = SecondsPerEpoch / SecondsPerJulianYear;
        var epochFfStart = (int)(2025 + 255 * yearsPerEpoch);
        var epochFfEnd = (int)(2025 + 256 * yearsPerEpoch);

        epochFfStart.Should().Be(EpochFfStartYear, "epoch 0xFF start year");
        epochFfEnd.Should().Be(EpochFfEndYear, "epoch 0xFF end year");
    }

    [Fact]
    public void Total_Coverage_Should_Be_34841_Years()
    {
        var yearsPerEpoch = SecondsPerEpoch / SecondsPerJulianYear;
        var totalYears = (int)(TotalEpochs * yearsPerEpoch);

        totalYears.Should().Be(TotalCoverageYears,
            $"256 epochs × {yearsPerEpoch:F2} years/epoch = {TotalEpochs * yearsPerEpoch:F2} years");
    }

    [Fact]
    public void Sign_Bit_Should_Preserve_Monotonic_Sort_Order()
    {
        // First half (sign=1): negative longs — covers seconds 0 to 2^31-1
        // Second half (sign=0): positive longs — covers the next 2^31 seconds
        var earlyTimestamp = 31_536_000u; // ~1 year from epoch

        var builderEarly = NumberBuilder.GetLong();
        builderEarly.SetResidueValue(earlyTimestamp);
        builderEarly.TryAdd(1u, 6);
        builderEarly.TryAdd(1u, 5);
        builderEarly.TryAdd(1u, 21);
        var earlySkid = builderEarly.GetValue();

        var builderLate = NumberBuilder.GetLong();
        builderLate.MakePositive();
        builderLate.SetResidueValue(earlyTimestamp);
        builderLate.TryAdd(1u, 6);
        builderLate.TryAdd(1u, 5);
        builderLate.TryAdd(1u, 21);
        var lateSkid = builderLate.GetValue();

        earlySkid.Should().BeNegative("first epoch half produces negative SKIDs");
        lateSkid.Should().BePositive("second epoch half produces positive SKIDs");
        earlySkid.Should().BeLessThan(lateSkid,
            "negative (first half) sorts before positive (second half) in signed comparison");
    }

    [Fact]
    public void Max_Timestamp_Should_Fit_In_31_Bits()
    {
        var maxTimestamp = (uint)((1L << TimestampBits) - 1); // 2,147,483,647
        maxTimestamp.Should().Be(2_147_483_647u);

        var builder = NumberBuilder.GetLong();
        builder.SetResidueValue(maxTimestamp);
        var skid = builder.GetValue();

        var parser = NumberParser.Get(skid);
        var recovered = parser.ReadResidueValue();
        recovered.Should().Be(maxTimestamp,
            "max 31-bit timestamp must round-trip through NumberBuilder/NumberParser");
    }
}