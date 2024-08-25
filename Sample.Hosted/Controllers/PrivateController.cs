using DRN.Framework.Hosting.Authentication;

namespace Sample.Hosted.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
public class PrivateController(IScopedUser scopedUser) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(200)]
    public async Task<ActionResult<string>> Authorized() => Ok(scopedUser);

    [AllowAnonymous]
    [HttpGet("anonymous")]
    [ProducesResponseType(200)]
    public async Task<ActionResult<string>> Anonymous() => Ok(scopedUser);
}