using System.Security.Claims;
using System.Text.Json.Serialization;

namespace DRN.Framework.Hosting.Authentication;

public interface IScopedUser
{
    [JsonIgnore] ClaimsPrincipal? Principal { get; }
    [JsonIgnore] public ClaimsIdentity? PrimaryIdentity { get; }
    [JsonIgnore] public IReadOnlyDictionary<string, IReadOnlySet<Claim>> ClaimsByType { get; }

    bool Authenticated { get; }

    public string? Id { get; }
    [JsonIgnore] public Claim? IdClaim { get; }

    public string? Name { get; }
    [JsonIgnore] public Claim? NameClaim { get; }

    public string? Email { get; }
    [JsonIgnore] public Claim? EmailClaim { get; }
}