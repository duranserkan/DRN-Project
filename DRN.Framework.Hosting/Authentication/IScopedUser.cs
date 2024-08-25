using System.Security.Claims;
using System.Text.Json.Serialization;

namespace DRN.Framework.Hosting.Authentication;

public interface IScopedUser
{
    [JsonIgnore] ClaimsPrincipal? Principal { get; }
    public IReadOnlyList<Claim> Claims { get; }
    [JsonIgnore] public IReadOnlyDictionary<string, IReadOnlySet<Claim>> ClaimsByType { get; }

    public string? Name { get; }
    public string? Email { get; }
    public string? Id { get; }
    bool Authenticated { get; }
}