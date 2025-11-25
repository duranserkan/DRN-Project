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
        idInfo.CreatedAt.Should().BeBefore(afterIdGenerated);
        idInfo.CreatedAt.Should().BeAfter(beforeIdGenerated);
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

        idInfo1.CreatedAt.Should().BeBefore(afterIdGenerated);
        idInfo1.CreatedAt.Should().BeAfter(beforeIdGenerated);

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
}