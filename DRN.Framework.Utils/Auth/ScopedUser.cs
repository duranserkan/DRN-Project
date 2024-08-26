using System.Collections.Frozen;
using System.Security.Claims;
using System.Text.Json.Serialization;
using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Utils.Auth;

[Scoped<IScopedUser>]
public class ScopedUser : IScopedUser
{
    private static readonly IReadOnlyDictionary<string, ClaimGroup> DefaultClaimsByType =
        new Dictionary<string, ClaimGroup>(0).ToFrozenDictionary();

    public static ScopedUser FromClaimsPrincipal(ClaimsPrincipal principal)
    {
        var scopedUser = new ScopedUser();
        scopedUser.SetUser(principal);

        return scopedUser;
    }

    [JsonIgnore] public ClaimsPrincipal? Principal { get; private set; }

    [JsonIgnore] public ClaimsIdentity? PrimaryIdentity { get; private set; }

    public IReadOnlyDictionary<string, ClaimGroup> ClaimsByType { get; private set; } = DefaultClaimsByType;

    public bool Authenticated { get; private set; }

    public string? Id => IdClaim?.Value;
    [JsonIgnore] public ClaimGroup? IdClaim { get; private set; }

    public string? Name => NameClaim?.Value;
    [JsonIgnore] public ClaimGroup? NameClaim { get; private set; }

    public string? Email => EmailClaim?.Value;
    [JsonIgnore] public ClaimGroup? EmailClaim { get; private set; }

    public bool ClaimExists(string type) => ClaimsByType.ContainsKey(type);
    public ClaimGroup? FindClaimGroup(string type) => ClaimsByType.TryGetValue(type, out var claimGroup) ? claimGroup : null;

    public Claim? FindClaim(string type, string value) => FindClaimGroup(type)?.FindClaim(value) ?? null;

    public Claim? FindClaim(string type, string value, string issuer)
    {
        var claim = FindClaim(type, value);
        return claim?.Issuer == issuer ? claim : null;
    }

    public bool ValueExists(string type, string value) => FindClaim(type, value) != null;
    public bool ValueExists(string type, string value, string issuer) => FindClaim(type, value, issuer) != null;

    internal void SetUser(ClaimsPrincipal user)
    {
        Principal = user;
        Authenticated = Principal.Identities.All(i => i.IsAuthenticated);
        if (!Authenticated) return;

        PrimaryIdentity = Principal.Identity as ClaimsIdentity;

        var claimsDictionary = new Dictionary<string, HashSet<Claim>>();
        foreach (var claim in user.Claims)
            if (claimsDictionary.TryGetValue(claim.Type, out var claimsByType))
                claimsByType.Add(claim);
            else
                claimsDictionary.Add(claim.Type, [claim]);
        ClaimsByType = claimsDictionary.ToFrozenDictionary(pair => pair.Key, pair => new ClaimGroup(pair.Value, PrimaryIdentity!));

        IdClaim = FindClaimGroup(ClaimConventions.NameIdentifier);
        NameClaim = FindClaimGroup(ClaimConventions.Name);
        EmailClaim = FindClaimGroup(ClaimConventions.Email);
    }
}