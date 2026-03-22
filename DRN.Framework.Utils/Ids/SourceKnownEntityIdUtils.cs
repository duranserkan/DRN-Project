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
    /// <summary>Dispatches to <c>GenerateSecure</c> or <c>GeneratePlain</c> based on <c>AppSettings.NexusAppSettings.UseSecureSourceKnownIds</c>.</summary>
    SourceKnownEntityId Generate<TEntity>(long id) where TEntity : SourceKnownEntity;

    /// <inheritdoc cref="Generate{TEntity}(long)"/>
    SourceKnownEntityId Generate(SourceKnownEntity entity);

    /// <inheritdoc cref="Generate{TEntity}(long)"/>
    new SourceKnownEntityId Generate(long id, byte entityType);

    SourceKnownEntityId GenerateSecure<TEntity>(long id) where TEntity : SourceKnownEntity;
    SourceKnownEntityId GenerateSecure(SourceKnownEntity entity);
    SourceKnownEntityId GenerateSecure(long id, byte entityType);

    SourceKnownEntityId GeneratePlain<TEntity>(long id) where TEntity : SourceKnownEntity;
    SourceKnownEntityId GeneratePlain(SourceKnownEntity entity);
    SourceKnownEntityId GeneratePlain(long id, byte entityType);

    SourceKnownEntityId? Parse(Guid? entityId);
    new SourceKnownEntityId Parse(Guid entityId);

    SourceKnownEntityId? Validate(Guid? entityId, byte entityType);
    SourceKnownEntityId Validate(Guid entityId, byte entityType);

    SourceKnownEntityId? Validate<TEntity>(Guid? entityId) where TEntity : SourceKnownEntity;
    SourceKnownEntityId Validate<TEntity>(Guid entityId) where TEntity : SourceKnownEntity;

    new SourceKnownEntityId ToSecure(SourceKnownEntityId id);
    SourceKnownEntityId? ToSecure(SourceKnownEntityId? id);
    new SourceKnownEntityId ToPlain(SourceKnownEntityId id);
    SourceKnownEntityId? ToPlain(SourceKnownEntityId? id);
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
    // RFC 9562 V8 big-endian byte layout (network byte order):
    // 0    epoch
    // 1-4  SKID upper half (sign-toggled, big-endian) — epoch half + timestamp
    // 5    SKID lower half byte 0 (MSB) — timestamp LSB + appId MSBs
    // 6    version marker 0x8D — RFC 9562 §5.8 octet 6 (UUID V8)
    // 7    entity type
    // 8    variant marker 0x8D — RFC 9562 §4.1 octet 8
    // 9-11 SKID lower half bytes 1-3 — appId/appInstanceId/sequence
    // 12-15 BLAKE3 keyed MAC (contiguous)
    //
    // Sign-bit toggle: SKID uses signed comparison (negative < positive), but UUID byte comparison
    // is unsigned (0x00 < 0x80). XOR the MSB of the upper half with 0x80000000 on write/read to
    // convert between signed sort order and unsigned lexicographic order.
    // First half (negative SKIDs, 0x80..→0xFF..) → 0x00..→0x7F.. (sorts first)
    // Second half (positive SKIDs, 0x00..→0x7F..) → 0x80..→0xFF.. (sorts second)
    //
    // .NET Guid uses mixed endianness internally (Data1/Data2/Data3 little-endian, Data4 sequential),
    // but Guid(ReadOnlySpan<byte>, bigEndian: true) and ToByteArray(bigEndian: true) provide consistent
    // big-endian byte order for cross-platform RFC 9562 compliance.
    private const byte GuidLength = 16;
    private const uint SignBitToggle = 0x80000000; // XOR mask for signed↔unsigned sort-order conversion

    private const byte EpochIndex = 0; // 0

    private const byte EntityIdUpperHalfOffset = 1;
    private const byte EntityIdUpperHalfLength = 4; // 1-4

    private const byte EntityTypeIndex = 7; // 7
    private const byte InvalidEntityType = byte.MaxValue;

    // Epoch at byte 0, first epoch starts at 2025-01-01.
    // Each epoch is approximately 68 years long with 2 halves separated with sign bit
    // 2^32 ticks / 4 ticks/s = 2^30 seconds * 2^1 epoch half flag in source known id timestamp.
    // With epoch support, source known ids can address ~17,421 monotonic time years starting from 2025-01-01
    //todo handle epoch management (not urgent for next 60 years)

    private const byte MacHashLength = 4;
    private const byte MacHashOffset = 12; // 12-15 (contiguous)

    // 8D8D mark — plaintext markers used for non-secure variant and inside the encrypted block for secure variant
    // Ensures that the source-known entityId is easily identifiable by humans and UUID V8 compatible (RFC 9562 §5.8)
    // Version at RFC 9562 octet 6, variant at RFC 9562 octet 8 — standard positions in big-endian layout
    private const byte SourceKnownMarkerVersionIndex = 6;
    private const byte SourceKnownMarkerVariantIndex = 8;
    private const byte SourceKnownMarkerVersionByte = 0x8D; //6 | V8 => UUID V8 per RFC 9562 §5.8
    private const byte SourceKnownMarkerVariantByte = 0x8D; //8 | Variant RFC 9562 §4.1
    private const byte SourceKnownMarkerVariantMaxByte = 0xBF; //8 | Variant RFC 9562 §4.1 upper bound (collision guard)

    // SKID lower half is split around the variant marker:
    // byte 5 = MSB of lower half, bytes 9-11 = remaining 3 bytes
    private const byte EntityIdLowerByte0Index = 5;
    private const byte EntityIdLowerBytes123Offset = 9; // 9-11

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
        => _useSecure ? GenerateSecure(id, entityType) : GeneratePlain(id, entityType);

    public SourceKnownEntityId GeneratePlain<TEntity>(long id) where TEntity : SourceKnownEntity => GeneratePlain(id, SourceKnownEntity.GetEntityType<TEntity>());
    public SourceKnownEntityId GeneratePlain(SourceKnownEntity entity) => GeneratePlain(entity.Id, SourceKnownEntity.GetEntityType(entity));

    public SourceKnownEntityId GeneratePlain(long id, byte entityType)
    {
        Span<byte> hashBytes = stackalloc byte[MacHashLength];
        Span<byte> guidBytes = stackalloc byte[GuidLength]; // Allocate 16 bytes on the stack for the GUID
        guidBytes.Clear();

        WriteIdAndMarkers(guidBytes, id, entityType, SourceKnownMarkerVariantByte);
        ComputeMac(guidBytes, hashBytes, _defaultMacKey);
        WriteMacToGuid(guidBytes, hashBytes);

        var entityId = new Guid(guidBytes, bigEndian: true);
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

        // Collision guard: if ciphertext coincidentally has plain markers (0x8D8D, ~1/65536)
        // AND the MAC extracted from ciphertext matches a recomputed MAC (~1/2^32),
        // the parse path would misclassify this Secure SKEID as Plain.
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

        var entityId = new Guid(guidBytes, bigEndian: true);
        var sourceKnownId = sourceKnownIdUtils.Parse(id);

        return new SourceKnownEntityId(sourceKnownId, entityId, entityType, true, Secure: true);
    }

    public SourceKnownEntityId? Parse(Guid? entityId) => entityId.HasValue ? Parse(entityId.Value) : null;

    public SourceKnownEntityId Parse(Guid entityId)
    {
        Span<byte> guidBytes = stackalloc byte[GuidLength];
        entityId.TryWriteBytes(guidBytes, bigEndian: true, out _);

        // Try non-secure path first (plaintext 8D8D markers visible)
        if (HasValidMarkers(guidBytes))
        {
            var result = VerifyAndParse(guidBytes, entityId, secure: false);
            if (result.Valid)
                return result;

            // Markers were coincidental in ciphertext (~1/65536) — restore and try decrypt path
            // Interestingly, when 6,291,453 ids generated around 96 entities (92, 101 etc.) had coincidental markers
            entityId.TryWriteBytes(guidBytes, bigEndian: true, out _);
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

    public SourceKnownEntityId ToPlain(SourceKnownEntityId id)
    {
        id.ValidateId();
        return id.Secure ? GeneratePlain(id.Source.Id, id.EntityType) : id;
    }

    public SourceKnownEntityId? ToPlain(SourceKnownEntityId? id)
        => id.HasValue ? ToPlain(id.Value) : null;

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
    /// Checks whether ciphertext bytes, when interpreted as an plain SKEID,
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
    /// Reads the SKID upper half from bytes 1–4 (big-endian, sign-toggled) and the split lower half
    /// from byte 5 + bytes 9–11 (big-endian). XOR untoggle restores the original signed representation.
    /// </summary>
    private SourceKnownEntityId VerifyAndParse(Span<byte> guidBytes, Guid entityId, bool secure)
    {
        Span<byte> actualHashBytes = stackalloc byte[MacHashLength];
        Span<byte> expectedHashBytes = stackalloc byte[MacHashLength];

        ReadMacFromGuid(guidBytes, actualHashBytes);

        // Epoch at byte 0 — covered by MAC; todo: consume in multi-epoch parsing

        // Read upper half (big-endian) and untoggle sign bit for signed sort-order restoration
        var idUpperHalf = BinaryPrimitives.ReadUInt32BigEndian(guidBytes.Slice(EntityIdUpperHalfOffset, EntityIdUpperHalfLength)) ^ SignBitToggle;

        // Read split lower half: byte 5 (MSB) + bytes 9-11
        var idLowerHalf = ((uint)guidBytes[EntityIdLowerByte0Index] << 24)
                        | ((uint)guidBytes[EntityIdLowerBytes123Offset] << 16)
                        | ((uint)guidBytes[EntityIdLowerBytes123Offset + 1] << 8)
                        | guidBytes[EntityIdLowerBytes123Offset + 2];

        var entityType = guidBytes[EntityTypeIndex];

        var idBuilder = NumberBuilder.GetLongUnsigned();
        idBuilder.TryAddUInt(idUpperHalf);
        idBuilder.TryAddUInt(idLowerHalf);
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
        // Read upper half (big-endian) and untoggle sign bit
        var idUpperHalf = BinaryPrimitives.ReadUInt32BigEndian(
            decryptedBytes.Slice(EntityIdUpperHalfOffset, EntityIdUpperHalfLength)) ^ SignBitToggle;

        // Read split lower half: byte 5 (MSB) + bytes 9-11
        var idLowerHalf = ((uint)decryptedBytes[EntityIdLowerByte0Index] << 24)
                        | ((uint)decryptedBytes[EntityIdLowerBytes123Offset] << 16)
                        | ((uint)decryptedBytes[EntityIdLowerBytes123Offset + 1] << 8)
                        | decryptedBytes[EntityIdLowerBytes123Offset + 2];

        var entityType = decryptedBytes[EntityTypeIndex];

        var idBuilder = NumberBuilder.GetLongUnsigned();
        idBuilder.TryAddUInt(idUpperHalf);
        idBuilder.TryAddUInt(idLowerHalf);
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
    /// Writes epoch, ID halves, entity type, version marker, and variant marker into the guid byte span
    /// using RFC 9562 big-endian layout. The upper half MSB is toggled (XOR 0x80000000) so that
    /// the signed SKID chronological order maps to unsigned lexicographic byte order.
    /// The lower half is split: byte 5 (MSB) + bytes 9-11 (remaining), around the markers at bytes 6-8.
    /// </summary>
    private static void WriteIdAndMarkers(Span<byte> guidBytes, long id, byte entityType, byte variantByte)
    {
        var idParser = NumberParser.Get((ulong)id);
        var upperHalf = idParser.ReadUInt();
        var lowerHalf = idParser.ReadUInt();

        guidBytes[EpochIndex] = 0; // Epoch 0 (todo: parameterize for epoch management)
        BinaryPrimitives.WriteUInt32BigEndian(guidBytes.Slice(EntityIdUpperHalfOffset, EntityIdUpperHalfLength), upperHalf ^ SignBitToggle); // 1-4 sign-toggled
        guidBytes[EntityIdLowerByte0Index] = (byte)(lowerHalf >> 24); // 5 — SKID lower half MSB (timestamp LSB + appId MSBs)
        guidBytes[SourceKnownMarkerVersionIndex] = SourceKnownMarkerVersionByte; // 6
        guidBytes[EntityTypeIndex] = entityType; // 7

        // Split lower half (big-endian): byte 5 = MSB, bytes 9-11 = remaining
        guidBytes[SourceKnownMarkerVariantIndex] = variantByte; // 8
        guidBytes[EntityIdLowerBytes123Offset] = (byte)(lowerHalf >> 16); // 9
        guidBytes[EntityIdLowerBytes123Offset + 1] = (byte)(lowerHalf >> 8); // 10
        guidBytes[EntityIdLowerBytes123Offset + 2] = (byte)lowerHalf; // 11
        // 12-15 reserved for MAC
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
    /// Writes 4-byte MAC hash into guid byte positions 12-15 (contiguous).
    /// </summary>
    private static void WriteMacToGuid(Span<byte> guidBytes, ReadOnlySpan<byte> hashBytes)
    {
        hashBytes[..MacHashLength].CopyTo(guidBytes[MacHashOffset..]);
    }

    /// <summary>
    /// Reads 4-byte MAC hash from guid byte positions 12-15 (contiguous).
    /// </summary>
    private static void ReadMacFromGuid(ReadOnlySpan<byte> guidBytes, Span<byte> hashBytes)
    {
        guidBytes[MacHashOffset..(MacHashOffset + MacHashLength)].CopyTo(hashBytes);
    }

    /// <summary>
    /// Zeros the MAC slots (bytes 12-15) in the guid bytes for MAC re-computation.
    /// </summary>
    private static void ClearMacSlots(Span<byte> guidBytes)
    {
        guidBytes[MacHashOffset..(MacHashOffset + MacHashLength)].Clear();
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