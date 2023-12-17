using System.Text.Json;
using DRN.Framework.Utils.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace DRN.Framework.Utils.Configurations;

public class JsonSerializerConfigurationSource : JsonStreamConfigurationSource
{
    public JsonSerializerConfigurationSource(object toBeSerialized)
    {
        ToBeSerializedObject = toBeSerialized;
        Stream = JsonSerializer.Serialize(toBeSerialized).ToStream();
    }

    public object ToBeSerializedObject { get; }
}

public static partial class ConfigurationExtensions
{
    public static IConfigurationBuilder AddJsonSerializerConfiguration(this IConfigurationBuilder builder, object toBeSerialized)
    {
        return builder.Add(new JsonSerializerConfigurationSource(toBeSerialized));
    }
}