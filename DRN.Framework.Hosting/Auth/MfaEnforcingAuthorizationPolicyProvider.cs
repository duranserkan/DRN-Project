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
    private readonly bool _enforceMFA = MfaAuthorization.IsMfaEnforced(options.Value);

    // This provider uses fixed authorization options. Derived providers must opt in themselves.
    public virtual bool AllowsCachingPolicies => GetType() == typeof(MfaEnforcingAuthorizationPolicyProvider);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => Task.FromResult(_options.DefaultPolicy);
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _policyProvider.GetFallbackPolicyAsync();

    public async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var policy = await _policyProvider.GetPolicyAsync(policyName);
        if (policy == null)
            return null;

        if (!_enforceMFA || MfaAuthorization.IsPolicyMfaExempt(policy))
            return policy;

        var defaultPolicy = await GetDefaultPolicyAsync();
        var builder = new AuthorizationPolicyBuilder();

        if (policy.AuthenticationSchemes.Count > 0)
            foreach (var scheme in policy.AuthenticationSchemes)
                builder.AuthenticationSchemes.Add(scheme);
        else
            foreach (var scheme in defaultPolicy.AuthenticationSchemes)
                builder.AuthenticationSchemes.Add(scheme);

        foreach (var requirement in policy.Requirements)
            builder.Requirements.Add(requirement);

        foreach (var requirement in defaultPolicy.Requirements)
            if (!builder.Requirements.Contains(requirement))
                builder.Requirements.Add(requirement);

        return builder.Build();
    }
}
