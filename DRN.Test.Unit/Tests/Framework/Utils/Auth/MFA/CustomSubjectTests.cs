using System.Security.Claims;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;

namespace DRN.Test.Unit.Tests.Framework.Utils.Auth.MFA;

public class CustomSubjectTests
{
    [Fact]
    public void Explicit_Mapping_Should_Require_Subject_While_Default_Subjectless_Completion_Remains_Supported()
    {
        var identity = new ClaimsIdentity([new Claim("amr", "mfa")], "external");
        var principal = new ClaimsPrincipal(identity);
        MfaPrincipal.IsCompleted(principal, AuthenticationClaimConfig.Default).Should().BeTrue();
        var defaultsCopy = new AuthenticationClaimConfig { Subject = new(ClaimTypes.NameIdentifier, "sub") };
        MfaPrincipal.IsCompleted(principal, defaultsCopy).Should().BeTrue();
        MfaPrincipal.MatchesIdentity(identity, identity, defaultsCopy).Should().BeTrue();
        var custom = new AuthenticationClaimConfig { Subject = new("uid") };
        MfaPrincipal.IsCompleted(principal, custom).Should().BeFalse();
        MfaPrincipal.MatchesIdentity(identity, identity, custom).Should().BeFalse();

        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, "user"));
        identity.AddClaim(new Claim("sub", "other"));
        MfaPrincipal.IsCompleted(principal, AuthenticationClaimConfig.Default).Should().BeFalse();
        MfaPrincipal.MatchesIdentity(identity, identity).Should().BeFalse();
    }

    [Theory]
    [DataInlineUnit("uid")]
    [DataInlineUnit("sub")]
    [DataInlineUnit(ClaimTypes.NameIdentifier)]
    public void Subject_Should_Bind_Completion_Assurance_And_Scoped_Id(string claimType)
    {
        var identity = new ClaimsIdentity([
            new Claim(claimType, "user", ClaimValueTypes.String, "issuer"),
            new Claim("amr", "mfa", ClaimValueTypes.String, "issuer"),
            new Claim("acr", "strong", ClaimValueTypes.String, "issuer"),
            new Claim("auth_time", "1700000000", ClaimValueTypes.Integer64, "issuer")
        ], "provider");
        var principal = new ClaimsPrincipal([identity, identity.Clone()]);
        var config = new AuthenticationClaimConfig { Subject = new(claimType) };
        var now = DateTimeOffset.FromUnixTimeSeconds(1700000030);

        MfaPrincipal.IsCompleted(principal, config).Should().BeTrue();
        MfaPrincipal.IsRecent(principal, config, "issuer", TimeSpan.FromMinutes(1), now).Should().BeTrue();
        MfaPrincipal.IsPhishingResistant(principal, config, "issuer", new MfaClaimConfig("acr", "strong")).Should().BeTrue();
        MfaPrincipal.MatchesIdentity(identity, identity.Clone(), config).Should().BeTrue();
        var scoped = new ScopedUser(config);
        scoped.SetUser(new ClaimsPrincipal(identity));
        scoped.Id.Should().Be("user");

        identity.AddClaim(new Claim("sub", "other", ClaimValueTypes.String, "issuer"));
        MfaPrincipal.IsCompleted(principal, config).Should().BeFalse();
        MfaPrincipal.IsRecent(principal, config, "issuer", TimeSpan.FromMinutes(1), now).Should().BeFalse();
        MfaPrincipal.IsPhishingResistant(principal, config, "issuer", new MfaClaimConfig("acr", "strong")).Should().BeFalse();
        MfaPrincipal.MatchesIdentity(identity, identity, config).Should().BeFalse();
        MfaPrincipal.HasSingleAccount(principal, config, requireSubject: true).Should().BeFalse();
        scoped.SetUser(new ClaimsPrincipal(identity));
        scoped.Id.Should().BeNull();
    }

    [Theory]
    [DataInlineUnit("missing")]
    [DataInlineUnit("issuer")]
    [DataInlineUnit("account")]
    public void Explicit_Subject_Should_Reject_Invalid_Secondary_Evidence(string scenario)
    {
        var first = new ClaimsIdentity([new Claim("uid", "user", ClaimValueTypes.String, "issuer"), new Claim("amr", "mfa")], "provider");
        var second = new ClaimsIdentity("provider");
        if (scenario != "missing")
            second.AddClaim(new Claim("uid", scenario == "account" ? "other" : "user", ClaimValueTypes.String,
                scenario == "issuer" ? "other" : "issuer"));
        var config = new AuthenticationClaimConfig { Subject = new("uid") };
        MfaPrincipal.IsCompleted(new ClaimsPrincipal([first, second]), config).Should().BeFalse();
        MfaPrincipal.MatchesIdentity(first, second, config).Should().BeFalse();
    }

    [Fact]
    public void Scoped_User_Should_Resolve_Custom_Subject_Without_Identity_Services()
    {
        var config = new AuthenticationClaimConfig { Subject = new("uid") };
        var identity = new ClaimsIdentity([new Claim("uid", "user")], "external");
        var user = new ScopedUser(config);
        user.SetUser(new ClaimsPrincipal(identity));
        user.Id.Should().Be("user");

        ScopedUser.FromClaimsPrincipal(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "oidc-user")], "external")))
            .Id.Should().Be("oidc-user");
    }
}
