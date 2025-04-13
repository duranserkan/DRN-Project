using DRN.Framework.Testing.Contexts;
using DRN.Framework.Testing.DataAttributes;
using DRN.Framework.Utils.Common;
using DRN.Framework.Utils.Common.Sequences;
using DRN.Framework.Utils.Settings;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DRN.Test.Unit.Tests.Framework.Utils.Common;

public class SourceKnownIdTests
{
    [Theory]
    [DataInlineUnit]
    public async Task SourceKnownIDs_Should_Be_Generate_Id(UnitTestContext context)
    {
        var nexusSettings = new NexusAppSettings
        {
            NexusAppId = 5,
            NexusAppInstanceId = 12
        };

        var customSettings = new
        {
            NexusAppSettings = nexusSettings
        };

        context.AddToConfiguration(customSettings);
        var generator = context.GetRequiredService<ISourceKnownIdGenerator>();

        var epoch = SourceKnownIdGenerator.Epoch2025;
        var beforeIdGenerated = DateTimeOffset.UtcNow;

        await Task.Delay(1000);
        var id = generator.NextId<SourceKnownIdTests>();
        await Task.Delay(1000);

        var afterIdGenerated = DateTimeOffset.UtcNow;

        id.Should().BeNegative();
        epoch.Should().BeBefore(beforeIdGenerated);

        var idInfo = generator.Parse(id);
        idInfo.AppId.Should().Be(nexusSettings.NexusAppId);
        idInfo.AppInstanceId.Should().Be(nexusSettings.NexusAppInstanceId);

        idInfo.CreatedAt.Should().BeBefore(afterIdGenerated);
        idInfo.CreatedAt.Should().BeAfter(beforeIdGenerated);
    }

    [Theory]
    [DataInlineUnit]
    public async Task SourceKnownIDs_Should_Be_Generate_Ids_For_3_Seconds(UnitTestContext context)
    {
        var nexusSettings = new NexusAppSettings
        {
            NexusAppId = 7,
            NexusAppInstanceId = 24
        };

        var customSettings = new
        {
            NexusAppSettings = nexusSettings
        };

        context.AddToConfiguration(customSettings);
        var generator = context.GetRequiredService<ISourceKnownIdGenerator>();
        var bucketCount = 3;
        var idCount = (int)(SequenceTimeScope.MaxValue * bucketCount);
        var ids = new long[idCount];

        var epoch = SourceKnownIdGenerator.Epoch2025;
        var beforeIdGenerated = DateTimeOffset.UtcNow;

        await Task.Delay(1001);
        _ = ids
            .AsParallel()
            .WithDegreeOfParallelism(8)
            .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
            .Select((id,index) =>
        {
            ids[index] = generator.NextId<ISourceKnownIdGenerator>();
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
            idInfo.AppId.Should().Be(nexusSettings.NexusAppId);
            idInfo.AppInstanceId.Should().Be(nexusSettings.NexusAppInstanceId);

            idInfo.CreatedAt.Should().BeBefore(afterIdGenerated);
            idInfo.CreatedAt.Should().BeAfter(beforeIdGenerated);
        });

        var idInfoGroups = idInfos.GroupBy(x => x.CreatedAt).ToArray();
        var buckets = idInfoGroups.Select(x => x.Key).ToArray();

        buckets.Length.Should().BeGreaterThanOrEqualTo(bucketCount); //during generation initial and last buckets may be halflings
        buckets.Length.Should().BeLessThanOrEqualTo(bucketCount + 1);

        var duration = afterIdGenerated - beforeIdGenerated; // it is expected to be complete in bucket count + 1 seconds.
        duration.TotalSeconds.Should().BeInRange(bucketCount, bucketCount + 1.5); //we should also consider testing overhead by adding 0.5 seconds

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