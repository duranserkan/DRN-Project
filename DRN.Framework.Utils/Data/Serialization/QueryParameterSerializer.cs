using System.Text.Json;
using Flurl;

namespace DRN.Framework.Utils.Data.Serialization;

public static class QueryParameterSerializer
{
    private const int DefaultMaxDepth = 10;

    public static string SerializeToQueryString<T>(T? obj, int maxDepth = DefaultMaxDepth)
    {
        if (obj is null)
            return string.Empty;

        try
        {
            var jsonDocument = JsonSerializer.SerializeToDocument(obj);
            var parameters = new Dictionary<string, string>();

            FlattenJsonElement(jsonDocument.RootElement, string.Empty, parameters, maxDepth, 0);

            return parameters.BuildQueryString();
        }
        catch (NotSupportedException)
        {
            throw new ArgumentException($"Type {typeof(T)} cannot be serialized to JSON", nameof(obj));
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException($"Invalid object structure for query parameter serialization: {ex.Message}", nameof(obj));
        }
    }

    private static void FlattenJsonElement(JsonElement element, string prefix, Dictionary<string, string> parameters, int maxDepth, int currentDepth)
    {
        if (currentDepth > maxDepth)
            throw new InvalidOperationException($"Maximum depth of {maxDepth} exceeded during query parameter serialization");

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                FlattenObject(element, prefix, parameters, maxDepth, currentDepth + 1);
                break;

            case JsonValueKind.Array:
                FlattenArray(element, prefix, parameters, maxDepth, currentDepth + 1);
                break;

            case JsonValueKind.String:
                AddParameter(parameters, prefix, element.GetString() ?? string.Empty);
                break;

            case JsonValueKind.Number:
                AddParameter(parameters, prefix, GetNumberValue(element));
                break;

            case JsonValueKind.True:
            case JsonValueKind.False:
                AddParameter(parameters, prefix, element.GetBoolean().ToString().ToLowerInvariant());
                break;

            case JsonValueKind.Null:
                AddParameter(parameters, prefix, string.Empty);
                break;

            case JsonValueKind.Undefined:
                // Skip undefined values
                break;

            default:
                throw new ArgumentException($"Unsupported JSON value kind: {element.ValueKind}");
        }
    }

    private static void FlattenObject(JsonElement element, string prefix, Dictionary<string, string> parameters, int maxDepth, int currentDepth)
    {
        foreach (var property in element.EnumerateObject())
        {
            var key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
            FlattenJsonElement(property.Value, key, parameters, maxDepth, currentDepth);
        }
    }

    private static void FlattenArray(JsonElement element, string prefix, Dictionary<string, string> parameters, int maxDepth, int currentDepth)
    {
        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            var key = $"{prefix}[{index}]";
            FlattenJsonElement(item, key, parameters, maxDepth, currentDepth);
            index++;
        }
    }

    // Use GetRawText to preserve the exact format and avoid precision loss
    private static string GetNumberValue(JsonElement element) => element.GetRawText();

    private static void AddParameter(Dictionary<string, string> parameters, string key, string value)
    {
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Parameter key cannot be null or empty", nameof(key));

        parameters[key] = value;
    }

    public static string BuildQueryString(this Dictionary<string, string> parameters) => parameters.Count == 0
        ? string.Empty
        : new Uri("http://x").SetQueryParams(parameters).Query.TrimStart('?');
}