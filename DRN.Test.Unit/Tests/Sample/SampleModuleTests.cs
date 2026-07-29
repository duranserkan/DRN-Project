using System.Security.Claims;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Sample.Hosted;

namespace DRN.Test.Unit.Tests.Sample;

public class SampleModuleTests
{
    [Theory]
    [DataInlineUnit]
    public void AddSampleHostedServices_Should_Expose_Cookie_ClaimsIssuer_Through_ScopedUser_And_ScopeContext(
        DrnTestContextUnit context, IAppSettings settings, IScopedLog scopedLog)
    {
        settings.ApplicationName.Returns("sample issuer");
        settings.GetAppSpecificName("Identity").Returns("_SampleIssuer.Identity.Unit");
        context.ServiceCollection.AddSampleHostedServices(settings);

        var cookieOptions = context.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        cookieOptions.ClaimsIssuer.Should().Be("SampleIssuer");

        var claimsIssuer = cookieOptions.ClaimsIssuer!;
        var claim = new Claim(ClaimTypes.NameIdentifier, "user-42", ClaimValueTypes.String, claimsIssuer);
        var identity = new ClaimsIdentity([claim], IdentityConstants.ApplicationScheme);
        var scopedUser = ScopedUser.FromClaimsPrincipal(new ClaimsPrincipal(identity));

        ScopeContext.InitializeForTest(context, scopedLog: scopedLog, scopedUser: scopedUser);

        scopedUser.Authenticated.Should().BeTrue();
        scopedUser.Id.Should().Be("user-42");
        scopedUser.IdClaim!.Issuer.Should().Be(claimsIssuer);
        scopedUser.ClaimExists(ClaimTypes.NameIdentifier, claimsIssuer).Should().BeTrue();
        scopedUser.ClaimExists(ClaimTypes.NameIdentifier, "another issuer").Should().BeFalse();
        scopedUser.GetClaimParameter<string>(ClaimTypes.NameIdentifier, "another issuer").Should().BeNull();

        ScopeContext.User.Should().BeSameAs(scopedUser);
        ScopeContext.UserId.Should().Be("user-42");
        ScopeContext.GetClaimParameter<string>(ClaimTypes.NameIdentifier, claimsIssuer).Should().Be("user-42");
        ScopeContext.GetClaimParameter<string>(ClaimTypes.NameIdentifier, "another issuer").Should().BeNull();
    }
}
