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
    public string Issuer => Claim.Issuer;

    /// <summary>
    /// Checks claim from primary identity if issuer is not provided
    /// </summary>
    public bool ClaimExists(string? issuer = null) => issuer == null
        ? IsPrimaryClaim
        : Claims.Any(c => c.Issuer == issuer);

    /// <summary>
    /// Gets claim from primary identity if issuer is not provided
    /// </summary>
    public bool ValueExists(string value, string? issuer = null, bool multipleValue = false) => multipleValue
        ? FindClaims(issuer).Any(c => c.Value == value)
        : FindClaim(value, issuer) != null;

    /// <summary>
    /// Gets claim from primary identity if issuer is not provided
    /// </summary>
    public string? GetValue(string? issuer = null)
    {
        if (issuer == null && IsPrimaryClaim)
            return Claim.Value;

        return Claims.FirstOrDefault(c => c.Issuer == issuer)?.Value;
    }

    /// <summary>
    /// Gets claim from primary identity if issuer is not provided
    /// </summary>
    public IReadOnlyList<string> GetValues(string? issuer = null)
    {
        if (issuer == null && IsPrimaryClaim)
            issuer = Claim.Issuer;

        return Claims.Where(c => c.Issuer == issuer).Select(c => c.Value).ToArray();
    }

    public IEnumerable<ClaimValue> GetAllValues() => Claims.Select(c => new ClaimValue(c.Value, c.Issuer, c.Subject?.Name));

    /// <summary>
    /// Gets claim from primary identity if issuer is not provided
    /// </summary>
    public Claim? FindClaim(string value, string? issuer = null)
    {
        if (issuer == null && IsPrimaryClaim)
            return Claim.Value == value ? Claim : null;

        return Claims.FirstOrDefault(c => c.Value == value && c.Issuer == issuer);
    }

    /// <summary>
    /// Gets claim from primary identity if issuer is not provided
    /// </summary>
    public IReadOnlyList<Claim> FindClaims(string? issuer = null)
    {
        if (issuer == null && IsPrimaryClaim)
            issuer = Claim.Issuer;

        return Claims.Where(c => c.Issuer == issuer).ToArray();
    }
}

public record ClaimValue(string Value, string Issuer, string? Name);