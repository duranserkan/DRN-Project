using System.Security.Claims;
using System.Text.Json.Serialization;

namespace DRN.Framework.Utils.Auth;

public interface IScopedUser
{
    [JsonIgnore] ClaimsPrincipal? Principal { get; }
    [JsonIgnore] ClaimsIdentity? PrimaryIdentity { get; }

    bool Authenticated { get; }

    string? Id { get; }
    [JsonIgnore] ClaimGroup? IdClaim { get; }

    string? Name { get; }
    [JsonIgnore] ClaimGroup? NameClaim { get; }

    string? Email { get; }
    [JsonIgnore] ClaimGroup? EmailClaim { get; }

    //https://datatracker.ietf.org/doc/html/rfc8176#section-2
    //https://github.com/dotnet/aspnetcore/blob/b2c348b222ffd4f5f5a49ff90f5cd237d51e5231/src/Identity/Core/src/SignInManager.cs#L501
    string? Amr { get; }
    [JsonIgnore] public ClaimGroup? AmrClaim { get; }
    
    string? AuthenticationMethod { get; }
    [JsonIgnore] ClaimGroup? AuthenticationMethodClaim { get; }

    [JsonIgnore] ClaimGroup? RoleClaim { get; }
    bool IsInRole(string role);

    IReadOnlyDictionary<string, ClaimGroup> ClaimsByType { get; }

    ClaimGroup? FindClaimGroup(string type);
    Claim? FindClaim(string type, string value, string? issuer = null);
    IReadOnlyList<Claim> FindClaims(string type, string? issuer = null);

    bool ClaimExists(string type, string? issuer = null);
    bool ValueExists(string type, string value, string? issuer = null);
    string GetClaimValue(string claim, string? issuer = null, string defaultValue = "");
    IReadOnlyList<string> GetClaimValues(string claim, string? issuer = null);
}