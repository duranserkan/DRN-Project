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
        if (exemptionOptions.ExemptAuthSchemes.Count > 0)
            foreach (var exemptAuthScheme in exemptionOptions.ExemptAuthSchemes)
            {
                var result = await httpContext.AuthenticateAsync(exemptAuthScheme);
                if (result is not { Succeeded: true, Principal: not null })
                    continue;

                var hasAuthenticatedIdentity = false;
                var isSetupRequired = false;

                foreach (var identity in result.Principal.Identities)
                {
                    if (!identity.IsAuthenticated)
                        continue;

                    hasAuthenticatedIdentity = true;

                    foreach (var claim in identity.Claims)
                    {
                        if (!string.Equals(claim.Type, ClaimConventions.AuthenticationMethod, StringComparison.OrdinalIgnoreCase) || claim.Value != MfaClaimValues.MfaSetupRequired)
                            continue;
                        isSetupRequired = true;
                        break;
                    }

                    if (isSetupRequired)
                        break;
                }

                if (!hasAuthenticatedIdentity || isSetupRequired)
                    continue;

                if (scopedUser is not ScopedUser concreteScopedUser)
                    continue;

                concreteScopedUser.SetExemption(exemptAuthScheme, result.Principal);
                break;
            }

        await next(httpContext);
    }
}
