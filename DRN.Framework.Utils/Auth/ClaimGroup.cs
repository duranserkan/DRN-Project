using System.Collections.Frozen;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace DRN.Framework.Utils.Auth;

public class ClaimGroup
{
    public ClaimGroup(IReadOnlySet<Claim> claims, ClaimsIdentity primary)
    {
        IsSingleClaim = claims.Count == 1;
        Claim = claims.FirstOrDefault(c => c.Subject == primary) ?? claims.First();
        IsPrimaryClaim = Claim.Subject == primary;
        Claims = claims.ToFrozenSet();
    }

    [JsonIgnore] public Claim Claim { get; }
    [JsonIgnore] public IReadOnlySet<Claim> Claims { get; }
    [JsonIgnore] public bool IsSingleClaim { get; }

    public bool IsPrimaryClaim { get; }
    public string Type => Claim.Type;
    public string Value => Claim.Value;
    public IEnumerable<ClaimValue> Values => Claims.Select(c => new ClaimValue(c.Value, c.Issuer, c.Subject?.Name));

    public bool ValueExists(string value) => FindClaim(value) != null;
    public bool ValueExists(string value, string issuer) => FindClaim(value, issuer) != null;

    public Claim? FindClaim(string value) => Claims.FirstOrDefault(c => c.Value == value);
    public Claim? FindClaim(string value, string issuer) => Claims.FirstOrDefault(c => c.Value == value && c.Issuer == issuer);
}

public record ClaimValue(string Value, string Issuer, string? Name)
{
}