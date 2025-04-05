using DRN.Framework.Testing.Contexts;
using DRN.Framework.Testing.DataAttributes;
using DRN.Framework.Utils.Common;
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

        var epoch = IdGenerator.Epoch2025;
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
        var idCount = ushort.MaxValue * bucketCount;
        var idArray = new long[idCount];

        var epoch = IdGenerator.Epoch2025;
        var beforeIdGenerated = DateTimeOffset.UtcNow;

        await Task.Delay(1000);
        for (var i = 0; i < idCount; i++)
            idArray[i] = generator.NextId<ISourceKnownIdGenerator>();
        await Task.Delay(1000);

        var afterIdGenerated = DateTimeOffset.UtcNow;

        epoch.Should().BeBefore(beforeIdGenerated);

        var idInfos = idArray.Select(id => generator.Parse(id)).ToArray();

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

        buckets.Length.Should().BeGreaterThanOrEqualTo(bucketCount);
        buckets.Length.Should().BeLessThan(bucketCount + 1);

        var duration = afterIdGenerated - beforeIdGenerated;
        duration.TotalSeconds.Should().BeGreaterThanOrEqualTo(bucketCount);
        duration.TotalSeconds.Should().BeLessThan(bucketCount + 1);

        foreach (var group in idInfoGroups)
        {
            var groupCount = group.Count();
            group.First().InstanceId.Should().Be(0);
            if (groupCount > 1)
                group.Skip(1).First().InstanceId.Should().Be(1);

            group.Last().InstanceId.Should().Be((ushort)(groupCount - 1));
        }
        //todo measure performance, how fast they are generated?
    }
}