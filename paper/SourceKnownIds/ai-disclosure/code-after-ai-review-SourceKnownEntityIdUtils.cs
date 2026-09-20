using System.Buffers.Binary;
using System.Security.Cryptography;
using Blake3;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Numbers;
using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Utils.Ids;

/// <summary>
/// Embeds a keyed hash, a 16‑bit entity‑type, 16‑bit GUID markers, and a 64‑bit ID into a
/// 128‑bit GUID, providing a reversible mapping with integrity checking.
/// Secure variants encrypt the entire 16-byte GUID using AES-256-ECB (true PRP) for confidentiality.
/// </summary>
public interface ISourceKnownEntityIdUtils
{
    /// <summary>Dispatches to <c>GenerateSecure</c> or <c>GenerateUnsecure</c> based on <c>AppSettings.NexusAppSettings.UseSecureSourceKnownIds</c>.</summary>
    SourceKnownEntityId Generate<TEntity>(long id) where TEntity : SourceKnownEntity;

    /// <inheritdoc cref="Generate{TEntity}(long)"/>
    SourceKnownEntityId Generate(SourceKnownEntity entity);

    /// <inheritdoc cref="Generate{TEntity}(long)"/>
    SourceKnownEntityId Generate(long id, byte entityType);

    SourceKnownEntityId GenerateSecure<TEntity>(long id) where TEntity : SourceKnownEntity;
    SourceKnownEntityId GenerateSecure(SourceKnownEntity entity);
    SourceKnownEntityId GenerateSecure(long id, byte entityType);

    SourceKnownEntityId GenerateUnsecure<TEntity>(long id) where TEntity : SourceKnownEntity;
    SourceKnownEntityId GenerateUnsecure(SourceKnownEntity entity);
    SourceKnownEntityId GenerateUnsecure(long id, byte entityType);

    SourceKnownEntityId? Parse(Guid? entityId);
    SourceKnownEntityId Parse(Guid entityId);

    SourceKnownEntityId? Validate(Guid? entityId, byte entityType);
    SourceKnownEntityId Validate(Guid entityId, byte entityType);

    SourceKnownEntityId? Validate<TEntity>(Guid? entityId) where TEntity : SourceKnownEntity;
    SourceKnownEntityId Validate<TEntity>(Guid entityId) where TEntity : SourceKnownEntity;
}

/// <summary>
/// Embeds a keyed hash, a 16‑bit entity‑type, 16‑bit GUID markers, and a 64‑bit ID into a
/// 128‑bit GUID, providing a reversible mapping with integrity checking.
/// Secure variants encrypt the entire 16-byte GUID using AES-256-ECB as a pseudo-random permutation (PRP).
/// AES-ECB on a single 128-bit block is a conjectured PRP (NIST FIPS 197) — no nonce required, no nonce-reuse vulnerability.
/// Parse auto-detects by trying AES-ECB decryption when plaintext markers are absent.
/// Add rate limit to endpoints that accept SourceKnownEntityId from untrusted sources to prevent brute force attacks.
/// Optionally, add randomness to instance id generation to improve brute force durability.
/// If still not enough, don't use source known ids, consider using a different id generation strategy such as true guid v4.
/// </summary>
[Singleton<ISourceKnownEntityIdUtils>]
public class SourceKnownEntityIdUtils(IAppSettings appSettings, ISourceKnownIdUtils sourceKnownIdUtils) : ISourceKnownEntityIdUtils, IDisposable
{
    private const byte EntityIdFirstHalfOffset = 0;
    private const byte EntityIdFirstHalfLength = 4; // 0-3

    private const byte EntityTypeIndex = 4; //4
    private const byte InvalidEntityType = byte.MaxValue;

    //5 and 6 are reserved for hash

    //4D8D mark — plaintext markers used for non-secure variant and inside the encrypted block for secure variant
    //Ensures that the source-known entityId is easily identifiable by humans and UUID V4 compatible
    private const byte SourceKnownMarkerVersionIndex = 7;
    private const byte SourceKnownMarkerVariantIndex = 8;
    private const byte SourceKnownMarkerVersionByte = 0x4D; //7 | V4 => V4 is used as a cover to prevent detection and ensure compatibility
    private const byte SourceKnownMarkerVariantByte = 0x8D; //8 | Variant RFC 4122

    //9, 10 and 11 are reserved for hash
    private const byte EntityIdSecondHalfOffset = 12;
    private const byte EntityIdSecondHalfLength = 4; // 12-15

    // Key separation: KeyAsBinary and AlternativeKeyAsBinary are cryptographically independent keys from the same keyring entry.
    // KeyAsBinary → BLAKE3 keyed MAC (integrity). AlternativeKeyAsBinary → AES-256-ECB (confidentiality).
    private readonly BinaryData _defaultMacKey = appSettings.NexusAppSettings.GetDefaultMacKey().KeyAsBinary; // todo add keyring rotation
    private readonly bool _useSecure = appSettings.NexusAppSettings.UseSecureSourceKnownIds;

    // Singleton — DI container disposes at shutdown. EncryptEcb/DecryptEcb are stateless single-block ops safe for concurrent use.
    // Stress tested with SourceKnownEntityIdUtilsTests
    // Post-quantum readiness: AES-256 retains 128-bit security under Grover's algorithm — NIST recommended for post-quantum symmetric encryption.
    private readonly Aes _aes = CreateAes(appSettings.NexusAppSettings.GetDefaultMacKey().AlternativeKeyAsBinary);

    public SourceKnownEntityId Generate<TEntity>(long id) where TEntity : SourceKnownEntity => Generate(id, SourceKnownEntity.GetEntityType<TEntity>());
    public SourceKnownEntityId Generate(SourceKnownEntity entity) => Generate(entity.Id, SourceKnownEntity.GetEntityType(entity));

    public SourceKnownEntityId Generate(long id, byte entityType)
        => _useSecure ? GenerateSecure(id, entityType) : GenerateUnsecure(id, entityType);

    public SourceKnownEntityId GenerateUnsecure<TEntity>(long id) where TEntity : SourceKnownEntity => GenerateUnsecure(id, SourceKnownEntity.GetEntityType<TEntity>());
    public SourceKnownEntityId GenerateUnsecure(SourceKnownEntity entity) => GenerateUnsecure(entity.Id, SourceKnownEntity.GetEntityType(entity));

    public SourceKnownEntityId GenerateUnsecure(long id, byte entityType)
    {
        Span<byte> hashBytes = stackalloc byte[5];
        Span<byte> guidBytes = stackalloc byte[16]; // Allocate 16 bytes on the stack for the GUID
        guidBytes.Clear();

        WriteIdAndMarkers(guidBytes, id, entityType, SourceKnownMarkerVariantByte);
        ComputeMac(guidBytes, hashBytes, _defaultMacKey);
        WriteMacToGuid(guidBytes, hashBytes);

        var entityId = new Guid(guidBytes);
        var sourceKnownId = sourceKnownIdUtils.Parse(id);

        return new SourceKnownEntityId(sourceKnownId, entityId, entityType, true);
    }

    public SourceKnownEntityId GenerateSecure<TEntity>(long id) where TEntity : SourceKnownEntity => GenerateSecure(id, SourceKnownEntity.GetEntityType<TEntity>());
    public SourceKnownEntityId GenerateSecure(SourceKnownEntity entity) => GenerateSecure(entity.Id, SourceKnownEntity.GetEntityType(entity));

    public SourceKnownEntityId GenerateSecure(long id, byte entityType)
    {
        Span<byte> hashBytes = stackalloc byte[5];
        Span<byte> guidBytes = stackalloc byte[16];
        guidBytes.Clear();

        // Build guid identically to non-secure (same layout, same markers)
        WriteIdAndMarkers(guidBytes, id, entityType, SourceKnownMarkerVariantByte);
        ComputeMac(guidBytes, hashBytes, _defaultMacKey);
        WriteMacToGuid(guidBytes, hashBytes);

        // Encrypt entire 16-byte block with AES-ECB (true PRP — no nonce needed)
        EncryptGuidBlock(guidBytes, _aes);

        var entityId = new Guid(guidBytes);
        var sourceKnownId = sourceKnownIdUtils.Parse(id);

        return new SourceKnownEntityId(sourceKnownId, entityId, entityType, true);
    }

    public SourceKnownEntityId? Parse(Guid? entityId) => entityId.HasValue ? Parse(entityId.Value) : null;

    public SourceKnownEntityId Parse(Guid entityId)
    {
        Span<byte> guidBytes = stackalloc byte[16];
        entityId.TryWriteBytes(guidBytes);

        // Try non-secure path first (plaintext 4D8D markers visible)
        if (HasValidMarkers(guidBytes))
        {
            var result = VerifyAndParse(guidBytes, entityId);
            if (result.Valid)
                return result;

            // Markers were coincidental in ciphertext (~1/65536) — restore and try decrypt path
            // Interestingly, when 6,291,453 ids generated around 96 entities (92, 101 etc.) had coincidental markers
            // Tested with DRN.Test.Unit/Tests/Framework/Utils/Ids/SourceKnownEntityIdUtilsTests.cs
            entityId.TryWriteBytes(guidBytes);
        }

        // Try secure path: AES-ECB decrypt full block, then check for markers
        DecryptGuidBlock(guidBytes, _aes);

        if (HasValidMarkers(guidBytes))
            return VerifyAndParse(guidBytes, entityId);

        return CreateInvalid(entityId);
    }

    public SourceKnownEntityId? Validate(Guid? entityId, byte entityType)
        => entityId.HasValue ? Validate(entityId.Value, entityType) : null;

    public SourceKnownEntityId Validate(Guid entityId, byte entityType)
    {
        var sourceKnownId = Parse(entityId);
        sourceKnownId.Validate(entityType);

        return sourceKnownId;
    }

    public SourceKnownEntityId? Validate<TEntity>(Guid? entityId) where TEntity : SourceKnownEntity
        => entityId.HasValue ? Validate<TEntity>(entityId.Value) : null;

    public SourceKnownEntityId Validate<TEntity>(Guid entityId) where TEntity : SourceKnownEntity
    {
        var sourceKnownId = Parse(entityId);
        sourceKnownId.Validate<TEntity>();

        return sourceKnownId;
    }

    private static SourceKnownEntityId CreateInvalid(Guid entityId) => new(default, entityId, InvalidEntityType, false);

    private static bool HasValidMarkers(ReadOnlySpan<byte> guidBytes)
        => guidBytes[SourceKnownMarkerVersionIndex] == SourceKnownMarkerVersionByte
           && guidBytes[SourceKnownMarkerVariantIndex] == SourceKnownMarkerVariantByte;

    /// <summary>
    /// Verifies MAC integrity and extracts ID/entityType from a plaintext (or decrypted) guid byte span.
    /// </summary>
    private SourceKnownEntityId VerifyAndParse(Span<byte> guidBytes, Guid entityId)
    {
        Span<byte> actualHashBytes = stackalloc byte[5];
        Span<byte> expectedHashBytes = stackalloc byte[5];

        ReadMacFromGuid(guidBytes, actualHashBytes);

        var idFirstHalf = BinaryPrimitives.ReadUInt32LittleEndian(guidBytes.Slice(EntityIdFirstHalfOffset, EntityIdFirstHalfLength));
        var idSecondHalf = BinaryPrimitives.ReadUInt32LittleEndian(guidBytes.Slice(EntityIdSecondHalfOffset, EntityIdSecondHalfLength));
        var entityType = guidBytes[EntityTypeIndex];

        var idBuilder = NumberBuilder.GetLongUnsigned();
        idBuilder.TryAddUInt(idFirstHalf);
        idBuilder.TryAddUInt(idSecondHalf);
        var id = (long)idBuilder.GetValue();

        ClearMacSlots(guidBytes);
        ComputeMac(guidBytes, expectedHashBytes, _defaultMacKey);

        if (expectedHashBytes.SequenceEqual(actualHashBytes))
            return new SourceKnownEntityId(sourceKnownIdUtils.Parse(id), entityId, entityType, true);
        
        return CreateInvalid(entityId);
    }

    /// <summary>
    /// Writes ID halves, entity type, version marker, and variant marker into the guid byte span.
    /// </summary>
    private static void WriteIdAndMarkers(Span<byte> guidBytes, long id, byte entityType, byte variantByte)
    {
        var idParser = NumberParser.Get((ulong)id);
        var firstHalf = idParser.ReadUInt();
        var secondHalf = idParser.ReadUInt();

        BinaryPrimitives.WriteUInt32LittleEndian(guidBytes.Slice(EntityIdFirstHalfOffset, EntityIdFirstHalfLength), firstHalf); //0-3
        guidBytes[EntityTypeIndex] = entityType; //4
        //5 and 6 are reserved for hash
        guidBytes[SourceKnownMarkerVersionIndex] = SourceKnownMarkerVersionByte; //7
        guidBytes[SourceKnownMarkerVariantIndex] = variantByte; //8
        //9, 10 and 11 are reserved for hash
        BinaryPrimitives.WriteUInt32LittleEndian(guidBytes.Slice(EntityIdSecondHalfOffset, EntityIdSecondHalfLength), secondHalf); //12-15
    }

    /// <summary>
    /// Computes BLAKE3 keyed MAC over the guid bytes (MAC slots must be zeroed before calling).
    /// </summary>
    private static void ComputeMac(ReadOnlySpan<byte> guidBytes, Span<byte> hashBytes, BinaryData defaultMacKey)
    {
        using var hasher = Hasher.NewKeyed(defaultMacKey);
        hasher.Update(guidBytes);
        hasher.Finalize(hashBytes);
    }

    /// <summary>
    /// Writes 5-byte MAC hash into guid byte positions 5-6 and 9-11.
    /// </summary>
    private static void WriteMacToGuid(Span<byte> guidBytes, ReadOnlySpan<byte> hashBytes)
    {
        guidBytes[5] = hashBytes[3];
        guidBytes[6] = hashBytes[4];
        guidBytes[9] = hashBytes[0];
        guidBytes[10] = hashBytes[1];
        guidBytes[11] = hashBytes[2];
    }

    /// <summary>
    /// Reads 5-byte MAC hash from guid byte positions 5-6 and 9-11.
    /// </summary>
    private static void ReadMacFromGuid(ReadOnlySpan<byte> guidBytes, Span<byte> hashBytes)
    {
        hashBytes[3] = guidBytes[5];
        hashBytes[4] = guidBytes[6];
        hashBytes[0] = guidBytes[9];
        hashBytes[1] = guidBytes[10];
        hashBytes[2] = guidBytes[11];
    }

    /// <summary>
    /// Zeros the MAC slots (5-6 and 9-11) in the guid bytes for MAC re-computation.
    /// </summary>
    private static void ClearMacSlots(Span<byte> guidBytes)
    {
        guidBytes[5] = 0;
        guidBytes[6] = 0;
        guidBytes[9] = 0;
        guidBytes[10] = 0;
        guidBytes[11] = 0;
    }

    /// <summary>
    /// Encrypts all 16 guid bytes in-place using AES-256-ECB (single-block PRP).
    /// For a single 128-bit block, ECB is mathematically identical to CBC with a zero IV
    /// (C = AES(Key, P ⊕ 0) = AES(Key, P)), but avoids the IV allocation and XOR overhead.
    /// ECB's known weakness (identical blocks → identical ciphertexts) does not apply here
    /// because there is only one block per encryption call.
    /// </summary>
    private static void EncryptGuidBlock(Span<byte> guidBytes, Aes aes)
    {
        Span<byte> output = stackalloc byte[16];
        aes.EncryptEcb(guidBytes, output, PaddingMode.None);
        output.CopyTo(guidBytes);
    }

    /// <summary>
    /// Decrypts all 16 guid bytes in-place using AES-256-ECB (single-block PRP inverse).
    /// See <see cref="EncryptGuidBlock"/> for rationale on ECB vs CBC for single-block operations.
    /// </summary>
    private static void DecryptGuidBlock(Span<byte> guidBytes, Aes aes)
    {
        Span<byte> output = stackalloc byte[16];
        aes.DecryptEcb(guidBytes, output, PaddingMode.None);
        output.CopyTo(guidBytes);
    }

    private static Aes CreateAes(BinaryData key)
    {
        var keyBytes = key.ToArray();
        if (keyBytes.Length != 32)
            throw new ArgumentException(
                $"AES-256-ECB requires a 32-byte key but received {keyBytes.Length} bytes. " +
                $"Verify that AlternativeKeyAsBinary produces a 256-bit key.",
                nameof(key));

        var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        aes.Key = keyBytes;

        return aes;
    }

    public void Dispose() => _aes.Dispose();
}