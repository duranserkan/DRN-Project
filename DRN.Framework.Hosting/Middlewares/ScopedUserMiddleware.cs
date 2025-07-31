using DRN.Framework.Hosting.Consent;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DRN.Framework.Hosting.Middlewares;

public class ScopedUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, IScopedUser scopedUser, IScopedLog log, IOptions<CookiePolicyOptions> options)
    {
        ((ScopedUser)scopedUser).SetUser(httpContext.User);

        log.Add("User_Authenticated", scopedUser.Authenticated);
        log.AddIfNotNullOrEmpty("User_Id", scopedUser.Id ?? string.Empty);
        log.AddIfNotNullOrEmpty("User_Amr", scopedUser.Amr ?? string.Empty);

        var consentCookie = httpContext.ToConsentCookieModel(options.Value);
        ScopeContext.Data.SetParameter(nameof(ConsentCookie), consentCookie);

        var userConsentNeeded = options.Value.CheckConsentNeeded?.Invoke(httpContext) ?? false;
        if (!string.IsNullOrWhiteSpace(consentCookie.ConsentString) && userConsentNeeded)
        {
            log.Add("Consent_Analytics", consentCookie.Values.AnalyticsConsent ?? false);
            log.Add("Consent_Marketing", consentCookie.Values.MarketingConsent ?? false);
        }

        await next(httpContext);
    }
}