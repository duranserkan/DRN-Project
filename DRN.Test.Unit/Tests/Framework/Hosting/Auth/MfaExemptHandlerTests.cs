using System.Security.Claims;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Utils.Auth;
using Microsoft.AspNetCore.Authorization;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Auth;

public class MfaExemptHandlerTests
{
    [Theory]
    [DataInlineUnit(true)]
    [DataInlineUnit(false)]
    public async Task HandleAsync_Should_Match_Ambient_Authentication(bool hasAuthenticatedIdentity)
    {
        var principal = hasAuthenticatedIdentity
            ? new ClaimsPrincipal([new ClaimsIdentity(), new ClaimsIdentity(authenticationType: "Test")])
            : new ClaimsPrincipal();
        var scopedUser = ScopedUser.FromClaimsPrincipal(principal);
        var requirement = new MfaExemptRequirement();
        var authorizationContext = new AuthorizationHandlerContext([requirement], principal, resource: null);

        await new MfaExemptHandler().HandleAsync(authorizationContext);

        scopedUser.Authenticated.Should().Be(hasAuthenticatedIdentity);
        authorizationContext.HasSucceeded.Should().Be(hasAuthenticatedIdentity);
    }
}
