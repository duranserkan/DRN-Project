using System.Buffers.Binary;
using System.Security.Cryptography;
using Blake3;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Numbers;
using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Utils.Ids;

/// <summary>
/// Embeds a keyed hash, an 8‑bit entity‑type, 16‑bit GUID markers, and a 64‑bit ID into a
/// 128‑bit GUID, providing a reversible mapping with integrity checking.
/// Secure variants encrypt the entire 16-byte GUID using AES-256-ECB (true PRP) for confidentiality.
/// </summary>
public interface ISourceKnownEntityIdUtils : ISourceKnownEntityIdOperations
{
    /// <summary>Dispatches to <c>GenerateSecure</c> or <c>GenerateUnsecure</c> based on <c>AppSettings.NexusAppSettings.UseSecureSourceKnownIds</c>.</summary>
    SourceKnownEntityId Generate<TEntity>(long id) where TEntity : SourceKnownEntity;

    /// <inheritdoc cref="Generate{TEntity}(long)"/>
    SourceKnownEntityId Generate(SourceKnownEntity entity);

    /// <inheritdoc cref="Generate{TEntity}(long)"/>
    new SourceKnownEntityId Generate(long id, byte entityType);

    SourceKnownEntityId GenerateSecure<TEntity>(long id) where TEntity : SourceKnownEntity;
    SourceKnownEntityId GenerateSecure(SourceKnownEntity entity);
    SourceKnownEntityId GenerateSecure(long id, byte entityType);

    SourceKnownEntityId GenerateUnsecure<TEntity>(long id) where TEntity : SourceKnownEntity;
    SourceKnownEntityId GenerateUnsecure(SourceKnownEntity entity);
    SourceKnownEntityId GenerateUnsecure(long id, byte entityType);

    SourceKnownEntityId? Parse(Guid? entityId);
    new SourceKnownEntityId Parse(Guid entityId);

    SourceKnownEntityId? Validate(Guid? entityId, byte entityType);
    SourceKnownEntityId Validate(Guid entityId, byte entityType);

    SourceKnownEntityId? Validate<TEntity>(Guid? entityId) where TEntity : SourceKnownEntity;
    SourceKnownEntityId Validate<TEntity>(Guid entityId) where TEntity : SourceKnownEntity;

    new SourceKnownEntityId ToSecure(SourceKnownEntityId id);
    SourceKnownEntityId? ToSecure(SourceKnownEntityId? id);
    new SourceKnownEntityId ToUnsecure(SourceKnownEntityId id);
    SourceKnownEntityId? ToUnsecure(SourceKnownEntityId? id);
}

/// <summary>
/// Embeds a keyed hash, an 8‑bit entity‑type, 16‑bit GUID markers, and a 64‑bit ID into a
/// 128‑bit GUID, providing a reversible mapping with integrity checking.
/// Secure variants encrypt the entire 16-byte GUID using AES-256-ECB as a pseudo-random permutation (PRP).
/// AES-ECB on a single 128-bit block is a conjectured PRP (NIST FIPS 197) — no nonce required, no nonce-reuse vulnerability.
/// Parse auto-detects by trying AES-ECB decryption when plaintext markers are absent.
/// Add rate limit to endpoints that accept SourceKnownEntityId from untrusted sources to prevent brute force attacks.
/// Optionally, add randomness to instance id generation to improve brute force durability.
/// If still not enough, don't use source known ids, consider using a different id generation strategy such as true guid v4.
/// </summary>
[Singleton<ISourceKnownEntityIdUtils>]
public sealed class SourceKnownEntityIdUtils(IAppSettings appSettings, ISourceKnownIdUtils sourceKnownIdUtils) : ISourceKnownEntityIdUtils, IDisposable
{
    //0-3 Source Known Id first half
    //4 entity type
    //5 epoch
    //6 is reserved for MAC hash along with 9, 10, 11
    //7-8 Source Known Id version & variant marker — required for UUID V8 RFC 9562 §5.8 compliance; also contributes to the integrity check
    //9, 10 and 11 are reserved for MAC hashing along with 6
    //12-15 Source Known Id second half
    private const byte GuidLength = 16;

    private const byte EntityIdFirstHalfOffset = 0;
    private const byte EntityIdFirstHalfLength = 4; // 0-3

    private const byte EntityTypeIndex = 4; //4
    private const byte InvalidEntityType = byte.MaxValue;

    //5th index is reserved for epoch, first epoch starts at 2025-01-01.
    //Each epoch is approximately 68 years long with 2 halves separated with sign bit
    //2^30 seconds * 2^1 epoch half flag in source known id timestamp.
    //5th index was initially reserved for MAC hash but with Secure Source Known id's this byte is repurposed for epoch usage
    //With epoch support, source known ids can address ~17,420 monotonic time years starting from 2025-01-01
    //todo handle epoch management (not urgent for next 60 years)

    private const byte MacHashLength = 4;
    private const byte MacHashFirstIndex = 6;
    private const byte MacHashSecondIndex = 9;
    private const byte MacHashThirdIndex = 10;
    private const byte MacHashFourthIndex = 11;

    //8D8D mark — plaintext markers used for non-secure variant and inside the encrypted block for secure variant
    //Ensures that the source-known entityId is easily identifiable by humans and UUID V8 compatible (RFC 9562 §5.8)
    private const byte SourceKnownMarkerVersionIndex = 7;
    private const byte SourceKnownMarkerVariantIndex = 8;
    private const byte SourceKnownMarkerVersionByte = 0x8D; //7 | V8 => UUID V8 per RFC 9562 §5.8
    private const byte SourceKnownMarkerVariantByte = 0x8D; //8 | Variant RFC 9562 §4.1
    private const byte SourceKnownMarkerVariantMaxByte = 0xBF; //8 | Variant RFC 9562 §4.1 upper bound (collision guard)

    private const byte EntityIdSecondHalfOffset = 12;
    private const byte EntityIdSecondHalfLength = 4; // 12-15

    // Key separation: KeyAsBinary and AlternativeKeyAsBinary are cryptographically independent keys from the same keyring entry.
    // KeyAsBinary → BLAKE3 keyed MAC (integrity). AlternativeKeyAsBinary → AES-256-ECB (confidentiality).
    private readonly BinaryData _defaultMacKey = appSettings.NexusAppSettings.GetDefaultMacKey().KeyAsBinary; // todo add keyring rotation
    private readonly bool _useSecure = appSettings.NexusAppSettings.UseSecureSourceKnownIds;

    // Singleton — DI container disposes at shutdown.
    // Stress tested with SourceKnownEntityIdUtilsTests
    // EncryptEcb/DecryptEcb are stateless single-block ops safe for concurrent use.
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
        Span<byte> hashBytes = stackalloc byte[MacHashLength];
        Span<byte> guidBytes = stackalloc byte[GuidLength]; // Allocate 16 bytes on the stack for the GUID
        guidBytes.Clear();

        WriteIdAndMarkers(guidBytes, id, entityType, SourceKnownMarkerVariantByte);
        ComputeMac(guidBytes, hashBytes, _defaultMacKey);
        WriteMacToGuid(guidBytes, hashBytes);

        var entityId = new Guid(guidBytes);
        var sourceKnownId = sourceKnownIdUtils.Parse(id);

        return new SourceKnownEntityId(sourceKnownId, entityId, entityType, true, Secure: false);
    }

    public SourceKnownEntityId GenerateSecure<TEntity>(long id) where TEntity : SourceKnownEntity => GenerateSecure(id, SourceKnownEntity.GetEntityType<TEntity>());
    public SourceKnownEntityId GenerateSecure(SourceKnownEntity entity) => GenerateSecure(entity.Id, SourceKnownEntity.GetEntityType(entity));

    public SourceKnownEntityId GenerateSecure(long id, byte entityType)
    {
        Span<byte> hashBytes = stackalloc byte[MacHashLength];
        Span<byte> guidBytes = stackalloc byte[GuidLength];
        guidBytes.Clear();

        // Build guid identically to non-secure (same layout, same markers)
        var variantByte = SourceKnownMarkerVariantByte;
        WriteIdAndMarkers(guidBytes, id, entityType, variantByte);
        ComputeMac(guidBytes, hashBytes, _defaultMacKey);
        WriteMacToGuid(guidBytes, hashBytes);

        // Encrypt entire 16-byte block with AES-ECB (true PRP — no nonce needed)
        EncryptGuidBlock(guidBytes, _aes);

        // Collision guard: if ciphertext coincidentally has unsecure markers (0x8D8D, ~1/65536)
        // AND the MAC extracted from ciphertext matches a recomputed MAC (~1/2^32),
        // the parse path would misclassify this Secure SKEID as Unsecure.
        // Fix: iterate variant bytes 0x8E→0xBF — AES avalanche guarantees distinct ciphertext per variant.
        // Deterministic termination: at most 51 iterations. Exhaustion throws JackpotException.
        if (HasValidMarkers(guidBytes) && HasCoincidentalMacMatch(guidBytes))
        {
            for (variantByte = SourceKnownMarkerVariantByte + 1;
                 variantByte <= SourceKnownMarkerVariantMaxByte;
                 variantByte++)
            {
                guidBytes.Clear();
                hashBytes.Clear();
                WriteIdAndMarkers(guidBytes, id, entityType, variantByte);
                ComputeMac(guidBytes, hashBytes, _defaultMacKey);
                WriteMacToGuid(guidBytes, hashBytes);
                EncryptGuidBlock(guidBytes, _aes);

                if (!HasValidMarkers(guidBytes) || !HasCoincidentalMacMatch(guidBytes))
                    break;
            }

            if (variantByte > SourceKnownMarkerVariantMaxByte)
                throw ExceptionFor.Jackpot(
                    $"All 50 alternative variant bytes " +
                    $"(0x{SourceKnownMarkerVariantByte + 1:X2}–0x{SourceKnownMarkerVariantMaxByte:X2}) exhausted " +
                    $"for id={id}, entityType={entityType}. Probability: ~1/2^(48×51).");
        }

        var entityId = new Guid(guidBytes);
        var sourceKnownId = sourceKnownIdUtils.Parse(id);

        return new SourceKnownEntityId(sourceKnownId, entityId, entityType, true, Secure: true);
    }

    public SourceKnownEntityId? Parse(Guid? entityId) => entityId.HasValue ? Parse(entityId.Value) : null;

    public SourceKnownEntityId Parse(Guid entityId)
    {
        Span<byte> guidBytes = stackalloc byte[GuidLength];
        entityId.TryWriteBytes(guidBytes);

        // Try non-secure path first (plaintext 8D8D markers visible)
        if (HasValidMarkers(guidBytes))
        {
            var result = VerifyAndParse(guidBytes, entityId, secure: false);
            if (result.Valid)
                return result;

            // Markers were coincidental in ciphertext (~1/65536) — restore and try decrypt path
            // Interestingly, when 6,291,453 ids generated around 96 entities (92, 101 etc.) had coincidental markers
            // Tested with DRN.Test.Unit/Tests/Framework/Utils/Ids/SourceKnownEntityIdUtilsTests.cs
            entityId.TryWriteBytes(guidBytes);
        }

        // Try secure path: AES-ECB decrypt full block, then check for markers
        // HasValidMarkersSecure accepts RFC 9562 §4.1 variant range 0x80–0xBF (collision guard uses 0x8D–0xBF)
        DecryptGuidBlock(guidBytes, _aes);

        if (!HasValidMarkersSecure(guidBytes)) 
            return CreateInvalid(entityId);
        
        var recoveredVariant = guidBytes[SourceKnownMarkerVariantIndex];

        // Non-default variant → backward collision-guard verification
        if (recoveredVariant is > SourceKnownMarkerVariantByte and <= SourceKnownMarkerVariantMaxByte)
        {
            if (!VerifyCollisionGuardProof(guidBytes, (byte)(recoveredVariant - 1)))
                return CreateInvalid(entityId);
        }
        else if (recoveredVariant != SourceKnownMarkerVariantByte)
        {
            return CreateInvalid(entityId);
        }

        return VerifyAndParse(guidBytes, entityId, secure: true);
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

    public SourceKnownEntityId ToSecure(SourceKnownEntityId id)
    {
        id.ValidateId();
        return id.Secure ? id : GenerateSecure(id.Source.Id, id.EntityType);
    }

    public SourceKnownEntityId? ToSecure(SourceKnownEntityId? id)
        => id.HasValue ? ToSecure(id.Value) : null;

    public SourceKnownEntityId ToUnsecure(SourceKnownEntityId id)
    {
        id.ValidateId();
        return id.Secure ? GenerateUnsecure(id.Source.Id, id.EntityType) : id;
    }

    public SourceKnownEntityId? ToUnsecure(SourceKnownEntityId? id)
        => id.HasValue ? ToUnsecure(id.Value) : null;

    private static SourceKnownEntityId CreateInvalid(Guid entityId) => new(default, entityId, InvalidEntityType, false, Secure: false);

    private static bool HasValidMarkers(ReadOnlySpan<byte> guidBytes)
        => guidBytes[SourceKnownMarkerVersionIndex] == SourceKnownMarkerVersionByte
           && guidBytes[SourceKnownMarkerVariantIndex] == SourceKnownMarkerVariantByte;

    /// <summary>
    /// Accepts the primary variant (0x8D) and any RFC 9562 §4.1 variant byte (0x80–0xBF).
    /// Used only on the post-decryption (secure) path — non-default variant bytes are
    /// produced by the collision guard in <see cref="GenerateSecure(long, byte)"/>.
    /// </summary>
    private static bool HasValidMarkersSecure(ReadOnlySpan<byte> guidBytes)
        => guidBytes[SourceKnownMarkerVersionIndex] == SourceKnownMarkerVersionByte
           && (guidBytes[SourceKnownMarkerVariantIndex] & 0xC0) == 0x80;

    /// <summary>
    /// Checks whether ciphertext bytes, when interpreted as an unsecure SKEID,
    /// produce a coincidental MAC match. Used by the collision guard in
    /// <see cref="GenerateSecure(long, byte)"/> to detect the ~1/2^48 edge case.
    /// </summary>
    private bool HasCoincidentalMacMatch(ReadOnlySpan<byte> ciphertextBytes)
    {
        Span<byte> actualHash = stackalloc byte[MacHashLength];
        Span<byte> expectedHash = stackalloc byte[MacHashLength];
        Span<byte> workingCopy = stackalloc byte[GuidLength];
        ciphertextBytes.CopyTo(workingCopy);

        ReadMacFromGuid(workingCopy, actualHash);
        ClearMacSlots(workingCopy);
        ComputeMac(workingCopy, expectedHash, _defaultMacKey);

        return expectedHash.SequenceEqual(actualHash);
    }

    /// <summary>
    /// Verifies MAC integrity and extracts ID/entityType from a plaintext (or decrypted) guid byte span.
    /// </summary>
    private SourceKnownEntityId VerifyAndParse(Span<byte> guidBytes, Guid entityId, bool secure)
    {
        Span<byte> actualHashBytes = stackalloc byte[MacHashLength];
        Span<byte> expectedHashBytes = stackalloc byte[MacHashLength];

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

        return expectedHashBytes.SequenceEqual(actualHashBytes)
            ? new SourceKnownEntityId(sourceKnownIdUtils.Parse(id), entityId, entityType, true, secure)
            : CreateInvalid(entityId);
    }

    /// <summary>
    /// Backward collision verification: reconstructs the SKEID with (variant−1),
    /// encrypts, and checks whether ciphertext would have triggered the collision guard.
    /// Returns true if the previous variant genuinely collided (legitimate escalation).
    /// Called only for non-default variant bytes (recoveredVariant > 0x8D) during Parse.
    /// </summary>
    private bool VerifyCollisionGuardProof(ReadOnlySpan<byte> decryptedBytes, byte previousVariant)
    {
        var idFirstHalf = BinaryPrimitives.ReadUInt32LittleEndian(
            decryptedBytes.Slice(EntityIdFirstHalfOffset, EntityIdFirstHalfLength));
        var idSecondHalf = BinaryPrimitives.ReadUInt32LittleEndian(
            decryptedBytes.Slice(EntityIdSecondHalfOffset, EntityIdSecondHalfLength));
        var entityType = decryptedBytes[EntityTypeIndex];

        var idBuilder = NumberBuilder.GetLongUnsigned();
        idBuilder.TryAddUInt(idFirstHalf);
        idBuilder.TryAddUInt(idSecondHalf);
        var id = (long)idBuilder.GetValue();

        // Reconstruct with previous variant
        Span<byte> hashBytes = stackalloc byte[MacHashLength];
        Span<byte> reconstructed = stackalloc byte[GuidLength];
        reconstructed.Clear();

        WriteIdAndMarkers(reconstructed, id, entityType, previousVariant);
        ComputeMac(reconstructed, hashBytes, _defaultMacKey);
        WriteMacToGuid(reconstructed, hashBytes);
        EncryptGuidBlock(reconstructed, _aes);

        // Previous variant's ciphertext must have collided (markers + MAC match)
        return HasValidMarkers(reconstructed) && HasCoincidentalMacMatch(reconstructed);
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
        //5 is reserved for epoch, 6 is reserved for MAC hash
        guidBytes[SourceKnownMarkerVersionIndex] = SourceKnownMarkerVersionByte; //7
        guidBytes[SourceKnownMarkerVariantIndex] = variantByte; //8
        //9, 10 and 11 are reserved for MAC hash along with 6
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
    /// Writes 4-byte MAC hash into guid byte positions 6, 9, 10, 11.
    /// </summary>
    private static void WriteMacToGuid(Span<byte> guidBytes, ReadOnlySpan<byte> hashBytes)
    {
        guidBytes[MacHashFirstIndex] = hashBytes[0];
        guidBytes[MacHashSecondIndex] = hashBytes[1];
        guidBytes[MacHashThirdIndex] = hashBytes[2];
        guidBytes[MacHashFourthIndex] = hashBytes[3];
    }

    /// <summary>
    /// Reads 4-byte MAC hash from guid byte positions 6, 9, 10, 11.
    /// </summary>
    private static void ReadMacFromGuid(ReadOnlySpan<byte> guidBytes, Span<byte> hashBytes)
    {
        hashBytes[0] = guidBytes[MacHashFirstIndex];
        hashBytes[1] = guidBytes[MacHashSecondIndex];
        hashBytes[2] = guidBytes[MacHashThirdIndex];
        hashBytes[3] = guidBytes[MacHashFourthIndex];
    }

    /// <summary>
    /// Zeros the MAC slots (6 and 9-11) in the guid bytes for MAC re-computation.
    /// </summary>
    private static void ClearMacSlots(Span<byte> guidBytes)
    {
        guidBytes[MacHashFirstIndex] = 0;
        guidBytes[MacHashSecondIndex] = 0;
        guidBytes[MacHashThirdIndex] = 0;
        guidBytes[MacHashFourthIndex] = 0;
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
        Span<byte> output = stackalloc byte[GuidLength];
        aes.EncryptEcb(guidBytes, output, PaddingMode.None);
        output.CopyTo(guidBytes);
    }

    /// <summary>
    /// Decrypts all 16 guid bytes in-place using AES-256-ECB (single-block PRP inverse).
    /// See <see cref="EncryptGuidBlock"/> for rationale on ECB vs CBC for single-block operations.
    /// </summary>
    private static void DecryptGuidBlock(Span<byte> guidBytes, Aes aes)
    {
        Span<byte> output = stackalloc byte[GuidLength];
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