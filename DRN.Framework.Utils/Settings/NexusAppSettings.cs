using System.Text;
using System.Text.Json.Serialization;
using DRN.Framework.Utils.Data.Encodings;
using DRN.Framework.Utils.Data.Hashing;
using DRN.Framework.Utils.Data.Validation;

namespace DRN.Framework.Utils.Settings;

/// <summary>
/// In production values will be obtained from nexus as a remote configuration source
/// </summary>
public class NexusAppSettings
{
    private IReadOnlyList<NexusMacKey> _macKeys = [];

    public static string GetKey(string shortKey) => $"{nameof(NexusAppSettings)}:{shortKey}";

    public string NexusAddress { get; init; } = "nexus";

    //Nexus App will generate ids randomly in production
    public byte AppId { get; init; }
    public byte AppInstanceId { get; init; }

    /// <summary>
    /// When true (default), <see cref="Ids.SourceKnownEntityIdUtils.Generate(long, byte)"/> produces AES-256-ECB encrypted entity IDs.
    /// When false, it produces plaintext entity IDs with visible 8D8D markers.
    /// Explicit <c>GenerateSecure</c>/<c>GeneratePlain</c> methods bypass this flag.
    /// </summary>
    public bool UseSecureSourceKnownIds { get; init; } = true;

    public IReadOnlyList<NexusMacKey> MacKeys
    {
        get => _macKeys;
        init => _macKeys = value ?? [];
    }

    public NexusMacKey GetDefaultMacKey() => MacKeys.First(x => x.Default);

    internal void AddNexusMacKey(NexusMacKey key)
    {
        if (_macKeys is List<NexusMacKey> list)
            list.Add(key);
        _macKeys = _macKeys.Union([key]).ToArray();
    }

    public void Validate()
    {
        if (MacKeys.Count == 0)
            throw ExceptionFor.Configuration($"{nameof(NexusAppSettings)}.{nameof(MacKeys)} must contain at least 1 {nameof(NexusMacKey)}");

        for (var index = 0; index < MacKeys.Count; index++)
        {
            if (MacKeys[index] is null)
                throw ExceptionFor.Configuration($"{nameof(NexusAppSettings)}.{nameof(MacKeys)}[{index}] must not be null");
        }

        var defaultKeyCount = MacKeys.Count(k => k.Default);
        if (defaultKeyCount == 0)
            throw ExceptionFor.Configuration($"Default {nameof(NexusMacKey)}, not found");
        if (defaultKeyCount != 1)
            throw ExceptionFor.Configuration($"Only 1 default {nameof(NexusMacKey)} is allowed");

        for (var index = 0; index < MacKeys.Count; index++)
        {
            var key = MacKeys[index];
            key.ValidateDataAnnotationsThrowIfInvalid();
            if (!key.IsValid)
                throw ExceptionFor.Configuration($"{nameof(NexusAppSettings)}.{nameof(MacKeys)}[{index}] must resolve to exactly 32 bytes");
        }
    }
}

public class NexusMacKey
{
    private const int RequiredKeyByteLength = 32;
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    [JsonConstructor]
    public NexusMacKey(string key, ByteEncoding format = ByteEncoding.Utf8)
    {
        Key = key;
        Format = format;
        KeyAsBinary = DecodeKey(key, format);
        KeyHash = KeyAsBinary.Hash();
        AlternativeKey = (((KeyHash + "1919").Hash() + "1923").Hash() + "193∞").Hash();
        AlternativeKeyAsBinary = AlternativeKey.Decode();

        if (!IsValid)
            throw ExceptionFor.Configuration($"{nameof(NexusMacKey)} must resolve to exactly 32-byte MAC and alternative keys");
    }

    internal NexusMacKey(BinaryData keyAsBinary) : this(keyAsBinary.Encode(), ByteEncoding.Base64UrlEncoded)
    {
    }

    internal NexusMacKey(ReadOnlySpan<byte> key) : this(key.Encode(), ByteEncoding.Base64UrlEncoded)
    {
    }


    public string Key { get; }
    public ByteEncoding Format { get; }

    [JsonIgnore]
    public string KeyHash { get; }

    [JsonIgnore]
    public BinaryData KeyAsBinary { get; }

    [JsonIgnore]
    public string AlternativeKey { get; }

    [JsonIgnore]
    public BinaryData AlternativeKeyAsBinary { get; }

    public bool Default { get; init; }

    [JsonIgnore]
    public bool IsValid
        => KeyAsBinary.ToMemory().Length == RequiredKeyByteLength
           && AlternativeKeyAsBinary.ToMemory().Length == RequiredKeyByteLength;

    private static BinaryData DecodeKey(string key, ByteEncoding format)
    {
        if (string.IsNullOrEmpty(key))
            throw ExceptionFor.Configuration($"{nameof(NexusMacKey)}.{nameof(Key)} must not be empty");

        byte[] keyBytes;
        try
        {
            keyBytes = format switch
            {
                ByteEncoding.Utf8 => DecodeUtf8(key),
                ByteEncoding.Hex => Convert.FromHexString(key),
                ByteEncoding.Base64 => Convert.FromBase64String(key),
                ByteEncoding.Base64UrlEncoded => key.Decode(ByteEncoding.Base64UrlEncoded).ToArray(),
                _ => throw ExceptionFor.Configuration($"{nameof(NexusMacKey)}.{nameof(Format)} is not supported")
            };
        }
        catch (EncoderFallbackException exception)
        {
            throw ExceptionFor.Configuration($"{nameof(NexusMacKey)}.{nameof(Key)} must be valid {format} and resolve to exactly 32 bytes", exception);
        }
        catch (FormatException exception)
        {
            throw ExceptionFor.Configuration($"{nameof(NexusMacKey)}.{nameof(Key)} must be valid {format} and resolve to exactly 32 bytes", exception);
        }
        catch (ArgumentException exception) when (format is not ByteEncoding.Utf8)
        {
            throw ExceptionFor.Configuration($"{nameof(NexusMacKey)}.{nameof(Key)} must be valid {format} and resolve to exactly 32 bytes", exception);
        }

        if (keyBytes.Length != RequiredKeyByteLength)
            throw ExceptionFor.Configuration($"{nameof(NexusMacKey)}.{nameof(Key)} with format {format} must resolve to exactly 32 bytes; resolved length: {keyBytes.Length}");

        return BinaryData.FromBytes(keyBytes);
    }

    private static byte[] DecodeUtf8(string key)
    {
        var byteCount = StrictUtf8.GetByteCount(key);
        if (byteCount != RequiredKeyByteLength)
            throw ExceptionFor.Configuration($"{nameof(NexusMacKey)}.{nameof(Key)} with format {ByteEncoding.Utf8} must be exactly 32 UTF-8 bytes");

        var keyBytes = StrictUtf8.GetBytes(key);

        return keyBytes;
    }
}
