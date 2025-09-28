using System.Text.Json;
using Flurl;

namespace DRN.Framework.Utils.Data.Serialization;

public static class QueryParameterSerializer
{
    public static string SerializeToQueryString<T>(T? obj)
    {
        if (obj == null) return string.Empty;

        // First serialize to JSON
        var json = JsonSerializer.Serialize(obj);

        // Parse JSON and flatten to query parameters
        var jsonDocument = JsonDocument.Parse(json);
        var parameters = new Dictionary<string, string>();

        FlattenJsonElement(jsonDocument.RootElement, string.Empty, parameters);

        return String.Empty.SetQueryParams(parameters).ToString().TrimStart('?');
    }

    private static void FlattenJsonElement(JsonElement element, string prefix, Dictionary<string, string> parameters)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
                    FlattenJsonElement(property.Value, key, parameters);
                }

                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    var key = $"{prefix}[{index}]";
                    FlattenJsonElement(item, key, parameters);
                    index++;
                }

                break;

            case JsonValueKind.String:
                parameters[prefix] = element.GetString() ?? string.Empty;
                break;

            case JsonValueKind.Number:
                parameters[prefix] = element.GetRawText();
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                parameters[prefix] = element.GetBoolean().ToString().ToLower();
                break;

            case JsonValueKind.Null:
                parameters[prefix] = string.Empty;
                break;
        }
    }
}