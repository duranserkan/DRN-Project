using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
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

    /// <summary>Parses the selected format; ConfiguredDefault uses UseSecureSourceKnownIds. Null input remains null.</summary>
    new SourceKnownEntityId? Parse(Guid? entityId, SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault);

    /// <summary>Parses the selected format; ConfiguredDefault uses UseSecureSourceKnownIds.</summary>
    new SourceKnownEntityId Parse(Guid entityId, SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault);

    /// <summary>Validates integrity and entity type against the configured AppId. Null remains null.</summary>
    new SourceKnownEntityId? Validate(Guid? entityId, byte entityType, SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault);

    /// <summary>Validates integrity and entity type against NexusAppSettings.AppId.</summary>
    new SourceKnownEntityId Validate(Guid entityId, byte entityType, SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault);

    /// <summary>Explicitly selects an application partition through IAppId. Null remains null.</summary>
    new SourceKnownEntityId? Validate<TApp>(Guid? entityId, byte entityType, SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault) where TApp : IAppId;

    /// <summary>Validates integrity, entity type, and the explicit TApp.AppId.</summary>
    new SourceKnownEntityId Validate<TApp>(Guid entityId, byte entityType, SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault) where TApp : IAppId;

    new SourceKnownEntityId? Validate(Guid? entityId, EntityTypeId entityTypeId, SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault);

    /// <summary>Validates integrity and both explicitly supplied identity components.</summary>
    new SourceKnownEntityId Validate(Guid entityId, EntityTypeId entityTypeId, SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault);

    new SourceKnownEntityId? Validate<TEntity>(Guid? entityId, SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault) where TEntity : SourceKnownEntity;

    /// <summary>Validates against the entity's declared identity, independently of configured AppId.</summary>
    new SourceKnownEntityId Validate<TEntity>(Guid entityId, SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault) where TEntity : SourceKnownEntity;

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
/// Parse and Validate resolve ConfiguredDefault through UseSecureSourceKnownIds; Secure, Plain and Auto override it.
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

    private const byte EntityTypeIndex = 7; // 7
    private const byte InvalidEntityType = byte.MaxValue;

    // Epoch at byte 0; the default origin is 2025-01-01, configurable through SourceKnownIdSettings.
    // Each epoch is approximately 68 years long with 2 halves separated with sign bit
    // 2^32 ticks / 4 ticks/s = 2^30 seconds * 2^1 epoch half flag in source known id timestamp.
    // Only encoded epoch 0 is supported. Custom origin parameters do not select a different encoded epoch.

    private const byte MacLength = 4;
    private const byte MacOffset = 12; // 12-15 (contiguous)
    private const int MacLane = MacOffset / sizeof(uint);

    // 8D8D mark — plaintext markers used for non-secure variant and inside the encrypted block for secure variant
    // Ensures that the source-known entityId is easily identifiable by humans and UUID V8 compatible (RFC 9562 §5.8)
    // Version at RFC 9562 octet 6, variant at RFC 9562 octet 8 — standard positions in big-endian layout
    private const byte SourceKnownMarkerVariantIndex = 8;
    private const byte SourceKnownMarkerVersionByte = 0x8D; //6 | V8 => UUID V8 per RFC 9562 §5.8
    private const byte SourceKnownMarkerVariantByte = 0x8D; //8 | Variant RFC 9562 §4.1
    private const byte SourceKnownMarkerVariantMaxByte = 0xBF; //8 | Variant RFC 9562 §4.1 upper bound (collision guard)

    // Epoch zero can join the equality check; multiple supported epochs require a range check.
    [SuppressMessage("ReSharper", "HeuristicUnreachableCode")]
    private const byte EpochMask = SourceKnownGenerationTimePolicy.MaxSupportedEpoch == 0 ? 0xFF : 0;

    // Plain IDs require both complete marker bytes; secure IDs require only the variant prefix.
    private static readonly Vector128<byte> PlainMarkerMask = Vector128.Create(EpochMask, 0, 0, 0, 0, 0, 0xFF, 0, 0xFF, 0, 0, 0, 0, 0, 0, 0);

    private static readonly Vector128<byte> PlainMarkerExpected =
        Vector128.Create(0, 0, 0, 0, 0, 0, SourceKnownMarkerVersionByte, 0, SourceKnownMarkerVariantByte, 0, 0, 0, 0, 0, 0, 0);

    private static readonly Vector128<byte> SecureMarkerMask = Vector128.Create(EpochMask, 0, 0, 0, 0, 0, 0xFF, 0, 0xC0, 0, 0, 0, 0, 0, 0, 0);
    private static readonly Vector128<byte> SecureMarkerExpected = Vector128.Create(0, 0, 0, 0, 0, 0, SourceKnownMarkerVersionByte, 0, 0x80, 0, 0, 0, 0, 0, 0, 0);

    private static readonly Vector128<byte> DefaultMarkers = Vector128.Create(
        SourceKnownGenerationTimePolicy.MaxSupportedEpoch, 0, 0, 0, 0, 0,
        SourceKnownMarkerVersionByte, 0, SourceKnownMarkerVariantByte, 0, 0, 0, 0, 0, 0, 0);

    // Scatter the ID in network order; out-of-range indices leave marker and MAC slots zero.
    private static readonly Vector128<byte> IdWriteIndices = BitConverter.IsLittleEndian
        ? Vector128.Create(255, 7, 6, 5, 4, 3, 255, 255, 255, 2, 1, 0, 255, 255, 255, 255)
        : Vector128.Create(255, 0, 1, 2, 3, 4, 255, 255, 255, 5, 6, 7, 255, 255, 255, 255);

    // Gather network-order ID bytes into one native-order ulong lane.
    private static readonly Vector128<byte> IdReadIndices = BitConverter.IsLittleEndian
        ? Vector128.Create(11, 10, 9, 5, 4, 3, 2, 1, 255, 255, 255, 255, 255, 255, 255, 255)
        : Vector128.Create(1, 2, 3, 4, 5, 9, 10, 11, 255, 255, 255, 255, 255, 255, 255, 255);

    // Key separation: MacKey and EncryptionKey are cryptographically independent keys from the same keyring entry.
    // MacKey -> BLAKE3 keyed MAC (integrity). EncryptionKey -> AES-256-ECB (confidentiality).
    private readonly NexusKeyRing _keyRing;
    private readonly Aes256 _aes;
    private readonly SecretKey32 _macKey;
    private readonly bool _useSourceKnownIdKeyFallback;
    public SourceKnownEntityIdFormat DefaultFormat { get; }
    private readonly ISourceKnownIdUtils _sourceKnownIdUtils;
    private readonly byte _appId;


    public SourceKnownEntityIdUtils(IAppSettings appSettings, ISourceKnownIdUtils sourceKnownIdUtils)
    {
        _appId = appSettings.NexusAppSettings.AppId;
        ArgumentOutOfRangeException.ThrowIfGreaterThan(_appId, IAppId.MaxAppId);
        _sourceKnownIdUtils = sourceKnownIdUtils;
        _keyRing = new NexusKeyRing(appSettings.NexusAppSettings);
        _useSourceKnownIdKeyFallback = appSettings.NexusAppSettings.UseSourceKnownIdKeyFallback;
        _aes = _keyRing.Default.Aes;
        _macKey = _keyRing.Default.MacKey;
        DefaultFormat = appSettings.NexusAppSettings.UseSecureSourceKnownIds
            ? SourceKnownEntityIdFormat.Secure
            : SourceKnownEntityIdFormat.Plain;
    }

    public SourceKnownEntityId Generate<TEntity>() where TEntity : SourceKnownEntity
        => Generate<TEntity>(_sourceKnownIdUtils.Next<TEntity>());

    public SourceKnownEntityId Generate<TEntity>(long id) where TEntity : SourceKnownEntity
        => Generate(id, SourceKnownEntity.GetEntityTypeId<TEntity>());

    public SourceKnownEntityId Generate(SourceKnownEntity entity)
        => Generate(entity.Id, SourceKnownEntity.GetEntityTypeId(entity));

    public SourceKnownEntityId Generate(long id, EntityTypeId entityTypeId)
        => DefaultFormat == SourceKnownEntityIdFormat.Secure ? GenerateSecure(id, entityTypeId) : GeneratePlain(id, entityTypeId);

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

    internal Guid GeneratePlainGuid(long id, byte entityType)
    {
        var block = CreateIdAndMarkers(id, entityType, SourceKnownMarkerVariantByte);
        return ToGuid(WriteBlake3Mac(block, _macKey));
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

    internal Guid GenerateSecureGuid(long id, byte entityType)
    {
        // Build guid identically to non-secure (same layout, same markers)
        var variantByte = SourceKnownMarkerVariantByte;
        var block = WriteBlake3Mac(CreateIdAndMarkers(id, entityType, variantByte), _macKey);

        // Encrypt entire 16-byte block with AES-ECB (true PRP — no nonce needed)
        block = _aes.Encrypt(block);

        // Ciphertext with epoch 0, both 0x8D markers, and a valid MAC would be misclassified as plain.
        // Check every enabled verification key; keys added later are outside this guarantee.
        // With three fixed bytes and a 32-bit MAC, collision probability is approximately 2^-56 per key,
        // assuming pseudorandom ciphertext.
        // Retry variants 0x8E through 0xBF; the AES permutation gives distinct ciphertext for each variant.
        // At most 50 retries after the initial candidate. Exhaustion throws JackpotException.
        if (IsSecureBlockHotPath(block) || IsSecureBlockColdPath(block))
            return ToGuid(block);

        for (variantByte = SourceKnownMarkerVariantByte + 1; variantByte <= SourceKnownMarkerVariantMaxByte; variantByte++)
        {
            block = WriteBlake3Mac(CreateIdAndMarkers(id, entityType, variantByte), _macKey);
            block = _aes.Encrypt(block);

            if (IsSecureBlockHotPath(block) || IsSecureBlockColdPath(block))
                return ToGuid(block);
        }

        throw ExceptionFor.Jackpot(
            $"All 50 alternative variant bytes " +
            $"(0x{SourceKnownMarkerVariantByte + 1:X2}–0x{SourceKnownMarkerVariantMaxByte:X2}) exhausted " +
            $"for id={id}, entityType={entityType}.");
    }

    public SourceKnownEntityId? Parse(Guid? entityId, SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault)
    {
        var selectedFormat = ResolveFormat(format);
        return entityId.HasValue ? ParseCore(entityId.Value, selectedFormat) : null;
    }

    public SourceKnownEntityId Parse(Guid entityId, SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault)
        => ParseCore(entityId, ResolveFormat(format));

    private SourceKnownEntityId ParseCore(Guid entityId, SourceKnownEntityIdFormat format)
    {
        var block = FromGuid(entityId);
        if (format == SourceKnownEntityIdFormat.Plain && !HasValidMarkers(block))
            return CreateInvalid(entityId);

        var result = ParseWithKey(block, entityId, _keyRing.Default, format, out var authenticated);

        return authenticated
            ? result
            : ParseWithFallbackKeys(block, entityId, format);
    }

    private SourceKnownEntityId ParseWithFallbackKeys(Vector128<byte> block, Guid entityId, SourceKnownEntityIdFormat format)
    {
        var keys = _keyRing.Fallback;
        for (var index = 0; index < keys.Count; index++)
        {
            var result = ParseWithKey(block, entityId, keys[index], format, out var authenticated);
            if (authenticated)
                return result;
        }

        return CreateInvalid(entityId);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SourceKnownEntityIdFormat ResolveFormat(SourceKnownEntityIdFormat format)
    {
        if (format == SourceKnownEntityIdFormat.ConfiguredDefault)
            return DefaultFormat;

        SourceKnownEntityId.ValidateFormat(format);
        return format;
    }

    private SourceKnownEntityId ParseWithKey(Vector128<byte> block, Guid entityId, NexusSecret key, SourceKnownEntityIdFormat format,
        out bool authenticated)
    {
        authenticated = false;
        // Try non-secure path first (plaintext 8D8D markers visible)
        // Plain markers have already been checked once in ParseCore.
        if (format == SourceKnownEntityIdFormat.Plain || (format == SourceKnownEntityIdFormat.Auto && HasValidMarkers(block)))
        {
            var result = VerifyAndParse(block, entityId, secure: false, macKey: key.MacKey);
            authenticated = result.Valid;
            if (result.Valid || format == SourceKnownEntityIdFormat.Plain)
                return result;
        }

        // Try secure path: AES-ECB decrypt full block, then check for markers
        // HasValidMarkersSecure accepts RFC 9562 §4.1 variant range 0x80–0xBF (collision guard uses 0x8D–0xBF)
        block = key.Aes.Decrypt(block);

        if (!HasValidMarkersSecure(block))
            return CreateInvalid(entityId);

        var secureResult = VerifyAndParse(block, entityId, secure: true, macKey: key.MacKey);
        authenticated = secureResult.Valid;
        if (!authenticated)
            return secureResult;

        // Once authenticated, keep this key even if the variant or its collision proof is invalid.
        var recoveredVariant = block[SourceKnownMarkerVariantIndex];
        if (recoveredVariant == SourceKnownMarkerVariantByte)
            return secureResult;

        // Non-default variants require every earlier candidate to have collided, through the base variant.
        if (recoveredVariant is > SourceKnownMarkerVariantByte and <= SourceKnownMarkerVariantMaxByte)
            return VerifyCollisionGuardProof(block, recoveredVariant, key)
                ? secureResult
                : CreateInvalid(entityId);

        return CreateInvalid(entityId);
    }

    public SourceKnownEntityId? Validate(Guid? entityId, byte entityType, SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault)
    {
        var selectedFormat = ResolveFormat(format);
        return entityId.HasValue ? ValidateCore(entityId.Value, new EntityTypeId(entityType, _appId), selectedFormat) : null;
    }

    public SourceKnownEntityId Validate(Guid entityId, byte entityType, SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault)
        => Validate(entityId, new EntityTypeId(entityType, _appId), format);

    public SourceKnownEntityId? Validate<TApp>(Guid? entityId, byte entityType, SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault)
        where TApp : IAppId
    {
        var selectedFormat = ResolveFormat(format);
        return entityId.HasValue ? ValidateCore(entityId.Value, new EntityTypeId(entityType, TApp.AppId), selectedFormat) : null;
    }

    public SourceKnownEntityId Validate<TApp>(Guid entityId, byte entityType, SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault)
        where TApp : IAppId
        => Validate(entityId, new EntityTypeId(entityType, TApp.AppId), format);

    public SourceKnownEntityId? Validate(Guid? entityId, EntityTypeId entityTypeId, SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault)
    {
        var selectedFormat = ResolveFormat(format);
        return entityId.HasValue ? ValidateCore(entityId.Value, entityTypeId, selectedFormat) : null;
    }

    public SourceKnownEntityId Validate(Guid entityId, EntityTypeId entityTypeId, SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault)
        => ValidateCore(entityId, entityTypeId, ResolveFormat(format));

    public SourceKnownEntityId? Validate<TEntity>(Guid? entityId, SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault)
        where TEntity : SourceKnownEntity
    {
        var selectedFormat = ResolveFormat(format);
        return entityId.HasValue ? ValidateCore(entityId.Value, SourceKnownEntity.GetEntityTypeId<TEntity>(), selectedFormat) : null;
    }

    public SourceKnownEntityId Validate<TEntity>(Guid entityId, SourceKnownEntityIdFormat format = SourceKnownEntityIdFormat.ConfiguredDefault)
        where TEntity : SourceKnownEntity
        => Validate(entityId, SourceKnownEntity.GetEntityTypeId<TEntity>(), format);

    private SourceKnownEntityId ValidateCore(Guid entityId, EntityTypeId entityTypeId, SourceKnownEntityIdFormat format)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(entityTypeId.AppId, IAppId.MaxAppId);
        var sourceKnownId = ParseCore(entityId, format);
        sourceKnownId.Validate(entityTypeId);

        return sourceKnownId;
    }

    public SourceKnownEntityId? ToSecure(SourceKnownEntityId? id) => id.HasValue ? ToSecure(id.Value) : null;

    public SourceKnownEntityId ToSecure(SourceKnownEntityId id)
    {
        id.ValidateId();

        return id.Secure
            ? id
            : id with { EntityId = GenerateSecureGuid(id.Source.Id, id.EntityType), Secure = true };
    }

    public SourceKnownEntityId? ToPlain(SourceKnownEntityId? id) => id.HasValue ? ToPlain(id.Value) : null;

    public SourceKnownEntityId ToPlain(SourceKnownEntityId id)
    {
        id.ValidateId();

        return id.Secure
            ? id with { EntityId = GeneratePlainGuid(id.Source.Id, id.EntityType), Secure = false }
            : id;
    }

    private static SourceKnownEntityId CreateInvalid(Guid entityId)
        => new(default, entityId, InvalidEntityType, false, Secure: false);

    [SuppressMessage("ReSharper", "HeuristicUnreachableCode")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasValidMarkers(Vector128<byte> guidBytes)
        => (guidBytes & PlainMarkerMask) == PlainMarkerExpected && guidBytes[EpochIndex] <= SourceKnownGenerationTimePolicy.MaxSupportedEpoch;

    /// <summary>
    /// Accepts the primary variant (0x8D) and any RFC 9562 §4.1 variant byte (0x80–0xBF).
    /// Used only on the post-decryption (secure) path — non-default variant bytes are
    /// produced by the collision guard in <see cref="GenerateSecureGuid"/>.
    /// </summary>
    [SuppressMessage("ReSharper", "HeuristicUnreachableCode")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasValidMarkersSecure(Vector128<byte> guidBytes)
        => (guidBytes & SecureMarkerMask) == SecureMarkerExpected && guidBytes[EpochIndex] <= SourceKnownGenerationTimePolicy.MaxSupportedEpoch;

    /// <summary>
    /// Verifies MAC integrity and extracts ID/entityType from a plaintext (or decrypted) block.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SourceKnownEntityId VerifyAndParse(Vector128<byte> guidBytes, Guid entityId, bool secure, SecretKey32 macKey)
    {
        if (!VerifyBlake3Mac(guidBytes, macKey))
            return CreateInvalid(entityId);

        // Epoch at byte 0 — covered by MAC; todo: consume in multi-epoch parsing
        var id = ReadId(guidBytes);
        return new SourceKnownEntityId(_sourceKnownIdUtils.Parse(id), entityId, guidBytes[EntityTypeIndex], true, secure);
    }

    /// <summary>
    /// Verifies that every variant before the recovered variant, down to 0x8D inclusive,
    /// would have triggered the enabled key ring's collision guard. At most 50 candidates are checked.
    /// Called only for non-default variant bytes (recoveredVariant > 0x8D) during Parse.
    /// </summary>
    private bool VerifyCollisionGuardProof(Vector128<byte> decryptedBytes, byte recoveredVariant, NexusSecret key)
    {
        var id = ReadId(decryptedBytes);
        var entityType = decryptedBytes[EntityTypeIndex];

        return VerifyCollisionGuardChain(recoveredVariant, previousVariant => HasCollisionAtVariant(id, entityType, previousVariant, key));
    }

    /// <summary>
    /// Reconstructs and encrypts one variant, then applies the generation collision guard:
    /// the ciphertext must have valid plain markers, a supported epoch, and a valid MAC under an enabled key.
    /// Reconstruction always uses the key that authenticated the recovered variant.
    /// </summary>
    private bool HasCollisionAtVariant(long id, byte entityType, byte variant, NexusSecret key)
    {
        var reconstructed = WriteBlake3Mac(CreateIdAndMarkers(id, entityType, variant), key.MacKey);
        reconstructed = key.Aes.Encrypt(reconstructed);

        return !IsSecureBlockHotPath(reconstructed) && !IsSecureBlockColdPath(reconstructed);
    }

    // Checks generated ciphertext for plaintext ambiguity; does not authenticate arbitrary input.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsSecureBlockHotPath(Vector128<byte> block)
        => block[EpochIndex] > SourceKnownGenerationTimePolicy.MaxSupportedEpoch ||
           (block & PlainMarkerMask) != PlainMarkerExpected;

    // Called only after the ciphertext passes the plaintext marker and epoch checks.
    private bool IsSecureBlockColdPath(Vector128<byte> block)
    {
        if (VerifyBlake3Mac(block, _macKey))
            return false;

        if (!_useSourceKnownIdKeyFallback)
            return true;

        var keys = _keyRing.Fallback;
        for (var index = 0; index < keys.Count; index++)
            if (VerifyBlake3Mac(block, keys[index].MacKey))
                return false;

        return true;
    }

    /// <summary>
    /// Checks preceding variants in descending order through 0x8D, stopping at the first missing collision.
    /// </summary>
    /// <param name="recoveredVariant">The decrypted variant; only 0x8D through 0xBF are accepted.</param>
    /// <param name="hasCollision">
    /// Returns whether a preceding variant triggers the generation collision guard for the same identity and key pair.
    /// </param>
    /// <returns>
    /// True when every preceding variant collides, including the empty chain for 0x8D; false for an
    /// out-of-range variant or any missing collision. The recovered candidate's MAC is verified separately by the caller.
    /// </returns>
    /// <remarks>
    /// The predicate separates chain traversal from cryptography so tests can supply controlled collision histories,
    /// such as a collision at 0x8E without one at 0x8D. Finding those cases with real AES and MAC computations is impractical.
    /// Production always binds this predicate to <see cref="HasCollisionAtVariant"/>.
    /// This binding allocates a capturing delegate on the non-default-variant parse path; the default 0x8D path bypasses it.
    /// The predicate is called synchronously, is not retained, and is invoked at most 50 times.
    /// </remarks>
    internal static bool VerifyCollisionGuardChain(byte recoveredVariant, Func<byte, bool> hasCollision)
    {
        if (recoveredVariant is < SourceKnownMarkerVariantByte or > SourceKnownMarkerVariantMaxByte)
            return false;

        for (var previousVariant = recoveredVariant - 1; previousVariant >= SourceKnownMarkerVariantByte; previousVariant--)
            if (!hasCollision((byte)previousVariant))
                return false;

        return true;
    }

    /// <summary>
    /// Creates a block containing epoch, ID halves, entity type, version marker, and variant marker
    /// using RFC 9562 big-endian layout. The upper half MSB is toggled (XOR 0x80000000) so that
    /// the signed SKID chronological order maps to unsigned lexicographic byte order.
    /// The lower half is split: byte 5 (MSB) + bytes 9-11 (remaining), around the markers at bytes 6-8.
    /// MAC slots are zeroed for <see cref="WriteBlake3Mac"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> CreateIdAndMarkers(long id, byte entityType, byte variantByte)
    {
        var bits = unchecked((ulong)(id ^ long.MinValue));
        var source = Vector128.CreateScalar(bits).AsByte();
        var payload = Vector128.Shuffle(source, IdWriteIndices);
        var markers = DefaultMarkers.WithElement(EntityTypeIndex, entityType);

        return variantByte == SourceKnownMarkerVariantByte
            ? payload | markers
            : payload | markers.WithElement(SourceKnownMarkerVariantIndex, variantByte);
    }

    /// <summary>
    /// Reads the SKID upper half from bytes 1–4 (big-endian, sign-toggled) and the split lower half
    /// from byte 5 + bytes 9–11 (big-endian). XOR untoggle restores the original signed representation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static long ReadId(Vector128<byte> guidBytes)
    {
        var bits = Vector128.Shuffle(guidBytes, IdReadIndices).AsUInt64().GetElement(0);
        return unchecked((long)(bits ^ ((ulong)SignBitToggle << 32)));
    }

    /// <summary>
    /// Authenticates the full GUID block with MAC slots zeroed by CreateIdAndMarkers, then writes the tag into bytes 12–15.
    /// </summary>
    private static Vector128<byte> WriteBlake3Mac(Vector128<byte> block, SecretKey32 macKey)
    {
        Span<byte> guidBytes = stackalloc byte[GuidLength];
        block.CopyTo(guidBytes);
        Span<byte> macBytes = stackalloc byte[MacLength];
        ComputeBlake3Mac(guidBytes, macBytes, macKey);
        // Native-order reading and lane insertion preserve the tag's exact byte sequence.
        return block.AsUInt32().WithElement(MacLane, MemoryMarshal.Read<uint>(macBytes)).AsByte();
    }

    /// <summary>
    /// Verifies the stored tag with its lane zeroed in a copy, preserving the original block for Auto parsing.
    /// </summary>
    private static bool VerifyBlake3Mac(Vector128<byte> block, SecretKey32 macKey)
    {
        var lanes = block.AsUInt32();
        var actualMac = lanes.GetElement(MacLane);
        Span<byte> guidBytes = stackalloc byte[GuidLength];
        lanes.WithElement(MacLane, 0U).AsByte().CopyTo(guidBytes);
        Span<byte> expectedMac = stackalloc byte[MacLength];
        ComputeBlake3Mac(guidBytes, expectedMac, macKey);

        return MemoryMarshal.Read<uint>(expectedMac) == actualMac;
    }

    /// <summary>
    /// Computes BLAKE3 keyed MAC over the guid bytes (MAC slots must be zeroed before calling).
    /// </summary>
    private static void ComputeBlake3Mac(ReadOnlySpan<byte> guidBytes, Span<byte> macBytes, SecretKey32 macKey)
    {
        using var hasher = Hasher.NewKeyed(macKey.Span);
        hasher.Update(guidBytes);
        hasher.Finalize(macBytes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> FromGuid(Guid entityId)
    {
        Span<byte> guidBytes = stackalloc byte[GuidLength];
        entityId.TryWriteBytes(guidBytes, bigEndian: true, out _);
        return Vector128.Create(guidBytes);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Guid ToGuid(Vector128<byte> block)
    {
        Span<byte> guidBytes = stackalloc byte[GuidLength];
        block.CopyTo(guidBytes);
        return new Guid(guidBytes, bigEndian: true);
    }

    // SourceKnownEntityIdUtils is registered as a singleton; the DI container disposes it at shutdown.
    public void Dispose() => _keyRing.Dispose();
}
