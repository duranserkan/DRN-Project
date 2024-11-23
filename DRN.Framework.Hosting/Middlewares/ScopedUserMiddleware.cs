using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace DRN.Framework.Hosting.Middlewares;

public class ScopedUserMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, IScopedUser scopedUser, IScopedLog log)
    {
        ((ScopedUser)scopedUser).SetUser(httpContext.User);

        log.Add("UserAuthenticated", scopedUser.Authenticated);
        log.Add("UserId", scopedUser.Id ?? string.Empty);
        log.Add("amr", scopedUser.Amr ?? string.Empty);

        var consentFeature = httpContext.Features.Get<ITrackingConsentFeature>();
        if (consentFeature != null)
        {
            log.Add(nameof(ITrackingConsentFeature.IsConsentNeeded), consentFeature.IsConsentNeeded);
            log.Add(nameof(ITrackingConsentFeature.HasConsent), consentFeature.HasConsent);
            log.Add(nameof(ITrackingConsentFeature.CanTrack), consentFeature.CanTrack);
        }


        await next(httpContext);
    }
}