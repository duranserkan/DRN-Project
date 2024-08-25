using System.Security.Claims;

namespace DRN.Framework.Hosting.Authentication;

public static class ClaimConventions
{
    public static string NameIdentifier { get; set; } = ClaimTypes.NameIdentifier;
    public static string Name { get; set; } = ClaimTypes.Name;
    public static string Email { get; set; } = ClaimTypes.Email;
}