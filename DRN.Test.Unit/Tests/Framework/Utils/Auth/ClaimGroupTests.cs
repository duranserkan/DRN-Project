using System.Security.Claims;
using DRN.Framework.Utils.Auth;

namespace DRN.Test.Unit.Tests.Framework.Utils.Auth;

public class ClaimGroupTests
{
    private const string ClaimType = "permission";
    private const string PrimaryIssuer = "primary";
    private const string SecondaryIssuer = "secondary";

    [Fact]
    public void ClaimLookups_Should_Resolve_Primary_And_Explicit_Issuers()
    {
        var primary = new ClaimsIdentity(
            [new Claim(ClaimType, "read", ClaimValueTypes.String, PrimaryIssuer)],
            authenticationType: "Primary");
        var secondary = new ClaimsIdentity(
            [new Claim(ClaimType, "write", ClaimValueTypes.String, SecondaryIssuer)],
            authenticationType: "Secondary");
        var claims = primary.Claims.Concat(secondary.Claims).ToHashSet();
        var group = new ClaimGroup(claims, primary);

        group.GetValue().Should().Be("read");
        group.GetValue(SecondaryIssuer).Should().Be("write");
        group.ClaimExists().Should().BeTrue();
        group.ClaimExists(SecondaryIssuer).Should().BeTrue();
        group.FindClaim("read").Should().BeSameAs(primary.Claims.Single());
        group.FindClaim("write", SecondaryIssuer).Should().BeSameAs(secondary.Claims.Single());
    }
}
