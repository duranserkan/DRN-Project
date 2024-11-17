using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Middlewares;

public class MfaExemptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, IScopedUser scopedUser, MfaExemptionOptions exemptionOptions)
    {
        if (!MfaFor.MfaCompleted && exemptionOptions.ExemptAuthSchemes.Any())
        {
            foreach (var exemptAuthScheme in exemptionOptions.ExemptAuthSchemes)
            {
                var result = await httpContext.AuthenticateAsync(exemptAuthScheme);
                if (result is not { Succeeded: true, Principal: not null }) continue;

                ((ScopedUser)scopedUser).SetExemptionScheme(exemptAuthScheme);
                break;
            }
        }

        await next(httpContext);
    }
}