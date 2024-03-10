using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Mvc;

namespace DRN.Nexus.Hosted.Controllers;

[ApiController]
[Route("[controller]")]
public class StatusController(IAppSettings appSettings) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(200)]
    public async Task<ActionResult<string>> Status([FromQuery] string name)
    {
        var status = $"{DateTimeOffset.Now:s} {AppConstants.ApplicationName} {appSettings.Environment.ToString()} Hi {name}";
        return Ok(status);
    }
}