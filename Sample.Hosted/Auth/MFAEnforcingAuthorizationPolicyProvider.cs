using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace Sample.Hosted.Auth;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;

[Singleton<IAuthorizationPolicyProvider>(tryAdd: false)]
public class MFAEnforcingAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider = new(options);
    private readonly AuthorizationOptions _options = options.Value;

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return Task.FromResult(_options.DefaultPolicy);
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _fallbackPolicyProvider.GetFallbackPolicyAsync();
    }

    public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // If the requested policy is the exemption policy, return it without combining
        if (policyName == AuthPolicy.MFAExempt)
            return await _fallbackPolicyProvider.GetPolicyAsync(policyName);

        var policy = await _fallbackPolicyProvider.GetPolicyAsync(policyName);
        if (policy == null) return null;

        // Combine the default policy with the requested policy
        var defaultPolicy = await GetDefaultPolicyAsync();

        var combinedPolicy = new AuthorizationPolicyBuilder()
            .AddRequirements(defaultPolicy.Requirements.ToArray())
            .AddRequirements(policy.Requirements.ToArray())
            .Build();

        return combinedPolicy;
    }
}