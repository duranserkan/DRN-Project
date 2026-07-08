using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sample.Hosted.Extensions;

/// <summary>
/// Extension methods for sending toast notifications to the client via HTMX HX-Trigger headers.
/// The client-side listener in appPostload.js handles the showToast event and delegates to DRN.Toast.
/// </summary>
public static class HttpResponseToastExtensions
{
    private const string HxTriggerHeader = "HX-Trigger";

    /// <summary>
    /// Sends a toast notification to the client via HX-Trigger response header.
    /// Works with any HTMX request — the client-side showToast event listener renders the toast.
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

        var trigger = new { showToast = payload };
        var json = JsonSerializer.Serialize(trigger, ToastJsonContext.Default.Options);

        // HX-Trigger values are merged if the header is set multiple times
        response.Headers[HxTriggerHeader] = json;
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
