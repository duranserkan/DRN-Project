using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Consent;

public static class CookieConsentExtensions
{
    public static ConsentCookie ToConsentCookieModel(this HttpContext httpContext, CookiePolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var cookieName = options.ConsentCookie.Name;
        if (cookieName == null)
            return new ConsentCookie(cookieName ?? string.Empty, null);

        _ = httpContext.Request.Cookies.TryGetValue(cookieName, out var consentCookie);
        var consentModel = new ConsentCookie(cookieName, consentCookie);

        return consentModel;
    }
}