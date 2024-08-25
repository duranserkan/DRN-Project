using System.Collections.Frozen;
using System.Security.Claims;
using System.Text.Json.Serialization;
using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Hosting.Authentication;

[Scoped<IScopedUser>]
public class ScopedUser : IScopedUser
{
    private static readonly Claim[] DefaultClaims = [];

    private static readonly IReadOnlyDictionary<string, IReadOnlySet<Claim>> DefaultClaimsByType =
        new Dictionary<string, IReadOnlySet<Claim>>(0).ToFrozenDictionary();

    [JsonIgnore] public ClaimsPrincipal? Principal { get; private set; }
    public IReadOnlyList<Claim> Claims { get; private set; } = DefaultClaims;
    [JsonIgnore] public IReadOnlyDictionary<string, IReadOnlySet<Claim>> ClaimsByType { get; private set; } = DefaultClaimsByType;

    public string? Id { get; private set; }
    public string? Name { get; private set; }
    public string? Email { get; private set; }
    public bool Authenticated { get; private set; }

    internal void SetUser(ClaimsPrincipal user)
    {
        Principal = user;
        Claims = user.Claims.ToArray();
        var claimsDictionary = new Dictionary<string, HashSet<Claim>>();
        foreach (var claim in Claims)
            if (claimsDictionary.TryGetValue(claim.Type, out var claimsByType))
                claimsByType.Add(claim);
            else
                claimsDictionary.Add(claim.Type, [claim]);
        ClaimsByType = claimsDictionary.ToDictionary(pair => pair.Key, pair => (IReadOnlySet<Claim>)pair.Value.ToFrozenSet());

        Name = user.FindFirstValue(ClaimTypes.Name);
        Email = user.FindFirstValue(ClaimTypes.Email);
        Id = user.FindFirstValue(ClaimTypes.NameIdentifier);
        Authenticated = user.Identity?.IsAuthenticated ?? false;
    }
}