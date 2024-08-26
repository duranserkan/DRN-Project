using DRN.Framework.Utils.Auth;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Middlewares;

public class ScopedUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, IScopedUser scopedUser)
    {
        var user = httpContext.User;
        ((ScopedUser)scopedUser).SetUser(user);

        await next(httpContext);
    }
}