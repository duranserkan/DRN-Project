using System.Collections.Frozen;
using System.Security.Claims;
using System.Text.Json.Serialization;
using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Hosting.Authentication;

[Scoped<IScopedUser>]
public class ScopedUser : IScopedUser
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<Claim>> DefaultClaimsByType =
        new Dictionary<string, IReadOnlySet<Claim>>(0).ToFrozenDictionary();

    [JsonIgnore] public ClaimsPrincipal? Principal { get; private set; }

    [JsonIgnore] public ClaimsIdentity? PrimaryIdentity { get; private set; }

    [JsonIgnore] public IReadOnlyDictionary<string, IReadOnlySet<Claim>> ClaimsByType { get; private set; } = DefaultClaimsByType;

    public bool Authenticated { get; private set; }

    public string? Id => IdClaim?.Value;
    [JsonIgnore] public Claim? IdClaim { get; private set; }

    public string? Name => NameClaim?.Value;
    [JsonIgnore] public Claim? NameClaim { get; private set; }

    public string? Email => EmailClaim?.Value;
    [JsonIgnore] public Claim? EmailClaim { get; private set; }

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

        ClaimsByType = claimsDictionary.ToFrozenDictionary(pair => pair.Key, pair => (IReadOnlySet<Claim>)pair.Value.ToFrozenSet());
        IdClaim = ClaimsByType.TryGetValue(ClaimConventions.NameIdentifier, out var nameIds)
            ? nameIds.FirstOrDefault(c => c.Subject == PrimaryIdentity) ?? nameIds.FirstOrDefault()
            : null;
        NameClaim = ClaimsByType.TryGetValue(ClaimConventions.Name, out var names)
            ? names.FirstOrDefault(c => c.Subject == PrimaryIdentity) ?? names.FirstOrDefault()
            : null;
        EmailClaim = ClaimsByType.TryGetValue(ClaimConventions.Email, out var emails)
            ? emails.FirstOrDefault(c => c.Subject == PrimaryIdentity) ?? emails.FirstOrDefault()
            : null;
    }
}