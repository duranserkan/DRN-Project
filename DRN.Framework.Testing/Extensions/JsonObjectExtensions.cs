using System.Text.Json;
using AwesomeAssertions;

namespace DRN.Framework.Testing.Extensions;

public static class JsonObjectExtensions
{
    public static void ValidateObjectSerialization<T>(this T obj)
    {
        var json = JsonSerializer.Serialize(obj);
        var deserializedObj = JsonSerializer.Deserialize<T>(json);

        obj.Should().BeEquivalentTo(deserializedObj);
    }
}