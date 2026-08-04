using System.Text.Json.Serialization;

namespace DRN.Framework.Utils.Settings;

[JsonConverter(typeof(JsonStringEnumConverter<NexusMacType>))]
public enum NexusMacType
{
    Blake3 = 1
}
