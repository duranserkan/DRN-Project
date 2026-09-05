using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using DRN.Framework.Hosting.Auth;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Auth;

public class MfaAuthorizationResultHandlerTests
{
    [Theory]
    [DataInlineUnit]
    public async Task Failed_Authorization_Should_Be_Delegated(DrnTestContextUnit context)
    {
        context.ServiceCollection.AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        var http = new DefaultHttpContext { RequestServices = context.BuildServiceProvider() };
        var options = EnforcedOptions();
        var handler = new MfaEnforcingAuthorizationMiddlewareResultHandler(Options.Create(options));

        await handler.HandleAsync(_ => Task.CompletedTask, http, options.DefaultPolicy, PolicyAuthorizationResult.Forbid());

        http.Response.StatusCode.Should().Be((int)HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Disabled_Mfa_Should_Continue_Without_Additional_Enforcement()
    {
        var options = new AuthorizationOptions
        {
            DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()
        };
        var handler = new MfaEnforcingAuthorizationMiddlewareResultHandler(Options.Create(options));
        var nextInvoked = false;

        await handler.HandleAsync(_ => { nextInvoked = true; return Task.CompletedTask; }, new DefaultHttpContext(),
            options.DefaultPolicy, PolicyAuthorizationResult.Success());

        nextInvoked.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit("user-1", true)]
    [DataInlineUnit("different-user", false)]
    public async Task Transformed_Identity_Should_Match_The_Exemption_Account(
        DrnTestContextUnit context, string subject, bool expectedSuccess)
    {
        const string scheme = "CustomApiKey";
        context.ServiceCollection.AddAuthentication(scheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(scheme, _ => { });
        var exempt = Principal(scheme, "user-1");
        var scopedUser = ScopedUser.FromClaimsPrincipal(exempt);
        scopedUser.SetExemption(scheme, exempt);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);
        var transformed = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, subject), new Claim("transformed", "true")
        ], scheme));
        var http = new DefaultHttpContext { RequestServices = ScopeContext.Services, User = transformed };
        MfaPolicyProof.Set(http, [new ExemptionProof(scheme, exempt)]);
        var policy = new AuthorizationPolicyBuilder(scheme).RequireAuthenticatedUser().Build();
        var handler = new MfaEnforcingAuthorizationMiddlewareResultHandler(
            Options.Create(EnforcedOptions()), exemptionOptions: Exemptions(scheme));
        var nextInvoked = false;

        await handler.HandleAsync(_ => { nextInvoked = true; return Task.CompletedTask; }, http, policy,
            PolicyAuthorizationResult.Success());

        nextInvoked.Should().Be(expectedSuccess);
        http.Response.StatusCode.Should().Be(expectedSuccess ? (int)HttpStatusCode.OK : (int)HttpStatusCode.Forbidden);
    }

    [Theory]
    [DataInlineUnit]
    public async Task Default_Scheme_Should_Work_Without_Explicit_Authenticate_Scheme(DrnTestContextUnit context)
    {
        const string scheme = "CustomApiKey";
        context.ServiceCollection.AddAuthentication(scheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(scheme, _ => { });
        var principal = Principal(scheme, "user-1");
        var scopedUser = ScopedUser.FromClaimsPrincipal(principal);
        scopedUser.SetExemption(scheme, principal);
        ScopeContext.InitializeForTest(context, scopedUser: scopedUser);
        var http = new DefaultHttpContext { RequestServices = ScopeContext.Services, User = principal };
        MfaPolicyProof.Set(http, [new ExemptionProof(scheme, principal)]);
        var policy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
        var handler = new MfaEnforcingAuthorizationMiddlewareResultHandler(
            Options.Create(EnforcedOptions()), exemptionOptions: Exemptions(scheme));
        var nextInvoked = false;

        await handler.HandleAsync(_ => { nextInvoked = true; return Task.CompletedTask; }, http, policy,
            PolicyAuthorizationResult.Success());

        nextInvoked.Should().BeTrue();
    }

    private static ClaimsPrincipal Principal(string scheme, string subject) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, subject)], scheme));

    private static AuthorizationOptions EnforcedOptions() => new()
    {
        DefaultPolicy = new AuthorizationPolicyBuilder().AddRequirements(new MfaRequirement()).Build()
    };

    private static MfaExemptionOptions Exemptions(string scheme)
    {
        var options = new MfaExemptionOptions();
        options.MapFromConfig(new MfaExemptionConfig { ExemptAuthSchemes = [scheme] });
        return options;
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync() => Task.FromResult(AuthenticateResult.NoResult());
    }
}
