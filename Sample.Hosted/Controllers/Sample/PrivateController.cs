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

    [HttpGet("scope-summary")]
    [ProducesResponseType(200)]
    public ActionResult<ScopeSummary> Context() => Ok(ScopeContext.Value.GetScopeSummary());

    [AllowAnonymous]
    [HttpGet("validate-scope")]
    [ProducesResponseType(200)]
    public ActionResult<bool> ValidateScope()
    {
        var isValid = ScopeContext.TraceId == HttpContext.TraceIdentifier
                      && ScopeContext.User == scopedUser
                      && ScopeContext.User.Principal == User
                      && ScopeContext.Log == scopedLog;

        return isValid ? Ok() : throw ExceptionFor.Configuration("InvalidScope");
    }
}