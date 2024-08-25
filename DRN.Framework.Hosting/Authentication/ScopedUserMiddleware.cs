using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Authentication;

public class ScopedUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, IScopedUser scopedUser)
    {
        var user = httpContext.User;
        ((ScopedUser)scopedUser).SetUser(user);

        await next(httpContext);
    }
}