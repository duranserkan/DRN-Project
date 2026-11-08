using System.Diagnostics.CodeAnalysis;

namespace DRN.Framework.Hosting.Endpoints;

[SuppressMessage("SonarQube", "S2325", Justification = "The Get.Csp accessor exposes policy names through a CspFor instance.")]
public class CspFor
{
    public const string CspPolicyName = nameof(CspPolicyName);
    public const string CspPolicySelf = nameof(CspPolicySelf);
    public const string CspPolicyInline = nameof(CspPolicyInline);
    public const string CspPolicySwagger = nameof(CspPolicySwagger);
    
    [SuppressMessage("Performance", "CA1822", Justification = "Preserve the public Get.Csp.SelfPolicy instance accessor.")]
    public string SelfPolicy => CspPolicySelf;

    [SuppressMessage("Performance", "CA1822", Justification = "Preserve the public Get.Csp.InlinePolicy instance accessor.")]
    public string InlinePolicy => CspPolicyInline;
}
