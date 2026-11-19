using System.Security.Claims;
using DRN.Framework.Utils.Auth.MFA;
using Microsoft.AspNetCore.Authentication;

namespace DRN.Framework.Hosting.Auth.Policies;

/// <summary>Controls access to detailed Development exception responses and previews.</summary>
public static class DeveloperExceptionAccess
{
    public const string Policy = "DrnDeveloperException";

    /// <summary>Checks built-in viewer access and the configured policy for the current request.</summary>
    public static async Task<bool> AllowsAsync(IAppSettings settings, HttpContext context)
    {
        if (!settings.IsDevelopmentEnvironment)
            return false;

        try
        {
            var services = context.RequestServices;
            if (services == null)
                return false;
            var provider = services.GetRequiredService<IAuthorizationPolicyProvider>();
            if (await provider.GetPolicyAsync(Policy) is not { } policy)
                return false;

            var viewer = context.User;
            if (policy.AuthenticationSchemes.Count > 0)
            {
                // Evaluate only the selected schemes without replacing the failing request's principal.
                var identities = new List<ClaimsIdentity>();
                foreach (var scheme in policy.AuthenticationSchemes)
                {
                    var result = await context.AuthenticateAsync(scheme);
                    if (result?.Succeeded != true)
                        continue;

                    identities.RemoveAll(identity => !identity.IsAuthenticated && !identity.Claims.Any());
                    identities.InsertRange(0, result.Principal!.Identities);
                }
                viewer = new ClaimsPrincipal(identities);
            }

            if (!Allows(settings, viewer, services.GetService<AuthenticationClaimConfig>()))
                return false;

            var authorization = services.GetRequiredService<IAuthorizationService>();
            return (await authorization.AuthorizeAsync(viewer, context, policy)).Succeeded;
        }
        catch (Exception)
        {
            // Authorization services can fail while handling another exception. Withhold diagnostics.
            return false;
        }
    }

    internal static bool Allows(IAppSettings settings, ClaimsPrincipal user, AuthenticationClaimConfig? claims)
    {
        var config = claims ?? AuthenticationClaimConfig.Default;
        return settings.IsDevelopmentEnvironment && MfaPrincipal.IsCompleted(user, config) &&
               user.Identities.Any(identity => identity.IsAuthenticated && identity.HasClaim(claim =>
                   (config.Roles.Accepts(claim.Type) && claim.Value == "SystemAdmin") ||
                   (claim.Type == "TestPagesAccess" && bool.TryParse(claim.Value, out var enabled) && enabled)));
    }
}
