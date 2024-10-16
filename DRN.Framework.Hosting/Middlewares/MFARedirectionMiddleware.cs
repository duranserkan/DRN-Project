using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Middlewares;

public class MFARedirectionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, MFARedirectionOptions options)
    {
        var redirectionNotNeeded = !options.AppPages.Contains(httpContext.Request.Path);
        if (redirectionNotNeeded)
        {
            await next(httpContext);
            return;
        }

        if (MFAFor.MFAInProgress && httpContext.Request.Path != options.MFALoginUrl)
            httpContext.Response.Redirect(options.MFALoginUrl);
        else if (MFAFor.MFASetupRequired && httpContext.Request.Path != options.MFASetupUrl)
            httpContext.Response.Redirect(options.MFASetupUrl);
        else
            await next(httpContext);
    }
}

[Singleton<MFARedirectionOptions>]
public class MFARedirectionOptions
{
    public string MFALoginUrl { get; internal set; } = string.Empty;
    public string MFASetupUrl { get; internal set; } = string.Empty;
    public HashSet<string> AppPages { get; internal set; } = [];
}

/// <summary>
/// Required to configure MFA Redirection. When provided by <see cref="DrnProgramBase{TProgram}.ConfigureMFARedirection"/>,
/// MFARedirectionMiddleware will be added.
/// </summary>
/// <param name="MFALoginUrl"><see cref="MFAFor.MFAInProgress"/> Redirect url</param>
/// <param name="MFASetupUrl"><see cref="MFAFor.MFASetupRequired"/> Redirect url</param>
/// <param name="AppPages">Page whitelist that requires redirection. Static assets like Favicon doesn't require redirection</param>
public record MFARedirectionConfig(string MFALoginUrl, string MFASetupUrl, HashSet<string> AppPages);