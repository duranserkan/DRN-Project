using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DRN.Nexus.Hosted.Controllers;

[ApiController]
[Route("[controller]")]
public class StatusController(IAppSettings appSettings) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(200)]
    [Authorize]
    public ActionResult Status()
    {
        return Ok(appSettings.GetDebugView().ToSummary());
    }
}