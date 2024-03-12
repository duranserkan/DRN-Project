using System.Text.Json;
using DRN.Framework.Utils.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace DRN.Framework.Utils.Configurations;

public static partial class ConfigurationExtensions
{
    public static IConfigurationBuilder AddObjectToJsonConfiguration(this IConfigurationBuilder builder, object toBeSerialized)
        => builder.Add(new ObjectToJsonConfigurationSource(toBeSerialized));
}

public class ObjectToJsonConfigurationSource : JsonStreamConfigurationSource
{
    public ObjectToJsonConfigurationSource(object toBeSerialized)
    {
        ToBeSerializedObject = toBeSerialized;
        Stream = JsonSerializer.Serialize(toBeSerialized).ToStream();
    }

    public object ToBeSerializedObject { get; }

    /// <summary>
    /// Builds the <see cref="ObjectToJsonConfigurationProvider"/> for this source.
    /// </summary>
    /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
    /// <returns>An <see cref="ObjectToJsonConfigurationProvider"/></returns>
    public override IConfigurationProvider Build(IConfigurationBuilder builder) => new ObjectToJsonConfigurationProvider(this);
}

public class ObjectToJsonConfigurationProvider(ObjectToJsonConfigurationSource source) : JsonStreamConfigurationProvider(source)
{
    public Type ObjectType { get; } = source.ToBeSerializedObject.GetType();

    public override string ToString() => $"{GetType().Name} for {ObjectType.FullName}";
}