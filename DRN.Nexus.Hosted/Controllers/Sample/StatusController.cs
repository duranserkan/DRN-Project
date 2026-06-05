using DRN.Framework.Utils.Settings;

namespace DRN.Nexus.Hosted.Controllers.Sample;

[ApiController]
[Route(NexusEndpointFor.ControllerRouteTemplate)]
public class StatusController(IAppSettings appSettings) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public ActionResult Status()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        return Ok(appSettings.GetDebugView().ToSummary());
    }
}
