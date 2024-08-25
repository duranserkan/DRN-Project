using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DRN.Framework.SharedKernel.Json;

public class ClaimJsonConverter : JsonConverter<Claim>
{
    public override Claim Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Read the JSON into a Claim object
        using JsonDocument doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        var type = root.GetProperty("Type").GetString();
        var value = root.GetProperty("Value").GetString();


        return new Claim(type ?? string.Empty, value ?? string.Empty);
    }

    public override void Write(Utf8JsonWriter writer, Claim value, JsonSerializerOptions options)
    {
        // Write the Claim object as JSON
        writer.WriteStartObject();
        writer.WriteString("Type", value.Type);
        writer.WriteString("Value", value.Value);
        writer.WriteEndObject();
    }
}