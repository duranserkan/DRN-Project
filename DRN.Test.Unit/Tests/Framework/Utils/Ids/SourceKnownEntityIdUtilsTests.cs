using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Testing.Contexts;
using DRN.Framework.Testing.DataAttributes;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Settings;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using SourceKnownIdUtils = DRN.Framework.Utils.Ids.SourceKnownIdUtils;

namespace DRN.Test.Unit.Tests.Framework.Utils.Ids;

public class SourceKnownEntityIdUtilsTests
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
        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();

        var entity = new XEntity();
        var epoch = SourceKnownIdUtils.Epoch2025;
        var beforeIdGenerated = DateTimeOffset.UtcNow;

        await Task.Delay(1100); // 100ms buffer added to compensate caching effect
        var entityId = entityIdUtils.Generate(entity);
        await Task.Delay(1100);

        var afterIdGenerated = DateTimeOffset.UtcNow;

        entityId.Source.Id.Should().BeNegative();
        epoch.Should().BeBefore(beforeIdGenerated);

        var idUtils = context.GetRequiredService<ISourceKnownIdUtils>();
        var idInfo = idUtils.Parse(entityId.Source.Id);
        idInfo.AppId.Should().Be(nexusSettings.NexusAppId);
        idInfo.AppInstanceId.Should().Be(nexusSettings.NexusAppInstanceId);

        idInfo.CreatedAt.Should().BeBefore(afterIdGenerated);
        idInfo.CreatedAt.Should().BeAfter(beforeIdGenerated);
    }

    public class XEntity : Entity
    {
    }
}