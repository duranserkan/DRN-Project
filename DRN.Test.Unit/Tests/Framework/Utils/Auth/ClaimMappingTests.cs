using System.Security.Claims;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;

namespace DRN.Test.Unit.Tests.Framework.Utils.Auth;

public class ClaimMappingTests
{
    [Fact]
    public void Custom_Profile_And_Role_Aliases_Should_Be_Explicit_And_Scalar_Conflicts_Should_Fail_Closed()
    {
        var config = new AuthenticationClaimConfig
        {
            Subject = new("uid"), Name = new("display", "username"),
            Email = new("mail"), Roles = new("app-role", "group")
        };
        var identity = new ClaimsIdentity([
            new Claim("uid", "user"), new Claim("username", "User"),
            new Claim("mail", "user@example.test"), new Claim("group", "reader"),
            new Claim("email", "ignored@example.test"), new Claim(ClaimTypes.Role, "ignored"),
            new Claim("APP-ROLE", "ignored-case")
        ], "provider");
        var user = new ScopedUser(config);
        user.SetUser(new ClaimsPrincipal(identity));
        user.Name.Should().Be("User");
        user.Email.Should().Be("user@example.test");
        user.IsInRole("reader").Should().BeTrue();
        user.IsInRole("ignored").Should().BeFalse();
        user.IsInRole("ignored-case").Should().BeFalse();

        identity.AddClaim(new Claim("display", "Conflicting"));
        identity.AddClaim(new Claim("mail", "user@example.test", ClaimValueTypes.String, "other-issuer"));
        user.SetUser(new ClaimsPrincipal(identity));
        user.Name.Should().BeNull();
        user.Email.Should().BeNull();
    }

    [Theory]
    [DataInlineUnit(true, "user")]
    [DataInlineUnit(false, "user")]
    [DataInlineUnit(true, "other")]
    [DataInlineUnit(false, "other")]
    public void Scoped_Id_Should_Reject_Collisions_And_Select_Exact_Subject(bool upperFirst, string upperValue)
    {
        var upper = new Claim("SUB", upperValue);
        var lower = new Claim("sub", "user");
        var identity = new ClaimsIdentity(upperFirst ? [upper, lower] : [lower, upper], "provider");
        var user = ScopedUser.FromClaimsPrincipal(new ClaimsPrincipal(identity));
        user.Id.Should().Be(upperValue == "user" ? "user" : null);
        if (upperValue == "user")
            user.IdClaim!.Claim.Should().BeSameAs(identity.Claims.Single(claim => claim.Type == "sub"));
    }

    [Theory]
    [DataInlineUnit("amr", "mfa", true)]
    [DataInlineUnit("AMR", "mfa", false)]
    [DataInlineUnit("amr", "MFA", false)]
    [DataInlineUnit("amr", "otp", false)]
    [DataInlineUnit("acr", "2", false)]
    [DataInlineUnit("mfa", "true", false)]
    public void Completed_Mfa_Should_Require_Exact_Marker_Type_And_Value(string type, string value, bool completed)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("sub", "user"), new Claim(type, value)
        ], "provider"));
        MfaPrincipal.IsCompleted(principal, AuthenticationClaimConfig.Default).Should().Be(completed);
    }

    [Fact]
    public void Incorrectly_Cased_Subject_Should_Not_Use_The_Subjectless_Compatibility_Path()
    {
        var identity = new ClaimsIdentity([new Claim("SUB", "user"), new Claim("amr", "mfa")], "provider");
        var principal = new ClaimsPrincipal(identity);
        MfaPrincipal.IsCompleted(principal, AuthenticationClaimConfig.Default).Should().BeFalse();
        MfaPrincipal.MatchesIdentity(identity, identity).Should().BeFalse();
        ScopedUser.FromClaimsPrincipal(principal).Id.Should().BeNull();
    }

    [Fact]
    public void Handler_Metadata_Should_Align_Native_And_Scoped_Claims()
    {
        var config = new AuthenticationClaimConfig
        {
            Subject = new("sub"), Name = new("preferred_username"), Email = new("email"), Roles = new("roles")
        };
        var identity = new ClaimsIdentity([
            new Claim("sub", "user"), new Claim("preferred_username", "User"),
            new Claim("email", "user@example.test"), new Claim("roles", "reader")
        ], "oidc", config.Name.Type, config.Roles.Type);
        var principal = new ClaimsPrincipal(identity);
        var user = new ScopedUser(config);
        user.SetUser(principal);
        user.Id.Should().Be("user");
        user.Email.Should().Be("user@example.test");
        user.Name.Should().Be("User");
        user.Name.Should().Be(identity.Name);
        user.IsInRole("reader").Should().BeTrue();
        principal.IsInRole("reader").Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit("user", true)]
    [DataInlineUnit("other", false)]
    public void Duplicate_Subjects_Should_Be_Accepted_Only_When_They_Agree(string duplicate, bool valid)
    {
        var identity = new ClaimsIdentity([new Claim("uid", "user"), new Claim("uid", duplicate)], "provider");
        var config = new AuthenticationClaimConfig { Subject = new("uid") };
        var principal = new ClaimsPrincipal(identity);
        MfaPrincipal.HasSingleAccount(principal, config, requireSubject: true).Should().Be(valid);
        var user = new ScopedUser(config);
        user.SetUser(principal);
        user.Id.Should().Be(valid ? "user" : null);
    }

    [Fact]
    public void Assurance_Should_Not_Combine_Mfa_And_Time_From_Separate_Identities()
    {
        var config = new AuthenticationClaimConfig { Subject = new("uid") };
        var completed = new ClaimsIdentity([
            new Claim("uid", "user", ClaimValueTypes.String, "issuer"),
            new Claim("amr", "mfa", ClaimValueTypes.String, "issuer")
        ], "provider");
        var timed = new ClaimsIdentity([
            new Claim("uid", "user", ClaimValueTypes.String, "issuer"),
            new Claim("auth_time", "1700000000", ClaimValueTypes.Integer64, "issuer")
        ], "provider");
        var principal = new ClaimsPrincipal([completed, timed]);
        MfaPrincipal.IsCompleted(principal, config).Should().BeTrue();
        MfaPrincipal.IsRecent(principal, config, "issuer", TimeSpan.FromMinutes(1),
            DateTimeOffset.FromUnixTimeSeconds(1700000030)).Should().BeFalse();
    }
}
