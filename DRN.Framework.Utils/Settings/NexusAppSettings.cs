using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using DRN.Framework.Utils.Data.Encryption;
using DRN.Framework.Utils.Data.Encodings;
using DRN.Framework.Utils.Data.Hashing;
using DRN.Framework.Utils.Data.Validation;

namespace DRN.Framework.Utils.Settings;

/// <summary>
/// In production values will be obtained from nexus as a remote configuration source
/// </summary>
public sealed class NexusAppSettings : IDisposable
{
    private IReadOnlyList<NexusKey> _keys = [];

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

    public IReadOnlyList<NexusKey> Keys
    {
        get => _keys;
        init => _keys = value ?? [];
    }

    public NexusKey GetDefaultKey() => Keys.First(x => x.Default);

    internal bool HasDefaultKey()
    {
        ThrowIfKeysContainNullEntries();

        return Keys.Any(k => k.Default);
    }

    internal void AddNexusKey(NexusKey key) => _keys = [.. _keys, key];

    public void Validate()
    {
        if (Keys.Count == 0)
            throw ExceptionFor.Configuration($"{nameof(NexusAppSettings)}.{nameof(Keys)} must contain at least 1 {nameof(NexusKey)}");

        ThrowIfKeysContainNullEntries();

        var defaultKeyCount = Keys.Count(k => k.Default);
        if (defaultKeyCount == 0)
            throw ExceptionFor.Configuration($"Default {nameof(NexusKey)}, not found");
        if (defaultKeyCount != 1)
            throw ExceptionFor.Configuration($"Only 1 default {nameof(NexusKey)} is allowed");
    }

    private void ThrowIfKeysContainNullEntries()
    {
        for (var index = 0; index < Keys.Count; index++)
            if (Keys[index] is null)
                throw ExceptionFor.Configuration($"{nameof(NexusAppSettings)}.{nameof(Keys)}[{index}] must not be null");
    }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var key in Keys)
            key?.Dispose();
    }
}

/// <summary>
/// Represents a Nexus configuration key and handles BLAKE3 subkey derivation for MAC and Encryption keys.
/// </summary>
/// <remarks>
/// Key derivation uses BLAKE3 context-string key derivation mode. See:
/// <see href="https://docs.rs/blake3/latest/blake3/fn.derive_key.html"/>
/// </remarks>
public sealed class NexusKey : IDisposable
{
    private const int RequiredKeyByteLength = 32;
    private const string MacKeyDerivationContext =
        "DRN.Framework.Utils NexusKey 1881 1919 1923 193∞ derive_key mackey 2026-06-29 21:57:43 v1";
    private const string EncryptionKeyDerivationContext =
        "DRN.Framework.Utils NexusKey 1881 1919 1923 193∞ derive_key encryption key 2026-06-29 21:57:43 v1";
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    [JsonConstructor]
    public NexusKey(string keyMaterial, ByteEncoding format = ByteEncoding.Utf8)
    {
        KeyMaterial = keyMaterial;
        Format = format;

        var decodedKeyMaterial = DecodeKey(keyMaterial, format);
        SecretKey32? derivedMacKey = null;
        SecretKey32? derivedEncryptionKey = null;
        try
        {
            derivedMacKey = Blake3KeyDerivation.Derive32ByteKey(decodedKeyMaterial, MacKeyDerivationContext);
            derivedEncryptionKey = Blake3KeyDerivation.Derive32ByteKey(decodedKeyMaterial, EncryptionKeyDerivationContext);

            if (derivedMacKey.Length != RequiredKeyByteLength || derivedEncryptionKey.Length != RequiredKeyByteLength)
                throw ExceptionFor.Configuration($"{nameof(NexusKey)} must resolve to exactly 32-byte MAC and encryption keys");

            MacKey = derivedMacKey;
            EncryptionKey = derivedEncryptionKey;
        }
        catch
        {
            derivedMacKey?.Dispose();
            derivedEncryptionKey?.Dispose();
            throw;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(decodedKeyMaterial);
        }
    }


    public string KeyMaterial { get; }
    public ByteEncoding Format { get; }
    public bool Default { get; init; }

    [JsonIgnore]
    internal SecretKey32 MacKey { get; }

    [JsonIgnore]
    internal SecretKey32 EncryptionKey { get; }

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        MacKey?.Dispose();
        EncryptionKey?.Dispose();
    }

    private static byte[] DecodeKey(string key, ByteEncoding format)
    {
        if (string.IsNullOrEmpty(key))
            throw ExceptionFor.Configuration($"{nameof(NexusKey)}.{nameof(KeyMaterial)} must not be empty");

        byte[]? keyBytes = null;
        try
        {
            try
            {
                keyBytes = format switch
                {
                    ByteEncoding.Utf8 => DecodeUtf8(key),
                    ByteEncoding.Hex => Convert.FromHexString(key),
                    ByteEncoding.Base64 => Convert.FromBase64String(key),
                    ByteEncoding.Base64UrlEncoded => key.Decode(ByteEncoding.Base64UrlEncoded).ToArray(),
                    _ => throw ExceptionFor.Configuration($"{nameof(NexusKey)}.{nameof(Format)} is not supported")
                };
            }
            catch (EncoderFallbackException exception)
            {
                throw ExceptionFor.Configuration($"{nameof(NexusKey)}.{nameof(KeyMaterial)} must be valid {format} and resolve to exactly 32 bytes", exception);
            }
            catch (FormatException exception)
            {
                throw ExceptionFor.Configuration($"{nameof(NexusKey)}.{nameof(KeyMaterial)} must be valid {format} and resolve to exactly 32 bytes", exception);
            }
            catch (ArgumentException exception) when (format is not ByteEncoding.Utf8)
            {
                throw ExceptionFor.Configuration($"{nameof(NexusKey)}.{nameof(KeyMaterial)} must be valid {format} and resolve to exactly 32 bytes", exception);
            }

            if (keyBytes.Length != RequiredKeyByteLength)
                throw ExceptionFor.Configuration(
                    $"{nameof(NexusKey)}.{nameof(KeyMaterial)} with format {format} must resolve to exactly 32 bytes; resolved length: {keyBytes.Length}");

            var result = keyBytes;
            keyBytes = null;
            return result;
        }
        finally
        {
            if (keyBytes is not null)
                CryptographicOperations.ZeroMemory(keyBytes);
        }
    }

    private static byte[] DecodeUtf8(string key)
    {
        var byteCount = StrictUtf8.GetByteCount(key);
        if (byteCount != RequiredKeyByteLength)
            throw ExceptionFor.Configuration($"{nameof(NexusKey)}.{nameof(KeyMaterial)} with format {ByteEncoding.Utf8} must be exactly 32 UTF-8 bytes");

        var keyBytes = StrictUtf8.GetBytes(key);

        return keyBytes;
    }
}
