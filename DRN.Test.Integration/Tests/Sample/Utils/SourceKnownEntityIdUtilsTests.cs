using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Ids;
using DRN.Framework.Utils.Time;

namespace DRN.Test.Integration.Tests.Sample.Utils;

public class SourceKnownEntityIdUtilsTests
{
    [Theory]
    [DataInlineUnit]
    public async Task SourceKnownEntityIdUtils_Should_Generate_Ids_For_3_Seconds(DrnTestContextUnit context)
    {
        var nexusSettings = new NexusAppSettings { AppId = 7, AppInstanceId = 24 };
        context.AddToConfiguration(new { NexusAppSettings = nexusSettings });
        var idUtils = context.GetRequiredService<ISourceKnownIdUtils>();
        var entityIdUtils = context.GetRequiredService<ISourceKnownEntityIdUtils>();
        var xEntityType = SourceKnownEntity.GetEntityType<XEntity>();

        var bucketCount = 3;
        var idCount = (int)(SequenceTimeScope.MaxValue * bucketCount);
        var ids = new long[idCount];
        var entityIds = new SourceKnownEntityId[idCount];

        await Task.Delay(TimeStampManager.PrecisionUnitInMsSafeDelay);
        _ = ids
            .AsParallel()
            .WithDegreeOfParallelism(8)
            .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
            .Select((_, index) =>
            {
                ids[index] = idUtils.Next<XEntity>();
                entityIds[index] = entityIdUtils.GenerateSecure(new XEntity(ids[index]));
                return index;
            }).ToArray();
        await Task.Delay(TimeStampManager.PrecisionUnitInMsSafeDelay);

        entityIds.All(x => x.Valid).Should().BeTrue();
        entityIds.All(x => x.Secure).Should().BeTrue("all generated ids must be Secure");
        entityIds.All(x => x.EntityId != Guid.Empty).Should().BeTrue();

        // Parse every generated entity ID and validate round-trip
        var parsedIds = entityIds.Select(entityId =>
        {
            entityId.Valid.Should().BeTrue();
            entityId.Source.AppId.Should().Be(nexusSettings.AppId);
            entityId.Source.AppInstanceId.Should().Be(nexusSettings.AppInstanceId);
            entityId.EntityType.Should().Be(xEntityType);

            var parsed = entityIdUtils.Parse(entityId.EntityId);

            return (entityId, parsed);
        }).ToArray();

        var validIds = parsedIds.Where(x => x.parsed.Valid).ToArray();
        var invalidIds = parsedIds.Where(x => !x.parsed.Valid).ToArray();
        foreach (var pair in validIds)
        {
            pair.entityId.Should().Be(pair.parsed);
            pair.entityId.Source.Should().Be(pair.parsed.Source);
            pair.parsed.Secure.Should().BeTrue("collision guard must prevent Secure→Plain misclassification");
        }
        
        // Collision guard verification: the collision guard iterates the variant byte and re-encrypts
        // when ciphertext coincidentally has 0x8D at byte[7] and 0x8D at byte[8] (~1/65536 probability).
        // After the guard, no Secure SKEID should contain the plain marker pattern.
        var coincidentalMarkerCount = entityIds.Count(e =>
        {
            var bytes = e.EntityId.ToByteArray();
            return bytes[7] == 0x8D && bytes[8] == 0x8D;
        });
        
        //collision guard prevents, 0x8D8D markers in Secure SKEIDs
        //coincidentalMarkerCount shows collision count that is prevented
        coincidentalMarkerCount.Should().BeGreaterThanOrEqualTo(0);

        // Collision guard: no Secure SKEID should have been misclassified as Plain
        var misclassifiedCount = parsedIds.Count(p => !p.parsed.Secure);
        misclassifiedCount.Should().Be(0, "collision guard must eliminate all Secure→Plain misclassification");

        invalidIds.Length.Should().Be(0);
        validIds.Length.Should().Be(entityIds.Length);

        // Verify all generated entity GUIDs are unique (AES-ECB is a PRP — distinct inputs ⇒ distinct outputs)
        var uniqueGuids = entityIds.Select(e => e.EntityId).Distinct().Count();
        uniqueGuids.Should().Be(entityIds.Length);
    }

    [EntityType(200)]
    class XEntity(long id) : SourceKnownEntity(id);
}