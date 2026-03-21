using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Time;

namespace DRN.Test.Unit.Tests.Framework.Utils.Ids;

/// <summary>
/// Verifies the throughput claims in paper/paper-peerj.md § Throughput Analysis and
/// § Interpretation of Results.
/// All capacities are derived from reference implementation constants:
/// <see cref="SourceKnownIdUtils.MaxAppId"/>, <see cref="SourceKnownIdUtils.MaxAppInstanceId"/>,
/// and <see cref="SequenceTimeScope.MaxValue"/>.
/// </summary>
public class PaperThroughputAnalysisTests
{
    // Capacities derived from source constants (0-based max → count)
    private static readonly int MaxApplications = SourceKnownIdUtils.MaxAppId + 1; // 127 + 1 = 128
    private static readonly int MaxInstancesPerApp = SourceKnownIdUtils.MaxAppInstanceId + 1; // 63 + 1 = 64
    private static readonly long SequenceCapPerTick = SequenceTimeScope.MaxValue + 1L; // 262,143 + 1 = 262,144
    private static readonly long SequenceCapPerSecond = SequenceCapPerTick * TimeStampManager.TicksPerSecondMultiplier; // 262,144 × 4 = 1,048,576
    private static readonly int TotalGenerators = MaxApplications * MaxInstancesPerApp; // 8,192
    private static readonly long SystemWideThroughput = SequenceCapPerSecond * TotalGenerators; // 8,589,934,592

    [Fact]
    public void Sequence_Field_Should_Cap_At_262144_Per_Tick_Per_Instance()
    {
        // 18-bit sequence caps at 262,144 per 250ms tick per instance.
        SequenceCapPerTick.Should().Be(262_144,
            $"SequenceTimeScope.MaxValue ({SequenceTimeScope.MaxValue:N0}) + 1 = {SequenceCapPerTick:N0} identifiers per 250ms tick per instance");
    }

    [Fact]
    public void Per_Second_Throughput_Should_Be_1048576()
    {
        // 262,144 per tick × 4 ticks/s = 1,048,576 per second per instance.
        SequenceCapPerSecond.Should().Be(1_048_576,
            $"{SequenceCapPerTick:N0} × {TimeStampManager.TicksPerSecondMultiplier} = {SequenceCapPerSecond:N0} identifiers per second per instance");
    }

    [Fact]
    public void App_Id_Should_Support_128_Applications()
    {
        // Paper: "With 128 applications..."
        // Source: SourceKnownIdUtils.MaxAppId => 127 (0-based, so 128 values)
        MaxApplications.Should().Be(128,
            $"MaxAppId ({SourceKnownIdUtils.MaxAppId}) + 1 = {MaxApplications} applications");
    }

    [Fact]
    public void App_Instance_Id_Should_Support_64_Instances()
    {
        // Paper: "...and 64 instances per application..."
        // Source: SourceKnownIdUtils.MaxAppInstanceId => 63 (0-based, so 64 values)
        MaxInstancesPerApp.Should().Be(64,
            $"MaxAppInstanceId ({SourceKnownIdUtils.MaxAppInstanceId}) + 1 = {MaxInstancesPerApp} instances per application");
    }

    [Fact]
    public void Total_Generators_Should_Be_8192()
    {
        // Paper: "...(8,192 total generators)..."
        TotalGenerators.Should().Be(8_192,
            $"{MaxApplications} applications × {MaxInstancesPerApp} instances = {TotalGenerators:N0} generators");
    }

    [Fact]
    public void System_Wide_Throughput_Should_Be_Approximately_8_6_Billion()
    {
        // Paper: "...the theoretical maximum system-wide throughput is approximately 8.6 billion identifiers per second."
        // Exact: 1,048,576 × 8,192 = 8,589,934,592 (= 2^33)
        SystemWideThroughput.Should().Be(8_589_934_592L,
            $"{SequenceCapPerSecond:N0} × {TotalGenerators:N0} = {SystemWideThroughput:N0}");

        // Verify the "approximately 8.6 billion" claim
        var billions = SystemWideThroughput / 1_000_000_000.0;
        billions.Should().BeApproximately(8.6, 0.05,
            $"{SystemWideThroughput:N0} / 1B = {billions:F2} billion ≈ 8.6 billion");
    }

    [Fact]
    public void System_Wide_Throughput_Should_Equal_2_To_The_33()
    {
        // The exact product is 2^33 — a clean power of two
        var twoTo33 = 1L << 33;

        SystemWideThroughput.Should().Be(twoTo33,
            "system-wide throughput must equal 2^33");
    }
}
