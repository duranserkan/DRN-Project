using System.Security.Claims;
using DRN.Framework.Hosting.Auth;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DRN.Framework.Hosting.Identity;

/// <summary>Identity sign-in integration for configured claims and original-evidence refresh.</summary>
public class DrnSignInManager<TUser>(UserManager<TUser> userManager, IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<TUser> claimsFactory, IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<TUser>> logger, IAuthenticationSchemeProvider schemes, IUserConfirmation<TUser> confirmation,
    AuthenticationClaimConfig claims)
    : SignInManager<TUser>(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
    where TUser : class
{
    private ClaimsPrincipal? _refreshPrincipal;

    public override async Task<ClaimsPrincipal> CreateUserPrincipalAsync(TUser user)
    {
        var principal = await base.CreateUserPrincipalAsync(user);
        var hasUnambiguousAccount = MfaPrincipal.HasSingleAccount(principal, claims, requireSubject: true);
        if (!hasUnambiguousAccount)
            throw new System.Security.SecurityException("The principal factory must identify one account.");

        var identities = principal.Identities.Where(identity => identity.IsAuthenticated)
            .Select(identity => identity.Clone()).ToArray();
        foreach (var identity in identities)
        {
            // Stored profile claims cannot establish a completed authentication ceremony.
            var storedMfaMarkers = identity.Claims.Where(claim =>
            {
                var matchesConfiguredMfaMarker = claim.Type == claims.Mfa.ClaimType && claim.Value == claims.Mfa.ClaimValue;
                var matchesIdentityMfaMarker = claim is { Type: AuthClaimTypes.AuthenticationMethods, Value: MfaClaimValues.Amr };
                return matchesConfiguredMfaMarker || matchesIdentityMfaMarker;
            }).ToArray();

            foreach (var claim in storedMfaMarkers)
                identity.RemoveClaim(claim);

            if (_refreshPrincipal == null) continue;

            var refreshEvidencePreserved = MfaClaimPreservation.Preserve(_refreshPrincipal, identity, claims);
            if (!refreshEvidencePreserved)
                throw new System.Security.SecurityException("Cannot refresh a different or ambiguous account.");
        }
        return new ClaimsPrincipal(identities);
    }

    public override Task SignInWithClaimsAsync(TUser user, AuthenticationProperties? authenticationProperties,
        IEnumerable<Claim> additionalClaims)
    {
        if (_refreshPrincipal != null)
            return base.SignInWithClaimsAsync(user, authenticationProperties, []);

        var evidence = additionalClaims.ToList();
        // Identity supplies this additional claim only after successful two-factor sign-in.
        // It is deliberately not read from the principal factory or an ambient provider principal.
        var usesIdentityMfaMarker = claims.Mfa == MfaClaimConfig.AspNetIdentity;
        if (usesIdentityMfaMarker)
            return base.SignInWithClaimsAsync(user, authenticationProperties, evidence);

        foreach (var marker in evidence.Where(claim => claim is { Type: AuthClaimTypes.AuthenticationMethods, Value: MfaClaimValues.Amr }).ToArray())
        {
            var mapped = new Claim(claims.Mfa.ClaimType, claims.Mfa.ClaimValue, marker.ValueType,
                marker.Issuer, marker.OriginalIssuer);
            foreach (var property in marker.Properties)
                mapped.Properties.Add(property.Key, property.Value);
            evidence.Add(mapped);
        }

        return base.SignInWithClaimsAsync(user, authenticationProperties, evidence);
    }

    public override async Task RefreshSignInAsync(TUser user)
    {
        var result = await Context.AuthenticateAsync(AuthenticationScheme);
        var principal = result.Principal;
        if (!result.Succeeded || principal == null)
            return;

        var hasAuthenticatedIdentity = principal.Identity?.IsAuthenticated == true;
        if (!hasAuthenticatedIdentity)
            return;

        var hasUnambiguousAccount = MfaPrincipal.HasSingleAccount(principal, claims, requireSubject: true);
        if (!hasUnambiguousAccount)
            return;

        _refreshPrincipal = principal.Clone();
        try
        {
            await base.RefreshSignInAsync(user);
        }
        finally
        {
            _refreshPrincipal = null;
        }
    }
}
