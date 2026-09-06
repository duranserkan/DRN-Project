using System.Security.Claims;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;

namespace DRN.Test.Unit.Tests.Framework.Utils.Auth.MFA;

public class MfaAssuranceTests
{
    private const string Issuer = "trusted-provider";
    private static readonly MfaClaimConfig Assurance = new("acr", "urn:test:phishing-resistant");
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1700000300);

    [Theory]
    [DataInlineUnit("1700000300", true)]
    [DataInlineUnit("1700000000", true)]
    [DataInlineUnit("1699999999", false)]
    [DataInlineUnit("1700000301", false)]
    [DataInlineUnit("-1", false)]
    [DataInlineUnit("-62135596801", false)]
    [DataInlineUnit("1700000000.5", false)]
    [DataInlineUnit("253402300800", false)]
    [DataInlineUnit("999999999999999999999", false)]
    [DataInlineUnit("", false)]
    public void Recency_Should_Use_Inclusive_Age_And_Reject_Invalid_Or_Future_Time(string timestamp, bool expected)
    {
        var identity = Identity();
        identity.AddClaim(Claim("auth_time", timestamp));

        MfaPrincipal.IsRecent(new ClaimsPrincipal(identity), AuthenticationClaimConfig.Default, Issuer,
            TimeSpan.FromMinutes(5), Now).Should().Be(expected);
    }

    [Fact]
    public void Recency_Should_Require_Unambiguous_Trusted_Time_And_Allow_Explicit_Mfa_Time()
    {
        var identity = Identity();
        var principal = new ClaimsPrincipal(identity);
        MfaPrincipal.IsRecent(principal, AuthenticationClaimConfig.Default, Issuer, TimeSpan.FromMinutes(5), Now).Should().BeFalse();

        identity.AddClaim(Claim("mfa_time", "1700000300"));
        MfaPrincipal.IsRecent(principal, AuthenticationClaimConfig.Default, Issuer, TimeSpan.Zero, Now, "mfa_time").Should().BeTrue();
        MfaPrincipal.IsRecent(principal, AuthenticationClaimConfig.Default, Issuer, TimeSpan.Zero, Now.AddTicks(1), "mfa_time").Should().BeFalse();

        identity.AddClaim(Claim("mfa_time", "1700000300"));
        MfaPrincipal.IsRecent(principal, AuthenticationClaimConfig.Default, Issuer, TimeSpan.Zero, Now, "mfa_time").Should().BeTrue();
        identity.AddClaim(Claim("mfa_time", "1700000000"));
        MfaPrincipal.IsRecent(principal, AuthenticationClaimConfig.Default, Issuer, TimeSpan.FromMinutes(5), Now, "mfa_time").Should().BeFalse();
    }

    [Theory]
    [DataInlineUnit("valid", true)]
    [DataInlineUnit("unauthenticated-noise", true)]
    [DataInlineUnit("anonymous", false)]
    [DataInlineUnit("missing-subject", false)]
    [DataInlineUnit("ambiguous-subject", false)]
    [DataInlineUnit("other-account", false)]
    [DataInlineUnit("other-subject-issuer", false)]
    [DataInlineUnit("untrusted-completion", false)]
    [DataInlineUnit("untrusted-evidence", false)]
    [DataInlineUnit("split-evidence", false)]
    [DataInlineUnit("setup", false)]
    [DataInlineUnit("pending", false)]
    public void Assurance_Should_Require_Same_Identity_Trusted_Evidence(string scenario, bool expected)
    {
        var identity = Identity();
        var principal = new ClaimsPrincipal(identity);
        var evidence = identity;
        if (scenario == "unauthenticated-noise")
            principal.AddIdentity(new ClaimsIdentity([Claim("sub", "other"), Claim("auth_time", "999999999999")]));
        if (scenario == "missing-subject")
            identity.RemoveClaim(identity.FindFirst("sub")!);
        if (scenario == "ambiguous-subject")
            identity.AddClaim(Claim(ClaimTypes.NameIdentifier, "other"));
        if (scenario is "other-account" or "other-subject-issuer")
            principal.AddIdentity(new ClaimsIdentity([Claim("sub", scenario == "other-account" ? "other" : "user",
                scenario == "other-subject-issuer" ? "other-issuer" : Issuer)], "secondary"));
        if (scenario == "untrusted-completion")
        {
            identity.RemoveClaim(identity.FindFirst("amr")!);
            identity.AddClaim(Claim("amr", "mfa", "untrusted"));
        }
        if (scenario == "split-evidence")
        {
            evidence = new ClaimsIdentity([Claim("sub", "user")], "secondary");
            principal.AddIdentity(evidence);
        }
        if (scenario is "setup" or "pending")
            identity.AddClaim(Claim(ClaimConventions.AuthenticationMethod,
                scenario == "setup" ? MfaClaimValues.MfaSetupRequired : MfaClaimValues.MfaInProgress));
        var evidenceIssuer = scenario == "untrusted-evidence" ? "untrusted" : Issuer;
        evidence.AddClaim(Claim("auth_time", "1700000000", evidenceIssuer));
        evidence.AddClaim(Claim(Assurance.ClaimType, Assurance.ClaimValue, evidenceIssuer));
        if (scenario == "anonymous")
            principal = new ClaimsPrincipal(new ClaimsIdentity(identity.Claims));

        MfaPrincipal.IsRecent(principal, AuthenticationClaimConfig.Default, Issuer, TimeSpan.FromMinutes(5), Now).Should().Be(expected);
        MfaPrincipal.IsPhishingResistant(principal, AuthenticationClaimConfig.Default, Issuer, Assurance).Should().Be(expected);
    }

    [Fact]
    public void Completed_Mfa_Alone_Should_Not_Imply_Phishing_Resistance()
    {
        var identity = Identity();
        var principal = new ClaimsPrincipal(identity);
        MfaPrincipal.IsCompleted(principal, AuthenticationClaimConfig.Default).Should().BeTrue();
        MfaPrincipal.IsPhishingResistant(principal, AuthenticationClaimConfig.Default, Issuer, Assurance).Should().BeFalse();
        MfaPrincipal.IsPhishingResistant(principal, AuthenticationClaimConfig.Default, Issuer, MfaClaimConfig.AspNetIdentity).Should().BeFalse();
        identity.AddClaim(Claim(Assurance.ClaimType, Assurance.ClaimValue));
        MfaPrincipal.IsPhishingResistant(principal, AuthenticationClaimConfig.Default, Issuer, Assurance).Should().BeTrue();
        MfaPrincipal.IsPhishingResistant(principal, AuthenticationClaimConfig.Default, "other-issuer", Assurance).Should().BeFalse();
    }

    [Fact]
    public void Assurance_Should_Support_Custom_Completed_Marker_Without_Changing_Completion_Policy()
    {
        var config = new AuthenticationClaimConfig { Mfa = new("completed", "yes") };
        var identity = new ClaimsIdentity([
            Claim(ClaimTypes.NameIdentifier, "user"), Claim(config.Mfa.ClaimType, config.Mfa.ClaimValue),
            Claim("auth_time", "1700000300"), Claim(Assurance.ClaimType, Assurance.ClaimValue)
        ], "provider");
        var principal = new ClaimsPrincipal(identity);

        MfaPrincipal.IsRecent(principal, config, Issuer, TimeSpan.Zero, Now).Should().BeTrue();
        MfaPrincipal.IsPhishingResistant(principal, config, Issuer, Assurance).Should().BeTrue();
        MfaPrincipal.IsCompleted(principal, AuthenticationClaimConfig.Default).Should().BeFalse();
    }

    [Fact]
    public void Assurance_Should_Reject_Null_Principals_And_Invalid_Configuration()
    {
        MfaPrincipal.IsRecent(null, AuthenticationClaimConfig.Default, Issuer, TimeSpan.Zero, Now).Should().BeFalse();
        MfaPrincipal.IsPhishingResistant(null, AuthenticationClaimConfig.Default, Issuer, Assurance).Should().BeFalse();
        var negativeAge = () => MfaPrincipal.IsRecent(null, AuthenticationClaimConfig.Default, Issuer, TimeSpan.FromSeconds(-1), Now);
        negativeAge.Should().Throw<ArgumentOutOfRangeException>();
        var missingIssuer = () => MfaPrincipal.IsPhishingResistant(null, AuthenticationClaimConfig.Default, "", Assurance);
        missingIssuer.Should().Throw<ArgumentException>();
    }

    private static ClaimsIdentity Identity() => new([Claim("sub", "user"), Claim("amr", "mfa")], "provider");
    private static Claim Claim(string type, string value, string issuer = Issuer) => new(type, value, ClaimValueTypes.String, issuer);
}
