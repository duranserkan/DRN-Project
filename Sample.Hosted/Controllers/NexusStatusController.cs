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
    public async Task<ActionResult<string>> Status([FromQuery] string? name)
    {
        var result = await "http://nexus/status"
            .AppendQueryParam("name", name)
            //.WithSettings(x => x.HttpVersion = "2.0")
            .AllowAnyHttpStatus()
            .GetAsync();
        var status = await result.GetStringAsync();

        return StatusCode(result.StatusCode, status);
    }
}