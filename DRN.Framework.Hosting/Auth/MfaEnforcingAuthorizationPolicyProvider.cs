using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace DRN.Framework.Hosting.Auth;

[Singleton<IAuthorizationPolicyProvider>(tryAdd: false)]
public class MfaEnforcingAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _policyProvider = new(options);
    private readonly AuthorizationOptions _options = options.Value;

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => Task.FromResult(_options.DefaultPolicy);
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _policyProvider.GetFallbackPolicyAsync();

    public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // If the requested policy is the exemption policy, return it without combining
        if (policyName == AuthPolicy.MfaExempt)
            return await _policyProvider.GetPolicyAsync(policyName);

        var policy = await _policyProvider.GetPolicyAsync(policyName);
        if (policy == null) return null;

        var defaultPolicy = await GetDefaultPolicyAsync();
        var enforceMFA = defaultPolicy.Requirements.Count(r => r.GetType() == typeof(MfaRequirement)) == 1;
        if (!enforceMFA) return policy;

        var combinedPolicy = new AuthorizationPolicyBuilder()
            .AddRequirements(defaultPolicy.Requirements.ToArray())
            .AddRequirements(policy.Requirements.ToArray())
            .Build();

        return combinedPolicy;

    }
}