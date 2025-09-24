using DRN.Framework.Utils.Encodings;
using DRN.Framework.Utils.Extensions;

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

    public IReadOnlyList<NexusMacKey> MacKeys
    {
        get => _macKeys;
        init => _macKeys = value;
    }

    public NexusMacKey GetDefaultMacKey() => MacKeys.First(x => x.Default);

    internal void AddNexusMacKey(NexusMacKey key)
    {
        if (_macKeys is List<NexusMacKey> list)
            list.Add(key);
        _macKeys = _macKeys.Union([key]).ToArray();
    }
}

public class NexusMacKey
{
    public NexusMacKey(string key)
    {
        Key = key;
        KeyAsBinary = key.Decode(ByteEncoding.Base64UrlEncoded);
    }

    internal NexusMacKey(BinaryData keyAsBinary)
    {
        Key = keyAsBinary.Encode(ByteEncoding.Base64UrlEncoded);
        KeyAsBinary = keyAsBinary;
    }

    internal NexusMacKey(ReadOnlySpan<byte> key)
    {
        Key = key.Encode(ByteEncoding.Base64UrlEncoded);
        KeyAsBinary = BinaryData.FromBytes(key.ToArray());
    }

    public string Key { get; }
    public BinaryData KeyAsBinary { get; }
    public bool Default { get; init; }
    public bool IsValid => Key.Length >= 32;
}