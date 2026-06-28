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

        // Normalize test execution bounds and CreatedAt timestamps to 250ms epoch ticks.
        // TimeStampManager truncates timestamps to 250ms tick boundaries, so converting bounds
        // to ticks prevents sub-second precision race conditions with high-precision UtcNow times.
        var beforeIdGeneratedTimestamp = EpochTimeUtils.ConvertToTicks(beforeIdGenerated, epoch);
        var afterIdGeneratedTimestamp = EpochTimeUtils.ConvertToTicks(afterIdGenerated, epoch);
        var idInfos = ids.Select(id => generator.Parse(id)).ToArray();

        idInfos.Length.Should().Be(idCount);
        idInfos.Should().AllSatisfy(idInfo =>
        {
            idInfo.Id.Should().BeNegative();
            idInfo.AppId.Should().Be(nexusSettings.AppId);
            idInfo.AppInstanceId.Should().Be(nexusSettings.AppInstanceId);

            var createdAtTimestamp = EpochTimeUtils.ConvertToTicks(idInfo.CreatedAt, epoch);
            createdAtTimestamp.Should().BeGreaterThanOrEqualTo(beforeIdGeneratedTimestamp);
            createdAtTimestamp.Should().BeLessThanOrEqualTo(afterIdGeneratedTimestamp);
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
