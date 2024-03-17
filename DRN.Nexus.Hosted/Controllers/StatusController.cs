using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Mvc;

namespace DRN.Nexus.Hosted.Controllers;

[ApiController]
[Route("[controller]")]
public class StatusController(IAppSettings appSettings) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(200)]
    public ActionResult Status([FromQuery] string? name)
    {
        return Ok(appSettings.GetDebugView().ToSummary());
    }
}