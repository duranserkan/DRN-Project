using DRN.Framework.Utils.Factories;
using Flurl.Http;
using Microsoft.AspNetCore.Mvc;

namespace Sample.Hosted.Controllers;

[ApiController]
[Route("[controller]")]
public class NexusStatusController(IInternalRequest request) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(200)]
    public async Task<ActionResult<string>> Status()
    {
        var response = await request.For("nexus/status").AllowAnyHttpStatus().GetAsync();
        var payload = await response.GetStringAsync();

        return StatusCode(response.StatusCode, payload);
    }
}