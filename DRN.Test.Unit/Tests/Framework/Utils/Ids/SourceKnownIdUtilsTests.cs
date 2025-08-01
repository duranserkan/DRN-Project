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
    public async Task SourceKnownIdUtils_Should_Generate_Next_Valid_Id(UnitTestContext context)
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

    [Theory]
    [DataInlineUnit]
    public async Task SourceKnownIdUtils_Should_Generate_Ids_For_3_Seconds(UnitTestContext context)
    {
        var nexusSettings = new NexusAppSettings
        {
            AppId = 7,
            AppInstanceId = 24
        };

        var customSettings = new
        {
            NexusAppSettings = nexusSettings
        };

        context.AddToConfiguration(customSettings);
        var generator = context.GetRequiredService<ISourceKnownIdUtils>();
        var bucketCount = 3;
        var idCount = (int)(SequenceTimeScope.MaxValue * bucketCount);
        var ids = new long[idCount];

        var epoch = EpochTimeUtils.Epoch2025;
        var beforeIdGenerated = DateTimeOffset.UtcNow;

        await Task.Delay(1001);
        _ = ids
            .AsParallel()
            .WithDegreeOfParallelism(8)
            .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
            .Select((_, index) =>
            {
                ids[index] = generator.Next<ISourceKnownIdUtils>();
                return index;
            }).ToArray();
        await Task.Delay(1001);

        var afterIdGenerated = DateTimeOffset.UtcNow;

        epoch.Should().BeBefore(beforeIdGenerated);

        var idInfos = ids.Select(id => generator.Parse(id)).ToArray();

        idInfos.Length.Should().Be(idCount);
        idInfos.Should().AllSatisfy(idInfo =>
        {
            idInfo.Id.Should().BeNegative();
            idInfo.AppId.Should().Be(nexusSettings.AppId);
            idInfo.AppInstanceId.Should().Be(nexusSettings.AppInstanceId);

            idInfo.CreatedAt.Should().BeBefore(afterIdGenerated);
            idInfo.CreatedAt.Should().BeAfter(beforeIdGenerated);
        });

        var idInfoGroups = idInfos
            .DistinctBy(id => id.Id)
            .GroupBy(x => x.CreatedAt)
            .ToArray();

        var buckets = idInfoGroups.Select(x => x.Key).ToArray();
        //todo: fix flaky test
        buckets.Length.Should().BeGreaterThanOrEqualTo(bucketCount); //during generation initial and last buckets may be halflings
        buckets.Length.Should().BeLessThanOrEqualTo(bucketCount + 2); //we should also consider bucket creep testing overhead by adding another 1 bucket

        var duration = afterIdGenerated - beforeIdGenerated; // it is expected to be complete in bucket count + 1 second.
        duration.TotalSeconds.Should().BeGreaterThanOrEqualTo(bucketCount); //tests can be slower, restricted upper limit may not work every time

        var actualCount = 0;
        foreach (var group in idInfoGroups)
        {
            var orderedIds = group.OrderBy(x => x.Id).ToArray();
            var groupCount = group.Count();
            orderedIds.First().InstanceId.Should().Be(0);
            if (groupCount > 1)
                orderedIds.Skip(1).First().InstanceId.Should().Be(1);

            orderedIds.Last().InstanceId.Should().Be((uint)(groupCount - 1));
            actualCount += groupCount;
        }

        actualCount.Should().Be(idCount);
    }
}