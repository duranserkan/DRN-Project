using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Http;

namespace DRN.Framework.Hosting.Middlewares;

public class MfaRedirectionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, MfaRedirectionOptions redirectionOptions)
    {
        var requestPath = httpContext.Request.Path;
        if (redirectionOptions.RedirectionNotNeeded(requestPath))
        {
            await next(httpContext);
            return;
        }

        var pathIsMFALoginUrl = redirectionOptions.IsMfaLoginUrl(requestPath);
        if (MfaFor.MfaInProgress)
        {
            if (pathIsMFALoginUrl)
                await next(httpContext);
            else
                httpContext.Response.Redirect(redirectionOptions.MfaLoginUrl);
            return;
        }

        var pathIsMFASetupUrl = redirectionOptions.IsMfaSetupUrl(requestPath);
        if (MfaFor.MfaSetupRequired)
        {
            if (pathIsMFASetupUrl)
                await next(httpContext);
            else
                httpContext.Response.Redirect(redirectionOptions.MfaSetupUrl);
            return;
        }

        if (MfaFor.MfaRenewalRequired || pathIsMFALoginUrl || pathIsMFASetupUrl)
        {
            httpContext.Response.Redirect(redirectionOptions.LoginUrl);
            return;
        }

        await next(httpContext);
    }
}

[Singleton<MfaRedirectionOptions>]
public class MfaRedirectionOptions
{
    public string MfaLoginUrl { get; internal set; } = string.Empty;
    public string MfaSetupUrl { get; internal set; } = string.Empty;
    public string LoginUrl { get; internal set; } = string.Empty;
    public string LogoutUrl { get; internal set; } = string.Empty;
    public HashSet<string> AppPages { get; internal set; } = new(StringComparer.OrdinalIgnoreCase);

    internal void MapFromConfig(MfaRedirectionConfig config)
    {
        MfaLoginUrl = config.MfaLoginUrl;
        MfaSetupUrl = config.MfaSetupUrl;
        LoginUrl = config.LoginUrl;
        LogoutUrl = config.LogoutUrl;
        AppPages = config.AppPages;
    }

    /// <summary>
    ///  If not in redirection list let it go
    /// </summary>
    public bool RedirectionNotNeeded(string requestPath) => MfaFor.MfaCompleted || !AppPages.Contains(requestPath);

    public bool IsMfaLoginUrl(string requestPath) => requestPath.Equals(MfaLoginUrl, StringComparison.OrdinalIgnoreCase);
    public bool IsMfaSetupUrl(string requestPath) => requestPath.Equals(MfaSetupUrl, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Required to configure MFA Redirection. When provided by <see cref="DrnProgramBase{TProgram}.ConfigureMFARedirection"/>,
/// MFARedirectionMiddleware will be added.
/// </summary>
public class MfaRedirectionConfig
{
    /// <summary>
    /// Required to configure MFA Redirection. When provided by <see cref="DrnProgramBase{TProgram}.ConfigureMFARedirection"/>,
    /// MFARedirectionMiddleware will be added.
    /// </summary>
    /// <param name="mfaSetupUrl"><see cref="MfaFor.MfaSetupRequired"/> Redirect url</param>
    /// <param name="mfaLoginUrl"><see cref="MfaFor.MfaInProgress"/> Redirect url</param>
    /// <param name="loginUrl"><see cref="MfaFor.MfaRenewalRequired"/> Redirect url</param>
    /// <param name="logoutUrl">Redirection exception for logout requests</param>
    /// <param name="appPages">Page whitelist that requires redirection. Non whitelisted paths and static assets like Favicon doesn't require redirection</param>
    public MfaRedirectionConfig(string mfaSetupUrl, string mfaLoginUrl, string loginUrl, string logoutUrl, HashSet<string> appPages)
    {
        //todo: make urls array to support multiple pages
        MfaSetupUrl = mfaSetupUrl;
        MfaLoginUrl = mfaLoginUrl;
        LoginUrl = loginUrl;
        LogoutUrl = logoutUrl;

        //create new set to keep original set unchanged
        AppPages = appPages.ToHashSet();
        AppPages.Remove(loginUrl);
        AppPages.Remove(logoutUrl);
    }

    /// <summary><see cref="MfaFor.MfaSetupRequired"/> Redirect url</summary>
    public string MfaSetupUrl { get; }

    /// <summary><see cref="MfaFor.MfaInProgress"/> Redirect url</summary>
    public string MfaLoginUrl { get; }

    /// <summary><see cref="MfaFor.MfaRenewalRequired"/> Redirect url</summary>
    public string LoginUrl { get; }

    /// <summary>Redirection exception for logout requests</summary>
    public string LogoutUrl { get; }

    /// <summary>Page whitelist that requires redirection. Non whitelisted paths such as api endpoints and static assets like Favicon doesn't require redirection</summary>
    public HashSet<string> AppPages { get; }
}