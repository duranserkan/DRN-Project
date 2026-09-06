using System.Security.Claims;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.Identity;
using DRN.Framework.Utils.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Auth;

public class IdentitySubjectTests
{
    [Fact]
    public async Task Provider_Neutral_Handler_And_Exemption_Should_Use_Custom_Subject_Without_Identity()
    {
        var services = new ServiceCollection();
        var config = new AuthenticationClaimConfig { Subject = new("uid") };
        services.AddSingleton(config);
        using var provider = services.BuildServiceProvider();
        var identity = new ClaimsIdentity([new Claim("uid", "user"), new Claim("amr", "mfa")], "external");
        var principal = new ClaimsPrincipal([identity, identity.Clone()]);
        var requirement = new MfaRequirement();
        var context = new AuthorizationHandlerContext([requirement], principal, null);
        await ActivatorUtilities.CreateInstance<RequireMfaHandler>(provider).HandleAsync(context);
        context.HasSucceeded.Should().BeTrue();

        var exemptions = new MfaExemptionOptions();
        exemptions.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = ["external"] });
        var passwordIdentity = new ClaimsIdentity([new Claim("uid", "user")], "external");
        MfaAuthorization.IsMfaSatisfied(new ClaimsPrincipal(passwordIdentity), config,
            exemptions, "external", new ClaimsPrincipal(passwordIdentity.Clone())).Should().BeTrue();
        var wrongAccount = new ClaimsIdentity([new Claim("uid", "other")], "external");
        MfaAuthorization.IsMfaSatisfied(new ClaimsPrincipal(passwordIdentity), config,
            exemptions, "external", new ClaimsPrincipal(wrongAccount)).Should().BeFalse();
    }

    [Fact]
    public async Task Identity_Policies_Should_Use_Configured_Subject_For_Multiple_Identities()
    {
        var services = new ServiceCollection();
        services.AddDrnIdentityMfaPolicies();
        var config = new AuthenticationClaimConfig { Subject = new("uid") };
        services.AddSingleton(config);
        using var provider = services.BuildServiceProvider();
        var identity = new ClaimsIdentity([new Claim("uid", "user"), new Claim("amr", "mfa")], "provider");
        var user = new ClaimsPrincipal([identity, identity.Clone()]);
        var policy = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value.GetPolicy(IdentityMfaPolicy.Enrollment)!;
        var assertion = policy.Requirements.OfType<Microsoft.AspNetCore.Authorization.Infrastructure.AssertionRequirement>().Single();
        var context = new AuthorizationHandlerContext(policy.Requirements, user, null);
        (await assertion.Handler(context)).Should().BeTrue();
        IdentityMfaPolicy.CanManage(user, true, true, config).Should().BeTrue();

        var requirement = new MfaRequirement();
        var completion = new AuthorizationHandlerContext([requirement], user, null);
        await new RequireMfaHandler(config).HandleAsync(completion);
        completion.HasSucceeded.Should().BeTrue();
    }
}
