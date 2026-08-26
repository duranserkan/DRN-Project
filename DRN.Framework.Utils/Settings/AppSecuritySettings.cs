using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using DRN.Framework.Utils.Data.Encodings;
using DRN.Framework.Utils.Data.Hashing;
using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Utils.Settings;

public interface IAppSecuritySettings
{
    /// <summary>
    ///  Default app key, can be used publicly. For example, to separate development and production data.
    /// </summary>
    string AppKey { get; }

    /// <summary>
    ///  Default app specific hash Key, use privately, never expose
    /// </summary>
    string AppHashKey { get; }

    /// <summary>
    ///  Default app specific encryption Key, use privately, never expose
    /// </summary>
    string AppEncryptionKey { get; }

    long AppSeed { get; }

    /// <summary>
    /// Derives a context-specific 32-byte key from <see cref="AppEncryptionKey"/> using BLAKE3
    /// and initializes an <see cref="AesGcm"/> cipher with 128-bit authentication tags.
    /// </summary>
    /// <param name="context">
    /// Globally unique, application-specific domain separation context (e.g. <c>"MyApp.DataProtection 2026-08-26 UserTokens v1"</c>).
    /// </param>
    AesGcm CreateAesGcm(string context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        var keyMaterial = Encoding.UTF8.GetBytes(AppEncryptionKey);
        try
        {
            using var derivedKey = Blake3KeyDerivation.Derive32ByteKey(keyMaterial, context);
            return new AesGcm(derivedKey.Span, AesGcm.TagByteSizes.MaxSize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyMaterial);
        }
    }
}

[Singleton<IAppSecuritySettings>]
public class AppSecuritySettings : IAppSecuritySettings
{
    private const string AppHashKeyDerivationContext =
        "DRN.Framework.Utils AppSecuritySettings Peace at home AppHashKey 2026-06-29 21:57:43 v1";
    private const string AppEncryptionKeyDerivationContext =
        "DRN.Framework.Utils AppSecuritySettings Peace in the world AppEncryptionKey 2026-06-29 21:57:43 v1";
    private const string AppKeyDerivationContext =
        "DRN.Framework.Utils AppSecuritySettings 1919 MKA 1923 AppKey 2026-06-29 21:57:43 v1";
    private const string AppSeedDerivationContext =
        "DRN.Framework.Utils AppSecuritySettings 1923 DRN 2923 AppSeed 2026-06-29 21:57:43 v1";

    public AppSecuritySettings(DrnAppFeatures features)
    {
        ArgumentNullException.ThrowIfNull(features);
        ArgumentException.ThrowIfNullOrWhiteSpace(features.SeedKey, nameof(features.SeedKey));

        var seedKey = Encoding.UTF8.GetBytes(features.SeedKey);

        try
        {
            AppHashKey = DeriveBase64UrlKey(seedKey, AppHashKeyDerivationContext);
            AppEncryptionKey = DeriveBase64UrlKey(seedKey, AppEncryptionKeyDerivationContext);
            AppKey = DeriveBase64UrlKey(seedKey, AppKeyDerivationContext)[..8];
            AppSeed = DeriveSeed(seedKey);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(seedKey);
        }
    }

    /// <summary>
    /// Initializes an instance with default <see cref="DrnAppFeatures"/> settings. For testing and internal use only.
    /// </summary>
    internal AppSecuritySettings() : this(new DrnAppFeatures())
    {
    }

    /// <summary>
    ///  Default app key, can be used publicly. For example, to separate development and production data.
    /// </summary>
    public string AppKey { get; }

    /// <summary>
    ///  Default app specific hash Key, use privately, never expose
    /// </summary>
    public string AppHashKey { get; }

    /// <summary>
    ///  Default app specific encryption Key, use privately, never expose
    /// </summary>
    public string AppEncryptionKey { get; }

    public long AppSeed { get; }

    /// <summary>
    /// Derives a context-specific 32-byte key from <see cref="AppEncryptionKey"/> using BLAKE3
    /// and initializes an <see cref="AesGcm"/> cipher with 128-bit authentication tags.
    /// </summary>
    /// <param name="context">
    /// Globally unique, application-specific domain separation context (e.g. <c>"MyApp.DataProtection 2026-08-26 UserTokens v1"</c>).
    /// </param>
    public AesGcm CreateAesGcm(string context)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(context);

        var keyMaterial = Encoding.UTF8.GetBytes(AppEncryptionKey);
        try
        {
            using var derivedKey = Blake3KeyDerivation.Derive32ByteKey(keyMaterial, context);
            return new AesGcm(derivedKey.Span, AesGcm.TagByteSizes.MaxSize);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyMaterial);
        }
    }

    private static string DeriveBase64UrlKey(ReadOnlySpan<byte> keyMaterial, string context)
    {
        using var derivedKey = Blake3KeyDerivation.Derive32ByteKey(keyMaterial, context);

        return derivedKey.Span.Encode(ByteEncoding.Base64UrlEncoded);
    }

    private static long DeriveSeed(ReadOnlySpan<byte> keyMaterial)
    {
        using var seed = Blake3KeyDerivation.Derive32ByteKey(keyMaterial, AppSeedDerivationContext);

        return BinaryPrimitives.ReadInt64LittleEndian(seed.Span);
    }
}
