using System.ComponentModel.DataAnnotations;
using DRN.Framework.Utils.Logging;

namespace Sample.Hosted.Controllers.Sample;

/// <summary>
/// Receives client-side JavaScript error reports.
/// Rate-limited by payload size (3.6KB max) and client-side throttling.
/// </summary>
[ApiController]
[Route(SampleApiFor.ControllerRouteTemplate)]
public class ClientErrorController(IScopedLog scopedLog) : ControllerBase
{
    [HttpPost("Report")]
    [RequestSizeLimit(3600)]
    public IActionResult Report([FromBody] ClientErrorPayload? payload)
    {
        if (payload is null || string.IsNullOrWhiteSpace(payload.Message))
            return BadRequest();

        scopedLog.AddProperties("ClientError", payload);
        scopedLog.AddWarning($"Client-side error: {payload.Message}");

        return Ok();
    }
}

/// <summary>
/// Payload shape for client-side error reports.
/// Field lengths are enforced client-side; server validates non-null message only.
/// </summary>
public record ClientErrorPayload
{
    [StringLength(500)]
    public string Message { get; init; } = string.Empty;

    [StringLength(200)]
    public string Source { get; init; } = string.Empty;

    public int Line { get; init; }
    public int Column { get; init; }

    [StringLength(2000)]
    public string Stack { get; init; } = string.Empty;

    [StringLength(500)]
    public string Url { get; init; } = string.Empty;

    [StringLength(300)]
    public string UserAgent { get; init; } = string.Empty;

    [StringLength(30)]
    public string Timestamp { get; init; } = string.Empty;
}
