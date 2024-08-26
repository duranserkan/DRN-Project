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
        ClaimsByValue = claims.ToFrozenDictionary(p => p.Value, p => p);
    }

    [JsonIgnore] public Claim Claim { get; }
    [JsonIgnore] public IReadOnlyDictionary<string, Claim> ClaimsByValue { get; }

    [JsonIgnore] public bool IsPrimaryClaim { get; }
    [JsonIgnore] public bool IsSingleClaim { get; }

    public string Value => Claim.Value;

    public bool ValueExists(string value) => ClaimsByValue.ContainsKey(value);
    public bool ValueExists(string value, string issuer) => ClaimsByValue.TryGetValue(value, out var claim) && claim.Issuer == issuer;

    public Claim? FindClaim(string value) => ClaimsByValue.TryGetValue(value, out var claim) ? claim : null;
    public Claim? FindClaim(string value, string issuer) => ClaimsByValue.TryGetValue(value, out var claim) && claim.Issuer == issuer ? claim : null;
}