using DRN.Framework.Hosting.Nexus;

namespace Sample.Hosted.Controllers.Sample;

[ApiController]
[Route(ControllerRoutes.Sample)]
public class NexusStatusController(INexusClient client) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(200)]
    public async Task<ActionResult<string>> Status()
    {
        var response = await client.GetStatusAsync();

        return StatusCode(response.HttpStatus, response.Payload);
    }
}
