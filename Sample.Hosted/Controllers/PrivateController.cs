using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Scope;

namespace Sample.Hosted.Controllers;

[ApiController]
[Route("[controller]")]
public class PrivateController(IScopedUser scopedUser, IScopedLog scopedLog) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(200)]
    public ActionResult<string> Authorized() => Ok(scopedUser);

    [AllowAnonymous]
    [HttpGet("anonymous")]
    [ProducesResponseType(200)]
    public ActionResult<string> Anonymous() => Ok(scopedUser);

    [HttpGet("scope-context")]
    [ProducesResponseType(200)]
    public ActionResult<string> Context() => Ok(ScopeContext.Value);

    [AllowAnonymous]
    [HttpGet("validate-scope")]
    [ProducesResponseType(200)]
    public ActionResult<string> ValidateScope()
    {
        var isValid = ScopeContext.Value.TraceId == HttpContext.TraceIdentifier
                      && ScopeContext.Value.ScopedUser == scopedUser
                      && ScopeContext.Value.ScopedUser.Principal == User
                      && ScopeContext.Value.ScopedLog == scopedLog;

        return isValid ? Ok() : throw ExceptionFor.Configuration("InvalidScope");
    }
}