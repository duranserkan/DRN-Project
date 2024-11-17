using System.Collections.Frozen;
using System.Security.Claims;
using System.Text.Json.Serialization;
using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Utils.Auth;

[Scoped<IScopedUser>]
public class ScopedUser : IScopedUser
{
    private static readonly StringComparer ClaimTypeComparer = StringComparer.OrdinalIgnoreCase;

    private static readonly IReadOnlyDictionary<string, ClaimGroup> DefaultClaimsByType =
        new Dictionary<string, ClaimGroup>(0).ToFrozenDictionary(ClaimTypeComparer);

    public static ScopedUser FromClaimsPrincipal(ClaimsPrincipal principal)
    {
        var scopedUser = new ScopedUser();
        scopedUser.SetUser(principal);

        return scopedUser;
    }

    [JsonIgnore] public ClaimsPrincipal? Principal { get; private set; }
    [JsonIgnore] public ClaimsIdentity? PrimaryIdentity { get; private set; }
    [JsonIgnore] public string ExemptionScheme { get; internal set; } = string.Empty;

    public bool Authenticated { get; private set; }

    public string? Id => IdClaim?.GetValue();
    [JsonIgnore] public ClaimGroup? IdClaim { get; private set; }

    public string? Name => NameClaim?.GetValue();
    [JsonIgnore] public ClaimGroup? NameClaim { get; private set; }

    public string? Email => EmailClaim?.GetValue();
    [JsonIgnore] public ClaimGroup? EmailClaim { get; private set; }

    //https://datatracker.ietf.org/doc/html/rfc8176#section-2
    //https://github.com/dotnet/aspnetcore/blob/b2c348b222ffd4f5f5a49ff90f5cd237d51e5231/src/Identity/Core/src/SignInManager.cs#L501
    public string? Amr => AmrClaim?.GetValue();
    public ClaimGroup? AmrClaim { get; private set; }

    public string? AuthenticationMethod => AuthenticationMethodClaim?.GetValue();
    public ClaimGroup? AuthenticationMethodClaim { get; private set; }

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

        var claimsDictionary = new Dictionary<string, HashSet<Claim>>(ClaimTypeComparer);
        foreach (var claim in user.Claims)
            if (claimsDictionary.TryGetValue(claim.Type, out var claimsByType))
                claimsByType.Add(claim);
            else
                claimsDictionary.Add(claim.Type, [claim]);
        ClaimsByType = claimsDictionary.ToFrozenDictionary(
            pair => pair.Key,
            pair => new ClaimGroup(pair.Value, PrimaryIdentity!),
            ClaimTypeComparer);

        IdClaim = FindClaimGroup(ClaimConventions.NameIdentifier);
        NameClaim = FindClaimGroup(ClaimConventions.Name);
        EmailClaim = FindClaimGroup(ClaimConventions.Email);
        AmrClaim = FindClaimGroup(ClaimConventions.AuthenticationMethodReference);
        AuthenticationMethodClaim = FindClaimGroup(ClaimConventions.AuthenticationMethod);
    }

    internal void SetExemptionScheme(string exemptionScheme) => ExemptionScheme = exemptionScheme;
    internal bool HasExemptionScheme => Authenticated && !string.IsNullOrWhiteSpace(ExemptionScheme);
}