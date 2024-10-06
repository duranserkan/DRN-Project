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

    public bool Authenticated { get; private set; }

    public string? Id => IdClaim?.GetValue();
    [JsonIgnore] public ClaimGroup? IdClaim { get; private set; }

    public string? Name => NameClaim?.GetValue();
    [JsonIgnore] public ClaimGroup? NameClaim { get; private set; }

    public string? Email => EmailClaim?.GetValue();
    [JsonIgnore] public ClaimGroup? EmailClaim { get; private set; }

    public string? Amr => AmrClaim?.GetValue();
    public ClaimGroup? AmrClaim { get; private set; }


    public IReadOnlyDictionary<string, ClaimGroup> ClaimsByType { get; private set; } = DefaultClaimsByType;

    public ClaimGroup? FindClaimGroup(string type) => ClaimsByType.GetValueOrDefault(type);
    public Claim? FindClaim(string type, string value, string? issuer = null) => FindClaimGroup(type)?.FindClaim(value, issuer);
    public IReadOnlyList<Claim> FindClaims(string type, string? issuer = null) => FindClaimGroup(type)?.FindClaims(issuer) ?? Array.Empty<Claim>();


    public bool ClaimExists(string type, string? issuer = null) => FindClaimGroup(type)?.ClaimExists(issuer) ?? false;
    public bool ValueExists(string type, string value, string? issuer = null) => FindClaim(type, value, issuer) != null;

    public string GetClaimValue(string claim, string? issuer = null, string defaultValue = "")
        => FindClaimGroup(claim)?.GetValue(issuer) ?? defaultValue;

    public IReadOnlyList<string> GetClaimValues(string claim, string? issuer = null)
        => FindClaimGroup(claim)?.GetValues(issuer) ?? Array.Empty<string>();

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
        AmrClaim = FindClaimGroup(ClaimConventions.AuthenticationMethodReference);
    }
}