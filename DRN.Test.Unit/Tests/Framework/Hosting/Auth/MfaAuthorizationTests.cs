using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using DRN.Framework.Hosting.Auth;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Auth;

public class MfaAuthorizationTests
{
    [Fact]
    public void IsMfaEnforced_Should_Require_MfaRequirement_In_Default_Policy()
    {
        var withoutMfa = new AuthorizationOptions
        {
            DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()
        };
        var withMfa = new AuthorizationOptions
        {
            DefaultPolicy = new AuthorizationPolicyBuilder().AddRequirements(new MfaRequirement()).Build()
        };

        MfaAuthorization.IsMfaEnforced(withoutMfa).Should().BeFalse();
        MfaAuthorization.IsMfaEnforced(withMfa).Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task RequireMfaHandler_Should_Succeed_For_Mfa_Claim(DrnTestContextUnit context)
    {
        var principal = CreatePrincipal(
            new Claim(ClaimConventions.AuthenticationMethodReference, "pwd"),
            new Claim(ClaimConventions.AuthenticationMethodReference, MfaClaimValues.Amr));
        ScopeContext.InitializeForTest(context, scopedUser: ScopedUser.FromClaimsPrincipal(principal));
        var authorizationContext = CreateAuthorizationContext(principal);

        await new RequireMfaHandler().HandleAsync(authorizationContext);

        authorizationContext.HasSucceeded.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task RequireMfaHandler_Should_Succeed_For_Configured_Mfa_Claim(DrnTestContextUnit context)
    {
        var config = new MfaClaimConfig("acr", "urn:drn:test:mfa");
        context.ServiceCollection.AddSingleton(config);
        var principal = CreatePrincipal(new Claim(config.ClaimType, config.ClaimValue));
        ScopeContext.InitializeForTest(context, scopedUser: ScopedUser.FromClaimsPrincipal(principal));
        var authorizationContext = CreateAuthorizationContext(principal);

        await new RequireMfaHandler(claimConfig: config).HandleAsync(authorizationContext);

        authorizationContext.HasSucceeded.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task RequireMfaHandler_Should_Succeed_For_Configured_Exemption_Scheme(DrnTestContextUnit context)
    {
        var exemptionOptions = new MfaExemptionOptions();
        exemptionOptions.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = ["Bearer"] });
        context.ServiceCollection.AddSingleton(exemptionOptions);

        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimConventions.AuthenticationMethodReference, "pwd")], "Bearer"));
        var scopedUser = ScopedUser.FromClaimsPrincipal(principal);
        scopedUser.SetExemption("Bearer", principal);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);
        var authorizationContext = CreateAuthorizationContext(principal);

        await new RequireMfaHandler(exemptionOptions: exemptionOptions).HandleAsync(authorizationContext);

        authorizationContext.HasSucceeded.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task RequireMfaHandler_Should_Fail_For_Setup_Credential_With_Configured_Exemption_Scheme(DrnTestContextUnit context)
    {
        var exemptionOptions = new MfaExemptionOptions();
        exemptionOptions.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = ["Bearer"] });
        context.ServiceCollection.AddSingleton(exemptionOptions);

        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimConventions.AuthenticationMethod, MfaClaimValues.MfaSetupRequired)], "Bearer"));
        var scopedUser = ScopedUser.FromClaimsPrincipal(principal);
        scopedUser.SetExemption("Bearer", principal);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);
        var authorizationContext = CreateAuthorizationContext(principal);

        await new RequireMfaHandler(exemptionOptions: exemptionOptions).HandleAsync(authorizationContext);

        authorizationContext.HasFailed.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task RequireMfaHandler_Should_Evaluate_Context_User_Directly_Regardless_Of_Ambient_Scope(DrnTestContextUnit context)
    {
        // Ambient scope has MFA completed from another scheme
        var ambientPrincipal = CreatePrincipal(new Claim(ClaimConventions.AuthenticationMethodReference, MfaClaimValues.Amr));
        ScopeContext.InitializeForTest(context, scopedUser: ScopedUser.FromClaimsPrincipal(ambientPrincipal));

        // But the authorization context user being evaluated has only password authentication
        var targetPrincipal = CreatePrincipal(new Claim(ClaimConventions.AuthenticationMethodReference, "pwd"));
        var authorizationContext = CreateAuthorizationContext(targetPrincipal);

        await new RequireMfaHandler().HandleAsync(authorizationContext);

        authorizationContext.HasFailed.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task RequireMfaHandler_Should_Fail_For_Unauthenticated_Principal(DrnTestContextUnit context)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());
        ScopeContext.InitializeForTest(context, scopedUser: ScopedUser.FromClaimsPrincipal(principal));
        var authorizationContext = CreateAuthorizationContext(principal);

        await new RequireMfaHandler().HandleAsync(authorizationContext);

        authorizationContext.HasFailed.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task RequireMfaHandler_Should_Fail_For_Password_Only_Principal(DrnTestContextUnit context)
    {
        var principal = CreatePrincipal(new Claim(ClaimConventions.AuthenticationMethodReference, "pwd"));
        ScopeContext.InitializeForTest(context, scopedUser: ScopedUser.FromClaimsPrincipal(principal));
        var authorizationContext = CreateAuthorizationContext(principal);

        await new RequireMfaHandler().HandleAsync(authorizationContext);

        authorizationContext.HasFailed.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task RequireMfaHandler_Should_Fail_When_Mfa_Claim_Is_On_Unauthenticated_Identity(DrnTestContextUnit context)
    {
        var authenticatedIdentity = new ClaimsIdentity([new Claim(ClaimConventions.AuthenticationMethodReference, "pwd")], "Password");
        var unauthenticatedIdentity = new ClaimsIdentity([new Claim(ClaimConventions.AuthenticationMethodReference, MfaClaimValues.Amr)]);
        var principal = new ClaimsPrincipal([authenticatedIdentity, unauthenticatedIdentity]);

        ScopeContext.InitializeForTest(context, scopedUser: ScopedUser.FromClaimsPrincipal(principal));
        var authorizationContext = CreateAuthorizationContext(principal);

        await new RequireMfaHandler().HandleAsync(authorizationContext);

        authorizationContext.HasFailed.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task RequireMfaHandler_Should_Succeed_When_Mfa_Claim_Is_On_Authenticated_Secondary_Identity(DrnTestContextUnit context)
    {
        var primaryIdentity = new ClaimsIdentity([new Claim(ClaimConventions.AuthenticationMethodReference, "pwd")], "Password");
        var secondaryIdentity = new ClaimsIdentity([new Claim(ClaimConventions.AuthenticationMethodReference, MfaClaimValues.Amr)], "Federated");
        var principal = new ClaimsPrincipal([primaryIdentity, secondaryIdentity]);

        ScopeContext.InitializeForTest(context, scopedUser: ScopedUser.FromClaimsPrincipal(principal));
        var authorizationContext = CreateAuthorizationContext(principal);

        await new RequireMfaHandler().HandleAsync(authorizationContext);

        authorizationContext.HasSucceeded.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task RequireMfaHandler_Should_Ignore_Setup_Claim_On_Unauthenticated_Identity(DrnTestContextUnit context)
    {
        var authenticatedIdentity = new ClaimsIdentity([new Claim(ClaimConventions.AuthenticationMethodReference, MfaClaimValues.Amr)], "Password");
        var unauthenticatedIdentity = new ClaimsIdentity([new Claim(ClaimConventions.AuthenticationMethod, MfaClaimValues.MfaSetupRequired)]);
        var principal = new ClaimsPrincipal([authenticatedIdentity, unauthenticatedIdentity]);

        ScopeContext.InitializeForTest(context, scopedUser: ScopedUser.FromClaimsPrincipal(principal));
        var authorizationContext = CreateAuthorizationContext(principal);

        await new RequireMfaHandler().HandleAsync(authorizationContext);

        authorizationContext.HasSucceeded.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task RequireMfaHandler_Should_Succeed_When_Exemption_Scheme_Was_Authenticated_Via_Middleware(DrnTestContextUnit context)
    {
        var exemptionOptions = new MfaExemptionOptions();
        exemptionOptions.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = ["CustomApiKey"] });
        context.ServiceCollection.AddSingleton(exemptionOptions);

        var cookieIdentity = new ClaimsIdentity([new Claim(ClaimConventions.AuthenticationMethodReference, "pwd")], "DefaultCookie");
        var exemptIdentity = new ClaimsIdentity([new Claim("scope", "admin")], "CustomApiKey");
        var principal = new ClaimsPrincipal([cookieIdentity, exemptIdentity]);
        var scopedUser = ScopedUser.FromClaimsPrincipal(principal);
        scopedUser.SetExemption("CustomApiKey", principal);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);
        var authorizationContext = CreateAuthorizationContext(principal);

        await new RequireMfaHandler(exemptionOptions: exemptionOptions).HandleAsync(authorizationContext);

        authorizationContext.HasSucceeded.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task RequireMfaHandler_Should_Fail_When_Evaluated_Principal_Lacks_Exempt_Identity_Even_If_Ambient_Exemption_Present(DrnTestContextUnit context)
    {
        var exemptionOptions = new MfaExemptionOptions();
        exemptionOptions.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = ["CustomApiKey"] });
        context.ServiceCollection.AddSingleton(exemptionOptions);

        // Ambient scoped user was authenticated via CustomApiKey
        var ambientPrincipal = new ClaimsPrincipal(new ClaimsIdentity([], "CustomApiKey"));
        var scopedUser = ScopedUser.FromClaimsPrincipal(ambientPrincipal);
        scopedUser.SetExemption("CustomApiKey", ambientPrincipal);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);

        // Target principal evaluated by handler has only password auth from DefaultCookie
        var targetPrincipal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimConventions.AuthenticationMethodReference, "pwd")], "DefaultCookie"));
        var authorizationContext = CreateAuthorizationContext(targetPrincipal);

        await new RequireMfaHandler(exemptionOptions: exemptionOptions).HandleAsync(authorizationContext);

        authorizationContext.HasFailed.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task RequireMfaHandler_Should_Fail_When_Named_Policy_Principal_Does_Not_Include_Exempt_Identity(DrnTestContextUnit context)
    {
        var exemptionOptions = new MfaExemptionOptions();
        exemptionOptions.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = ["CustomApiKey"] });
        context.ServiceCollection.AddSingleton(exemptionOptions);

        // Ambient scoped user authenticated both Cookie and CustomApiKey
        var cookieIdentity = new ClaimsIdentity([new Claim(ClaimConventions.AuthenticationMethodReference, "pwd")], "DefaultCookie");
        var apiKeyIdentity = new ClaimsIdentity([new Claim("scope", "api")], "CustomApiKey");
        var fullPrincipal = new ClaimsPrincipal([cookieIdentity, apiKeyIdentity]);
        var scopedUser = ScopedUser.FromClaimsPrincipal(fullPrincipal);
        scopedUser.SetExemption("CustomApiKey", new ClaimsPrincipal(apiKeyIdentity));
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);

        // Named policy filters principal strictly to the Cookie identity (simulating PolicyEvaluator scheme isolation)
        var cookieOnlyPrincipal = new ClaimsPrincipal(cookieIdentity);
        var authorizationContext = CreateAuthorizationContext(cookieOnlyPrincipal);

        await new RequireMfaHandler(exemptionOptions: exemptionOptions).HandleAsync(authorizationContext);

        authorizationContext.HasFailed.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task MfaExemptionMiddleware_Should_Record_Exemption_Proof_Without_Mutating_Request_Principal(DrnTestContextUnit context)
    {
        const string exemptScheme = "CustomApiKey";
        var exemptionOptions = new MfaExemptionOptions();
        exemptionOptions.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = [exemptScheme] });

        var initialIdentity = new ClaimsIdentity([
            new Claim(ClaimConventions.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Role, "BaseRole")
        ], "DefaultCookie");
        var initialPrincipal = new ClaimsPrincipal(initialIdentity);

        var exemptIdentity = new ClaimsIdentity([
            new Claim(ClaimTypes.Role, "ApiRole"),
            new Claim("scope", "admin")
        ], exemptScheme);
        var exemptPrincipal = new ClaimsPrincipal(exemptIdentity);

        var authService = Substitute.For<IAuthenticationService>();
        authService.AuthenticateAsync(Arg.Any<HttpContext>(), exemptScheme)
            .Returns(AuthenticateResult.Success(new AuthenticationTicket(exemptPrincipal, exemptScheme)));

        context.ServiceCollection.AddSingleton(authService);
        var scopedUser = ScopedUser.FromClaimsPrincipal(initialPrincipal);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = ScopeContext.Services,
            User = initialPrincipal
        };

        var middleware = new MfaExemptionMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext, scopedUser, exemptionOptions);

        scopedUser.ExemptionScheme.Should().Be(exemptScheme);
        scopedUser.ExemptionPrincipal.Should().BeSameAs(exemptPrincipal);
        scopedUser.HasExemptionScheme.Should().BeTrue();
        scopedUser.IsInRole("BaseRole").Should().BeTrue();
        scopedUser.IsInRole("ApiRole").Should().BeFalse();
        httpContext.User.Should().BeSameAs(initialPrincipal);
        httpContext.User.Identities.Should().NotContain(exemptIdentity);
    }

    [Theory]
    [DataInlineUnit]
    public void MfaFor_Should_Not_Require_Renewal_When_Exemption_Scheme_Present(DrnTestContextUnit context)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimConventions.NameIdentifier, "user-1")], "CustomApiKey"));
        var scopedUser = ScopedUser.FromClaimsPrincipal(principal);
        scopedUser.SetExemption("CustomApiKey", principal);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);

        scopedUser.Authenticated.Should().BeTrue();
        scopedUser.HasExemptionScheme.Should().BeTrue();
        MfaFor.MfaCompleted.Should().BeFalse();
        MfaFor.MfaRenewalRequired.Should().BeFalse();
    }

    [Theory]
    [DataInlineUnit]
    public async Task MfaRedirectionMiddleware_Should_Not_Redirect_When_Exemption_Scheme_Present(DrnTestContextUnit context)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimConventions.NameIdentifier, "user-1")], "CustomApiKey"));
        var scopedUser = ScopedUser.FromClaimsPrincipal(principal);
        scopedUser.SetExemption("CustomApiKey", principal);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);

        var redirectionOptions = new MfaRedirectionOptions();
        redirectionOptions.MapFromConfig(new MfaRedirectionConfig(
            mfaSetupUrl: "/mfa-setup",
            mfaLoginUrl: "/mfa-login",
            loginUrl: "/login",
            logoutUrl: "/logout",
            appPages: ["/app/dashboard"]));

        var httpContext = new DefaultHttpContext
        {
            Request = { Path = "/app/dashboard" }
        };

        var nextCalled = false;
        var middleware = new MfaRedirectionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(httpContext, redirectionOptions);

        nextCalled.Should().BeTrue();
        httpContext.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task PolicyProvider_Should_Combine_Mfa_Default_With_Named_Policy()
    {
        var options = CreateAuthorizationOptions();
        var provider = new MfaEnforcingAuthorizationPolicyProvider(Options.Create(options));

        var policy = await provider.GetPolicyAsync("scope-read");

        policy.Should().NotBeNull();
        policy.Requirements.Should().ContainSingle(requirement => requirement is MfaRequirement);
        policy.Requirements.Should().ContainSingle(requirement => requirement is ClaimsAuthorizationRequirement);
        policy.AuthenticationSchemes.Should().ContainSingle().Which.Should().Be("ApiKey");
    }

    [Fact]
    public async Task PolicyProvider_Should_Preserve_Named_Policy_Schemes_Without_Default_Schemes()
    {
        var options = new AuthorizationOptions
        {
            DefaultPolicy = new AuthorizationPolicyBuilder("Cookies").AddRequirements(new MfaRequirement()).Build()
        };
        options.AddPolicy("ApiKeyPolicy", policy =>
        {
            policy.AuthenticationSchemes.Add("ApiKey");
            policy.RequireAuthenticatedUser();
        });

        var provider = new MfaEnforcingAuthorizationPolicyProvider(Options.Create(options));
        var policy = await provider.GetPolicyAsync("ApiKeyPolicy");

        policy.Should().NotBeNull();
        policy.AuthenticationSchemes.Should().ContainSingle().Which.Should().Be("ApiKey");
        policy.Requirements.Should().ContainSingle(r => r is MfaRequirement);
    }

    [Fact]
    public async Task PolicyProvider_Should_Not_Combine_Mfa_Default_With_Exemption_Policy()
    {
        var options = CreateAuthorizationOptions();
        var provider = new MfaEnforcingAuthorizationPolicyProvider(Options.Create(options));

        var policy = await provider.GetPolicyAsync(AuthPolicy.MfaExempt);

        policy.Should().NotBeNull();
        policy.Requirements.Should().ContainSingle(requirement => requirement is MfaExemptRequirement);
        policy.Requirements.Should().NotContain(requirement => requirement is MfaRequirement);
    }

    [Theory]
    [DataInlineUnit]
    public async Task ResultHandler_Should_Delegate_Immediately_When_Authorization_Failed(DrnTestContextUnit context)
    {
        context.ServiceCollection.AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        var sp = context.BuildServiceProvider();

        var options = new AuthorizationOptions
        {
            DefaultPolicy = new AuthorizationPolicyBuilder().AddRequirements(new MfaRequirement()).Build()
        };
        var handler = new MfaEnforcingAuthorizationMiddlewareResultHandler(Options.Create(options));
        var httpContext = new DefaultHttpContext
        {
            RequestServices = sp
        };
        var policy = options.DefaultPolicy;
        var authResult = PolicyAuthorizationResult.Forbid();

        await handler.HandleAsync(_ => Task.CompletedTask, httpContext, policy, authResult);

        httpContext.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ResultHandler_Should_Delegate_Immediately_When_Mfa_Is_Disabled()
    {
        var options = new AuthorizationOptions
        {
            DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()
        };
        var handler = new MfaEnforcingAuthorizationMiddlewareResultHandler(Options.Create(options));
        var context = new DefaultHttpContext();
        var policy = options.DefaultPolicy;
        var authResult = PolicyAuthorizationResult.Success();
        var nextInvoked = false;

        await handler.HandleAsync(_ => { nextInvoked = true; return Task.CompletedTask; }, context, policy, authResult);

        nextInvoked.Should().BeTrue();
    }

    [Fact]
    public void IsMfaSatisfied_Should_Fail_When_Exemption_Marker_Is_Missing_Or_Blank()
    {
        var exemptionOptions = new MfaExemptionOptions();
        exemptionOptions.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = ["CustomApiKey"] });

        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("scope", "api")], "CustomApiKey"));

        // Markerless calls (null, empty, whitespace scheme, or null exemption principal) must fail
        MfaAuthorization.IsMfaSatisfied(principal, MfaClaimConfig.AspNetIdentity, exemptionOptions, authenticatedExemptionScheme: null, exemptionPrincipal: null)
            .Should().BeFalse();
        MfaAuthorization.IsMfaSatisfied(principal, MfaClaimConfig.AspNetIdentity, exemptionOptions, authenticatedExemptionScheme: string.Empty, exemptionPrincipal: principal)
            .Should().BeFalse();
        MfaAuthorization.IsMfaSatisfied(principal, MfaClaimConfig.AspNetIdentity, exemptionOptions, authenticatedExemptionScheme: "   ", exemptionPrincipal: principal)
            .Should().BeFalse();
        MfaAuthorization.IsMfaSatisfied(principal, MfaClaimConfig.AspNetIdentity, exemptionOptions, authenticatedExemptionScheme: "CustomApiKey", exemptionPrincipal: null)
            .Should().BeFalse();
    }

    [Theory]
    [DataInlineUnit]
    public async Task MfaExemptionMiddleware_Should_Discover_Exemption_Even_When_Ambient_User_Completed_Mfa(DrnTestContextUnit context)
    {
        const string exemptScheme = "CustomApiKey";
        var exemptionOptions = new MfaExemptionOptions();
        exemptionOptions.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = [exemptScheme] });

        // Primary identity already completed MFA via amr=mfa
        var primaryIdentity = new ClaimsIdentity([
            new Claim(ClaimConventions.NameIdentifier, "user-1"),
            new Claim(ClaimConventions.AuthenticationMethodReference, MfaClaimValues.Amr)
        ], "DefaultCookie");
        var initialPrincipal = new ClaimsPrincipal(primaryIdentity);

        var exemptIdentity = new ClaimsIdentity([
            new Claim("scope", "api")
        ], exemptScheme);
        var exemptPrincipal = new ClaimsPrincipal(exemptIdentity);

        var authService = Substitute.For<IAuthenticationService>();
        authService.AuthenticateAsync(Arg.Any<HttpContext>(), exemptScheme)
            .Returns(AuthenticateResult.Success(new AuthenticationTicket(exemptPrincipal, exemptScheme)));

        context.ServiceCollection.AddSingleton(authService);
        var scopedUser = ScopedUser.FromClaimsPrincipal(initialPrincipal);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);

        // Pre-condition: MfaFor.MfaCompleted is true on the ambient user
        MfaFor.MfaCompleted.Should().BeTrue();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = ScopeContext.Services,
            User = initialPrincipal
        };

        var middleware = new MfaExemptionMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext, scopedUser, exemptionOptions);

        // Exemption discovery must NOT be suppressed by the ambient cookie's completed MFA
        scopedUser.ExemptionScheme.Should().Be(exemptScheme);
        scopedUser.ExemptionPrincipal.Should().BeSameAs(exemptPrincipal);
        scopedUser.HasExemptionScheme.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task MfaExemptionMiddleware_Should_Set_First_Matching_Exempt_Scheme_And_Short_Circuit(DrnTestContextUnit context)
    {
        const string certScheme = "ClientCert";
        const string apiKeyScheme = "CustomApiKey";
        var exemptionOptions = new MfaExemptionOptions();
        exemptionOptions.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = [certScheme, apiKeyScheme] });

        var initialPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        var certPrincipal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("thumbprint", "abc")], certScheme));
        var apiKeyPrincipal = new ClaimsPrincipal(new ClaimsIdentity([new Claim("key", "xyz")], apiKeyScheme));

        var authService = Substitute.For<IAuthenticationService>();
        authService.AuthenticateAsync(Arg.Any<HttpContext>(), certScheme)
            .Returns(AuthenticateResult.Success(new AuthenticationTicket(certPrincipal, certScheme)));
        authService.AuthenticateAsync(Arg.Any<HttpContext>(), apiKeyScheme)
            .Returns(AuthenticateResult.Success(new AuthenticationTicket(apiKeyPrincipal, apiKeyScheme)));

        context.ServiceCollection.AddSingleton(authService);
        var scopedUser = ScopedUser.FromClaimsPrincipal(initialPrincipal);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = ScopeContext.Services,
            User = initialPrincipal
        };

        var middleware = new MfaExemptionMiddleware(_ => Task.CompletedTask);
        await middleware.InvokeAsync(httpContext, scopedUser, exemptionOptions);

        // First matching exempt scheme is recorded and execution short-circuits
        scopedUser.ExemptionScheme.Should().Be(certScheme);
        scopedUser.ExemptionPrincipal.Should().BeSameAs(certPrincipal);
        scopedUser.HasExemptionScheme.Should().BeTrue();
        await authService.DidNotReceive().AuthenticateAsync(Arg.Any<HttpContext>(), apiKeyScheme);
    }

    [Theory]
    [DataInlineUnit]
    public async Task RequireMfaHandler_Should_Reject_When_Non_Exempt_Scheme_Emits_Equivalent_Claims_To_Exempt_Scheme(DrnTestContextUnit context)
    {
        const string exemptScheme = "CustomApiKey";
        var exemptionOptions = new MfaExemptionOptions();
        exemptionOptions.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = [exemptScheme] });
        context.ServiceCollection.AddSingleton(exemptionOptions);

        // Ambient scoped user authenticated via the exempt scheme
        var exemptIdentity = new ClaimsIdentity([
            new Claim(ClaimConventions.NameIdentifier, "shared-user-id"),
            new Claim("scope", "api")
        ], "Bearer");
        var exemptPrincipal = new ClaimsPrincipal(exemptIdentity);

        var scopedUser = ScopedUser.FromClaimsPrincipal(exemptPrincipal);
        scopedUser.SetExemption(exemptScheme, exemptPrincipal);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);

        // Target endpoint evaluated a non-exempt scheme emitting an identical user ID and AuthenticationType
        var nonExemptIdentity = new ClaimsIdentity([
            new Claim(ClaimConventions.NameIdentifier, "shared-user-id"),
            new Claim("scope", "api")
        ], "Bearer");
        var targetPrincipal = new ClaimsPrincipal(nonExemptIdentity);
        var authorizationContext = CreateAuthorizationContext(targetPrincipal);

        await new RequireMfaHandler(exemptionOptions: exemptionOptions).HandleAsync(authorizationContext);

        // Heuristic claim similarity must NOT waive MFA for the non-exempt scheme's identity
        authorizationContext.HasFailed.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task MfaEnforcingAuthorizationMiddlewareResultHandler_Should_Succeed_When_Policy_With_Exempt_Scheme_Has_Transformed_Principal(DrnTestContextUnit context)
    {
        const string exemptScheme = "CustomApiKey";
        var exemptionOptions = new MfaExemptionOptions();
        exemptionOptions.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = [exemptScheme] });
        context.ServiceCollection.AddSingleton(exemptionOptions);

        var exemptIdentity = new ClaimsIdentity([
            new Claim(ClaimConventions.NameIdentifier, "user-1")
        ], exemptScheme);
        var exemptPrincipal = new ClaimsPrincipal(exemptIdentity);

        var scopedUser = ScopedUser.FromClaimsPrincipal(exemptPrincipal);
        scopedUser.SetExemption(exemptScheme, exemptPrincipal);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);

        // Transformed principal with non-reference-equal identity (e.g. from IClaimsTransformation)
        var transformedPrincipal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimConventions.NameIdentifier, "user-1"),
            new Claim("transformed", "true")
        ], exemptScheme));

        var policy = new AuthorizationPolicyBuilder(exemptScheme).RequireAuthenticatedUser().Build();
        var handler = new MfaEnforcingAuthorizationMiddlewareResultHandler(
            Options.Create(new AuthorizationOptions()),
            exemptionOptions: exemptionOptions);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = ScopeContext.Services,
            User = transformedPrincipal
        };

        var nextInvoked = false;
        await handler.HandleAsync(
            _ => { nextInvoked = true; return Task.CompletedTask; },
            httpContext,
            policy,
            PolicyAuthorizationResult.Success());

        nextInvoked.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task RequireMfaHandler_Should_Succeed_When_Exempt_Principal_Was_Transformed_Via_ClaimsTransformation(DrnTestContextUnit context)
    {
        const string exemptScheme = "CustomApiKey";
        var exemptionOptions = new MfaExemptionOptions();
        exemptionOptions.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = [exemptScheme] });
        context.ServiceCollection.AddSingleton(exemptionOptions);

        var exemptIdentity = new ClaimsIdentity([
            new Claim(ClaimConventions.NameIdentifier, "user-1")
        ], exemptScheme);
        var exemptPrincipal = new ClaimsPrincipal(exemptIdentity);

        var scopedUser = ScopedUser.FromClaimsPrincipal(exemptPrincipal);
        scopedUser.SetExemption(exemptScheme, exemptPrincipal);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);

        // Transformed principal evaluated by RequireMfaHandler (simulates IClaimsTransformation creating a new ClaimsIdentity)
        var transformedPrincipal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimConventions.NameIdentifier, "user-1"),
            new Claim("transformed", "true")
        ], exemptScheme));

        var authorizationContext = CreateAuthorizationContext(transformedPrincipal);
        await new RequireMfaHandler(exemptionOptions: exemptionOptions).HandleAsync(authorizationContext);

        authorizationContext.HasSucceeded.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task MfaEnforcingAuthorizationMiddlewareResultHandler_Should_Succeed_When_DefaultScheme_Is_Configured_Without_Explicit_AuthenticateScheme(DrnTestContextUnit context)
    {
        const string exemptScheme = "CustomApiKey";
        var exemptionOptions = new MfaExemptionOptions();
        exemptionOptions.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = [exemptScheme] });
        context.ServiceCollection.AddSingleton(exemptionOptions);
        context.ServiceCollection.AddAuthentication(exemptScheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(exemptScheme, _ => { });

        var exemptIdentity = new ClaimsIdentity([
            new Claim(ClaimConventions.NameIdentifier, "user-1")
        ], exemptScheme);
        var exemptPrincipal = new ClaimsPrincipal(exemptIdentity);

        var scopedUser = ScopedUser.FromClaimsPrincipal(exemptPrincipal);
        scopedUser.SetExemption(exemptScheme, exemptPrincipal);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);

        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        var handler = new MfaEnforcingAuthorizationMiddlewareResultHandler(
            Options.Create(CreateAuthorizationOptions()),
            exemptionOptions: exemptionOptions);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = ScopeContext.Services,
            User = exemptPrincipal
        };

        var nextInvoked = false;
        await handler.HandleAsync(
            _ => { nextInvoked = true; return Task.CompletedTask; },
            httpContext,
            policy,
            PolicyAuthorizationResult.Success());

        nextInvoked.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task MfaEnforcingAuthorizationMiddlewareResultHandler_Should_Reject_When_Policy_Allows_Exempt_Scheme_But_User_Identity_Does_Not_Match_Exempt_Principal(DrnTestContextUnit context)
    {
        const string exemptScheme = "CustomApiKey";
        var exemptionOptions = new MfaExemptionOptions();
        exemptionOptions.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = [exemptScheme] });
        context.ServiceCollection.AddSingleton(exemptionOptions);
        context.ServiceCollection.AddAuthentication(exemptScheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(exemptScheme, _ => { });

        var exemptIdentity = new ClaimsIdentity([
            new Claim(ClaimConventions.NameIdentifier, "user-1")
        ], exemptScheme);
        var exemptPrincipal = new ClaimsPrincipal(exemptIdentity);

        var scopedUser = ScopedUser.FromClaimsPrincipal(exemptPrincipal);
        scopedUser.SetExemption(exemptScheme, exemptPrincipal);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);

        // Mismatched user on the request context
        var differentUserPrincipal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimConventions.NameIdentifier, "different-user")
        ], exemptScheme));

        var policy = new AuthorizationPolicyBuilder(exemptScheme).RequireAuthenticatedUser().Build();
        var handler = new MfaEnforcingAuthorizationMiddlewareResultHandler(
            Options.Create(CreateAuthorizationOptions()),
            exemptionOptions: exemptionOptions);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = ScopeContext.Services,
            User = differentUserPrincipal
        };

        var nextInvoked = false;
        await handler.HandleAsync(
            _ => { nextInvoked = true; return Task.CompletedTask; },
            httpContext,
            policy,
            PolicyAuthorizationResult.Success());

        nextInvoked.Should().BeFalse();
    }

    private static ClaimsPrincipal CreatePrincipal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "Test"));

    private static AuthorizationHandlerContext CreateAuthorizationContext(ClaimsPrincipal principal)
    {
        var requirement = new MfaRequirement();
        return new AuthorizationHandlerContext([requirement], principal, resource: null);
    }

    private static AuthorizationOptions CreateAuthorizationOptions()
    {
        var options = new AuthorizationOptions
        {
            DefaultPolicy = new AuthorizationPolicyBuilder().AddRequirements(new MfaRequirement()).Build()
        };
        options.AddPolicy("scope-read", policy =>
        {
            policy.AuthenticationSchemes.Add("ApiKey");
            policy.RequireClaim("scope", "read");
        });
        options.AddPolicy(AuthPolicy.MfaExempt, policy => policy.AddRequirements(new MfaExemptRequirement()));

        return options;
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
    }
}
