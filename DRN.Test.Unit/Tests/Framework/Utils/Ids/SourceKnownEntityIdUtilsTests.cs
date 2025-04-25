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
        var idUtils = context.GetRequiredService<ISourceKnownIdUtils>();
        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();

        var epoch = SourceKnownIdUtils.Epoch2025;
        var beforeIdGenerated = DateTimeOffset.UtcNow;

        await Task.Delay(1100); // 100 ms buffer added to compensate caching effect
        var longId = idUtils.Next<XEntity>();
        var entity = new XEntity(longId);
        var entityId = entityIdUtils.Generate(entity);
        entity.EntityIdSource = entityId;
        await Task.Delay(1000);

        var afterIdGenerated = DateTimeOffset.UtcNow;

        entityId.Source.Id.Should().Be(longId);
        entityId.Valid.Should().BeTrue();
        entityId.Source.AppId.Should().Be(nexusSettings.NexusAppId);
        entityId.Source.AppInstanceId.Should().Be(nexusSettings.NexusAppInstanceId);
        entityId.EntityTypeId.Should().Be(Entity.GetEntityTypeId<XEntity>());
        IsVersion4Rfc4122(entityId.EntityId).Should().BeTrue();

        var idInfo = idUtils.Parse(entityId.Source.Id);
        idInfo.AppId.Should().Be(nexusSettings.NexusAppId);
        idInfo.AppInstanceId.Should().Be(nexusSettings.NexusAppInstanceId);
        idInfo.CreatedAt.Should().BeBefore(afterIdGenerated);
        idInfo.CreatedAt.Should().BeAfter(beforeIdGenerated);
        epoch.Should().BeBefore(beforeIdGenerated);

        var parsedSourceKnownEntityId = entityIdUtils.Parse(entityId.EntityId);
        parsedSourceKnownEntityId.Should().Be(entityId);
        parsedSourceKnownEntityId.EntityId.Should().Be(entityId.EntityId);

        parsedSourceKnownEntityId.Source.Id.Should().Be(longId);
        parsedSourceKnownEntityId.Source.Id.Should().Be(entityId.Source.Id);
        parsedSourceKnownEntityId.Source.AppId.Should().Be(nexusSettings.NexusAppId);
        parsedSourceKnownEntityId.Source.AppInstanceId.Should().Be(nexusSettings.NexusAppInstanceId);
        parsedSourceKnownEntityId.Source.InstanceId.Should().Be(entityId.Source.InstanceId);
        parsedSourceKnownEntityId.Source.CreatedAt.Should().Be(entityId.Source.CreatedAt);
        parsedSourceKnownEntityId.Source.Should().Be(entityId.Source);
        parsedSourceKnownEntityId.EntityTypeId.Should().Be(entityId.EntityTypeId);
        parsedSourceKnownEntityId.Valid.Should().BeTrue();
    }

    [EntityTypeId(200)]
    class XEntity(long id) : Entity(id);

    private static bool IsVersion4Rfc4122(Guid guid)
    {
        var bytes = guid.ToByteArray();
        // Check the version: high nibble of byte 7 should be 4
        var isVersion4 = (bytes[7] >> 4) == 4;
        // Check variant: top two bits of byte 8 should be 10 (binary)
        var isRfc4122Variant = (bytes[8] & 0xC0) == 0x80;

        return isVersion4 && isRfc4122Variant;
    }
}