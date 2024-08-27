using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Scope;

namespace Sample.Hosted.Controllers;

[Authorize]
[ApiController]
[Route("[controller]")]
public class PrivateController(IScopedUser scopedUser, IScopedLog scopedLog) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(200)]
    public ActionResult<string> Authorized() => Ok(scopedUser);

    [HttpGet("scope-context")]
    [ProducesResponseType(200)]
    public ActionResult<string> Context() => Ok(ScopeContext.Value);

    [AllowAnonymous]
    [HttpGet("anonymous")]
    [ProducesResponseType(200)]
    public ActionResult<string> Anonymous() => Ok(ScopeContext.Value);

    [AllowAnonymous]
    [HttpGet("validate-scope")]
    [ProducesResponseType(200)]
    public ActionResult<string> ValidateScope() => Ok(ScopeContext.Value.TraceId == HttpContext.TraceIdentifier
                                                      && ScopeContext.Value.ScopedUser == scopedUser
                                                      && ScopeContext.Value.ScopedLog == scopedLog);
}