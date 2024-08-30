namespace DRN.Framework.Utils.Auth;

public class ClaimGroupSummary
{
    public string Type { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public bool IsPrimaryClaim { get; init; }
    public ClaimValue[] Values { get; init; } = [];
}