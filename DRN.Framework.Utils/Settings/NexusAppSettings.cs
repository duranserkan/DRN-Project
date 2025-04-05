namespace DRN.Framework.Utils.Settings;

public class NexusAppSettings
{
    public static string GetKey(string shortKey) => $"{nameof(NexusAppSettings)}:{shortKey}";
    
    public byte NexusAppId { get; init; }
    public byte NexusAppInstanceId { get; init; }
}