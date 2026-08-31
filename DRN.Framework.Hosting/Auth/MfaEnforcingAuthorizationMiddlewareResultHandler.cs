using System.Security.Claims;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DRN.Framework.Hosting.Auth;

[Singleton<IAuthorizationMiddlewareResultHandler>(tryAdd: false)]
public sealed class MfaEnforcingAuthorizationMiddlewareResultHandler(
    IOptions<AuthorizationOptions> authorizationOptions,
    MfaClaimConfig? claimConfig = null,
    MfaExemptionOptions? exemptionOptions = null) : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();
    private readonly bool _mfaNotEnforced = !MfaAuthorization.IsMfaEnforced(authorizationOptions.Value);
    private readonly MfaClaimConfig _claimConfig = claimConfig ?? MfaClaimConfig.AspNetIdentity;
    private readonly MfaExemptionOptions _exemptionOptions = exemptionOptions ?? new MfaExemptionOptions();

    public Task HandleAsync(RequestDelegate next, HttpContext context, AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        var authorizationFailed = !authorizeResult.Succeeded;
        var isPolicyMfaExempt = MfaAuthorization.IsPolicyMfaExempt(policy);

        if (authorizationFailed || _mfaNotEnforced || isPolicyMfaExempt)
            return _defaultHandler.HandleAsync(next, context, policy, authorizeResult);

        var scopedUser = ScopeContext.User;
        var allowedExemptionPrincipal = GetPolicyExemptionPrincipal(policy, _exemptionOptions, scopedUser, context);
        var isMfaSatisfied = MfaAuthorization.IsMfaSatisfied(context.User, _claimConfig, _exemptionOptions, scopedUser.ExemptionScheme, allowedExemptionPrincipal);

        if (isMfaSatisfied)
            return _defaultHandler.HandleAsync(next, context, policy, authorizeResult);

        var isAuthenticated = AuthenticationFor.IsAuthenticated(context.User);
        var mfaResult = isAuthenticated
            ? PolicyAuthorizationResult.Forbid()
            : PolicyAuthorizationResult.Challenge();

        return _defaultHandler.HandleAsync(next, context, policy, mfaResult);
    }

    private static ClaimsPrincipal? GetPolicyExemptionPrincipal(AuthorizationPolicy policy, MfaExemptionOptions exemptionOptions, IScopedUser? scopedUser, HttpContext context)
    {
        if (scopedUser is not { HasExemptionScheme: true } || exemptionOptions.ExemptAuthSchemes.Count == 0)
            return null;

        var exemptionScheme = scopedUser.ExemptionScheme;
        if (string.IsNullOrWhiteSpace(exemptionScheme) || !exemptionOptions.ExemptAuthSchemes.Contains(exemptionScheme))
            return null;

        if (policy.AuthenticationSchemes.Count > 0)
            return policy.AuthenticationSchemes.Contains(exemptionScheme, StringComparer.OrdinalIgnoreCase)
                ? scopedUser.ExemptionPrincipal
                : null;

        var authOptions = context.RequestServices.GetService<IOptions<AuthenticationOptions>>()?.Value;
        var defaultScheme = authOptions?.DefaultAuthenticateScheme ?? authOptions?.DefaultScheme;
        return string.Equals(defaultScheme, exemptionScheme, StringComparison.OrdinalIgnoreCase)
            ? scopedUser.ExemptionPrincipal
            : null;
    }
}
