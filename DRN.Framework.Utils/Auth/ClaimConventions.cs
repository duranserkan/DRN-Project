using System.Security.Claims;

namespace DRN.Framework.Utils.Auth;

public static class ClaimConventions
{
    public static string AuthenticationMethodReference { get; } = AuthClaimTypes.AuthenticationMethods;
    public static string AuthenticationMethod { get; } = ClaimTypes.AuthenticationMethod;
    public static string NameIdentifier { get; } = ClaimTypes.NameIdentifier;
    public static string Name { get; } = ClaimTypes.Name;
    public static string Email { get; } = ClaimTypes.Email;
}
