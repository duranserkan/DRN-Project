using System.Buffers.Binary;
using Blake3;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Numbers;
using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Utils.Ids;

/// <summary>
/// Embeds a keyed hash, a 16‑bit entity‑type, 16‑bit GUID markers, and a 64‑bit ID into a
/// 128‑bit GUID, providing a reversible mapping with integrity checking.
/// </summary>
public interface ISourceKnownEntityIdUtils
{
    SourceKnownEntityId Generate<TEntity>(long id) where TEntity : SourceKnownEntity;
    SourceKnownEntityId Generate(SourceKnownEntity sourceKnownEntity);
    SourceKnownEntityId Parse(Guid entityId);
    SourceKnownEntityId Validate(Guid entityId, byte entityTypeId);
}

/// <summary>
/// Embeds a keyed hash, a 16‑bit entity‑type, 16‑bit GUID markers, and a 64‑bit ID into a
/// 128‑bit GUID, providing a reversible mapping with integrity checking.
/// Add rate limit to endpoints that accept SourceKnownEntityId from untrusted sources to prevent brute force attacks.
/// Optionally, add randomness to instance id generation to improve brute force durability.
/// If still not enough, don't enough source known ids, consider using a different id generation strategy such as true guid v4
/// </summary>
[Singleton<ISourceKnownEntityIdUtils>]
public class SourceKnownEntityIdUtils(IAppSettings appSettings, ISourceKnownIdUtils sourceKnownIdUtils) : ISourceKnownEntityIdUtils
{
    private const byte EntityIdFirstHalfOffset = 0;
    private const byte EntityIdFirstHalfLength = 4; // 0-3

    private const byte EntityTypeIdIndex = 4; //4
    private const byte InvalidEntityTypeId = byte.MaxValue;

    //5 and 6 are reserved for hash

    //4D8D mark, Ensures that the source-known entityId is easily identifiable by humans and UUID V4 compatible
    private const byte SourceKnownMarkerVersionIndex = 7;
    private const byte SourceKnownMarkerVariantIndex = 8;
    private const byte SourceKnownMarkerVersionByte = 0x4D; //7 | V4 => V4 is used as a cover to prevent detection and ensure compatibility 
    private const byte SourceKnownMarkerVariantByte = 0x8D; //8 | Variant RFC 4122

    //9, 10 and 11 are reserved for hash

    private const byte EntityIdSecondHalfOffset = 12;
    private const byte EntityIdSecondHalfLength = 4; // 12-15

    private readonly byte[] _defaultMacKey = appSettings.NexusAppSettings.GetDefaultMacKey().KeyAsByteArray; // todo add keyring rotation 

    public SourceKnownEntityId Generate<TEntity>(long id) where TEntity : SourceKnownEntity => Generate(id, SourceKnownEntity.GetEntityTypeId<TEntity>());
    public SourceKnownEntityId Generate(SourceKnownEntity sourceKnownEntity) => Generate(sourceKnownEntity.Id, SourceKnownEntity.GetEntityTypeId(sourceKnownEntity));

    private SourceKnownEntityId Generate(long id, byte entityTypeId)
    {
        Span<byte> hashBytes = stackalloc byte[5];
        Span<byte> guidBytes = stackalloc byte[16]; // Allocate 16 bytes on the stack for the GUID
        guidBytes.Clear();

        var idParser = NumberParser.Get((ulong)id);
        var firstHalf = idParser.ReadUInt();
        var secondHalf = idParser.ReadUInt();

        BinaryPrimitives.WriteUInt32LittleEndian(guidBytes.Slice(EntityIdFirstHalfOffset, EntityIdFirstHalfLength), firstHalf); //0-3
        guidBytes[EntityTypeIdIndex] = entityTypeId; //4
        //5 and 6 are reserved for hash
        guidBytes[SourceKnownMarkerVersionIndex] = SourceKnownMarkerVersionByte; //7
        guidBytes[SourceKnownMarkerVariantIndex] = SourceKnownMarkerVariantByte; //8
        //9, 10 and 11 are reserved for hash
        BinaryPrimitives.WriteUInt32LittleEndian(guidBytes.Slice(EntityIdSecondHalfOffset, EntityIdSecondHalfLength), secondHalf); //12-15

        using var hasher = Hasher.NewKeyed(_defaultMacKey);
        hasher.Update(guidBytes);
        hasher.Finalize(hashBytes);

        guidBytes[5] = hashBytes[3];
        guidBytes[6] = hashBytes[4];
        guidBytes[9] = hashBytes[0];
        guidBytes[10] = hashBytes[1];
        guidBytes[11] = hashBytes[2];

        var entityId = new Guid(guidBytes);
        var sourceKnownId = sourceKnownIdUtils.Parse(id);

        return new SourceKnownEntityId(sourceKnownId, entityId, entityTypeId, true);
    }

    public SourceKnownEntityId Parse(Guid entityId)
    {
        Span<byte> guidBytes = stackalloc byte[16];
        Span<byte> actualHashBytes = stackalloc byte[5];
        Span<byte> expectedHashBytes = stackalloc byte[5];

        entityId.TryWriteBytes(guidBytes);

        actualHashBytes[3] = guidBytes[5];
        actualHashBytes[4] = guidBytes[6];
        actualHashBytes[0] = guidBytes[9];
        actualHashBytes[1] = guidBytes[10];
        actualHashBytes[2] = guidBytes[11];

        if (guidBytes[SourceKnownMarkerVersionIndex] != SourceKnownMarkerVersionByte || guidBytes[SourceKnownMarkerVariantIndex] != SourceKnownMarkerVariantByte)
            return CreateInvalid(entityId);

        var idFirstHalf = BinaryPrimitives.ReadUInt32LittleEndian(guidBytes.Slice(EntityIdFirstHalfOffset, EntityIdFirstHalfLength));
        var idSecondHalf = BinaryPrimitives.ReadUInt32LittleEndian(guidBytes.Slice(EntityIdSecondHalfOffset, EntityIdSecondHalfLength));
        var entityTypeId = guidBytes[EntityTypeIdIndex];

        var idBuilder = NumberBuilder.GetLongUnsigned();
        idBuilder.TryAddUInt(idFirstHalf);
        idBuilder.TryAddUInt(idSecondHalf);
        var id = (long)idBuilder.GetValue();

        guidBytes[5] = 0;
        guidBytes[6] = 0;
        guidBytes[9] = 0;
        guidBytes[10] = 0;
        guidBytes[11] = 0;

        using var hasher = Hasher.NewKeyed(_defaultMacKey); //consider object pooling for performance
        hasher.Update(guidBytes);
        hasher.Finalize(expectedHashBytes);

        return expectedHashBytes.SequenceEqual(actualHashBytes)
            ? new SourceKnownEntityId(sourceKnownIdUtils.Parse(id), entityId, entityTypeId, true)
            : CreateInvalid(entityId);
    }

    public SourceKnownEntityId Validate(Guid entityId, byte entityTypeId)
    {
        var sourceKnownId = Parse(entityId);
        if (!sourceKnownId.Valid)
            throw new ValidationException($"Invalid EntityId: {entityId:N}");

        if (sourceKnownId.EntityTypeId == entityTypeId) return sourceKnownId;

        var ex = new ValidationException($"Invalid Entity Type: EntityId:{entityId:N}");
        ex.Data.Add($"Expected_{nameof(SourceKnownEntityId.EntityTypeId)}", entityTypeId);
        ex.Data.Add($"Found_{nameof(SourceKnownEntityId.EntityTypeId)}", sourceKnownId.EntityTypeId);
        
        throw ex;
    }

    private static SourceKnownEntityId CreateInvalid(Guid entityId) => new(default, entityId, InvalidEntityTypeId, false);
}