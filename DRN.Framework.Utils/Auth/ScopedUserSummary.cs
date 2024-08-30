namespace DRN.Framework.Utils.Auth;

public class ScopedUserSummary : IScopedUserSummary
{
    public IReadOnlyDictionary<string, ClaimGroupSummary> ClaimsByType { get; init; } = new Dictionary<string, ClaimGroupSummary>();
    public bool Authenticated { get; init; }
    public string? Id { get; init; }
    public string? Name { get; init; }
    public string? Email { get; init; }
}

public interface IScopedUserSummary
{
    public IReadOnlyDictionary<string, ClaimGroupSummary> ClaimsByType { get; }
    bool Authenticated { get; }
    public string? Id { get; }
    public string? Name { get; }
    public string? Email { get; }
}