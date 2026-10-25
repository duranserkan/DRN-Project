using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting.Middlewares.ExceptionHandler;

public static class AccessDeniedCookieEventsExtensions
{
    /// <summary>
    /// Preserves the denied request's reference for <see cref="AccessDeniedPageModel"/> on 302 cookie redirects.
    /// </summary>
    /// <remarks>
    /// Requires MVC TempData services. Call on the active events instance after assigning other redirect callbacks.
    /// The existing callback runs first; nonredirect responses remain unchanged.
    /// </remarks>
    public static void PreserveAccessDeniedRequestId(this CookieAuthenticationEvents events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var redirectToAccessDenied = events.OnRedirectToAccessDenied;
        events.OnRedirectToAccessDenied = async context =>
        {
            await redirectToAccessDenied(context);
            if (context.Response.StatusCode != StatusCodes.Status302Found)
                return;

            var tempData = context.HttpContext.RequestServices.GetRequiredService<ITempDataDictionaryFactory>()
                .GetTempData(context.HttpContext);
            tempData[AccessDeniedPageModel.RequestIdTempDataKey] = context.HttpContext.TraceIdentifier;
            tempData.Save();
        };
    }
}
