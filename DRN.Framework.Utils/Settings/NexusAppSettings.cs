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

    public void Validate()
    {
        if (MacKeys.Count(k => k.Default) == 0)
            throw ExceptionFor.Configuration($"Default {nameof(NexusMacKey)}, not found");
        if (MacKeys.Count(k => k.Default) != 1)
            throw ExceptionFor.Configuration($"Only 1 default {nameof(NexusMacKey)} is allowed");

        foreach (var key in MacKeys)
            key.ValidateDataAnnotationsThrowIfInvalid();
    }
}

public class NexusMacKey
{
    public NexusMacKey(string key)
    {
        Key = key;
        KeyHash = Key.Hash();
        KeyAsBinary = Key.Decode();
        AlternativeKey = (((KeyHash + "1919").Hash() + "1923").Hash() + "193∞").Hash();
        AlternativeKeyAsBinary = AlternativeKey.Decode();
    }

    internal NexusMacKey(BinaryData keyAsBinary) : this(keyAsBinary.Encode())
    {
    }

    internal NexusMacKey(ReadOnlySpan<byte> key) : this(key.Encode())
    {
    }


    public string Key { get; }
    public string KeyHash { get; }

    public BinaryData KeyAsBinary { get; }

    public string AlternativeKey { get; }
    public BinaryData AlternativeKeyAsBinary { get; }
    public bool Default { get; init; }
    public bool IsValid => Key.Length == 32;
}