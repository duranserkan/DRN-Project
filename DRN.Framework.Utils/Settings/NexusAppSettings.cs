namespace DRN.Framework.Utils.Settings;

public class NexusAppSettings
{
    public static string GetKey(string shortKey) => $"{nameof(NexusAppSettings)}:{shortKey}";

    public byte NexusAppId { get; init; }
    public byte NexusAppInstanceId { get; init; }

    public IReadOnlyList<NexusMacKey> MacKeys { get; init; } = [];
    public NexusMacKey GetDefaultMacKey() => MacKeys.First(x => x.Default);
}

public class NexusMacKey
{
    public byte[] Key { get; init; } = [];
    public bool ReadOnly { get; init; }
    public bool Default { get; init; }
    public bool IsValid => Key.Length >= 32;
}