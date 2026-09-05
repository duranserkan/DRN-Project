using System.Security.Claims;
using DRN.Framework.Hosting.Auth;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Auth;

public class RequireMfaHandlerTests
{
    [Fact]
    public async Task Handler_Should_Use_The_Default_Mfa_Claim()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimConventions.AuthenticationMethodReference, "pwd"),
            new Claim(ClaimConventions.AuthenticationMethodReference, MfaClaimValues.Amr)
        ], "Test"));
        var authorization = CreateAuthorizationContext(principal);

        await new RequireMfaHandler().HandleAsync(authorization);

        authorization.HasSucceeded.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit("amr", "mfa")]
    [DataInlineUnit("acr", "urn:drn:test:mfa")]
    public async Task Handler_Should_Use_Configured_Completion_Claim(string claimType, string claimValue)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(claimType, claimValue)], "Test"));
        var authorization = CreateAuthorizationContext(principal);

        await new RequireMfaHandler(new MfaClaimConfig(claimType, claimValue)).HandleAsync(authorization);

        authorization.HasSucceeded.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit("pwd", true)]
    [DataInlineUnit(MfaClaimValues.MfaSetupRequired, false)]
    public async Task Handler_Should_Reject_Setup_Even_With_Exemption_Proof(string method, bool expected)
    {
        var type = method == "pwd" ? ClaimConventions.AuthenticationMethodReference : ClaimConventions.AuthenticationMethod;
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(type, method)], "Bearer"));
        var authorization = CreateAuthorizationContext(principal, new ExemptionProof("Bearer", principal));

        await new RequireMfaHandler(exemptionOptions: Exemptions("Bearer")).HandleAsync(authorization);

        authorization.HasSucceeded.Should().Be(expected);
        authorization.HasFailed.Should().Be(!expected);
    }

    [Theory]
    [DataInlineUnit]
    public async Task Handler_Should_Evaluate_Context_User_Regardless_Of_Ambient_Mfa(DrnTestContextUnit context)
    {
        var ambient = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimConventions.AuthenticationMethodReference, MfaClaimValues.Amr)], "Test"));
        ScopeContext.InitializeForTest(context, scopedUser: ScopedUser.FromClaimsPrincipal(ambient));
        var target = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimConventions.AuthenticationMethodReference, "pwd")], "Test"));
        var authorization = CreateAuthorizationContext(target);

        await new RequireMfaHandler().HandleAsync(authorization);

        authorization.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task Handler_Should_Accept_Exempt_Secondary_Identity_For_The_Same_Account()
    {
        var cookie = Identity("DefaultCookie", "same-user");
        cookie.AddClaim(new Claim(ClaimConventions.AuthenticationMethodReference, "pwd"));
        var key = Identity("CustomApiKey", "same-user");
        key.AddClaim(new Claim("scope", "admin"));
        var principal = new ClaimsPrincipal([cookie, key]);
        var authorization = CreateAuthorizationContext(principal, new ExemptionProof("CustomApiKey", principal));

        await new RequireMfaHandler(exemptionOptions: Exemptions("CustomApiKey")).HandleAsync(authorization);

        authorization.HasSucceeded.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public async Task Handler_Should_Reject_Exemption_Absent_From_Evaluated_Principal(
        DrnTestContextUnit context, bool ambientIncludesCookie)
    {
        var cookie = Identity("DefaultCookie", "same-user");
        cookie.AddClaim(new Claim(ClaimConventions.AuthenticationMethodReference, "pwd"));
        var key = Identity("CustomApiKey", "same-user");
        var proof = new ClaimsPrincipal(key);
        var ambient = ambientIncludesCookie ? new ClaimsPrincipal([cookie, key]) : proof;
        var scopedUser = ScopedUser.FromClaimsPrincipal(ambient);
        scopedUser.SetExemption("CustomApiKey", proof);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);
        var authorization = CreateAuthorizationContext(new ClaimsPrincipal(cookie), scopedUser.Exemption);

        await new RequireMfaHandler(exemptionOptions: Exemptions("CustomApiKey")).HandleAsync(authorization);

        authorization.HasFailed.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task Handler_Should_Reject_Equivalent_Claims_From_An_Unselected_Exempt_Scheme(DrnTestContextUnit context)
    {
        const string exemptScheme = "CustomApiKey";
        const string selectedScheme = "NonExemptBearer";
        var exemptions = Exemptions(exemptScheme);
        var exempt = new ClaimsPrincipal(Identity("Bearer", "shared-user-id"));
        var target = new ClaimsPrincipal(Identity("Bearer", "shared-user-id"));
        var auth = Substitute.For<IAuthenticationService>();
        auth.AuthenticateAsync(Arg.Any<HttpContext>(), exemptScheme)
            .Returns(AuthenticateResult.Success(new AuthenticationTicket(exempt, exemptScheme)));
        auth.AuthenticateAsync(Arg.Any<HttpContext>(), selectedScheme)
            .Returns(AuthenticateResult.Success(new AuthenticationTicket(target, selectedScheme)));
        context.ServiceCollection.AddSingleton(auth).AddAuthorization();
        var scopedUser = ScopedUser.FromClaimsPrincipal(exempt);
        scopedUser.SetExemption(exemptScheme, exempt);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);
        var http = new DefaultHttpContext { RequestServices = ScopeContext.Services, User = target };
        var policy = new AuthorizationPolicyBuilder(selectedScheme).RequireAuthenticatedUser().Build();
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(policy), "selected-policy"));

        await new MfaExemptionMiddleware(_ => Task.CompletedTask).InvokeAsync(http, scopedUser, exemptions);
        var authorization = new AuthorizationHandlerContext([new MfaRequirement()], target, http);
        await new RequireMfaHandler(exemptionOptions: exemptions).HandleAsync(authorization);

        authorization.HasFailed.Should().BeTrue();
        MfaPolicyProof.Get(http).Should().BeEmpty();
        await auth.Received(1).AuthenticateAsync(http, selectedScheme);
        await auth.DidNotReceive().AuthenticateAsync(Arg.Any<HttpContext>(), exemptScheme);
    }

    [Fact]
    public async Task Handler_Should_Accept_Transformed_Exempt_Identity()
    {
        var proof = new ClaimsPrincipal(Identity("CustomApiKey", "user-1"));
        var transformed = Identity("CustomApiKey", "user-1");
        transformed.AddClaim(new Claim("transformed", "true"));
        var authorization = CreateAuthorizationContext(new ClaimsPrincipal(transformed), new ExemptionProof("CustomApiKey", proof));

        await new RequireMfaHandler(exemptionOptions: Exemptions("CustomApiKey")).HandleAsync(authorization);

        authorization.HasSucceeded.Should().BeTrue();
    }

    private static ClaimsIdentity Identity(string scheme, string subject) =>
        new([new Claim(ClaimTypes.NameIdentifier, subject)], scheme);

    private static MfaExemptionOptions Exemptions(string scheme)
    {
        var options = new MfaExemptionOptions();
        options.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = [scheme] });
        return options;
    }

    private static AuthorizationHandlerContext CreateAuthorizationContext(ClaimsPrincipal principal, ExemptionProof? proof = null)
    {
        var http = new DefaultHttpContext { User = principal };
        if (proof != null)
            MfaPolicyProof.Set(http, [proof]);
        return new AuthorizationHandlerContext([new MfaRequirement()], principal, http);
    }
}
