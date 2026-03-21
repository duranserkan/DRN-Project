using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Time;

namespace DRN.Test.Integration.Tests.Sample.Utils;

public class SourceKnownIdUtilsTests
{
    [Theory]
    [DataInlineUnit]
    public async Task SourceKnownIdUtils_Should_Generate_Ids_For_3_Seconds(DrnTestContextUnit context)
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

        await Task.Delay(TimeStampManager.PrecisionUnitInMsSafeDelay); // wait for at least one tick
        _ = ids
            .AsParallel()
            .WithDegreeOfParallelism(8)
            .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
            .Select((_, index) =>
            {
                ids[index] = generator.Next<ISourceKnownIdUtils>();
                return index;
            }).ToArray();
        await Task.Delay(TimeStampManager.PrecisionUnitInMsSafeDelay); // wait for tick boundary

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
        buckets.Length.Should().BeGreaterThanOrEqualTo(bucketCount); //during generation initial and last buckets may be halflings
        buckets.Length.Should().BeLessThanOrEqualTo(bucketCount + 4); //with 250ms ticks, fewer buckets per second; overhead may add ticks

        var duration = afterIdGenerated - beforeIdGenerated;
        duration.TotalMilliseconds.Should().BeGreaterThanOrEqualTo(bucketCount * TimeStampManager.PrecisionUnitInMsSafeDelay); //at least bucketCount ticks

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