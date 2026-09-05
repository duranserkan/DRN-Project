using System.Security.Claims;
using DRN.Framework.Hosting.Auth;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.Identity;
using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Auth;

public class MfaPolicyBoundaryTests
{
    [Theory]
    [DataInlineUnit("", false)]
    [DataInlineUnit("selected", true)]
    [DataInlineUnit("unselected", false)]
    public async Task Exempt_Policy_Projects_Only_Selected_Proof(string proofScheme, bool expectedExemption)
    {
        var user = Principal("key", "user");
        var scopedUser = ScopedUser.FromClaimsPrincipal(user);
        scopedUser.SetExemption("stale", user);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IScopedLog>(new ScopedLog(AppSettings.Development()));
        services.AddSingleton<IScopedUser>(scopedUser);
        using var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider, User = user };
        if (proofScheme.Length > 0)
            MfaPolicyProof.Set(http, [new ExemptionProof(proofScheme, user)]);

        var exemptions = new MfaExemptionOptions();
        exemptions.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = ["selected", "unselected"] });
        var options = new AuthorizationOptions
        {
            DefaultPolicy = new AuthorizationPolicyBuilder().AddRequirements(new MfaRequirement()).Build()
        };
        var handler = new MfaEnforcingAuthorizationMiddlewareResultHandler(
            Microsoft.Extensions.Options.Options.Create(options), exemptionOptions: exemptions);
        var policy = new AuthorizationPolicyBuilder("selected").AddRequirements(new MfaExemptRequirement()).Build();
        var nextCalled = false;

        await handler.HandleAsync(_ => { nextCalled = true; return Task.CompletedTask; }, http, policy,
            PolicyAuthorizationResult.Success());

        nextCalled.Should().BeTrue();
        scopedUser.Principal.Should().BeSameAs(user);
        scopedUser.HasExemptionScheme.Should().Be(expectedExemption);
        scopedUser.ExemptionScheme.Should().Be(expectedExemption ? "selected" : null);
        MfaPolicyProof.Get(http).Should().HaveCount(expectedExemption ? 1 : 0);
    }

    [Fact]
    public async Task Completed_Mfa_Does_Not_Require_Exemption_Proof()
    {
        var user = Principal("cookie", "user", completed: true);
        var scopedUser = ScopedUser.FromClaimsPrincipal(user);
        var services = new ServiceCollection();
        services.AddSingleton<IScopedUser>(scopedUser);
        using var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider, User = user };
        var policy = new AuthorizationPolicyBuilder().AddRequirements(new MfaRequirement()).Build();
        var handler = new MfaEnforcingAuthorizationMiddlewareResultHandler(
            Microsoft.Extensions.Options.Options.Create(new AuthorizationOptions { DefaultPolicy = policy }));
        var nextCalled = false;

        await handler.HandleAsync(_ => { nextCalled = true; return Task.CompletedTask; }, http, policy,
            PolicyAuthorizationResult.Success());

        nextCalled.Should().BeTrue();
        scopedUser.HasExemptionScheme.Should().BeFalse();
    }

    [Theory]
    [DataInlineUnit(IdentityMfaPolicy.Enrollment)]
    [DataInlineUnit(IdentityMfaPolicy.BrowserEnrollment)]
    [DataInlineUnit(IdentityMfaPolicy.Challenge)]
    public async Task Local_Identity_Policies_Do_Not_Accept_An_Ambient_Api_Key(string policyName)
    {
        var services = new ServiceCollection();
        services.AddDrnIdentityMfaPolicies();
        var auth = Substitute.For<IAuthenticationService>();
        auth.AuthenticateAsync(Arg.Any<HttpContext>(), Arg.Any<string>()).Returns(AuthenticateResult.NoResult());
        services.AddSingleton(auth);
        using var provider = services.BuildServiceProvider();
        var policy = await provider.GetRequiredService<IAuthorizationPolicyProvider>().GetPolicyAsync(policyName);
        var http = new DefaultHttpContext { RequestServices = provider, User = Principal("api-key", "user", completed: true) };
        var evaluator = new PolicyEvaluator(Substitute.For<IAuthorizationService>());
        var result = await evaluator.AuthenticateAsync(policy!, http);
        result.Succeeded.Should().BeFalse();
        http.User.Identities.Should().OnlyContain(identity => !identity.IsAuthenticated);
        await auth.DidNotReceive().AuthenticateAsync(Arg.Any<HttpContext>(), "api-key");
    }

    [Fact]
    public async Task Default_Forwarding_Scheme_Retains_Selected_And_Concrete_Proof()
    {
        var options = new MfaExemptionOptions();
        options.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = ["key"] });
        var selected = Principal("handler-auth-type", "user");
        var auth = Substitute.For<IAuthenticationService>();
        auth.AuthenticateAsync(Arg.Any<HttpContext>(), "forward")
            .Returns(AuthenticateResult.Success(new AuthenticationTicket(selected, "key")));
        var schemes = Substitute.For<IAuthenticationSchemeProvider>();
        schemes.GetDefaultAuthenticateSchemeAsync().Returns(new AuthenticationScheme("forward", null, typeof(AuthenticationHandler<AuthenticationSchemeOptions>)));
        var services = new ServiceCollection();
        services.AddAuthorization();
        services.AddSingleton(auth).AddSingleton(schemes);
        using var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider, User = selected };
        Select(http, new AuthorizationPolicyBuilder().AddRequirements(new MfaRequirement()).Build());
        await new MfaExemptionMiddleware(_ => Task.CompletedTask).InvokeAsync(http, ScopedUser.FromClaimsPrincipal(selected), options);
        MfaPolicyProof.IsSatisfied(http, selected, AuthenticationClaimConfig.Default, options).Should().BeTrue();
        await auth.DidNotReceive().AuthenticateAsync(Arg.Any<HttpContext>(), "key");
        MfaPolicyProof.Get(http).Single().SelectedScheme.Should().Be("forward");
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public async Task Only_Selected_Key_Can_Satisfy_Mfa_Regardless_Of_Allowlist_Order(bool reverse)
    {
        var options = new MfaExemptionOptions();
        options.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = reverse ? ["second", "first"] : ["first", "second"] });
        var auth = Substitute.For<IAuthenticationService>();
        var selected = Principal("second", "selected-user");
        auth.AuthenticateAsync(Arg.Any<HttpContext>(), "second")
            .Returns(AuthenticateResult.Success(new AuthenticationTicket(selected, "second")));
        auth.AuthenticateAsync(Arg.Any<HttpContext>(), "first")
            .Returns(AuthenticateResult.Success(new AuthenticationTicket(Principal("first", "other-user"), "first")));
        var services = new ServiceCollection();
        services.AddAuthorization();
        services.AddSingleton(auth);
        using var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider, User = Principal("cookie", "other-user", completed: true) };
        Select(http, new AuthorizationPolicyBuilder("second").AddRequirements(new MfaRequirement()).Build());
        await new MfaExemptionMiddleware(_ => Task.CompletedTask).InvokeAsync(http, ScopedUser.FromClaimsPrincipal(http.User), options);
        await auth.DidNotReceive().AuthenticateAsync(Arg.Any<HttpContext>(), "first");

        var evaluation = new AuthorizationHandlerContext([new MfaRequirement()], selected, http);
        await new RequireMfaHandler(exemptionOptions: options).HandleAsync(evaluation);
        evaluation.HasSucceeded.Should().BeTrue();
        MfaPolicyProof.IsSatisfied(http, Principal("second", "different-user"), AuthenticationClaimConfig.Default, options).Should().BeFalse();
    }

    [Theory]
    [DataInlineUnit]
    public async Task Programmatic_Authorization_Does_Not_Use_Ambient_Exemption(DrnTestContextUnit context)
    {
        var user = Principal("key", "user");
        var scopedUser = ScopedUser.FromClaimsPrincipal(user);
        scopedUser.SetExemption("key", user);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);
        var evaluation = new AuthorizationHandlerContext([new MfaRequirement()], user, null);
        var options = new MfaExemptionOptions();
        options.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = ["key"] });
        await new RequireMfaHandler(exemptionOptions: options).HandleAsync(evaluation);
        evaluation.HasFailed.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit(MfaClaimValues.MfaSetupRequired)]
    [DataInlineUnit(MfaClaimValues.MfaInProgress)]
    public void Restricted_Credential_Cannot_Be_Promoted_By_Completed_Claim(string state)
    {
        var user = Principal("cookie", "user", completed: true);
        ((ClaimsIdentity)user.Identity!).AddClaim(new Claim(ClaimConventions.AuthenticationMethod, state));
        MfaPrincipal.IsCompleted(user, AuthenticationClaimConfig.Default).Should().BeFalse();
        IdentityMfaPolicy.CanManage(user, true, true, AuthenticationClaimConfig.Default).Should().BeFalse();
    }

    [Theory]
    [DataInlineUnit("other-user", "issuer")]
    [DataInlineUnit("user", "other-issuer")]
    public void Another_Subject_Or_Issuer_Cannot_Supply_Mfa(string subject, string issuer)
    {
        var user = Principal("external", "user");
        var other = Principal("external", subject, completed: true, issuer: issuer);
        user.AddIdentity((ClaimsIdentity)other.Identity!);
        MfaPrincipal.IsCompleted(user, AuthenticationClaimConfig.Default).Should().BeFalse();
        IdentityMfaPolicy.CanManage(user, true, true, AuthenticationClaimConfig.Default).Should().BeFalse();
    }

    [Fact]
    public void Transformed_Proof_Requires_Subject_And_Issuer()
    {
        MfaPrincipal.MatchesIdentity(new ClaimsIdentity("key"), new ClaimsIdentity("key")).Should().BeFalse();
        var user = Principal("key", "user");
        var equivalent = Principal("key", "user");
        MfaPrincipal.MatchesIdentity((ClaimsIdentity)user.Identity!, (ClaimsIdentity)equivalent.Identity!).Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit(false, false, false, true)]
    [DataInlineUnit(false, true, false, false)]
    [DataInlineUnit(false, true, true, true)]
    [DataInlineUnit(true, false, false, false)]
    [DataInlineUnit(true, true, true, false)]
    public void Enrollment_Depends_On_Account_State(bool enabled, bool enforced, bool setup, bool allowed)
    {
        var user = Principal("cookie", "user");
        if (setup)
            ((ClaimsIdentity)user.Identity!).AddClaim(new Claim(ClaimConventions.AuthenticationMethod, MfaClaimValues.MfaSetupRequired));
        IdentityMfaPolicy.CanManage(user, enabled, enforced, AuthenticationClaimConfig.Default).Should().Be(allowed);
    }

    [Fact]
    public async Task External_Mfa_Configuration_Is_Independent_Of_Identity_Default()
    {
        var external = new AuthenticationClaimConfig { Mfa = new("acr", "test:strong") };
        var user = Principal("external", "user");
        ((ClaimsIdentity)user.Identity!).AddClaim(new Claim(external.Mfa.ClaimType, external.Mfa.ClaimValue));
        var externalContext = new AuthorizationHandlerContext([new MfaRequirement()], user, null);
        var identityContext = new AuthorizationHandlerContext([new MfaRequirement()], user, null);
        await new RequireMfaHandler(external).HandleAsync(externalContext);
        await new RequireMfaHandler().HandleAsync(identityContext);
        externalContext.HasSucceeded.Should().BeTrue();
        identityContext.HasFailed.Should().BeTrue();
    }

    [Fact]
    public async Task Effective_Policy_Preserves_Fallback_Roles_Direct_And_Requirement_Metadata()
    {
        var services = new ServiceCollection();
        services.AddAuthorization(options =>
        {
            options.DefaultPolicy = new AuthorizationPolicyBuilder("cookie").AddRequirements(new MfaRequirement()).Build();
            options.FallbackPolicy = options.DefaultPolicy;
            options.AddPolicy("key", new AuthorizationPolicyBuilder("key").RequireClaim("scope", "read").Build());
        });
        using var provider = services.BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider };
        (await MfaPolicyProof.ResolvePolicyAsync(http))!.AuthenticationSchemes.Should().Equal("cookie");

        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizeAttribute { Roles = "manager" }, new ExtraRequirement()), "role"));
        var role = await MfaPolicyProof.ResolvePolicyAsync(http);
        role!.Requirements.Should().Contain(requirement => requirement is Microsoft.AspNetCore.Authorization.Infrastructure.RolesAuthorizationRequirement);
        role.Requirements.Should().NotContain(requirement => requirement is MfaRequirement);
        role.AuthenticationSchemes.Should().BeEmpty();
        role.Requirements.Should().Contain(requirement => requirement is ExtraRequirement.Check);

        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizeAttribute()), "default"));
        var defaultPolicy = await MfaPolicyProof.ResolvePolicyAsync(http);
        defaultPolicy!.Requirements.Should().Contain(requirement => requirement is MfaRequirement);
        defaultPolicy.AuthenticationSchemes.Should().Equal("cookie");

        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new AuthorizeAttribute("key")), "named"));
        (await MfaPolicyProof.ResolvePolicyAsync(http))!.AuthenticationSchemes.Should().Equal("key");
        Select(http, new AuthorizationPolicyBuilder("direct").RequireAuthenticatedUser().Build());
        (await MfaPolicyProof.ResolvePolicyAsync(http))!.AuthenticationSchemes.Should().Equal("direct");

        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowAnonymousAttribute()), "anonymous"));
        (await MfaPolicyProof.ResolvePolicyAsync(http)).Should().BeNull();
    }

    private sealed class ExtraRequirement : IAuthorizationRequirementData
    {
        internal sealed class Check : IAuthorizationRequirement;
        public IEnumerable<IAuthorizationRequirement> GetRequirements() => [new Check()];
    }

    private static ClaimsPrincipal Principal(string scheme, string subject, bool completed = false, string issuer = "issuer")
    {
        var identity = new ClaimsIdentity([new Claim("sub", subject, ClaimValueTypes.String, issuer)], scheme);
        if (completed)
            identity.AddClaim(new Claim("amr", "mfa"));
        return new ClaimsPrincipal(identity);
    }

    private static void Select(HttpContext http, AuthorizationPolicy policy) =>
        http.SetEndpoint(new Endpoint(_ => Task.CompletedTask, new EndpointMetadataCollection(policy), "selected"));
}
