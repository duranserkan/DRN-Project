using DRN.Framework.Utils.Settings;

namespace DRN.Nexus.Hosted.Controllers;

[ApiController]
[Route("[controller]")]
public class StatusController(IAppSettings appSettings) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(200)]
    public ActionResult Status()
    {
        return Ok(appSettings.GetDebugView().ToSummary());
    }
}