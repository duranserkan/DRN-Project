using System.Security.Claims;
using DRN.Framework.Hosting.Auth;
using DRN.Framework.Utils.Auth.MFA;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Auth;

public class MfaClaimPreservationTests
{
    [Theory]
    [DataInlineUnit("acr", "urn:test:mfa")]
    [DataInlineUnit("amr", "mfa")]
    public void Preserve_Should_Copy_All_Amr_And_Exact_Mfa_Without_Duplicates(string claimType, string claimValue)
    {
        var config = new MfaClaimConfig(claimType, claimValue);
        var source = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim("amr", "pwd"),
            new Claim("amr", "mfa"),
            new Claim(claimType, claimValue),
            new Claim("permission", "admin")
        ], "cookie"));
        source.AddIdentity(new ClaimsIdentity([new Claim("amr", "otp")], "secondary"));
        var target = new ClaimsIdentity([new Claim("amr", "pwd")], "cookie");

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
            new Claim("permission", value),
            new Claim("amr", "pwd")
        ], authenticated ? "cookie" : null));
        var target = new ClaimsIdentity("cookie");

        MfaClaimPreservation.Preserve(source, target, new MfaClaimConfig("permission", "mfa"));

        target.HasClaim("permission", "mfa").Should().Be(expected);
        target.HasClaim("permission", "admin").Should().BeFalse();
        target.HasClaim("amr", "pwd").Should().Be(authenticated);
    }
}
