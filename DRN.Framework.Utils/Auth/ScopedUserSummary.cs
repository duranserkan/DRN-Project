using DRN.Framework.SharedKernel.Attributes;

namespace DRN.Framework.Utils.Auth;

public class ScopedUserSummary : IScopedUserSummary
{
    public IReadOnlyDictionary<string, ClaimGroupSummary> ClaimsByType { get; init; } = new Dictionary<string, ClaimGroupSummary>();
    [Sensitive(false, Justification = "Authentication outcome flag.")]
    public bool Authenticated { get; init; }
    public string? Id { get; init; }
    public string? Name { get; init; }
    [Personal]
    public string? Email { get; init; }
}

public interface IScopedUserSummary
{
    public IReadOnlyDictionary<string, ClaimGroupSummary> ClaimsByType { get; }
    [Sensitive(false, Justification = "Authentication outcome flag.")]
    bool Authenticated { get; }
    public string? Id { get; }
    public string? Name { get; }
    [Personal]
    public string? Email { get; }
}
