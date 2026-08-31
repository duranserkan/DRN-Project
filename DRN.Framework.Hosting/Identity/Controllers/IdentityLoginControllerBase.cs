// This file is licensed to you under the MIT license.

using System.Security.Claims;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DRN.Framework.Hosting.Identity.Controllers;

[ApiController]
[AllowAnonymous]
public abstract class IdentityLoginControllerBase<TUser> : ControllerBase where TUser : IdentityUser
{
    private const string InvalidLoginMessage = "Invalid email or password.";
    private static readonly TimeSpan MfaSetupCredentialLifetime = TimeSpan.FromMinutes(5);

    private readonly SignInManager<TUser> _signInManager;
    private readonly TimeProvider _timeProvider;
    private readonly IOptionsMonitor<BearerTokenOptions> _bearerTokenOptions;
    private readonly IOptions<AuthorizationOptions> _authorizationOptions;
    private readonly MfaClaimConfig _mfaClaimConfig;

    protected IdentityLoginControllerBase()
    {
        var sp = ScopeContext.Services;
        _signInManager = sp.GetRequiredService<SignInManager<TUser>>();
        _timeProvider = sp.GetRequiredService<TimeProvider>();
        _bearerTokenOptions = sp.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>();
        _authorizationOptions = sp.GetRequiredService<IOptions<AuthorizationOptions>>();
        _mfaClaimConfig = sp.GetService<MfaClaimConfig>() ?? MfaClaimConfig.AspNetIdentity;
    }

    [HttpPost(nameof(Login))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(AccessTokenResponse), StatusCodes.Status200OK)]
    public virtual async Task<IResult> Login([FromBody] LoginRequest login, [FromQuery] bool? useCookies, [FromQuery] bool? useSessionCookies)
    {
        var useCookieScheme = useCookies == true || useSessionCookies == true;
        var isPersistent = useCookies == true && useSessionCookies != true;
        _signInManager.AuthenticationScheme = useCookieScheme ? IdentityConstants.ApplicationScheme : IdentityConstants.BearerScheme;

        var isMfaEnforced = MfaAuthorization.IsMfaEnforced(_authorizationOptions.Value);
        if (isMfaEnforced)
        {
            var user = await _signInManager.UserManager.FindByEmailAsync(login.Email);
            if (user == null)
                return TypedResults.Problem(InvalidLoginMessage, statusCode: StatusCodes.Status401Unauthorized);

            var isTwoFactorEnabled = await _signInManager.UserManager.GetTwoFactorEnabledAsync(user);
            if (!isTwoFactorEnabled)
            {
                var passwordCheck = await _signInManager.CheckPasswordSignInAsync(user, login.Password, lockoutOnFailure: true);
                if (!passwordCheck.Succeeded)
                    return TypedResults.Problem(InvalidLoginMessage, statusCode: StatusCodes.Status401Unauthorized);

                var expiresUtc = _timeProvider.GetUtcNow().Add(MfaSetupCredentialLifetime);
                if (useCookieScheme)
                {
                    await _signInManager.SignInAsync(user, new AuthenticationProperties
                    {
                        AllowRefresh = false,
                        ExpiresUtc = expiresUtc,
                        IsPersistent = false
                    }, MfaClaimValues.MfaSetupRequired);

                    return TypedResults.Empty;
                }

                var setupPrincipal = await _signInManager.CreateUserPrincipalAsync(user);
                if (setupPrincipal.Identity is not ClaimsIdentity setupIdentity)
                    throw new InvalidOperationException("The user principal must contain a claims identity.");

                setupIdentity.AddClaim(new Claim(ClaimConventions.AuthenticationMethod, MfaClaimValues.MfaSetupRequired));
                var setupTicket = new AuthenticationTicket(setupPrincipal, new AuthenticationProperties
                {
                    AllowRefresh = false,
                    ExpiresUtc = expiresUtc
                }, $"{IdentityConstants.BearerScheme}:AccessToken");
                var accessTokenProtector = _bearerTokenOptions.Get(IdentityConstants.BearerScheme).BearerTokenProtector;

                return TypedResults.Ok(new AccessTokenResponse
                {
                    AccessToken = accessTokenProtector.Protect(setupTicket),
                    ExpiresIn = (long)MfaSetupCredentialLifetime.TotalSeconds,
                    RefreshToken = string.Empty
                });
            }
        }

        var result = await _signInManager.PasswordSignInAsync(login.Email, login.Password, isPersistent, lockoutOnFailure: true);
        if (result.RequiresTwoFactor)
        {
            if (!string.IsNullOrEmpty(login.TwoFactorCode))
                result = await _signInManager.TwoFactorAuthenticatorSignInAsync(login.TwoFactorCode, isPersistent, rememberClient: isPersistent);
            else if (!string.IsNullOrEmpty(login.TwoFactorRecoveryCode))
                result = await _signInManager.TwoFactorRecoveryCodeSignInAsync(login.TwoFactorRecoveryCode);
        }

        if (!result.Succeeded)
            return TypedResults.Problem(InvalidLoginMessage, statusCode: StatusCodes.Status401Unauthorized);

        // The signInManager already produced the needed response in the form of a cookie or bearer token.
        return TypedResults.Empty;
    }

    [HttpPost(nameof(Refresh))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(AccessTokenResponse), StatusCodes.Status200OK)]
    public virtual async Task<IResult> Refresh([FromBody] RefreshRequest refreshRequest)
    {
        var refreshTokenProtector = _bearerTokenOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
        var refreshTicket = refreshTokenProtector.Unprotect(refreshRequest.RefreshToken);

        // Reject the /refresh attempt with a 401 if the token expired or the security stamp validation fails
        if (refreshTicket?.Properties.ExpiresUtc is not { } expiresUtc ||
            _timeProvider.GetUtcNow() >= expiresUtc ||
            await _signInManager.ValidateSecurityStampAsync(refreshTicket.Principal) is not { } user)
            return TypedResults.Challenge();

        var newPrincipal = await _signInManager.CreateUserPrincipalAsync(user);
        if (newPrincipal.Identity is not ClaimsIdentity newIdentity)
            return TypedResults.SignIn(newPrincipal, authenticationScheme: IdentityConstants.BearerScheme);

        PreserveClaims(refreshTicket.Principal, newIdentity, ClaimConventions.AuthenticationMethodReference);
        if (!string.Equals(_mfaClaimConfig.ClaimType, ClaimConventions.AuthenticationMethodReference, StringComparison.OrdinalIgnoreCase) &&
            HasAuthenticatedClaim(refreshTicket.Principal, _mfaClaimConfig.ClaimType, _mfaClaimConfig.ClaimValue) &&
            !newIdentity.HasClaim(_mfaClaimConfig.ClaimType, _mfaClaimConfig.ClaimValue))
        {
            newIdentity.AddClaim(new Claim(_mfaClaimConfig.ClaimType, _mfaClaimConfig.ClaimValue));
        }

        return TypedResults.SignIn(newPrincipal, authenticationScheme: IdentityConstants.BearerScheme);
    }

    private static void PreserveClaims(ClaimsPrincipal source, ClaimsIdentity target, string claimType)
    {
        foreach (var identity in source.Identities.Where(i => i.IsAuthenticated))
            foreach (var claim in identity.FindAll(claimType))
                if (!target.HasClaim(claim.Type, claim.Value))
                    target.AddClaim(new Claim(claim.Type, claim.Value));
    }

    private static bool HasAuthenticatedClaim(ClaimsPrincipal source, string claimType, string claimValue)
    {
        foreach (var identity in source.Identities)
            if (identity.IsAuthenticated && identity.HasClaim(claimType, claimValue))
                return true;

        return false;
    }
}
