using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Time;

namespace DRN.Test.Unit.Tests.Framework.Utils.Ids;

public class SourceKnownIdUtilsTests
{
    [Fact]
    public async Task SourceKnownIdUtils_Generate_Should_Generate_Valid_Id()
    {
        byte appId = 1;
        byte appInstanceId = 1;

        var epoch = EpochTimeUtils.Epoch2025;
        var beforeIdGenerated = DateTimeOffset.UtcNow;

        await Task.Delay(1200);
        var id = SourceKnownIdUtils.Generate<object>(appId, appInstanceId);
        await Task.Delay(1200);

        var afterIdGenerated = DateTimeOffset.UtcNow;
        var idInfo = SourceKnownIdUtils.ParseId(id, EpochTimeUtils.DefaultEpoch);

        idInfo.Id.Should().Be(id);
        idInfo.AppId.Should().Be(appId);
        idInfo.AppInstanceId.Should().Be(appInstanceId);

        epoch.Should().BeBefore(beforeIdGenerated);
        AssertCreatedAtWithinGeneratedRange(idInfo, beforeIdGenerated, afterIdGenerated, epoch);
    }

    [Theory]
    [DataInlineUnit]
    public async Task SourceKnownIdUtils_Should_Generate_Next_Valid_Id(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings
        {
            AppId = 5,
            AppInstanceId = 12
        };

        var customSettings = new
        {
            NexusAppSettings = nexusSettings
        };

        context.AddToConfiguration(customSettings);
        var generator = context.GetRequiredService<ISourceKnownIdUtils>();

        var epoch = EpochTimeUtils.Epoch2025;
        var beforeIdGenerated = DateTimeOffset.UtcNow;

        await Task.Delay(1100); // 100ms buffer added to compensate caching effect
        var id1 = generator.Next<SourceKnownIdUtilsTests>();
        await Task.Delay(1100);

        var afterIdGenerated = DateTimeOffset.UtcNow;

        id1.Should().BeNegative();
        epoch.Should().BeBefore(beforeIdGenerated);

        var idInfo1 = generator.Parse(id1);
        var idInfo1Duplicate = generator.Parse(id1);
        idInfo1.AppId.Should().Be(nexusSettings.AppId);
        idInfo1.AppInstanceId.Should().Be(nexusSettings.AppInstanceId);

        AssertCreatedAtWithinGeneratedRange(idInfo1, beforeIdGenerated, afterIdGenerated, epoch);

        var id2 = generator.Next<SourceKnownIdUtilsTests>();
        (id2 > id1).Should().BeTrue();

        var idInfo2 = generator.Parse(id2);
        var idInfo2Duplicate = generator.Parse(id2);
        (idInfo2 > idInfo1).Should().BeTrue();
        (idInfo2 >= idInfo1).Should().BeTrue();
        (idInfo2 >= idInfo2Duplicate).Should().BeTrue();
        (idInfo1 < idInfo2).Should().BeTrue();
        (idInfo1 <= idInfo2).Should().BeTrue();
        (idInfo1 <= idInfo1Duplicate).Should().BeTrue();
    }

    [Theory]
    [InlineData((byte)128, (byte)0)]
    [InlineData((byte)255, (byte)0)]
    public void Generate_With_AppId_Exceeding_MaxAppId_Should_Throw_ArgumentOutOfRangeException(byte appId, byte appInstanceId)
    {
        var act = () => SourceKnownIdUtils.Generate<object>(appId, appInstanceId);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData((byte)0, (byte)64)]
    [InlineData((byte)0, (byte)255)]
    public void Generate_With_AppInstanceId_Exceeding_MaxAppInstanceId_Should_Throw_ArgumentOutOfRangeException(byte appId, byte appInstanceId)
    {
        var act = () => SourceKnownIdUtils.Generate<object>(appId, appInstanceId);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [DataInlineUnit]
    public void Next_And_Parse_Should_Honor_Custom_Epoch(DrnTestContextUnit context)
    {
        var generator = context.GetRequiredService<ISourceKnownIdUtils>();
        var customEpoch = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var before = DateTimeOffset.UtcNow;
        var id = generator.Next<object>(appId: 10, appInstanceId: 5, epoch: customEpoch);
        var after = DateTimeOffset.UtcNow;
        var parsed = generator.Parse(id, epoch: customEpoch);

        parsed.Id.Should().Be(id);
        parsed.AppId.Should().Be(10);
        parsed.AppInstanceId.Should().Be(5);
        AssertCreatedAtWithinGeneratedRange(parsed, before, after, customEpoch);
    }

    [Theory]
    [DataInlineUnit((byte)128, (byte)1, "appId")]
    [DataInlineUnit((byte)1, (byte)64, "appInstanceId")]
    public void Constructor_With_Invalid_AppId_Or_AppInstanceId_In_Settings_Should_Throw(
        DrnTestContextUnit context,
        byte appId,
        byte appInstanceId,
        string paramName)
    {
        var invalidSettings = new
        {
            NexusAppSettings = new NexusAppSettings
            {
                AppId = appId,
                AppInstanceId = appInstanceId
            }
        };

        context.AddToConfiguration(invalidSettings);
        var act = () => context.GetRequiredService<ISourceKnownIdUtils>();
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName(paramName);
    }

    /// <summary>
    /// Validates that <paramref name="idInfo"/>.CreatedAt falls within the expected test execution range.
    /// Timestamps are converted to 250ms epoch ticks before assertion because TimeStampManager truncates
    /// timestamps to 250ms precision boundaries. Converting all bounds to ticks normalizes sub-second
    /// precision disparities and prevents race conditions with high-precision DateTimeOffset.UtcNow bounds.
    /// </summary>
    private static void AssertCreatedAtWithinGeneratedRange(
        SourceKnownId idInfo,
        DateTimeOffset beforeIdGenerated,
        DateTimeOffset afterIdGenerated,
        DateTimeOffset epoch)
    {
        var createdAtTimestamp = EpochTimeUtils.ConvertToTicks(idInfo.CreatedAt, epoch);
        createdAtTimestamp.Should().BeGreaterThanOrEqualTo(EpochTimeUtils.ConvertToTicks(beforeIdGenerated, epoch));
        createdAtTimestamp.Should().BeLessThanOrEqualTo(EpochTimeUtils.ConvertToTicks(afterIdGenerated, epoch));
    }
}
