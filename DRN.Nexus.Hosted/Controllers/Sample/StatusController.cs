using DRN.Framework.Utils.Configurations;
using DRN.Framework.Utils.Settings;

namespace DRN.Nexus.Hosted.Controllers.Sample;

[ApiController]
[Route(SampleApiFor.ControllerRouteTemplate)]
public class StatusController(IAppSettings appSettings) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ConfigurationDebugViewSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult Status()
    {
        if (!appSettings.IsDevelopmentEnvironment)
            return NotFound();

        return Ok(appSettings.GetDebugView().ToSummary());
    }
}
