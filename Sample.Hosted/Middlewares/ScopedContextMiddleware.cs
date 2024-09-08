using DRN.Framework.Utils.Scope;
using Sample.Domain.Identity;

namespace Sample.Hosted.Middlewares;

public class ScopedContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext)
    {
        var context = ScopeContext.Value;
        context.AddClaimValueToParametersAsInt(UserClaims.PPVersion);
        context.AddClaimValueToFlags(UserClaims.SlimUI);

        await next(httpContext);
    }
}