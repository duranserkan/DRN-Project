using DRN.Framework.Utils.Ids;

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
    private static readonly int MaxApplications = SourceKnownIdUtils.MaxAppId + 1; // 63 + 1 = 64
    private static readonly int MaxInstancesPerApp = SourceKnownIdUtils.MaxAppInstanceId + 1; // 31 + 1 = 32
    private static readonly long SequenceCapPerSecond = SequenceTimeScope.MaxValue + 1L; // 2,097,151 + 1 = 2,097,152
    private static readonly int TotalGenerators = MaxApplications * MaxInstancesPerApp; // 2,048
    private static readonly long SystemWideThroughput = SequenceCapPerSecond * TotalGenerators; // 4,294,967,296

    [Fact]
    public void Sequence_Field_Should_Cap_At_2097152_Per_Second_Per_Instance()
    {
        // Paper: "The 21-bit sequence field caps generation at 2,097,152 identifiers per second per instance."
        SequenceCapPerSecond.Should().Be(2_097_152,
            $"SequenceTimeScope.MaxValue ({SequenceTimeScope.MaxValue:N0}) + 1 = {SequenceCapPerSecond:N0} identifiers per second per instance");
    }

    [Fact]
    public void App_Id_Should_Support_64_Applications()
    {
        // Paper: "With 64 applications..."
        // Source: SourceKnownIdUtils.MaxAppId => 63 (0-based, so 64 values)
        MaxApplications.Should().Be(64,
            $"MaxAppId ({SourceKnownIdUtils.MaxAppId}) + 1 = {MaxApplications} applications");
    }

    [Fact]
    public void App_Instance_Id_Should_Support_32_Instances()
    {
        // Paper: "...and 32 instances per application..."
        // Source: SourceKnownIdUtils.MaxAppInstanceId => 31 (0-based, so 32 values)
        MaxInstancesPerApp.Should().Be(32,
            $"MaxAppInstanceId ({SourceKnownIdUtils.MaxAppInstanceId}) + 1 = {MaxInstancesPerApp} instances per application");
    }

    [Fact]
    public void Total_Generators_Should_Be_2048()
    {
        // Paper: "...(2,048 total generators)..."
        TotalGenerators.Should().Be(2_048,
            $"{MaxApplications} applications × {MaxInstancesPerApp} instances = {TotalGenerators:N0} generators");
    }

    [Fact]
    public void System_Wide_Throughput_Should_Be_Approximately_4_3_Billion()
    {
        // Paper: "...the theoretical maximum system-wide throughput is approximately 4.3 billion identifiers per second."
        // Exact: 2,097,152 × 2,048 = 4,294,967,296 (= 2^32)
        SystemWideThroughput.Should().Be(4_294_967_296L,
            $"{SequenceCapPerSecond:N0} × {TotalGenerators:N0} = {SystemWideThroughput:N0}");

        // Verify the "approximately 4.3 billion" claim
        var billions = SystemWideThroughput / 1_000_000_000.0;
        billions.Should().BeApproximately(4.3, 0.05,
            $"{SystemWideThroughput:N0} / 1B = {billions:F2} billion ≈ 4.3 billion");
    }

    [Fact]
    public void System_Wide_Throughput_Should_Equal_2_To_The_32()
    {
        // The exact product is 2^32 — a clean power of two
        var twoTo32 = 1L << 32;

        SystemWideThroughput.Should().Be(twoTo32,
            "system-wide throughput must equal 2^32");
    }
}
