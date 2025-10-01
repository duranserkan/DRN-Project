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
}

[Singleton<IAppSecuritySettings>]
public class AppSecuritySettings : IAppSecuritySettings
{
    public AppSecuritySettings(DrnAppFeatures features)
    {
        //Inside only usage
        AppHashKey = string.Concat("Peace at home", ("MKA " + features.SeedKey + " DRN")
                .Hash(HashAlgorithm.Sha512, ByteEncoding.Hex)
                .AsSpan(18, 81), "Peace in the world")[16..]
            .Hash(HashAlgorithm.Sha256, ByteEncoding.Hex).Hash().Hash();

        //Inside only usage
        AppEncryptionKey = (AppHashKey + "1919").Hash().Hash().Hash().Hash();
        //Outside only usage
        AppKey = (AppHashKey + "1923" + AppEncryptionKey + "2923").Hash().Hash().Hash().Hash().Hash().Hash()[16..];
        AppSeed = (features.SeedKey + "2923").GenerateSeedFromInputHash();
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
}