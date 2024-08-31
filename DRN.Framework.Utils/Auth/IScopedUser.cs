using System.Security.Claims;
using System.Text.Json.Serialization;

namespace DRN.Framework.Utils.Auth;

public interface IScopedUser
{
    [JsonIgnore] ClaimsPrincipal? Principal { get; }
    [JsonIgnore] public ClaimsIdentity? PrimaryIdentity { get; }

    bool Authenticated { get; }

    public string? Id { get; }
    [JsonIgnore] public ClaimGroup? IdClaim { get; }

    public string? Name { get; }
    [JsonIgnore] public ClaimGroup? NameClaim { get; }

    public string? Email { get; }
    [JsonIgnore] public ClaimGroup? EmailClaim { get; }

    public IReadOnlyDictionary<string, ClaimGroup> ClaimsByType { get; }

    bool ClaimExists(string type);
    ClaimGroup? FindClaimGroup(string type);

    Claim? FindClaim(string type, string value);
    Claim? FindClaim(string type, string value, string issuer);

    bool ValueExists(string type, string value);
    bool ValueExists(string type, string value, string issuer);
}