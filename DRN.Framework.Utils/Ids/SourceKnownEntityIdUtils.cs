using System.Buffers.Binary;
using Blake3;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Utils.Ids;

/// <summary>
/// Embeds a keyed hash, a 16‑bit entity‑type, 16‑bit GUID markers, and a 64‑bit ID into a
/// 128‑bit GUID, providing a reversible mapping with integrity checking.
/// </summary>
public interface ISourceKnownEntityIdUtils
{
    SourceKnownEntityId Generate<TEntity>(long id) where TEntity : Entity;
    SourceKnownEntityId Generate(Entity entity);
    SourceKnownEntityId Parse(Guid entityId);
}

/// <summary>
/// Embeds a keyed hash, a 16‑bit entity‑type, 16‑bit GUID markers, and a 64‑bit ID into a
/// 128‑bit GUID, providing a reversible mapping with integrity checking.
/// </summary>
[Singleton<ISourceKnownEntityIdUtils>]
public class SourceKnownEntityIdUtils(IAppSettings appSettings, ISourceKnownIdUtils sourceKnownIdUtils) : ISourceKnownEntityIdUtils
{
    private const byte HashOffset = 0;
    private const byte HashLength = 4; //0-3
    private const byte PayloadLength = 12; //4-15

    private const byte EntityTypeIdOffset = 4;
    private const byte EntityTypeLength = 2; //4-5
    private const ushort InvalidEntityTypeId = ushort.MaxValue;
    
    //4D8D mark, Ensures that the source-known entityId is easily identifiable by humans and UUID V4 compatible
    private const byte SourceKnownMarkerVersionByte = 0x4D; //6 | V4
    private const byte SourceKnownMarkerVariantByte = 0x8D; //7 | Variant RFC 4122
    
    private const byte EntityIdOffset = 8;
    private const byte EntityIdLength = 8; // 8-15
    
    private readonly IReadOnlyList<NexusMacKey> _macKeys = appSettings.Nexus.MacKeys;
    private readonly byte[] _defaultMacKey = appSettings.Nexus.GetDefaultMacKey().Key; // todo add keyring rotation 

    public SourceKnownEntityId Generate<TEntity>(long id) where TEntity : Entity => Generate(id, Entity.GetEntityTypeId<TEntity>());
    public SourceKnownEntityId Generate(Entity entity) => Generate(entity.Id, Entity.GetEntityTypeId(entity));

    private SourceKnownEntityId Generate(long id, ushort entityTypeId)
    {
        Span<byte> guidBytes = stackalloc byte[16]; // Allocate 16 bytes on the stack for the GUID

        //0-3 is reserved for Mac hash
        BinaryPrimitives.WriteUInt16LittleEndian(guidBytes.Slice(EntityTypeIdOffset, EntityTypeLength), entityTypeId); // 4-5
        guidBytes[6] = SourceKnownMarkerVersionByte; //6
        guidBytes[7] = SourceKnownMarkerVariantByte; //7
        BinaryPrimitives.WriteInt64LittleEndian(guidBytes.Slice(EntityIdOffset, EntityIdLength), id); //8-15

        using var hasher = Hasher.NewKeyed(_defaultMacKey);
        hasher.Update(guidBytes.Slice(EntityTypeIdOffset, PayloadLength)); //4-15
        hasher.Finalize(guidBytes.Slice(HashOffset, HashLength)); //0-3

        var entityId = new Guid(guidBytes);
        var sourceKnownId = sourceKnownIdUtils.Parse(id);

        return new SourceKnownEntityId(sourceKnownId, entityId, entityTypeId, true);
    }

    public SourceKnownEntityId Parse(Guid entityId)
    {
        Span<byte> guidBytes = stackalloc byte[16];
        entityId.TryWriteBytes(guidBytes);

        if (guidBytes[6] != SourceKnownMarkerVersionByte || guidBytes[7] != SourceKnownMarkerVariantByte)
            return CreateInvalid(entityId);

        var id = BinaryPrimitives.ReadInt64LittleEndian(guidBytes.Slice(EntityIdOffset, EntityIdLength));
        var entityTypeId = BinaryPrimitives.ReadUInt16LittleEndian(guidBytes.Slice(EntityTypeIdOffset, EntityTypeLength));

        Span<byte> expectedHashBytes = stackalloc byte[4];
        var actualHashBytes = guidBytes.Slice(HashOffset, HashLength);
        
        using var hasher = Hasher.NewKeyed(_defaultMacKey); //consider object pooling for performance
        hasher.Update(guidBytes.Slice(EntityTypeIdOffset, PayloadLength)); //4-15
        hasher.Finalize(expectedHashBytes); //0-3
        
        return expectedHashBytes.SequenceEqual(actualHashBytes)
            ? new SourceKnownEntityId(sourceKnownIdUtils.Parse(id), entityId, entityTypeId, true)
            : CreateInvalid(entityId);
    }

    private SourceKnownEntityId CreateInvalid(Guid entityId) => new(default, entityId, InvalidEntityTypeId, false);
}