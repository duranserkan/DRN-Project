using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Logging;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Middlewares;

public class ScopedUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, IScopedUser scopedUser, IScopedLog log)
    {
        var user = httpContext.User;
        ((ScopedUser)scopedUser).SetUser(user);

        log.Add("UserAuthenticated", scopedUser.Authenticated);
        log.Add("UserId", scopedUser.Id ?? string.Empty);
        log.Add("amr", scopedUser.Amr ?? string.Empty);

        await next(httpContext);
    }
}