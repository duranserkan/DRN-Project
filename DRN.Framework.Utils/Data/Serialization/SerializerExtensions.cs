using System.Text.Json;

namespace DRN.Framework.Utils.Data.Serialization;

public static class SerializerExtensions
{
    public static string Serialize<TModel>(this TModel model, SerializationMethod serializationMethod = SerializationMethod.SystemTextJson)
        => serializationMethod switch
        {
            SerializationMethod.SystemTextJson => JsonSerializer.Serialize(model),
            SerializationMethod.QueryString => QueryParameterSerializer.SerializeToQueryString(model),
            _ => throw new ArgumentOutOfRangeException(nameof(serializationMethod), serializationMethod, null)
        };
    
    public static TModel? Deserialize<TModel>(this string data, SerializationMethod serializationMethod = SerializationMethod.SystemTextJson)
        => serializationMethod switch
        {
            SerializationMethod.SystemTextJson => JsonSerializer.Deserialize<TModel>(data),
            _ => throw new ArgumentOutOfRangeException(nameof(serializationMethod), serializationMethod, null)
        };
}