using System.Security.Claims;
using DRN.Framework.Hosting.Auth;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Auth;

public class MfaClaimPreservationTests
{
    [Theory]
    [DataInlineUnit("matching", true)]
    [DataInlineUnit("different-account", false)]
    [DataInlineUnit("different-issuer", false)]
    [DataInlineUnit("missing-source", false)]
    [DataInlineUnit("missing-target", false)]
    [DataInlineUnit("ambiguous-source", false)]
    [DataInlineUnit("ambiguous-target", false)]
    [DataInlineUnit("conflicting-alias", false)]
    [DataInlineUnit("conflicting-secondary", false)]
    public void Renewal_Should_Validate_Configured_Subject_Claim(string scenario, bool expected)
    {
        var sourceIdentity = new ClaimsIdentity([
            new Claim("uid", scenario == "different-account" ? "other" : "user", ClaimValueTypes.String,
                scenario == "different-issuer" ? "other-issuer" : "issuer"),
            new Claim("amr", "mfa"),
            new Claim("auth_time", "1700000000")
        ], "cookie");
        var target = new ClaimsIdentity([new Claim("uid", "user", ClaimValueTypes.String, "issuer")], "cookie");
        target.AddClaim(new Claim("auth_time", "1800000000"));
        var source = new ClaimsPrincipal(sourceIdentity);
        if (scenario == "missing-source")
            sourceIdentity.RemoveClaim(sourceIdentity.FindFirst("uid")!);
        if (scenario == "missing-target")
            target.RemoveClaim(target.FindFirst("uid")!);
        if (scenario == "ambiguous-source")
            sourceIdentity.AddClaim(new Claim("uid", "other", ClaimValueTypes.String, "issuer"));
        if (scenario == "ambiguous-target")
            target.AddClaim(new Claim("uid", "other", ClaimValueTypes.String, "issuer"));
        if (scenario == "conflicting-alias")
            sourceIdentity.AddClaim(new Claim("sub", "other", ClaimValueTypes.String, "issuer"));
        if (scenario == "conflicting-secondary")
            source.AddIdentity(new ClaimsIdentity([new Claim("uid", "other", ClaimValueTypes.String, "issuer")], "secondary"));

        MfaClaimPreservation.Preserve(source, target, new AuthenticationClaimConfig { Subject = new("uid") }).Should().Be(expected);

        target.HasClaim("amr", "mfa").Should().Be(expected);
        target.HasClaim("auth_time", "1700000000").Should().Be(expected);
        target.HasClaim("auth_time", "1800000000").Should().BeFalse();
    }

    [Theory]
    [DataInlineUnit("amr", "mfa", "other-user", "issuer", false, false)]
    [DataInlineUnit("amr", "mfa", "user", "other-issuer", false, false)]
    [DataInlineUnit("completed", "yes", "other-user", "issuer", false, false)]
    [DataInlineUnit("completed", "yes", "user", "other-issuer", false, false)]
    [DataInlineUnit("amr", "mfa", "other-user", "issuer", true, false)]
    [DataInlineUnit("completed", "yes", "other-user", "issuer", true, false)]
    [DataInlineUnit("amr", "mfa", "user", "issuer", false, true)]
    [DataInlineUnit("completed", "yes", "user", "issuer", false, true)]
    [DataInlineUnit("amr", "mfa", "user", "issuer", true, true)]
    [DataInlineUnit("completed", "yes", "user", "issuer", true, true)]
    public void Renewal_Should_Bind_Mfa_To_The_Renewed_Account(
        string claimType, string claimValue, string subject, string issuer, bool hasTimestamp, bool expected)
    {
        var config = new AuthenticationClaimConfig { Mfa = new(claimType, claimValue) };
        var primary = Account("user", "issuer");
        var secondary = Account(subject, issuer, "secondary");
        secondary.AddClaim(new Claim(claimType, claimValue));
        if (hasTimestamp)
            secondary.AddClaim(new Claim("auth_time", "1700000000"));
        var source = new ClaimsPrincipal([primary, secondary]);
        var target = Account("user", "issuer", "renewed");

        MfaPrincipal.IsCompleted(source, config).Should().Be(expected);
        MfaClaimPreservation.Preserve(source, target, config);

        target.HasClaim(claimType, claimValue).Should().Be(expected);
        target.HasClaim("auth_time", "1700000000").Should().Be(hasTimestamp && expected);
        var renewed = new ClaimsPrincipal(target);
        MfaPrincipal.IsCompleted(renewed, config).Should().Be(expected);

        var nextTarget = Account("user", "issuer", "renewed-again");
        MfaClaimPreservation.Preserve(renewed, nextTarget, config);
        MfaPrincipal.IsCompleted(new ClaimsPrincipal(nextTarget), config).Should().Be(expected);
        nextTarget.HasClaim(claimType, claimValue).Should().Be(expected);
        nextTarget.HasClaim("auth_time", "1700000000").Should().Be(hasTimestamp && expected);
    }

    [Theory]
    [DataInlineUnit("different-target")]
    [DataInlineUnit("missing-source")]
    [DataInlineUnit("missing-target")]
    [DataInlineUnit("ambiguous-source")]
    [DataInlineUnit("ambiguous-target")]
    [DataInlineUnit("anonymous-target")]
    [DataInlineUnit("conflicting-secondary")]
    public void Renewal_Should_Not_Copy_Markers_When_Account_Binding_Is_Invalid(string scenario)
    {
        var identity = Account("user", "issuer");
        identity.AddClaim(new Claim("amr", "mfa"));
        var source = new ClaimsPrincipal(identity);
        var target = Account(scenario == "different-target" ? "other-user" : "user", "issuer",
            scenario == "anonymous-target" ? null : "renewed");
        target.AddClaim(new Claim("auth_time", "1800000000"));

        if (scenario == "missing-source")
            identity.RemoveClaim(identity.FindFirst(ClaimTypes.NameIdentifier)!);
        if (scenario == "missing-target")
            target.RemoveClaim(target.FindFirst(ClaimTypes.NameIdentifier)!);
        if (scenario == "ambiguous-source")
            identity.AddClaim(new Claim("sub", "other-user", ClaimValueTypes.String, "issuer"));
        if (scenario == "ambiguous-target")
            target.AddClaim(new Claim("sub", "other-user", ClaimValueTypes.String, "issuer"));
        if (scenario == "conflicting-secondary")
            source.AddIdentity(Account("other-user", "issuer", "secondary"));

        MfaClaimPreservation.Preserve(source, target, AuthenticationClaimConfig.Default);

        target.FindAll("amr").Should().BeEmpty();
        target.FindAll("auth_time").Should().BeEmpty();
        MfaPrincipal.IsCompleted(new ClaimsPrincipal(target), AuthenticationClaimConfig.Default).Should().BeFalse();
    }

    [Fact]
    public void Renewal_Should_Bind_Sub_Claims_And_Ignore_Unauthenticated_Accounts()
    {
        var identity = new ClaimsIdentity([
            new Claim("sub", "user", ClaimValueTypes.String, "issuer"),
            new Claim("amr", "mfa")
        ], "cookie");
        var source = new ClaimsPrincipal([identity, Account("other-user", "other-issuer", null)]);
        var target = Account("user", "issuer", "renewed");

        MfaClaimPreservation.Preserve(source, target, AuthenticationClaimConfig.Default);

        MfaPrincipal.IsCompleted(new ClaimsPrincipal(target), AuthenticationClaimConfig.Default).Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit("amr", "mfa", "paired", true, ClaimsIdentity.DefaultIssuer)]
    [DataInlineUnit("amr", "mfa", "split", false, ClaimsIdentity.DefaultIssuer)]
    [DataInlineUnit("amr", "mfa", "factory", false, ClaimsIdentity.DefaultIssuer)]
    [DataInlineUnit("amr", "mfa", "untrusted-marker", false, ClaimsIdentity.DefaultIssuer)]
    [DataInlineUnit("completed", "yes", "paired", true, ClaimsIdentity.DefaultIssuer)]
    [DataInlineUnit("completed", "yes", "split", false, ClaimsIdentity.DefaultIssuer)]
    [DataInlineUnit("completed", "yes", "factory", false, ClaimsIdentity.DefaultIssuer)]
    [DataInlineUnit("completed", "yes", "untrusted-marker", false, ClaimsIdentity.DefaultIssuer)]
    [DataInlineUnit("amr", "mfa", "paired", true, "trusted-provider")]
    [DataInlineUnit("amr", "mfa", "split", false, "trusted-provider")]
    [DataInlineUnit("amr", "mfa", "factory", false, "trusted-provider")]
    [DataInlineUnit("amr", "mfa", "untrusted-marker", false, "trusted-provider")]
    [DataInlineUnit("completed", "yes", "paired", true, "trusted-provider")]
    [DataInlineUnit("completed", "yes", "split", false, "trusted-provider")]
    [DataInlineUnit("completed", "yes", "factory", false, "trusted-provider")]
    [DataInlineUnit("completed", "yes", "untrusted-marker", false, "trusted-provider")]
    public void Renewal_Should_Not_Promote_Unpaired_Evidence_To_Recent_Mfa(
        string claimType, string claimValue, string scenario, bool expected, string issuer)
    {
        var config = new AuthenticationClaimConfig { Mfa = new(claimType, claimValue) };
        var now = DateTimeOffset.FromUnixTimeSeconds(1700000000);
        var identity = Account("user", issuer);
        identity.AddClaim(new Claim("auth_time", "1700000000", ClaimValueTypes.Integer64, issuer));
        var secondary = Account("user", issuer, "secondary");
        var target = Account("user", issuer, "renewed");
        var markerIdentity = scenario == "split" ? secondary : scenario == "factory" ? target : identity;
        markerIdentity.AddClaim(new Claim(claimType, claimValue, ClaimValueTypes.String,
            scenario == "untrusted-marker" ? "other-issuer" : issuer));
        var source = new ClaimsPrincipal([identity, secondary]);

        MfaPrincipal.IsRecent(source, config, issuer, TimeSpan.Zero, now).Should().Be(expected);

        MfaClaimPreservation.Preserve(source, target, config);

        var renewed = new ClaimsPrincipal(target);
        MfaPrincipal.IsCompleted(renewed, config).Should().Be(scenario != "factory");
        MfaPrincipal.IsRecent(renewed, config, issuer, TimeSpan.Zero, now).Should().Be(expected);
        // An untrusted marker retains its issuer, so retaining the timestamp cannot grant assurance.
        target.HasClaim("auth_time", "1700000000").Should().Be(expected || scenario is "untrusted-marker" or "factory");
        if (scenario == "factory")
            target.FindAll(claimType).Should().BeEmpty();
        else
            target.FindAll(claimType).Should().ContainSingle().Which.Issuer.Should()
                .Be(scenario == "untrusted-marker" ? "other-issuer" : issuer);

        var nextTarget = Account("user", issuer, "renewed-again");
        MfaClaimPreservation.Preserve(renewed, nextTarget, config);
        MfaPrincipal.IsRecent(new ClaimsPrincipal(nextTarget), config, issuer, TimeSpan.Zero, now)
            .Should().Be(expected);
    }

    [Theory]
    [DataInlineUnit("amr", "mfa")]
    [DataInlineUnit("completed", "yes")]
    public void Renewal_Should_Discard_Factory_Markers_And_Preserve_Original_Provenance(string claimType, string claimValue)
    {
        const string issuer = "trusted-provider";
        var config = new AuthenticationClaimConfig { Mfa = new(claimType, claimValue) };
        var now = DateTimeOffset.FromUnixTimeSeconds(1700000000);
        var original = new Claim(claimType, claimValue, ClaimValueTypes.String, issuer, "original-provider");
        original.Properties.Add("provenance", "validated");
        var identity = Account("user", issuer);
        identity.AddClaim(original);
        identity.AddClaim(new Claim("auth_time", "1700000000", ClaimValueTypes.Integer64, issuer));
        var source = new ClaimsPrincipal(identity);
        var target = Account("user", issuer, "renewed");
        target.AddClaim(new Claim(claimType, claimValue));

        MfaClaimPreservation.Preserve(source, target, config);
        MfaClaimPreservation.Preserve(source, target, config);

        target.FindAll(claimType).Should().ContainSingle();
        var preserved = target.FindAll(claimType).Single(claim => claim.Issuer == issuer);
        preserved.ValueType.Should().Be(original.ValueType);
        preserved.Value.Should().Be(original.Value);
        preserved.Issuer.Should().Be(original.Issuer);
        preserved.OriginalIssuer.Should().Be(original.OriginalIssuer);
        preserved.Properties.Should().BeEquivalentTo(original.Properties);
        preserved.Subject.Should().BeSameAs(target);
        MfaPrincipal.IsRecent(new ClaimsPrincipal(target), config, issuer, TimeSpan.Zero, now).Should().BeTrue();
    }

    [Fact]
    public void Renewal_Should_Preserve_Original_Time_And_Provenance_Without_Duplicates()
    {
        var original = new Claim("auth_time", "1700000000", ClaimValueTypes.Integer64, "issuer", "original-issuer");
        original.Properties.Add("provenance", "validated");
        var identity = Account("user", "issuer");
        identity.AddClaim(original);
        identity.AddClaim(original.Clone());
        var source = new ClaimsPrincipal(identity);
        var target = Account("user", "issuer", "renewed");
        target.AddClaim(new Claim("auth_time", "1800000000"));

        MfaClaimPreservation.Preserve(source, target, AuthenticationClaimConfig.Default);
        MfaClaimPreservation.Preserve(source, target, AuthenticationClaimConfig.Default);

        var preserved = target.FindAll("auth_time").Should().ContainSingle().Which;
        preserved.Value.Should().Be(original.Value);
        preserved.ValueType.Should().Be(original.ValueType);
        preserved.Issuer.Should().Be(original.Issuer);
        preserved.OriginalIssuer.Should().Be(original.OriginalIssuer);
        preserved.Properties.Should().BeEquivalentTo(original.Properties);
        preserved.Subject.Should().BeSameAs(target);
    }

    [Theory]
    [DataInlineUnit((string?)null)]
    [DataInlineUnit("")]
    [DataInlineUnit("-1")]
    [DataInlineUnit("1.5")]
    [DataInlineUnit(" 1700000000")]
    [DataInlineUnit("253402300800")]
    [DataInlineUnit("9999999999999999999999999")]
    public void Renewal_Should_Not_Create_Time_When_Original_Is_Missing_Or_Invalid(string? value)
    {
        var identity = Account("user", "issuer");
        if (value != null)
            identity.AddClaim(new Claim("auth_time", value));
        var target = Account("user", "issuer");
        target.AddClaim(new Claim("auth_time", "1800000000"));

        // A custom MFA mapping must not bypass timestamp validation.
        MfaClaimPreservation.Preserve(new ClaimsPrincipal(identity), target,
            new AuthenticationClaimConfig { Mfa = new("auth_time", string.IsNullOrWhiteSpace(value) ? "missing" : value) });

        target.FindAll("auth_time").Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("other-user", "issuer", true)]
    [DataInlineUnit("user", "other-issuer", true)]
    [DataInlineUnit("user", "issuer", false)]
    [DataInlineUnit("", "issuer", true)]
    public void Renewal_Should_Reject_Time_From_Unbound_Identity(string subject, string issuer, bool authenticated)
    {
        var identity = Account(subject, issuer, authenticated ? "cookie" : null);
        identity.AddClaim(new Claim("auth_time", "1700000000"));
        var target = Account("user", "issuer");

        MfaClaimPreservation.Preserve(new ClaimsPrincipal(identity), target, AuthenticationClaimConfig.Default);

        target.FindAll("auth_time").Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("1700000001", "issuer", "issuer", "user")]
    [DataInlineUnit("1700000000", "other-issuer", "issuer", "user")]
    [DataInlineUnit("1700000000", "issuer", "other-original", "user")]
    [DataInlineUnit("1700000000", "issuer", "issuer", "other-user")]
    public void Renewal_Should_Reject_Conflicting_Authenticated_Evidence(string value, string issuer, string originalIssuer, string subject)
    {
        var identity = Account("user", "issuer");
        identity.AddClaim(new Claim("auth_time", "1700000000", ClaimValueTypes.Integer64, "issuer"));
        var secondary = Account(subject, "issuer");
        secondary.AddClaim(new Claim("auth_time", value, ClaimValueTypes.Integer64, issuer, originalIssuer));
        var target = Account("user", "issuer");

        MfaClaimPreservation.Preserve(new ClaimsPrincipal([identity, secondary]), target, AuthenticationClaimConfig.Default);

        target.FindAll("auth_time").Should().BeEmpty();
    }

    private static ClaimsIdentity Account(string subject, string issuer, string? authenticationType = "cookie") =>
        new([new Claim(ClaimTypes.NameIdentifier, subject, ClaimValueTypes.String, issuer)], authenticationType);

    [Fact]
    public void Renewal_Should_Ignore_Unauthenticated_Time_And_Reject_Ambiguous_Target()
    {
        var identity = Account("user", "issuer");
        identity.AddClaim(new Claim("auth_time", "1700000000"));
        var secondary = Account("other", "other-issuer", null);
        secondary.AddClaim(new Claim("auth_time", "1800000000"));
        var source = new ClaimsPrincipal([identity, secondary]);
        var target = Account("user", "issuer");

        MfaClaimPreservation.Preserve(source, target, AuthenticationClaimConfig.Default);
        target.FindAll("auth_time").Should().ContainSingle().Which.Value.Should().Be("1700000000");

        target.AddClaim(new Claim("sub", "another-user", ClaimValueTypes.String, "issuer"));
        MfaClaimPreservation.Preserve(source, target, AuthenticationClaimConfig.Default);
        target.FindAll("auth_time").Should().BeEmpty();

        var anonymousTarget = Account("user", "issuer", null);
        MfaClaimPreservation.Preserve(source, anonymousTarget, AuthenticationClaimConfig.Default);
        anonymousTarget.FindAll("auth_time").Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit("acr", "urn:test:mfa")]
    [DataInlineUnit("amr", "mfa")]
    public void Preserve_Should_Copy_All_Amr_And_Exact_Mfa_Without_Duplicates(string claimType, string claimValue)
    {
        var config = new AuthenticationClaimConfig { Mfa = new(claimType, claimValue) };
        var source = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user"),
            new Claim("amr", "pwd"),
            new Claim("amr", "mfa"),
            new Claim(claimType, claimValue),
            new Claim("permission", "admin")
        ], "cookie"));
        var secondary = Account("user", ClaimsIdentity.DefaultIssuer, "secondary");
        secondary.AddClaim(new Claim("amr", "otp"));
        source.AddIdentity(secondary);
        var target = Account("user", ClaimsIdentity.DefaultIssuer);
        target.AddClaim(new Claim("amr", "pwd"));

        MfaClaimPreservation.Preserve(source, target, config);
        MfaClaimPreservation.Preserve(source, target, config);

        target.FindAll("amr").Select(c => c.Value).Should().BeEquivalentTo("pwd", "mfa", "otp");
        target.FindAll(claimType).Count(c => c.Value == claimValue).Should().Be(1);
        target.HasClaim("permission", "admin").Should().BeFalse();
    }

    [Theory]
    [DataInlineUnit(true, "mfa", true)]
    [DataInlineUnit(false, "mfa", false)]
    [DataInlineUnit(true, "admin", false)]
    public void Preserve_Should_Require_Authenticated_Exact_Custom_Marker(bool authenticated, string value, bool expected)
    {
        var source = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user"),
            new Claim("permission", value),
            new Claim("amr", "pwd")
        ], authenticated ? "cookie" : null));
        var target = Account("user", ClaimsIdentity.DefaultIssuer);

        MfaClaimPreservation.Preserve(source, target, new AuthenticationClaimConfig { Mfa = new("permission", "mfa") });

        target.HasClaim("permission", "mfa").Should().Be(expected);
        target.HasClaim("permission", "admin").Should().BeFalse();
        target.HasClaim("amr", "pwd").Should().Be(authenticated);
    }
}
