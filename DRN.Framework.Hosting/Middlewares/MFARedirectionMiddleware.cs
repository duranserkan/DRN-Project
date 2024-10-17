using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Middlewares;

public class MFARedirectionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, MFARedirectionOptions options)
    {
        var requestPath = httpContext.Request.Path;
        if (!options.RedirectionNeeded(requestPath))
        {
            await next(httpContext);
            return;
        }

        var pathIsMFALoginUrl = options.IsMFALoginUrl(requestPath);
        if (MFAFor.MFAInProgress)
        {
            if (pathIsMFALoginUrl)
                await next(httpContext);
            else
                httpContext.Response.Redirect(options.MFALoginUrl);
            return;
        }

        var pathIsMFASetupUrl = options.IsMFASetupUrl(requestPath);
        if (MFAFor.MFASetupRequired)
        {
            if (pathIsMFASetupUrl)
                await next(httpContext);
            else
                httpContext.Response.Redirect(options.MFASetupUrl);
            return;
        }

        if (MFAFor.MFARenewalRequired)
        {
            httpContext.Response.Redirect(options.MFALoginUrl);
            return;
        }

        if (pathIsMFALoginUrl || pathIsMFASetupUrl)
        {
            httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
            await httpContext.Response.WriteAsync("Resource not available");
            return;
        }

        await next(httpContext);
    }
}

[Singleton<MFARedirectionOptions>]
public class MFARedirectionOptions
{
    public string MFALoginUrl { get; internal set; } = string.Empty;
    public string MFASetupUrl { get; internal set; } = string.Empty;
    public string LoginUrl { get; internal set; } = string.Empty;
    public string LogoutUrl { get; internal set; } = string.Empty;
    public HashSet<string> AppPages { get; internal set; } = new(StringComparer.OrdinalIgnoreCase);

    public bool RedirectionNeeded(string requestPath) => AppPages.Contains(requestPath) && !requestPath.Equals(LogoutUrl);
    public bool IsMFALoginUrl(string requestPath) => requestPath.Equals(MFALoginUrl, StringComparison.OrdinalIgnoreCase);
    public bool IsMFASetupUrl(string requestPath) => requestPath.Equals(MFASetupUrl, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Required to configure MFA Redirection. When provided by <see cref="DrnProgramBase{TProgram}.ConfigureMFARedirection"/>,
/// MFARedirectionMiddleware will be added.
/// </summary>
/// <param name="MFASetupUrl"><see cref="MFAFor.MFASetupRequired"/> Redirect url</param>
/// <param name="MFALoginUrl"><see cref="MFAFor.MFAInProgress"/> Redirect url</param>
/// <param name="LoginUrl"><see cref="MFAFor.MFARenewalRequired"/> Redirect url</param>
/// <param name="LogoutUrl">Redirection exception for logout requests</param>
/// <param name="AppPages">Page whitelist that requires redirection. Non whitelisted paths and static assets like Favicon doesn't require redirection</param>
public record MFARedirectionConfig(string MFASetupUrl, string MFALoginUrl, string LoginUrl, string LogoutUrl, HashSet<string> AppPages);