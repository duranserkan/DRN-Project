using System.Collections.Frozen;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace DRN.Framework.Utils.Auth;

//todo: write tests
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
    public string Issuer => Claim.Issuer;

    /// <summary>
    /// Gets claim from primary identity if issuer is not provided
    /// </summary>
    public bool ValueExists(string value, string? issuer = null) => FindClaim(value, issuer) != null;

    /// <summary>
    /// Gets claim from primary identity if issuer is not provided
    /// </summary>
    public string? GetValue(string? issuer = null) => GetValuesEnumerable(issuer).FirstOrDefault();

    /// <summary>
    /// Gets claim from primary identity if issuer is not provided
    /// </summary>
    public IReadOnlyList<string> GetValues(string? issuer = null) => GetValuesEnumerable(issuer).ToArray();

    /// <summary>
    /// Gets claim from primary identity if issuer is not provided
    /// </summary>
    public IEnumerable<string> GetValuesEnumerable(string? issuer = null) => FindClaimsEnumerable(issuer).Select(c => c.Value);

    public IEnumerable<ClaimValue> GetAllValues() => Claims.Select(c => new ClaimValue(c.Value, c.Issuer, c.Subject?.Name));

    
    /// <summary>
    /// Checks claim from primary identity if issuer is not provided
    /// </summary>
    public bool ClaimExists(string? issuer = null) => FindClaimsEnumerable(issuer).Any();
    
    /// <summary>
    /// Gets claim from primary identity if issuer is not provided
    /// </summary>
    public Claim? FindClaim(string value, string? issuer = null) => FindClaimsEnumerable(issuer).FirstOrDefault(c => c.Value == value);

    /// <summary>
    /// Gets claim from primary identity if issuer is not provided
    /// </summary>
    public IReadOnlyList<Claim> FindClaims(string? issuer = null) => FindClaimsEnumerable(issuer).ToArray();

    /// <summary>
    /// Gets claim from primary identity if issuer is not provided
    /// </summary>
    public IEnumerable<Claim> FindClaimsEnumerable(string? issuer = null)
    {
        if (issuer == null && IsPrimaryClaim)
            issuer = Claim.Issuer;
        if (IsSingleClaim)
            return Claim.Issuer == issuer ? [Claim] : [];

        return Claims.Where(c => c.Issuer == issuer);
    }
}

public record ClaimValue(string Value, string Issuer, string? Name);