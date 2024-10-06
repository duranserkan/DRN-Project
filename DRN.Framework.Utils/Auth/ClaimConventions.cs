using System.Security.Claims;

namespace DRN.Framework.Utils.Auth;

public static class ClaimConventions
{
    public static string NameIdentifier { get; set; } = ClaimTypes.NameIdentifier;
    public static string Name { get; set; } = ClaimTypes.Name;
    public static string Email { get; set; } = ClaimTypes.Email;

    //https://datatracker.ietf.org/doc/html/rfc8176#section-2
    public static string AuthenticationMethodReference { get; set; } = "amr";
}