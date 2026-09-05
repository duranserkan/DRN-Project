using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using DRN.Framework.Hosting.Auth;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Scope;
using Flurl;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting.Middlewares;

public class MfaRedirectionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext httpContext, MfaRedirectionOptions redirectionOptions)
    {
        var requestPath = httpContext.Request.Path;
        if (!redirectionOptions.AppPages.Contains(requestPath))
        {
            await next(httpContext);

            return;
        }

        var policy = await MfaPolicyProof.ResolvePolicyAsync(httpContext);
        if (policy == null)
        {
            await next(httpContext);
            return;
        }

        // Browser navigation must use the same selected user as authorization, not the default cookie snapshot.
        await httpContext.RequestServices.GetRequiredService<IPolicyEvaluator>().AuthenticateAsync(policy, httpContext);
        var user = httpContext.User;
        var config = httpContext.RequestServices.GetService<MfaClaimConfig>() ?? MfaClaimConfig.AspNetIdentity;
        var exemptions = httpContext.RequestServices.GetService<MfaExemptionOptions>() ?? new MfaExemptionOptions();
        if (MfaPolicyProof.IsSatisfied(httpContext, user, config, exemptions))
        {
            await next(httpContext);
            return;
        }

        var pathIsMFALoginUrl = redirectionOptions.IsMfaLoginUrl(requestPath);
        if (MfaPrincipal.HasState(user, MfaClaimValues.MfaInProgress))
        {
            if (pathIsMFALoginUrl)
                await next(httpContext);
            else
                httpContext.Response.Redirect(redirectionOptions.MfaLoginUrl);
            return;
        }

        var pathIsMFASetupUrl = redirectionOptions.IsMfaSetupUrl(requestPath);
        if (MfaPrincipal.HasState(user, MfaClaimValues.MfaSetupRequired))
        {
            if (pathIsMFASetupUrl)
                await next(httpContext);
            else
                httpContext.Response.Redirect(redirectionOptions.MfaSetupUrl);
            return;
        }

        if (user.Identities.Any(identity => identity.IsAuthenticated) || pathIsMFALoginUrl || pathIsMFASetupUrl)
        {
            httpContext.Response.Redirect(redirectionOptions.LoginUrl);
            return;
        }

        await next(httpContext);

        RedirectToLoginIfNotAuthorized(httpContext, redirectionOptions);
    }

    private static void RedirectToLoginIfNotAuthorized(HttpContext httpContext, MfaRedirectionOptions redirectionOptions)
    {
        var requestPath = httpContext.Request.Path;
        if (httpContext.Response.StatusCode != 401 || !redirectionOptions.AppPages.Contains(requestPath))
            return;

        var returnUrl = new Url(requestPath);
        if (httpContext.Request.Query.Count > 0)
            foreach (var pair in httpContext.Request.Query)
            {
                foreach (var parameterValue in pair.Value)
                    returnUrl.SetQueryParam(pair.Key, parameterValue);
            }

        var redirectionUrl = new Url(redirectionOptions.LoginUrl).SetQueryParam(DrnRedirection.ReturnUrl, returnUrl.ToString());
        httpContext.Response.Redirect(redirectionUrl.ToString());
    }
}

public static class DrnRedirection
{
    public const string ReturnUrl = nameof(ReturnUrl);
}

[Singleton<MfaRedirectionOptions>]
public class MfaRedirectionOptions
{
    public string MfaLoginUrl { get; private set; } = string.Empty;
    public string MfaSetupUrl { get; private set; } = string.Empty;
    public string LoginUrl { get; private set; } = string.Empty;
    public string LogoutUrl { get; private set; } = string.Empty;
    public IReadOnlySet<string> AppPages { get; private set; } = FrozenSet<string>.Empty;

    internal void MapFromConfig(MfaRedirectionConfig config)
    {
        MfaLoginUrl = config.MfaLoginUrl;
        MfaSetupUrl = config.MfaSetupUrl;
        LoginUrl = config.LoginUrl;
        LogoutUrl = config.LogoutUrl;
        AppPages = config.AppPages.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    ///  If not in redirection list let it go
    /// </summary>
    public bool RedirectionNotNeeded(string requestPath) =>
        MfaFor.MfaCompleted ||
        ScopeContext.User.HasExemptionScheme ||
        !AppPages.Contains(requestPath);

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
        AppPages = appPages.ToHashSet(StringComparer.OrdinalIgnoreCase);
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
