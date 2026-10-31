using System.ComponentModel.DataAnnotations;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Logging.Diagnostics;

namespace Sample.Hosted.Controllers.Sample;

/// <summary>
/// Receives client-side JavaScript error reports.
/// Authenticated reports are limited to five per minute per user and 3,600 bytes per request.
/// </summary>
[ApiController]
[Authorize]
[Route(SampleApiFor.ControllerRouteTemplate)]
public class ClientErrorController(IScopedLog scopedLog, IDiagnosticSanitizer sanitizer) : ControllerBase
{
    private static readonly DiagnosticCapturePolicy CapturePolicy = new(
        new DiagnosticNameException(DiagnosticCaptureKind.Property, "Random diagnostic correlation identifier.",
            "ClientError", nameof(ClientErrorPayload.ClientErrorId)));

    public ClientErrorController(IScopedLog scopedLog) : this(scopedLog, new DiagnosticSanitizer(new DiagnosticLogOptions())) { }

    [HttpPost("Report")]
    [RequestSizeLimit(3600)]
    public IActionResult Report([FromBody] ClientErrorPayload? payload)
    {
        if (string.IsNullOrWhiteSpace(ScopeContext.UserId))
            return Forbid();

        if (payload is null || string.IsNullOrWhiteSpace(payload.Message))
            return BadRequest();

        var safe = payload with
        {
            Message = sanitizer.SanitizeText(payload.Message), Source = sanitizer.SanitizeText(payload.Source),
            Stack = sanitizer.SanitizeText(payload.Stack), Url = sanitizer.SanitizeUrl(payload.Url),
            UserAgent = sanitizer.SanitizeText(payload.UserAgent), Timestamp = sanitizer.SanitizeText(payload.Timestamp)
        };
        scopedLog.AddPropertiesWithPolicy("ClientError", safe, CapturePolicy);
        scopedLog.AddWarning("Client-side error reported. See the grouped ClientError fields.");

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
    [Sensitive(false, Justification = "Random diagnostic correlation identifier.")]
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

    [Sensitive(false, Justification = "Diagnostic redaction status flag.")]
    public bool DiagnosticRedacted { get; init; }
    public bool DiagnosticLimited { get; init; }
}
