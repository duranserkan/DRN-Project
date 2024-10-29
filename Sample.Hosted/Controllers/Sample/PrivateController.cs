using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Scope;

namespace Sample.Hosted.Controllers.Sample;

[ApiController]
[Route(SampleApiFor.ControllerRouteTemplate)]
public class PrivateController(IScopedUser scopedUser, IScopedLog scopedLog) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(200)]
    public ActionResult<IScopedUser> Authorized() => Ok(scopedUser);

    [AllowAnonymous]
    [HttpGet("anonymous")]
    [ProducesResponseType(200)]
    public ActionResult<IScopedUser> Anonymous() => Ok(scopedUser);

    [HttpGet("scope-context")]
    [ProducesResponseType(200)]
    public ActionResult<ScopeContext> Context() => Ok(ScopeContext.Value);

    [AllowAnonymous]
    [HttpGet("validate-scope")]
    [ProducesResponseType(200)]
    public ActionResult<bool> ValidateScope()
    {
        var isValid = ScopeContext.Value.TraceId == HttpContext.TraceIdentifier
                      && ScopeContext.Value.ScopedUser == scopedUser
                      && ScopeContext.Value.ScopedUser.Principal == User
                      && ScopeContext.Value.ScopedLog == scopedLog;

        return isValid ? Ok() : throw ExceptionFor.Configuration("InvalidScope");
    }
}