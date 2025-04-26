using DRN.Framework.Utils.Encodings;

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
        KeyAsByteArray = Base64Utils.UrlSafeBase64DecodeToBytes(key);
    }

    internal NexusMacKey(byte[] keyAsByteArray)
    {
        Key = Base64Utils.UrlSafeBase64Encode(keyAsByteArray);
        KeyAsByteArray = keyAsByteArray;
    }

    public string Key { get; }
    public byte[] KeyAsByteArray { get; }
    public bool Default { get; init; }
    public bool IsValid => Key.Length >= 32;
}