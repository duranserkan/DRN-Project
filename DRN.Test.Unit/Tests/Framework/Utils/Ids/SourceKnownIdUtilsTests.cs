using DRN.Framework.Testing.Contexts;
using DRN.Framework.Testing.DataAttributes;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Settings;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DRN.Test.Unit.Tests.Framework.Utils.Ids;

public class SourceKnownIdUtilsTests
{
    [Fact]
    public async Task Generator_Should_Generate_Valid_Id()
    {
        byte appId = 1;
        byte appInstanceId = 1;
        
        var epoch = SourceKnownIdUtils.Epoch2025;
        var beforeIdGenerated = DateTimeOffset.UtcNow;
        
        await Task.Delay(1200);
        var id = SourceKnownIdUtils.Generate<object>(appId, appInstanceId);
        await Task.Delay(1200);
        
        var afterIdGenerated = DateTimeOffset.UtcNow;
        var idInfo = SourceKnownIdUtils.ParseId(id);
        
        idInfo.Id.Should().Be(id);
        idInfo.AppId.Should().Be(appId);
        idInfo.AppInstanceId.Should().Be(appInstanceId);

        epoch.Should().BeBefore(beforeIdGenerated);
        idInfo.CreatedAt.Should().BeBefore(afterIdGenerated);
        idInfo.CreatedAt.Should().BeAfter(beforeIdGenerated);
    }
    
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
        var generator = context.GetRequiredService<ISourceKnownIdUtils>();

        var epoch = SourceKnownIdUtils.Epoch2025;
        var beforeIdGenerated = DateTimeOffset.UtcNow;

        await Task.Delay(1100); // 100ms buffer added to compensate caching effect
        var id = generator.Next<SourceKnownIdUtilsTests>();
        await Task.Delay(1100);

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
        var generator = context.GetRequiredService<ISourceKnownIdUtils>();
        var bucketCount = 3;
        var idCount = (int)(SequenceTimeScope.MaxValue * bucketCount);
        var ids = new long[idCount];

        var epoch = SourceKnownIdUtils.Epoch2025;
        var beforeIdGenerated = DateTimeOffset.UtcNow;

        await Task.Delay(1001);
        _ = ids
            .AsParallel()
            .WithDegreeOfParallelism(8)
            .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
            .Select((_,index) =>
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
            idInfo.AppId.Should().Be(nexusSettings.NexusAppId);
            idInfo.AppInstanceId.Should().Be(nexusSettings.NexusAppInstanceId);

            idInfo.CreatedAt.Should().BeBefore(afterIdGenerated);
            idInfo.CreatedAt.Should().BeAfter(beforeIdGenerated);
        });

        var idInfoGroups = idInfos
            .DistinctBy(id=>id.Id)
            .GroupBy(x => x.CreatedAt)
            .ToArray();
        
        var buckets = idInfoGroups.Select(x => x.Key).ToArray();

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