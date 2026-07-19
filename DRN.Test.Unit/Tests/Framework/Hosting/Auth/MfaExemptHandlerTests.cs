using System.Security.Claims;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Utils.Auth;
using Microsoft.AspNetCore.Authorization;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Auth;

public class MfaExemptHandlerTests
{
    [Fact]
    public async Task HandleAsync_Should_Match_Ambient_Authentication_For_Mixed_Identities()
    {
        var unauthenticatedIdentity = new ClaimsIdentity();
        var authenticatedIdentity = new ClaimsIdentity(authenticationType: "Test");
        var principal = new ClaimsPrincipal([unauthenticatedIdentity, authenticatedIdentity]);
        var scopedUser = ScopedUser.FromClaimsPrincipal(principal);
        var requirement = new MfaExemptRequirement();
        var authorizationContext = new AuthorizationHandlerContext([requirement], principal, resource: null);

        await new MfaExemptHandler().HandleAsync(authorizationContext);

        scopedUser.Authenticated.Should().BeTrue();
        authorizationContext.HasSucceeded.Should().BeTrue();
    }

    [Fact]
    public async Task HandleAsync_Should_Match_Ambient_Authentication_For_Empty_Principal()
    {
        var principal = new ClaimsPrincipal();
        var scopedUser = ScopedUser.FromClaimsPrincipal(principal);
        var requirement = new MfaExemptRequirement();
        var authorizationContext = new AuthorizationHandlerContext([requirement], principal, resource: null);

        await new MfaExemptHandler().HandleAsync(authorizationContext);

        scopedUser.Authenticated.Should().BeFalse();
        authorizationContext.HasSucceeded.Should().BeFalse();
    }
}
