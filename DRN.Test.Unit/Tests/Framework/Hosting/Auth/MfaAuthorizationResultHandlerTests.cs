using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using DRN.Framework.Hosting.Auth;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Logging;
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
    [DataInlineUnit("challenge", 7401, "authorization", 401)]
    [DataInlineUnit("forbid", 7402, "authorization", 403)]
    [DataInlineUnit("failed-mfa", 7402, "mfa_requirement", 403)]
    [DataInlineUnit("missing-mfa", 7402, "mfa_required", 403)]
    [DataInlineUnit("anonymous", 7401, "mfa_required", 401)]
    [DataInlineUnit("policy", 7403, "policy_exemption", 200)]
    [DataInlineUnit("scheme", 7403, "scheme_exemption", 200)]
    [DataInlineUnit("unselected", 7402, "mfa_required", 403)]
    [DataInlineUnit("policy-denial", 7402, "authorization", 403)]
    [DataInlineUnit("completed", 0, "", 200)]
    [DataInlineUnit("disabled", 0, "", 200)]
    public async Task Audit_Should_Describe_Actual_Decision_Without_Sensitive_Data(
        DrnTestContextUnit context, string scenario, int eventId, string reason, int status)
    {
        using var activity = new Activity("audit-test").SetIdFormat(ActivityIdFormat.W3C).Start();
        var logger = new AuditLogger();
        var scopedLog = new ScopedLog(AppSettings.Development());
        context.ServiceCollection.AddSingleton<IScopedLog>(scopedLog);
        context.ServiceCollection.AddSingleton<ILogger<MfaEnforcingAuthorizationMiddlewareResultHandler>>(logger);
        context.ServiceCollection.AddAuthentication("Test")
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
        var user = scenario == "anonymous" ? new ClaimsPrincipal(new ClaimsIdentity()) : Principal("Test", "secret-user");
        ((ClaimsIdentity)user.Identity!).AddClaim(new Claim("token", "secret-token"));
        if (scenario == "completed")
            ((ClaimsIdentity)user.Identity!).AddClaim(new Claim("amr", "mfa"));
        var http = new DefaultHttpContext
        {
            RequestServices = context.BuildServiceProvider(), User = user, TraceIdentifier = "request-correlation",
            Request =
            {
                QueryString = new QueryString("?code=secret-code")
            }
        };
        var policy = scenario is "policy" or "policy-denial"
            ? new AuthorizationPolicyBuilder("Test").AddRequirements(new MfaExemptRequirement()).Build()
            : new AuthorizationPolicyBuilder("Test").RequireAuthenticatedUser().Build();
        if (scenario == "scheme")
            MfaPolicyProof.Set(http, [new ExemptionProof("Test", user)]);
        if (scenario == "unselected")
            MfaPolicyProof.Set(http, [new ExemptionProof("Other", user)]);
        var options = scenario == "disabled" ? new AuthorizationOptions { DefaultPolicy = policy } : EnforcedOptions();
        var handler = new MfaEnforcingAuthorizationMiddlewareResultHandler(Options.Create(options), exemptionOptions: Exemptions("Test"));
        var result = scenario switch
        {
            "challenge" => PolicyAuthorizationResult.Challenge(),
            "forbid" or "policy-denial" => PolicyAuthorizationResult.Forbid(),
            _ => PolicyAuthorizationResult.Success()
        };
        if (scenario == "failed-mfa")
        {
            var evaluation = new AuthorizationHandlerContext([new MfaRequirement()], user, http);
            await new RequireMfaHandler().HandleAsync(evaluation);
            evaluation.HasFailed.Should().BeTrue();
            result = PolicyAuthorizationResult.Forbid(AuthorizationFailure.Failed(evaluation.FailureReasons));
        }
        var nextCalled = false;

        await handler.HandleAsync(_ => { nextCalled = true; return Task.CompletedTask; }, http, policy, result);

        http.Response.StatusCode.Should().Be(status);
        nextCalled.Should().Be(status == 200);
        if (eventId == 0)
        {
            logger.Events.Should().BeEmpty();
            scopedLog.Event.Should().BeNull();
            return;
        }

        var logged = logger.Events.Should().ContainSingle().Which;
        logged.Id.Id.Should().Be(eventId);
        logged.Id.Name.Should().Be(status == 401 ? "MfaAuthorizationChallenge"
            : status == 403 ? "MfaAuthorizationForbid" : "MfaAuthorizationExemption");
        logged.Level.Should().Be(LogLevel.Information);
        logged.Fields.Keys.Should().BeEquivalentTo("EventOutcome", "EventReason", "TraceId", "CorrelationId", "{OriginalFormat}");
        logged.Fields["EventReason"].Should().Be(reason);
        logged.Fields["EventOutcome"].Should().Be(status == 401 ? "challenge" : status == 403 ? "forbid" : "exemption");
        logged.Fields["TraceId"].Should().Be(activity.TraceId.ToString());
        logged.Fields["CorrelationId"].Should().Be(scopedLog.CorrelationId);
        string.Join(" ", logged.Fields.Values).Should().NotContain("secret-");
        scopedLog.EventId.Should().Be(eventId);
        scopedLog.EventName.Should().Be(logged.Id.Name);
        scopedLog.EventOutcome.Should().Be((string)logged.Fields["EventOutcome"]!);
        scopedLog.EventReason.Should().Be(reason);
        scopedLog.TraceId.Should().Be(activity.TraceId.ToString());
        scopedLog.Event!.ToString().Should().NotContain("secret-");
    }

    [Theory]
    [DataInlineUnit]
    public async Task Untraced_Audit_Should_Use_Correlation_Without_A_TraceId(DrnTestContextUnit context)
    {
        var previous = Activity.Current;
        try
        {
            Activity.Current = null;
            var logger = new AuditLogger();
            var scopedLog = new ScopedLog(AppSettings.Development());
            context.ServiceCollection.AddSingleton<IScopedLog>(scopedLog);
            context.ServiceCollection.AddSingleton<ILogger<MfaEnforcingAuthorizationMiddlewareResultHandler>>(logger);
            context.ServiceCollection.AddAuthentication("Test")
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>("Test", _ => { });
            var http = new DefaultHttpContext
            {
                RequestServices = context.BuildServiceProvider(), TraceIdentifier = "http-request"
            };
            var options = EnforcedOptions();
            var handler = new MfaEnforcingAuthorizationMiddlewareResultHandler(Options.Create(options));

            await handler.HandleAsync(_ => Task.CompletedTask, http, options.DefaultPolicy, PolicyAuthorizationResult.Forbid());

            var logged = logger.Events.Should().ContainSingle().Which;
            logged.Fields["TraceId"].Should().BeNull();
            logged.Fields["CorrelationId"].Should().Be(scopedLog.CorrelationId);
            scopedLog.GetLogs().Should().NotContainKey("TraceId");
        }
        finally
        {
            Activity.Current = previous;
        }
    }

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

    [Theory]
    [DataInlineUnit]
    public async Task Disabled_Mfa_Should_Continue_Without_Additional_Enforcement(DrnTestContextUnit context)
    {
        var options = new AuthorizationOptions
        {
            DefaultPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()
        };
        var handler = new MfaEnforcingAuthorizationMiddlewareResultHandler(Options.Create(options));
        var nextInvoked = false;
        var http = new DefaultHttpContext { RequestServices = context.BuildServiceProvider() };

        await handler.HandleAsync(_ => { nextInvoked = true; return Task.CompletedTask; }, http,
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

    private sealed class AuditLogger : ILogger<MfaEnforcingAuthorizationMiddlewareResultHandler>
    {
        internal List<(LogLevel Level, EventId Id, Dictionary<string, object?> Fields)> Events { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Events.Add((logLevel, eventId, ((IEnumerable<KeyValuePair<string, object?>>)state!).ToDictionary(pair => pair.Key, pair => pair.Value)));
    }
}
