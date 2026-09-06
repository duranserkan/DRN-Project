using System.Security.Claims;
using DRN.Framework.Utils.Auth.MFA;

namespace DRN.Framework.Utils.Auth;

/// <summary>Application claim types, accepted aliases, and the exact completed-MFA marker.</summary>
public sealed record AuthenticationClaimConfig
{
    public static AuthenticationClaimConfig Default { get; } = new();

    public ClaimMapping Subject { get; init => field = value ?? throw new ArgumentNullException(nameof(Subject)); }
        = new(ClaimTypes.NameIdentifier, AuthClaimTypes.Subject);
    public ClaimMapping Name { get; init => field = value ?? throw new ArgumentNullException(nameof(Name)); }
        = new(ClaimTypes.Name, AuthClaimTypes.Name);
    public ClaimMapping Email { get; init => field = value ?? throw new ArgumentNullException(nameof(Email)); }
        = new(ClaimTypes.Email, AuthClaimTypes.Email);
    public ClaimMapping Roles { get; init => field = value ?? throw new ArgumentNullException(nameof(Roles)); }
        = new(ClaimTypes.Role, AuthClaimTypes.Roles);
    public MfaClaimConfig Mfa { get; init => field = value ?? throw new ArgumentNullException(nameof(Mfa)); }
        = MfaClaimConfig.AspNetIdentity;

    internal bool HasDefaultSubjectMapping => Subject is { Type: ClaimTypes.NameIdentifier, Aliases: [AuthClaimTypes.Subject] };

    /// <summary>The canonical issuance/native type and explicitly accepted input aliases.</summary>
    public sealed record ClaimMapping
    {
        public ClaimMapping(string type, params string[] aliases)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(type);
            ArgumentNullException.ThrowIfNull(aliases);
            var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { type };
            foreach (var alias in aliases)
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(alias);
                if (!types.Add(alias))
                    throw new ArgumentException("Claim types and aliases must be distinct, including casing.", nameof(aliases));
            }

            Type = type;
            Aliases = Array.AsReadOnly((string[])aliases.Clone());
        }

        public string Type { get; }
        public IReadOnlyList<string> Aliases { get; }

        public bool Accepts(string type) => Type == type || Aliases.Contains(type, StringComparer.Ordinal);

        internal bool MatchesIgnoringCase(string type) => string.Equals(Type, type, StringComparison.OrdinalIgnoreCase) ||
            Aliases.Contains(type, StringComparer.OrdinalIgnoreCase);
    }
}
