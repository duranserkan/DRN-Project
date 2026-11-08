using System.Diagnostics.CodeAnalysis;

namespace DRN.Framework.Hosting.Endpoints;

[SuppressMessage("SonarQube", "S2325", Justification = "The Get.Csp accessor exposes policy names through a CspFor instance.")]
public class CspFor
{
    public const string CspPolicyName = nameof(CspPolicyName);
    public const string CspPolicySelf = nameof(CspPolicySelf);
    public const string CspPolicyInline = nameof(CspPolicyInline);
    public const string CspPolicySwagger = nameof(CspPolicySwagger);
    
    public string SelfPolicy => CspPolicySelf;
    public string InlinePolicy => CspPolicyInline;
}
