using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Sample.Hosted.Extensions;

/// <summary>
/// Extension methods for sending toast notifications to the client via HTMX HX-Trigger headers.
/// The client-side notification handler renders the showToast event.
/// </summary>
public static class HttpResponseToastExtensions
{
    private const string HxTriggerHeader = "HX-Trigger";
    private const string ToastEvent = "showToast";

    /// <summary>
    /// Sets the showToast event in the HX-Trigger header while preserving other events.
    /// </summary>
    /// <param name="response">The HTTP response.</param>
    /// <param name="message">Toast message text.</param>
    /// <param name="type">Toast type: success, error, warning, or info.</param>
    /// <param name="duration">Optional auto-dismiss duration in milliseconds (default 5000).</param>
    public static void SendToast(this HttpResponse response, string message, ToastType type = ToastType.Info, int? duration = null)
    {
        var payload = new ToastPayload
        {
            Type = type.ToString().ToLowerInvariant(),
            Message = message,
            Duration = duration
        };

        var existingHeader = response.Headers[HxTriggerHeader].ToString().Trim();
        JsonObject triggers;
        if (existingHeader.StartsWith('{'))
        {
            try
            {
                triggers = JsonNode.Parse(existingHeader)!.AsObject();
            }
            catch (JsonException)
            {
                triggers = new JsonObject();
            }
        }
        else
        {
            triggers = new JsonObject();
            foreach (var eventName in existingHeader.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                triggers[eventName] = new JsonObject();
        }

        PreserveInheritedTarget(triggers);
        triggers.Remove(ToastEvent);
        triggers.Insert(0, ToastEvent, JsonSerializer.SerializeToNode(payload, ToastJsonContext.Default.ToastPayload));
        response.Headers[HxTriggerHeader] = triggers.ToJsonString();
    }

    private static void PreserveInheritedTarget(JsonObject triggers)
    {
        if (!triggers.TryGetPropertyValue(ToastEvent, out var toast, out var toastIndex) ||
            toast is not JsonObject toastDetails || !toastDetails.TryGetPropertyValue("target", out var target))
            return;

        // HTMX carries the target forward, so only the next event needs to set it.
        var nextIndex = toastIndex + 1;
        if (nextIndex == triggers.Count)
            return;

        var nextEvent = triggers.GetAt(nextIndex).Value;
        if (nextEvent is JsonObject details)
        {
            if (!details.ContainsKey("target"))
                details["target"] = target?.DeepClone();
            return;
        }

        // HTMX exposes non-object details through the value property.
        triggers.SetAt(nextIndex, new JsonObject { ["value"] = nextEvent?.DeepClone(), ["target"] = target?.DeepClone() });
    }

    /// <summary>Sends a success toast.</summary>
    public static void SendToastSuccess(this HttpResponse response, string message, int? duration = null)
        => response.SendToast(message, ToastType.Success, duration);

    /// <summary>Sends an error toast.</summary>
    public static void SendToastError(this HttpResponse response, string message, int? duration = null)
        => response.SendToast(message, ToastType.Error, duration);

    /// <summary>Sends a warning toast.</summary>
    public static void SendToastWarning(this HttpResponse response, string message, int? duration = null)
        => response.SendToast(message, ToastType.Warning, duration);

    /// <summary>Sends an info toast.</summary>
    public static void SendToastInfo(this HttpResponse response, string message, int? duration = null)
        => response.SendToast(message, ToastType.Info, duration);
}

/// <summary>
/// Toast severity types matching the client-side DRN.Toast ICON_MAP keys.
/// </summary>
public enum ToastType
{
    Success,
    Error,
    Warning,
    Info
}

internal record ToastPayload
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "info";

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("duration")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Duration { get; init; }
}

/// <summary>
/// Source-generated JSON serializer context for toast payloads.
/// Avoids reflection-based serialization overhead.
/// </summary>
[JsonSerializable(typeof(ToastPayload))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class ToastJsonContext : JsonSerializerContext;
