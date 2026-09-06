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
    [DataInlineUnit(true)]
    [DataInlineUnit(false)]
    public async Task Policy_Resolution_Should_Respect_Provider_Caching_And_Request_Boundaries(
        DrnTestContextUnit context, bool cacheable)
    {
        var provider = Substitute.For<IAuthorizationPolicyProvider>();
        provider.AllowsCachingPolicies.Returns(cacheable);
        var initial = new AuthorizationPolicyBuilder("First").RequireAuthenticatedUser().Build();
        var updated = new AuthorizationPolicyBuilder("Second").RequireAuthenticatedUser().Build();
        provider.GetPolicyAsync("selected").Returns(initial);
        context.ServiceCollection.AddSingleton(provider);
        var endpoint = new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizeAttribute("selected")), "selected");
        var http = new DefaultHttpContext { RequestServices = context };
        http.SetEndpoint(endpoint);

        var first = await MfaPolicyProof.ResolvePolicyAsync(http);
        provider.GetPolicyAsync("selected").Returns(updated);
        var second = await MfaPolicyProof.ResolvePolicyAsync(http);
        second!.AuthenticationSchemes.Should().Equal(cacheable ? "First" : "Second");
        if (cacheable)
            second.Should().BeSameAs(first);

        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, endpoint.Metadata, "replacement"));
        (await MfaPolicyProof.ResolvePolicyAsync(http))!.AuthenticationSchemes.Should().Equal("Second");

        var otherRequest = new DefaultHttpContext { RequestServices = context };
        otherRequest.SetEndpoint(endpoint);
        (await MfaPolicyProof.ResolvePolicyAsync(otherRequest))!.AuthenticationSchemes.Should().Equal("Second");
    }

    [Theory]
    [DataInlineUnit]
    public async Task Policy_Resolution_Should_Cache_Null_And_Respect_Provider_Replacement(DrnTestContextUnit context)
    {
        var provider = Substitute.For<IAuthorizationPolicyProvider>();
        provider.AllowsCachingPolicies.Returns(true);
        provider.GetFallbackPolicyAsync().Returns(Task.FromResult<AuthorizationPolicy?>(null));
        context.ServiceCollection.AddSingleton(provider);
        var http = new DefaultHttpContext { RequestServices = context };

        (await MfaPolicyProof.ResolvePolicyAsync(http)).Should().BeNull();
        (await MfaPolicyProof.ResolvePolicyAsync(http)).Should().BeNull();
        await provider.Received(1).GetFallbackPolicyAsync();

        var replacement = Substitute.For<IAuthorizationPolicyProvider>();
        replacement.AllowsCachingPolicies.Returns(true);
        replacement.GetFallbackPolicyAsync().Returns(new AuthorizationPolicyBuilder("Replacement").RequireAuthenticatedUser().Build());
        using var services = new ServiceCollection().AddSingleton(replacement).BuildServiceProvider();
        http.RequestServices = services;
        (await MfaPolicyProof.ResolvePolicyAsync(http))!.AuthenticationSchemes.Should().Equal("Replacement");
    }

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
    [DataInlineUnit(true)]
    [DataInlineUnit(false)]
    public async Task Redirection_Should_Use_Policy_Selected_Principal_Instead_Of_Cookie_Exemption(
        DrnTestContextUnit context, bool selectedPrincipalIsExempt)
    {
        var exemptions = new MfaExemptionOptions();
        exemptions.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = ["Cookies", "CustomApiKey"] });
        context.ServiceCollection.AddSingleton(exemptions).AddAuthorization();
        var cookiePrincipal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-1")], "Cookies"));
        var selectedPrincipal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "user-2")], "CustomApiKey"));
        var evaluator = Substitute.For<IPolicyEvaluator>();
        evaluator.AuthenticateAsync(Arg.Any<AuthorizationPolicy>(), Arg.Any<HttpContext>())
            .Returns(call =>
            {
                call.Arg<HttpContext>().User = selectedPrincipal;
                return AuthenticateResult.Success(new AuthenticationTicket(selectedPrincipal, "CustomApiKey"));
            });
        context.ServiceCollection.AddSingleton(evaluator);
        var scopedUser = ScopedUser.FromClaimsPrincipal(cookiePrincipal);
        scopedUser.SetExemption("Cookies", cookiePrincipal);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);
        var options = new MfaRedirectionOptions();
        options.MapFromConfig(new MfaRedirectionConfig(
            mfaSetupUrl: "/mfa-setup", mfaLoginUrl: "/mfa-login", loginUrl: "/login",
            logoutUrl: "/logout", appPages: ["/app/dashboard"]));
        var http = new DefaultHttpContext
        {
            RequestServices = ScopeContext.Services, User = cookiePrincipal, Request = { Path = "/app/dashboard" }
        };
        SelectPolicy(http, "CustomApiKey");
        MfaPolicyProof.Set(http, selectedPrincipalIsExempt
            ? [new ExemptionProof("CustomApiKey", selectedPrincipal)]
            : [new ExemptionProof("Cookies", cookiePrincipal)]);
        var nextCalled = false;
        var middleware = new MfaRedirectionMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(http, options);

        await evaluator.Received(1).AuthenticateAsync(
            Arg.Is<AuthorizationPolicy>(policy => policy.AuthenticationSchemes.Contains("CustomApiKey")), http);
        http.User.Should().BeSameAs(selectedPrincipal);
        scopedUser.ExemptionPrincipal.Should().BeSameAs(cookiePrincipal);
        nextCalled.Should().Be(selectedPrincipalIsExempt);
        http.Response.StatusCode.Should().Be(selectedPrincipalIsExempt ? 200 : 302);
        http.Response.Headers.Location.ToString().Should().Be(selectedPrincipalIsExempt ? string.Empty : "/login");
    }

    private static void SelectPolicy(HttpContext context, string scheme)
    {
        var policy = new AuthorizationPolicyBuilder(scheme).RequireAuthenticatedUser().Build();
        context.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(policy), "selected-test-policy"));
    }
}
