using Flurl;
using Flurl.Http;
using Microsoft.AspNetCore.Mvc;

namespace Sample.Hosted.Controllers;

[ApiController]
[Route("[controller]")]
public class NexusStatusController() : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(200)]
    public async Task<ActionResult<string>> Status([FromQuery] string name)
    {
        var status = await "http://nexus/status"
            .AppendQueryParam("name", name)
            .WithSettings(x => x.HttpVersion = "2.0")
            .GetStringAsync();

        return Ok(status);
    }
}