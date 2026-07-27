using System.Collections.Frozen;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace DRN.Framework.Utils.Auth;

public class ClaimGroup
{
    private readonly FrozenSet<Claim> _claims;

    public ClaimGroup(IReadOnlySet<Claim> claims, ClaimsIdentity primary)
    {
        IsSingleClaim = claims.Count == 1;
        Claim = claims.FirstOrDefault(c => c.Subject == primary) ?? claims.First();
        IsPrimaryClaim = Claim.Subject == primary;
        _claims = claims.ToFrozenSet();
    }

    [JsonIgnore] public Claim Claim { get; }
    [JsonIgnore] public IReadOnlySet<Claim> Claims => _claims;
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
    public string? GetValue(string? issuer = null)
    {
        issuer = ResolveIssuer(issuer);
        if (IsSingleClaim)
            return Claim.Issuer == issuer ? Claim.Value : null;

        foreach (var claim in _claims)
            if (claim.Issuer == issuer)
                return claim.Value;

        return null;
    }

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
    public bool ClaimExists(string? issuer = null)
    {
        issuer = ResolveIssuer(issuer);
        if (IsSingleClaim)
            return Claim.Issuer == issuer;

        foreach (var claim in _claims)
            if (claim.Issuer == issuer)
                return true;

        return false;
    }
    
    /// <summary>
    /// Gets claim from primary identity if issuer is not provided
    /// </summary>
    public Claim? FindClaim(string value, string? issuer = null)
    {
        issuer = ResolveIssuer(issuer);
        if (IsSingleClaim)
            return Claim.Issuer == issuer && Claim.Value == value ? Claim : null;

        foreach (var claim in _claims)
            if (claim.Issuer == issuer && claim.Value == value)
                return claim;

        return null;
    }

    /// <summary>
    /// Gets claim from primary identity if issuer is not provided
    /// </summary>
    public IReadOnlyList<Claim> FindClaims(string? issuer = null) => FindClaimsEnumerable(issuer).ToArray();

    /// <summary>
    /// Gets claim from primary identity if issuer is not provided
    /// </summary>
    public IEnumerable<Claim> FindClaimsEnumerable(string? issuer = null)
    {
        issuer = ResolveIssuer(issuer);
        if (IsSingleClaim)
            return Claim.Issuer == issuer ? [Claim] : [];

        return _claims.Where(c => c.Issuer == issuer);
    }

    private string? ResolveIssuer(string? issuer) => issuer ?? (IsPrimaryClaim ? Claim.Issuer : null);
}

public record ClaimValue(string Value, string Issuer, string? Name);
