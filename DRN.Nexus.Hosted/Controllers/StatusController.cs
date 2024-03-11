using DRN.Framework.SharedKernel.Enums;
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
        var status = new StatusModel(name, appSettings.Environment, appSettings.GetDebugView());

        return Ok(status);
    }
}

public class StatusModel(string requestMadeBy, AppEnvironment environment, string debugView)
{
    public string ApplicationName { get; set; } = AppConstants.ApplicationName;
    public DateTimeOffset RequestTime { get; } = DateTimeOffset.Now;
    public AppEnvironment Environment { get; } = environment;
    public string RequestMadeBy { get; } = requestMadeBy;
    public string DebugView { get; } = debugView;
}