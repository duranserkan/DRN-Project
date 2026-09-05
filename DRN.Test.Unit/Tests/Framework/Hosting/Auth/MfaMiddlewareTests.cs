using System.Security.Claims;
using DRN.Framework.Hosting.Auth;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Auth;

public class MfaMiddlewareTests
{
    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public async Task Exemption_Discovery_Should_Preserve_Request_Principal_Regardless_Of_Ambient_Mfa(
        DrnTestContextUnit context, bool ambientMfaCompleted)
    {
        const string scheme = "CustomApiKey";
        var options = new MfaExemptionOptions();
        options.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = [scheme] });
        var initialIdentity = new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, "user-1"), new Claim(ClaimTypes.Role, "BaseRole")
        ], "DefaultCookie");
        if (ambientMfaCompleted)
            initialIdentity.AddClaim(new Claim(ClaimConventions.AuthenticationMethodReference, MfaClaimValues.Amr));
        var initial = new ClaimsPrincipal(initialIdentity);
        var exemptIdentity = new ClaimsIdentity([
            new Claim(ClaimTypes.Role, "ApiRole"), new Claim("scope", "admin")
        ], scheme);
        var exempt = new ClaimsPrincipal(exemptIdentity);
        var auth = Substitute.For<IAuthenticationService>();
        auth.AuthenticateAsync(Arg.Any<HttpContext>(), scheme)
            .Returns(AuthenticateResult.Success(new AuthenticationTicket(exempt, scheme)));
        context.ServiceCollection.AddSingleton(auth).AddAuthorization();
        var scopedUser = ScopedUser.FromClaimsPrincipal(initial);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);
        MfaFor.MfaCompleted.Should().Be(ambientMfaCompleted);
        var http = new DefaultHttpContext { RequestServices = ScopeContext.Services, User = initial };
        SelectPolicy(http, scheme);

        await new MfaExemptionMiddleware(_ => Task.CompletedTask).InvokeAsync(http, scopedUser, options);

        scopedUser.ExemptionScheme.Should().Be(scheme);
        scopedUser.ExemptionPrincipal.Should().BeSameAs(exempt);
        scopedUser.HasExemptionScheme.Should().BeTrue();
        scopedUser.IsInRole("BaseRole").Should().BeTrue();
        scopedUser.IsInRole("ApiRole").Should().BeFalse();
        http.User.Should().BeSameAs(initial);
        http.User.Identities.Should().NotContain(exemptIdentity);
    }

    [Theory]
    [DataInlineUnit]
    public async Task Exemption_Discovery_Should_Authenticate_Only_The_Selected_Scheme(DrnTestContextUnit context)
    {
        const string certScheme = "ClientCert";
        const string apiKeyScheme = "CustomApiKey";
        var options = new MfaExemptionOptions();
        options.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = [certScheme, apiKeyScheme] });
        var initial = new ClaimsPrincipal(new ClaimsIdentity());
        var cert = new ClaimsPrincipal(new ClaimsIdentity([new Claim("thumbprint", "abc")], certScheme));
        var key = new ClaimsPrincipal(new ClaimsIdentity([new Claim("key", "xyz")], apiKeyScheme));
        var auth = Substitute.For<IAuthenticationService>();
        auth.AuthenticateAsync(Arg.Any<HttpContext>(), certScheme)
            .Returns(AuthenticateResult.Success(new AuthenticationTicket(cert, certScheme)));
        auth.AuthenticateAsync(Arg.Any<HttpContext>(), apiKeyScheme)
            .Returns(AuthenticateResult.Success(new AuthenticationTicket(key, apiKeyScheme)));
        context.ServiceCollection.AddSingleton(auth).AddAuthorization();
        var scopedUser = ScopedUser.FromClaimsPrincipal(initial);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);
        var http = new DefaultHttpContext { RequestServices = ScopeContext.Services, User = initial };
        SelectPolicy(http, certScheme);

        await new MfaExemptionMiddleware(_ => Task.CompletedTask).InvokeAsync(http, scopedUser, options);

        scopedUser.ExemptionScheme.Should().Be(certScheme);
        scopedUser.ExemptionPrincipal.Should().BeSameAs(cert);
        scopedUser.HasExemptionScheme.Should().BeTrue();
        await auth.DidNotReceive().AuthenticateAsync(Arg.Any<HttpContext>(), apiKeyScheme);
    }

    [Theory]
    [DataInlineUnit]
    public async Task Redirection_Should_Continue_When_Selected_Scheme_Is_Exempt(DrnTestContextUnit context)
    {
        var exemptions = new MfaExemptionOptions();
        exemptions.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = ["CustomApiKey"] });
        context.ServiceCollection.AddSingleton(exemptions).AddAuthorization();
        context.ServiceCollection.AddSingleton(Substitute.For<IPolicyEvaluator>());
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "CustomApiKey"));
        var scopedUser = ScopedUser.FromClaimsPrincipal(principal);
        scopedUser.SetExemption("CustomApiKey", principal);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);
        var options = new MfaRedirectionOptions();
        options.MapFromConfig(new MfaRedirectionConfig(
            mfaSetupUrl: "/mfa-setup", mfaLoginUrl: "/mfa-login", loginUrl: "/login",
            logoutUrl: "/logout", appPages: ["/app/dashboard"]));
        var http = new DefaultHttpContext
        {
            RequestServices = ScopeContext.Services, User = principal, Request = { Path = "/app/dashboard" }
        };
        SelectPolicy(http, "CustomApiKey");
        MfaPolicyProof.Set(http, [new ExemptionProof("CustomApiKey", principal)]);
        var nextCalled = false;
        var middleware = new MfaRedirectionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(http, options);

        nextCalled.Should().BeTrue();
        http.Response.StatusCode.Should().Be(200);
    }

    private static void SelectPolicy(HttpContext context, string scheme)
    {
        var policy = new AuthorizationPolicyBuilder(scheme).RequireAuthenticatedUser().Build();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(policy), "selected-test-policy"));
    }
}
