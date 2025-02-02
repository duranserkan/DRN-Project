using System.Security.Claims;

namespace DRN.Framework.Utils.Auth;

public static class ClaimConventions
{
    //https://datatracker.ietf.org/doc/html/rfc8176#section-2
    //https://github.com/dotnet/aspnetcore/blob/b2c348b222ffd4f5f5a49ff90f5cd237d51e5231/src/Identity/Core/src/SignInManager.cs#L501
    public static string AuthenticationMethodReference { get; internal set; } = "amr";
    public static string AuthenticationMethod { get; internal set; } = ClaimTypes.AuthenticationMethod;
    public static string NameIdentifier { get; internal set; } = ClaimTypes.NameIdentifier;
    public static string Name { get; internal set; } = ClaimTypes.Name;
    public static string Email { get; internal set; } = ClaimTypes.Email;
}