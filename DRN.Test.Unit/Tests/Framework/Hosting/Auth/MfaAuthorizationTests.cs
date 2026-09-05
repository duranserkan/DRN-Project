using System.Security.Claims;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Utils.Auth;
using Microsoft.AspNetCore.Authorization;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Auth;

public class MfaAuthorizationTests
{
    [Fact]
    public void IsMfaEnforced_Should_Require_MfaRequirement_In_Default_Policy()
    {
        var withoutMfa = new AuthorizationOptions
        {
            DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()
        };
        var withMfa = new AuthorizationOptions
        {
            DefaultPolicy = new AuthorizationPolicyBuilder().AddRequirements(new MfaRequirement()).Build()
        };

        MfaAuthorization.IsMfaEnforced(withoutMfa).Should().BeFalse();
        MfaAuthorization.IsMfaEnforced(withMfa).Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit(null, false)]
    [DataInlineUnit(null, true)]
    [DataInlineUnit("", true)]
    [DataInlineUnit("   ", true)]
    [DataInlineUnit("CustomApiKey", false)]
    public void Exemption_Should_Require_Both_Scheme_And_Principal(string? scheme, bool hasPrincipal)
    {
        var options = new MfaExemptionOptions();
        options.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = ["CustomApiKey"] });
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("scope", "api")], "CustomApiKey"));

        MfaAuthorization.IsMfaSatisfied(principal, AuthenticationClaimConfig.Default, options, scheme,
            hasPrincipal ? principal : null).Should().BeFalse();
    }
}
