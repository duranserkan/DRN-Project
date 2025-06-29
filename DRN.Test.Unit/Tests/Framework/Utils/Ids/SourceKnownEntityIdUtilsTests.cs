using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Time;

namespace DRN.Test.Unit.Tests.Framework.Utils.Ids;

public class SourceKnownEntityIdUtilsTests
{
    [Theory]
    [DataInlineUnit]
    public async Task SourceKnownIDs_Should_Be_Generate_Id(UnitTestContext context)
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
        var idUtils = context.GetRequiredService<ISourceKnownIdUtils>();
        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();

        var epoch = EpochTimeUtils.Epoch2025;
        var beforeIdGenerated = DateTimeOffset.UtcNow;

        await Task.Delay(1100); // 100 ms buffer added to compensate caching effect
        var longId1 = idUtils.Next<XEntity>();
        var entity1 = new XEntity(longId1);
        var entityId1 = entityIdUtils.Generate(entity1);
        var entity1Duplicate = new XEntity(longId1);
        var entityId1Duplicate = entityIdUtils.Generate(entity1Duplicate);
        entity1.EntityIdSource = entityId1;
        await Task.Delay(1000);

        var afterIdGenerated = DateTimeOffset.UtcNow;

        entityId1.Source.Id.Should().Be(longId1);
        entityId1.Valid.Should().BeTrue();
        entityId1.Source.AppId.Should().Be(nexusSettings.AppId);
        entityId1.Source.AppInstanceId.Should().Be(nexusSettings.AppInstanceId);
        entityId1.EntityTypeId.Should().Be(SourceKnownEntity.GetEntityTypeId<XEntity>());
        IsVersion4Rfc4122(entityId1.EntityId).Should().BeTrue();

        var idInfo = idUtils.Parse(entityId1.Source.Id);
        idInfo.AppId.Should().Be(nexusSettings.AppId);
        idInfo.AppInstanceId.Should().Be(nexusSettings.AppInstanceId);
        idInfo.CreatedAt.Should().BeBefore(afterIdGenerated);
        idInfo.CreatedAt.Should().BeAfter(beforeIdGenerated);
        epoch.Should().BeBefore(beforeIdGenerated);

        var parsedSourceKnownEntityId = entityIdUtils.Parse(entityId1.EntityId);
        parsedSourceKnownEntityId.Should().Be(entityId1);
        parsedSourceKnownEntityId.EntityId.Should().Be(entityId1.EntityId);

        parsedSourceKnownEntityId.Source.Id.Should().Be(longId1);
        parsedSourceKnownEntityId.Source.Id.Should().Be(entityId1.Source.Id);
        parsedSourceKnownEntityId.Source.AppId.Should().Be(nexusSettings.AppId);
        parsedSourceKnownEntityId.Source.AppInstanceId.Should().Be(nexusSettings.AppInstanceId);
        parsedSourceKnownEntityId.Source.InstanceId.Should().Be(entityId1.Source.InstanceId);
        parsedSourceKnownEntityId.Source.CreatedAt.Should().Be(entityId1.Source.CreatedAt);
        parsedSourceKnownEntityId.Source.Should().Be(entityId1.Source);
        parsedSourceKnownEntityId.EntityTypeId.Should().Be(entityId1.EntityTypeId);
        parsedSourceKnownEntityId.Valid.Should().BeTrue();

        var longId2 = idUtils.Next<XEntity>();
        var entity2 = new XEntity(longId2);
        var entityId2 = entityIdUtils.Generate(entity2);
        var entity2Duplicate = new XEntity(longId2);
        var entityId2Duplicate = entityIdUtils.Generate(entity2Duplicate);
        (entityId2 > entityId1).Should().BeTrue();
        (entityId2 >= entityId1).Should().BeTrue();
        (entityId2 >= entityId2Duplicate).Should().BeTrue();
        (entityId1 < entityId2).Should().BeTrue();
        (entityId1 <= entityId2).Should().BeTrue();
        (entityId1 <= entityId1Duplicate).Should().BeTrue();
        
        entityId1.HasSameEntityType(entityId2).Should().BeTrue();
        
        var entity3 = new YEntity(longId2);
        var entityId3 = entityIdUtils.Generate(entity3);
        entityId2.HasSameEntityType(entityId3).Should().BeFalse();
    }

    [EntityTypeId(200)]
    class XEntity(long id) : SourceKnownEntity(id);
    [EntityTypeId(201)]
    class YEntity(long id) : SourceKnownEntity(id);

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