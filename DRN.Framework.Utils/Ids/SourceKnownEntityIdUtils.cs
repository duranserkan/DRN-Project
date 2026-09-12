using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Blake3;
using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.Data.Encryption;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Utils.Ids;

/// <summary>
/// Embeds a keyed hash, an 8‑bit entity‑type, 16‑bit GUID markers, and a 64‑bit ID into a
/// 128‑bit GUID, providing a reversible mapping with integrity checking.
/// Secure variants encrypt the entire 16-byte GUID using AES-256-ECB (true PRP) for confidentiality.
/// </summary>
public interface ISourceKnownEntityIdUtils : ISourceKnownEntityIdOperations
{
    /// <summary>Generates a new ID via <see cref="ISourceKnownIdUtils.Next{TEntity}()"/> and dispatches to <c>GenerateSecure</c> or <c>GeneratePlain</c> based on <c>AppSettings.NexusAppSettings.UseSecureSourceKnownIds</c>.</summary>
    SourceKnownEntityId Generate<TEntity>() where TEntity : SourceKnownEntity;

    /// <summary>Dispatches to <c>GenerateSecure</c> or <c>GeneratePlain</c> based on <c>AppSettings.NexusAppSettings.UseSecureSourceKnownIds</c>.</summary>
    SourceKnownEntityId Generate<TEntity>(long id) where TEntity : SourceKnownEntity;

    /// <inheritdoc cref="Generate{TEntity}(long)"/>
    SourceKnownEntityId Generate(SourceKnownEntity entity);

    /// <summary>Generates a new ID via <see cref="ISourceKnownIdUtils.Next{TEntity}()"/> and produces a Secure <see cref="SourceKnownEntityId"/>.</summary>
    SourceKnownEntityId GenerateSecure<TEntity>() where TEntity : SourceKnownEntity;

    SourceKnownEntityId GenerateSecure<TEntity>(long id) where TEntity : SourceKnownEntity;
    SourceKnownEntityId GenerateSecure(SourceKnownEntity entity);
    SourceKnownEntityId GenerateSecure(long id, EntityTypeId entityTypeId);

    /// <summary>Generates a new ID via <see cref="ISourceKnownIdUtils.Next{TEntity}()"/> and produces a Plain <see cref="SourceKnownEntityId"/>.</summary>
    SourceKnownEntityId GeneratePlain<TEntity>() where TEntity : SourceKnownEntity;

    SourceKnownEntityId GeneratePlain<TEntity>(long id) where TEntity : SourceKnownEntity;
    SourceKnownEntityId GeneratePlain(SourceKnownEntity entity);
    SourceKnownEntityId GeneratePlain(long id, EntityTypeId entityTypeId);

    /// <summary>Parses the selected format; null format uses UseSecureSourceKnownIds. Null input remains null.</summary>
    new SourceKnownEntityId? Parse(Guid? entityId, SourceKnownEntityIdFormat? format = null);
    /// <summary>Parses the selected format; null format uses UseSecureSourceKnownIds.</summary>
    new SourceKnownEntityId Parse(Guid entityId, SourceKnownEntityIdFormat? format = null);

    /// <summary>Validates integrity and entity type against the configured AppId. Null remains null.</summary>
    new SourceKnownEntityId? Validate(Guid? entityId, byte entityType, SourceKnownEntityIdFormat? format = null);
    /// <summary>Validates integrity and entity type against NexusAppSettings.AppId.</summary>
    new SourceKnownEntityId Validate(Guid entityId, byte entityType, SourceKnownEntityIdFormat? format = null);

    /// <summary>Explicitly selects an application partition through IAppId. Null remains null.</summary>
    new SourceKnownEntityId? Validate<TApp>(Guid? entityId, byte entityType, SourceKnownEntityIdFormat? format = null) where TApp : IAppId;
    /// <summary>Validates integrity, entity type, and the explicit TApp.AppId.</summary>
    new SourceKnownEntityId Validate<TApp>(Guid entityId, byte entityType, SourceKnownEntityIdFormat? format = null) where TApp : IAppId;

    new SourceKnownEntityId? Validate(Guid? entityId, EntityTypeId entityTypeId, SourceKnownEntityIdFormat? format = null);
    /// <summary>Validates integrity and both explicitly supplied identity components.</summary>
    new SourceKnownEntityId Validate(Guid entityId, EntityTypeId entityTypeId, SourceKnownEntityIdFormat? format = null);

    new SourceKnownEntityId? Validate<TEntity>(Guid? entityId, SourceKnownEntityIdFormat? format = null) where TEntity : SourceKnownEntity;
    /// <summary>Validates against the entity's declared identity, independently of configured AppId.</summary>
    new SourceKnownEntityId Validate<TEntity>(Guid entityId, SourceKnownEntityIdFormat? format = null) where TEntity : SourceKnownEntity;

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
/// Parse and Validate default to UseSecureSourceKnownIds; explicit formats override configuration.
/// Auto tries AES-ECB decryption when plain epoch/marker checks or MAC verification fail.
/// Add rate limit to endpoints that accept SourceKnownEntityId from untrusted sources to prevent brute force attacks.
/// Optionally, add randomness to instance id generation to improve brute force durability.
/// If still not enough, don't use source known ids, consider using a different id generation strategy such as true guid v4.
/// </summary>
[Singleton<ISourceKnownEntityIdUtils>]
[SuppressMessage("ReSharper", "ForCanBeConvertedToForeach")]
public sealed class SourceKnownEntityIdUtils : ISourceKnownEntityIdUtils, IDisposable
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

    // Epoch at byte 0; the default origin is 2025-01-01, configurable through SourceKnownIdSettings.
    // Each epoch is approximately 68 years long with 2 halves separated with sign bit
    // 2^32 ticks / 4 ticks/s = 2^30 seconds * 2^1 epoch half flag in source known id timestamp.
    // Only encoded epoch 0 is supported. Custom origin parameters do not select a different encoded epoch.

    private const byte MacLength = 4;
    private const byte MacOffset = 12; // 12-15 (contiguous)

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

    // Key separation: MacKey and EncryptionKey are cryptographically independent keys from the same keyring entry.
    // MacKey -> BLAKE3 keyed MAC (integrity). EncryptionKey -> AES-256-ECB (confidentiality).
    private readonly NexusKeyRing _keyRing;
    private readonly Aes256 _aes;
    private readonly SecretKey32 _macKey;
    private readonly bool _useSecure;
    private readonly ISourceKnownIdUtils _sourceKnownIdUtils;
    private readonly byte _appId;


    public SourceKnownEntityIdUtils(IAppSettings appSettings, ISourceKnownIdUtils sourceKnownIdUtils)
    {
        _appId = appSettings.NexusAppSettings.AppId;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(_appId, IAppId.MaxAppId);
        _sourceKnownIdUtils = sourceKnownIdUtils;
        _keyRing = new NexusKeyRing(appSettings.NexusAppSettings);
        _aes = _keyRing.Default.Aes;
        _macKey = _keyRing.Default.MacKey;
        _useSecure = appSettings.NexusAppSettings.UseSecureSourceKnownIds;
    }

    public SourceKnownEntityId Generate<TEntity>() where TEntity : SourceKnownEntity
        => Generate<TEntity>(_sourceKnownIdUtils.Next<TEntity>());

    public SourceKnownEntityId Generate<TEntity>(long id) where TEntity : SourceKnownEntity
        => Generate(id, SourceKnownEntity.GetEntityTypeId<TEntity>());

    public SourceKnownEntityId Generate(SourceKnownEntity entity)
        => Generate(entity.Id, SourceKnownEntity.GetEntityTypeId(entity));

    public SourceKnownEntityId Generate(long id, EntityTypeId entityTypeId)
        => _useSecure ? GenerateSecure(id, entityTypeId) : GeneratePlain(id, entityTypeId);

    public SourceKnownEntityId GeneratePlain<TEntity>() where TEntity : SourceKnownEntity
        => GeneratePlain<TEntity>(_sourceKnownIdUtils.Next<TEntity>());

    public SourceKnownEntityId GeneratePlain<TEntity>(long id) where TEntity : SourceKnownEntity
        => GeneratePlain(id, SourceKnownEntity.GetEntityTypeId<TEntity>());

    public SourceKnownEntityId GeneratePlain(SourceKnownEntity entity)
        => GeneratePlain(entity.Id, SourceKnownEntity.GetEntityTypeId(entity));

    public SourceKnownEntityId GeneratePlain(long id, EntityTypeId entityTypeId)
    {
        var entityId = GeneratePlainGuid(id, entityTypeId.EntityType);
        var sourceKnownId = _sourceKnownIdUtils.Parse(id);
        var result = new SourceKnownEntityId(sourceKnownId, entityId, entityTypeId.EntityType, true, Secure: false);
        result.Validate(entityTypeId);

        return result;
    }

    private Guid GeneratePlainGuid(long id, byte entityType)
    {
        Span<byte> guidBytes = stackalloc byte[GuidLength];

        WriteIdAndMarkers(guidBytes, id, entityType, SourceKnownMarkerVariantByte);
        WriteMac(guidBytes, _macKey);

        return new Guid(guidBytes, bigEndian: true);
    }

    public SourceKnownEntityId GenerateSecure<TEntity>() where TEntity : SourceKnownEntity
        => GenerateSecure<TEntity>(_sourceKnownIdUtils.Next<TEntity>());

    public SourceKnownEntityId GenerateSecure<TEntity>(long id) where TEntity : SourceKnownEntity
        => GenerateSecure(id, SourceKnownEntity.GetEntityTypeId<TEntity>());

    public SourceKnownEntityId GenerateSecure(SourceKnownEntity entity)
        => GenerateSecure(entity.Id, SourceKnownEntity.GetEntityTypeId(entity));

    public SourceKnownEntityId GenerateSecure(long id, EntityTypeId entityTypeId)
    {
        var entityId = GenerateSecureGuid(id, entityTypeId.EntityType);
        var sourceKnownId = _sourceKnownIdUtils.Parse(id);
        var result = new SourceKnownEntityId(sourceKnownId, entityId, entityTypeId.EntityType, true, Secure: true);
        result.Validate(entityTypeId);

        return result;
    }

    private Guid GenerateSecureGuid(long id, byte entityType)
    {
        Span<byte> guidBytes = stackalloc byte[GuidLength];

        // Build guid identically to non-secure (same layout, same markers)
        var variantByte = SourceKnownMarkerVariantByte;
        WriteIdAndMarkers(guidBytes, id, entityType, variantByte);
        WriteMac(guidBytes, _macKey);

        // Encrypt entire 16-byte block with AES-ECB (true PRP — no nonce needed)
        EncryptGuidBlock(guidBytes, _aes);

        // Ciphertext with epoch 0, both 0x8D markers, and a valid MAC would be misclassified as plain.
        // With these three fixed bytes and a 32-bit MAC, collision probability is approximately 2^-56,
        // assuming pseudorandom ciphertext.
        // Retry variants 0x8E through 0xBF; the AES permutation gives distinct ciphertext for each variant.
        // At most 50 retries after the initial candidate. Exhaustion throws JackpotException.
        if (HasValidMarkers(guidBytes) && HasCoincidentalMacMatch(guidBytes, _macKey))
        {
            for (variantByte = SourceKnownMarkerVariantByte + 1; variantByte <= SourceKnownMarkerVariantMaxByte; variantByte++)
            {
                WriteIdAndMarkers(guidBytes, id, entityType, variantByte);
                WriteMac(guidBytes, _macKey);
                EncryptGuidBlock(guidBytes, _aes);

                if (!HasValidMarkers(guidBytes) || !HasCoincidentalMacMatch(guidBytes, _macKey))
                    break;
            }

            if (variantByte > SourceKnownMarkerVariantMaxByte)
                throw ExceptionFor.Jackpot(
                    $"All 50 alternative variant bytes " +
                    $"(0x{SourceKnownMarkerVariantByte + 1:X2}–0x{SourceKnownMarkerVariantMaxByte:X2}) exhausted " +
                    $"for id={id}, entityType={entityType}.");
        }

        return new Guid(guidBytes, bigEndian: true);
    }

    public SourceKnownEntityId? Parse(Guid? entityId, SourceKnownEntityIdFormat? format = null)
    {
        var selectedFormat = ResolveFormat(format);
        return entityId.HasValue ? Parse(entityId.Value, selectedFormat) : null;
    }

    public SourceKnownEntityId Parse(Guid entityId, SourceKnownEntityIdFormat? format = null)
    {
        var selectedFormat = ResolveFormat(format);
        var keys = _keyRing.AllWithDefaultAsFirstItem;
        for (var index = 0; index < keys.Count; index++)
        {
            var result = ParseWithKey(entityId, keys[index], selectedFormat);
            if (result.Valid)
                return result;
        }

        return CreateInvalid(entityId);
    }

    private SourceKnownEntityIdFormat ResolveFormat(SourceKnownEntityIdFormat? format)
        => format switch
        {
            null => _useSecure ? SourceKnownEntityIdFormat.Secure : SourceKnownEntityIdFormat.Plain,
            SourceKnownEntityIdFormat.Secure or SourceKnownEntityIdFormat.Plain or SourceKnownEntityIdFormat.Auto => format.Value,
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Undefined entity ID format.")
        };

    private SourceKnownEntityId ParseWithKey(Guid entityId, NexusSecret key, SourceKnownEntityIdFormat format)
    {
        Span<byte> guidBytes = stackalloc byte[GuidLength];
        entityId.TryWriteBytes(guidBytes, bigEndian: true, out _);

        // Try non-secure path first (plaintext 8D8D markers visible)
        if (format != SourceKnownEntityIdFormat.Secure && HasValidMarkers(guidBytes))
        {
            var result = VerifyAndParse(guidBytes, entityId, secure: false, macKey: key.MacKey);
            if (result.Valid)
                return result;

            // Plain MAC verification failed and cleared the MAC slots; restore the input before decryption.
            entityId.TryWriteBytes(guidBytes, bigEndian: true, out _);
        }

        if (format == SourceKnownEntityIdFormat.Plain)
            return CreateInvalid(entityId);

        // Try secure path: AES-ECB decrypt full block, then check for markers
        // HasValidMarkersSecure accepts RFC 9562 §4.1 variant range 0x80–0xBF (collision guard uses 0x8D–0xBF)
        DecryptGuidBlock(guidBytes, key.Aes);

        if (!HasValidMarkersSecure(guidBytes))
            return CreateInvalid(entityId);

        var recoveredVariant = guidBytes[SourceKnownMarkerVariantIndex];

        // Non-default variant → backward collision-guard verification
        if (recoveredVariant is > SourceKnownMarkerVariantByte and <= SourceKnownMarkerVariantMaxByte)
            return VerifyCollisionGuardProof(guidBytes, (byte)(recoveredVariant - 1), key)
                ? VerifyAndParse(guidBytes, entityId, secure: true, macKey: key.MacKey)
                : CreateInvalid(entityId);

        return recoveredVariant != SourceKnownMarkerVariantByte
            ? CreateInvalid(entityId)
            : VerifyAndParse(guidBytes, entityId, secure: true, macKey: key.MacKey);
    }

    public SourceKnownEntityId? Validate(Guid? entityId, byte entityType, SourceKnownEntityIdFormat? format = null)
    {
        var selectedFormat = ResolveFormat(format);
        return entityId.HasValue ? Validate(entityId.Value, entityType, selectedFormat) : null;
    }

    public SourceKnownEntityId Validate(Guid entityId, byte entityType, SourceKnownEntityIdFormat? format = null)
        => Validate(entityId, new EntityTypeId(entityType, _appId), format);

    public SourceKnownEntityId? Validate<TApp>(Guid? entityId, byte entityType, SourceKnownEntityIdFormat? format = null) where TApp : IAppId
    {
        var selectedFormat = ResolveFormat(format);
        return entityId.HasValue ? Validate<TApp>(entityId.Value, entityType, selectedFormat) : null;
    }

    public SourceKnownEntityId Validate<TApp>(Guid entityId, byte entityType, SourceKnownEntityIdFormat? format = null) where TApp : IAppId
        => Validate(entityId, new EntityTypeId(entityType, TApp.AppId), format);

    public SourceKnownEntityId? Validate(Guid? entityId, EntityTypeId entityTypeId, SourceKnownEntityIdFormat? format = null)
    {
        var selectedFormat = ResolveFormat(format);
        return entityId.HasValue ? Validate(entityId.Value, entityTypeId, selectedFormat) : null;
    }

    public SourceKnownEntityId Validate(Guid entityId, EntityTypeId entityTypeId, SourceKnownEntityIdFormat? format = null)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(entityTypeId.AppId, IAppId.MaxAppId);
        var sourceKnownId = Parse(entityId, format);
        sourceKnownId.Validate(entityTypeId, format);

        return sourceKnownId;
    }

    public SourceKnownEntityId? Validate<TEntity>(Guid? entityId, SourceKnownEntityIdFormat? format = null) where TEntity : SourceKnownEntity
    {
        var selectedFormat = ResolveFormat(format);
        return entityId.HasValue ? Validate<TEntity>(entityId.Value, selectedFormat) : null;
    }

    public SourceKnownEntityId Validate<TEntity>(Guid entityId, SourceKnownEntityIdFormat? format = null) where TEntity : SourceKnownEntity
        => Validate(entityId, SourceKnownEntity.GetEntityTypeId<TEntity>(), format);

    public SourceKnownEntityId ToSecure(SourceKnownEntityId id)
    {
        id.ValidateId();
        // Conversions accept either representation and revalidate the GUID independently of caller metadata.
        id = Parse(id.EntityId, SourceKnownEntityIdFormat.Auto);
        id.ValidateId();

        return id.Secure
            ? id
            : id with { EntityId = GenerateSecureGuid(id.Source.Id, id.EntityType), Secure = true };
    }

    public SourceKnownEntityId? ToSecure(SourceKnownEntityId? id)
        => id.HasValue ? ToSecure(id.Value) : null;

    public SourceKnownEntityId ToPlain(SourceKnownEntityId id)
    {
        id.ValidateId();
        // Conversions accept either representation and revalidate the GUID independently of caller metadata.
        id = Parse(id.EntityId, SourceKnownEntityIdFormat.Auto);
        id.ValidateId();

        return id.Secure
            ? id with { EntityId = GeneratePlainGuid(id.Source.Id, id.EntityType), Secure = false }
            : id;
    }

    public SourceKnownEntityId? ToPlain(SourceKnownEntityId? id)
        => id.HasValue ? ToPlain(id.Value) : null;

    private static SourceKnownEntityId CreateInvalid(Guid entityId)
        => new(default, entityId, InvalidEntityType, false, Secure: false);

    private static bool HasValidMarkers(ReadOnlySpan<byte> guidBytes)
        => guidBytes[EpochIndex] <= SourceKnownGenerationTimePolicy.MaxSupportedEpoch
           && guidBytes[SourceKnownMarkerVersionIndex] == SourceKnownMarkerVersionByte
           && guidBytes[SourceKnownMarkerVariantIndex] == SourceKnownMarkerVariantByte;

    /// <summary>
    /// Accepts the primary variant (0x8D) and any RFC 9562 §4.1 variant byte (0x80–0xBF).
    /// Used only on the post-decryption (secure) path — non-default variant bytes are
    /// produced by the collision guard in <see cref="GenerateSecureGuid"/>.
    /// </summary>
    private static bool HasValidMarkersSecure(ReadOnlySpan<byte> guidBytes)
        => guidBytes[EpochIndex] <= SourceKnownGenerationTimePolicy.MaxSupportedEpoch
           && guidBytes[SourceKnownMarkerVersionIndex] == SourceKnownMarkerVersionByte
           && (guidBytes[SourceKnownMarkerVariantIndex] & 0xC0) == 0x80;

    /// <summary>
    /// Checks whether ciphertext bytes, when interpreted as a plain SKEID,
    /// produce a coincidental MAC match. The collision guard in
    /// <see cref="GenerateSecureGuid"/> also requires matching epoch and marker bytes.
    /// </summary>
    private static bool HasCoincidentalMacMatch(ReadOnlySpan<byte> ciphertextBytes, SecretKey32 macKey)
    {
        Span<byte> workingCopy = stackalloc byte[GuidLength];
        ciphertextBytes.CopyTo(workingCopy);

        return VerifyMac(workingCopy, macKey);
    }

    /// <summary>
    /// Verifies MAC integrity and extracts ID/entityType from a plaintext (or decrypted) guid byte span.
    /// </summary>
    private SourceKnownEntityId VerifyAndParse(Span<byte> guidBytes, Guid entityId, bool secure, SecretKey32 macKey)
    {
        if (!VerifyMac(guidBytes, macKey))
            return CreateInvalid(entityId);

        // Epoch at byte 0 — covered by MAC; todo: consume in multi-epoch parsing
        var id = ReadId(guidBytes);
        return new SourceKnownEntityId(_sourceKnownIdUtils.Parse(id), entityId, guidBytes[EntityTypeIndex], true, secure);
    }

    /// <summary>
    /// Backward collision verification: reconstructs the SKEID with (variant−1),
    /// encrypts, and checks whether ciphertext would have triggered the collision guard.
    /// Returns true if the previous variant genuinely collided (legitimate escalation).
    /// Called only for non-default variant bytes (recoveredVariant > 0x8D) during Parse.
    /// </summary>
    private static bool VerifyCollisionGuardProof(ReadOnlySpan<byte> decryptedBytes, byte previousVariant, NexusSecret key)
    {
        var id = ReadId(decryptedBytes);
        var entityType = decryptedBytes[EntityTypeIndex];

        // Reconstruct with previous variant
        Span<byte> reconstructed = stackalloc byte[GuidLength];

        WriteIdAndMarkers(reconstructed, id, entityType, previousVariant);
        WriteMac(reconstructed, key.MacKey);
        EncryptGuidBlock(reconstructed, key.Aes);

        // Previous variant's ciphertext must have collided (markers + MAC match)
        return HasValidMarkers(reconstructed) && HasCoincidentalMacMatch(reconstructed, key.MacKey);
    }

    /// <summary>
    /// Writes epoch, ID halves, entity type, version marker, and variant marker into the guid byte span
    /// using RFC 9562 big-endian layout. The upper half MSB is toggled (XOR 0x80000000) so that
    /// the signed SKID chronological order maps to unsigned lexicographic byte order.
    /// The lower half is split: byte 5 (MSB) + bytes 9-11 (remaining), around the markers at bytes 6-8.
    /// All payload bytes (0–11) are overwritten; <see cref="WriteMac"/> initializes the remaining bytes.
    /// </summary>
    private static void WriteIdAndMarkers(Span<byte> guidBytes, long id, byte entityType, byte variantByte)
    {
        var bits = unchecked((ulong)id);
        var upperHalf = (uint)(bits >> 32);
        var lowerHalf = unchecked((uint)bits);

        guidBytes[EpochIndex] = SourceKnownGenerationTimePolicy.MaxSupportedEpoch;
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
    /// Reads the SKID upper half from bytes 1–4 (big-endian, sign-toggled) and the split lower half
    /// from byte 5 + bytes 9–11 (big-endian). XOR untoggle restores the original signed representation.
    /// </summary>
    private static long ReadId(ReadOnlySpan<byte> guidBytes)
    {
        var upperHalf = BinaryPrimitives.ReadUInt32BigEndian(
            guidBytes.Slice(EntityIdUpperHalfOffset, EntityIdUpperHalfLength)) ^ SignBitToggle;
        var lowerHalf = ((uint)guidBytes[EntityIdLowerByte0Index] << 24)
                        | ((uint)guidBytes[EntityIdLowerBytes123Offset] << 16)
                        | ((uint)guidBytes[EntityIdLowerBytes123Offset + 1] << 8)
                        | guidBytes[EntityIdLowerBytes123Offset + 2];

        return unchecked((long)((ulong)upperHalf << 32 | lowerHalf));
    }

    /// <summary>
    /// Computes BLAKE3 keyed MAC over the guid bytes (MAC slots must be zeroed before calling).
    /// </summary>
    private static void ComputeMac(ReadOnlySpan<byte> guidBytes, Span<byte> macBytes, SecretKey32 macKey)
    {
        using var hasher = Hasher.NewKeyed(macKey.Span);
        hasher.Update(guidBytes);
        hasher.Finalize(macBytes);
    }

    /// <summary>
    /// Clears the MAC slots, authenticates the full GUID block, and writes the tag into bytes 12–15.
    /// </summary>
    private static void WriteMac(Span<byte> guidBytes, SecretKey32 macKey)
    {
        Span<byte> macBytes = stackalloc byte[MacLength];
        ClearMacSlots(guidBytes);
        ComputeMac(guidBytes, macBytes, macKey);
        macBytes.CopyTo(guidBytes.Slice(MacOffset, MacLength));
    }

    /// <summary>
    /// Verifies the stored tag against the full GUID block, leaving the MAC slots cleared.
    /// </summary>
    private static bool VerifyMac(Span<byte> guidBytes, SecretKey32 macKey)
    {
        Span<byte> actualMac = stackalloc byte[MacLength];
        Span<byte> expectedMac = stackalloc byte[MacLength];
        guidBytes.Slice(MacOffset, MacLength).CopyTo(actualMac);
        ClearMacSlots(guidBytes);
        ComputeMac(guidBytes, expectedMac, macKey);

        return expectedMac.SequenceEqual(actualMac);
    }

    /// <summary>
    /// Zeros the MAC slots (bytes 12-15) in the guid bytes for MAC re-computation.
    /// </summary>
    private static void ClearMacSlots(Span<byte> guidBytes)
        => guidBytes.Slice(MacOffset, MacLength).Clear();

    private static void EncryptGuidBlock(Span<byte> guidBytes, Aes256 aes)
    {
        var block = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(guidBytes));
        aes.Encrypt(block).StoreUnsafe(ref MemoryMarshal.GetReference(guidBytes));
    }

    private static void DecryptGuidBlock(Span<byte> guidBytes, Aes256 aes)
    {
        var block = Vector128.LoadUnsafe(ref MemoryMarshal.GetReference(guidBytes));
        aes.Decrypt(block).StoreUnsafe(ref MemoryMarshal.GetReference(guidBytes));
    }

    // SourceKnownEntityIdUtils is registered as a singleton; the DI container disposes it at shutdown.
    public void Dispose() => _keyRing.Dispose();
}
