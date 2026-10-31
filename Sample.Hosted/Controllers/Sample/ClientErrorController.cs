using System.ComponentModel.DataAnnotations;
using DRN.Framework.Utils.Logging;

namespace Sample.Hosted.Controllers.Sample;

/// <summary>
/// Receives client-side JavaScript error reports.
/// Authenticated reports are limited to five per minute per user and 3,600 bytes per request.
/// </summary>
[ApiController]
[Authorize]
[Route(SampleApiFor.ControllerRouteTemplate)]
public class ClientErrorController(IScopedLog scopedLog) : ControllerBase
{
    [HttpPost("Report")]
    [RequestSizeLimit(3600)]
    public IActionResult Report([FromBody] ClientErrorPayload? payload)
    {
        if (string.IsNullOrWhiteSpace(ScopeContext.UserId))
            return Forbid();

        if (payload is null || string.IsNullOrWhiteSpace(payload.Message))
            return BadRequest();

        scopedLog.AddProperties("ClientError", payload);
        scopedLog.AddWarning($"Client-side error: {payload.Message}");

        return Ok();
    }
}

/// <summary>
/// Payload shape for client-side error reports.
/// The client bounds serialized UTF-8 bytes; the server enforces request size and field validation.
/// </summary>
public record ClientErrorPayload
{
    [StringLength(32)]
    [RegularExpression("^[a-f0-9]{32}$")]
    public string? ClientErrorId { get; init; }

    [StringLength(500)]
    public string Message { get; init; } = string.Empty;

    [StringLength(200)]
    public string Source { get; init; } = string.Empty;

    public int? Line { get; init; }
    public int? Column { get; init; }

    [StringLength(2000)]
    public string Stack { get; init; } = string.Empty;

    [StringLength(500)]
    public string Url { get; init; } = string.Empty;

    [StringLength(300)]
    public string UserAgent { get; init; } = string.Empty;

    [StringLength(30)]
    public string Timestamp { get; init; } = string.Empty;
}
