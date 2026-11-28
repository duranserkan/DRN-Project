using System.Security.Claims;

namespace DRN.Framework.Utils.Auth;

public static class ClaimConventions
{
    public static string AuthenticationMethodReference => AuthClaimTypes.AuthenticationMethods;
    public static string AuthenticationMethod => ClaimTypes.AuthenticationMethod;
    public static string NameIdentifier => ClaimTypes.NameIdentifier;
    public static string Name => ClaimTypes.Name;
    public static string Email => ClaimTypes.Email;
}
