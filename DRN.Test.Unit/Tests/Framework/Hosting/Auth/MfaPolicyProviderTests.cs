using DRN.Framework.Hosting.Auth;
using DRN.Framework.Hosting.Auth.Policies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Auth;

public class MfaPolicyProviderTests
{
    [Fact]
    public async Task Named_Policy_Should_Include_Mfa_And_Preserve_Its_Scheme()
    {
        var options = new AuthorizationOptions
        {
            DefaultPolicy = new AuthorizationPolicyBuilder().AddRequirements(new MfaRequirement()).Build()
        };
        options.AddPolicy("scope-read", policy =>
        {
            policy.AuthenticationSchemes.Add("ApiKey");
            policy.RequireClaim("scope", "read");
        });
        var provider = new MfaEnforcingAuthorizationPolicyProvider(Options.Create(options));

        var policy = await provider.GetPolicyAsync("scope-read");

        policy.Should().NotBeNull();
        policy.Requirements.Should().ContainSingle(requirement => requirement is MfaRequirement);
        policy.Requirements.Should().ContainSingle(requirement => requirement is ClaimsAuthorizationRequirement);
        policy.AuthenticationSchemes.Should().ContainSingle().Which.Should().Be("ApiKey");
    }

    [Fact]
    public async Task Named_Policy_Should_Not_Inherit_Default_Authentication_Schemes()
    {
        var options = new AuthorizationOptions
        {
            DefaultPolicy = new AuthorizationPolicyBuilder("Cookies").AddRequirements(new MfaRequirement()).Build()
        };
        options.AddPolicy("ApiKeyPolicy", policy =>
        {
            policy.AuthenticationSchemes.Add("ApiKey");
            policy.RequireAuthenticatedUser();
        });
        var provider = new MfaEnforcingAuthorizationPolicyProvider(Options.Create(options));

        var policy = await provider.GetPolicyAsync("ApiKeyPolicy");

        policy.Should().NotBeNull();
        policy.AuthenticationSchemes.Should().ContainSingle().Which.Should().Be("ApiKey");
        policy.Requirements.Should().ContainSingle(requirement => requirement is MfaRequirement);
        policy.Requirements.Should().ContainSingle(requirement => requirement is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task Exempt_Policy_Should_Not_Include_Default_Mfa_Requirement()
    {
        var options = new AuthorizationOptions
        {
            DefaultPolicy = new AuthorizationPolicyBuilder().AddRequirements(new MfaRequirement()).Build()
        };
        options.AddPolicy(AuthPolicy.MfaExempt, policy => policy.AddRequirements(new MfaExemptRequirement()));
        var provider = new MfaEnforcingAuthorizationPolicyProvider(Options.Create(options));

        var policy = await provider.GetPolicyAsync(AuthPolicy.MfaExempt);

        policy.Should().NotBeNull();
        policy.Requirements.Should().ContainSingle(requirement => requirement is MfaExemptRequirement);
        policy.Requirements.Should().NotContain(requirement => requirement is MfaRequirement);
    }
}
