using System.Security.Claims;

namespace DRN.Framework.Utils.Auth;

internal static class AuthenticationFor
{
    // Match ASP.NET Core's DenyAnonymousAuthorizationRequirement semantics.
    internal static bool IsAuthenticated(ClaimsPrincipal principal) =>
        principal.Identities.Any(IsAuthenticated);

    internal static bool IsAuthenticated(ClaimsIdentity? identity) =>
        identity?.IsAuthenticated == true;
}
