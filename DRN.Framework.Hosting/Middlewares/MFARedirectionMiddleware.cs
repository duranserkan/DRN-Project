using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Middlewares;

public class MFARedirectionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, IScopedUser scopedUser, MFARedirectionOptions redirectionOptions,
        MFAExemptionOptions exemptionOptions)
    {
        if (!MFAFor.MFACompleted && exemptionOptions.ExemptAuthSchemes.Any())
        {
            foreach (var exemptAuthScheme in exemptionOptions.ExemptAuthSchemes)
            {
                var result = await httpContext.AuthenticateAsync(exemptAuthScheme);
                if (result is not { Succeeded: true, Principal: not null }) continue;

                ((ScopedUser)scopedUser).SetExemptionSchemes(exemptAuthScheme);
                break;
            }
        }

        var requestPath = httpContext.Request.Path;
        if (redirectionOptions.RedirectionNotNeeded(requestPath))
        {
            await next(httpContext);
            return;
        }

        var pathIsMFALoginUrl = redirectionOptions.IsMFALoginUrl(requestPath);
        if (MFAFor.MFAInProgress)
        {
            if (pathIsMFALoginUrl)
                await next(httpContext);
            else
                httpContext.Response.Redirect(redirectionOptions.MFALoginUrl);
            return;
        }

        var pathIsMFASetupUrl = redirectionOptions.IsMFASetupUrl(requestPath);
        if (MFAFor.MFASetupRequired)
        {
            if (pathIsMFASetupUrl)
                await next(httpContext);
            else
                httpContext.Response.Redirect(redirectionOptions.MFASetupUrl);
            return;
        }

        if (MFAFor.MFARenewalRequired || pathIsMFALoginUrl || pathIsMFASetupUrl)
        {
            httpContext.Response.Redirect(redirectionOptions.LoginUrl);
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

    internal void MapFromConfig(MFARedirectionConfig config)
    {
        MFALoginUrl = config.MFALoginUrl;
        MFASetupUrl = config.MFASetupUrl;
        LoginUrl = config.LoginUrl;
        LogoutUrl = config.LogoutUrl;
        AppPages = config.AppPages;
    }

    /// <summary>
    ///  If not in redirection list let it go
    /// </summary>
    public bool RedirectionNotNeeded(string requestPath) => !AppPages.Contains(requestPath);

    public bool IsMFALoginUrl(string requestPath) => requestPath.Equals(MFALoginUrl, StringComparison.OrdinalIgnoreCase);
    public bool IsMFASetupUrl(string requestPath) => requestPath.Equals(MFASetupUrl, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Required to configure MFA Redirection. When provided by <see cref="DrnProgramBase{TProgram}.ConfigureMFARedirection"/>,
/// MFARedirectionMiddleware will be added.
/// </summary>
public class MFARedirectionConfig
{
    /// <summary>
    /// Required to configure MFA Redirection. When provided by <see cref="DrnProgramBase{TProgram}.ConfigureMFARedirection"/>,
    /// MFARedirectionMiddleware will be added.
    /// </summary>
    /// <param name="mfaSetupUrl"><see cref="MFAFor.MFASetupRequired"/> Redirect url</param>
    /// <param name="mfaLoginUrl"><see cref="MFAFor.MFAInProgress"/> Redirect url</param>
    /// <param name="loginUrl"><see cref="MFAFor.MFARenewalRequired"/> Redirect url</param>
    /// <param name="logoutUrl">Redirection exception for logout requests</param>
    /// <param name="appPages">Page whitelist that requires redirection. Non whitelisted paths and static assets like Favicon doesn't require redirection</param>
    public MFARedirectionConfig(string mfaSetupUrl, string mfaLoginUrl, string loginUrl, string logoutUrl, HashSet<string> appPages)
    {
        MFASetupUrl = mfaSetupUrl;
        MFALoginUrl = mfaLoginUrl;
        LoginUrl = loginUrl;
        LogoutUrl = logoutUrl;

        AppPages = appPages;
        AppPages.Remove(loginUrl);
        AppPages.Remove(logoutUrl);
    }

    /// <summary><see cref="MFAFor.MFASetupRequired"/> Redirect url</summary>
    public string MFASetupUrl { get; }

    /// <summary><see cref="MFAFor.MFAInProgress"/> Redirect url</summary>
    public string MFALoginUrl { get; }

    /// <summary><see cref="MFAFor.MFARenewalRequired"/> Redirect url</summary>
    public string LoginUrl { get; }

    /// <summary>Redirection exception for logout requests</summary>
    public string LogoutUrl { get; }

    /// <summary>Page whitelist that requires redirection. Non whitelisted paths such as api endpoints and static assets like Favicon doesn't require redirection</summary>
    public HashSet<string> AppPages { get; }
}