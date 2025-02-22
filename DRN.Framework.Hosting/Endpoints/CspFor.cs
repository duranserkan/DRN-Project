namespace DRN.Framework.Hosting.Endpoints;

public class CspFor
{
    public const string CspPolicyName = nameof(CspPolicyName);
    public const string CspPolicySelf = nameof(CspPolicySelf);
    public const string CspPolicyInline = nameof(CspPolicyInline);

    public string SelfPolicy => CspPolicySelf;
    public string InlinePolicy => CspPolicyInline;
}