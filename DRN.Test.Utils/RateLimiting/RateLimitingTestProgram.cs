using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.RateLimiting;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Hosting.RateLimiting;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.DependencyInjection;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace DRN.Test.Utils.RateLimiting;

public sealed class RateLimitingTestProgram : DrnProgramBase<RateLimitingTestProgram>, IDrnProgram
{
    public static async Task Main(string[] args) => await RunAsync(args);

    protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog)
    {
        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = TestAuthDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = TestAuthDefaults.AuthenticationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, RateLimitingTestAuthHandler>(
                TestAuthDefaults.AuthenticationScheme,
                _ => { });

        builder.Services.AddRateLimiter(options =>
        {
            options.OnRejected = (context, _) =>
            {
                context.HttpContext.Response.Headers[TestHeaders.ConfiguredOnRejected] = TestHeaderValues.True;
                return ValueTask.CompletedTask;
            };
        });

        builder.Services.AddServicesWithAttributes();

        return Task.CompletedTask;
    }

    protected override void ConfigureSwaggerOptions(DrnProgramSwaggerOptions options, IAppSettings appSettings)
    {
        base.ConfigureSwaggerOptions(options, appSettings);
        options.AddSwagger = false;
    }

    protected override void ConfigurePostAuthRateLimiterOptions(
        RateLimiterOptions options,
        IServiceProvider serviceProvider,
        IAppSettings appSettings)
    {
        base.ConfigurePostAuthRateLimiterOptions(options, serviceProvider, appSettings);
        options.AddTokenBucketLimiter(TestPolicies.NativeStrict, opt => TestRateLimitBuckets.Configure(opt, tokenLimit: 1));
        options.AddTokenBucketLimiter(TestPolicies.NativeRelaxed, opt => TestRateLimitBuckets.Configure(opt, tokenLimit: 100));
        options.AddTokenBucketLimiter(TestPolicies.DrnRuleStrict, opt => TestRateLimitBuckets.Configure(opt, tokenLimit: 100));
        options.AddTokenBucketLimiter(TestPolicies.OtherDrnRule, opt => TestRateLimitBuckets.Configure(opt, tokenLimit: 100));
    }

    protected override void MapApplicationEndpoints(WebApplication application, IAppSettings appSettings)
    {
        base.MapApplicationEndpoints(application, appSettings);

        application.MapGet(TestPaths.Anonymous, () => Results.Ok("anonymous"))
            .AllowAnonymous();

        application.MapGet(TestPaths.Authenticated, (HttpContext httpContext) =>
                Results.Ok(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value))
            .RequireAuthorization();

        application.MapGet(TestPaths.Chained, (HttpContext httpContext) =>
                Results.Ok(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value))
            .RequireAuthorization();

        application.MapGet(TestPaths.CustomPreAuth, () => Results.Ok("custom-pre-auth"))
            .AllowAnonymous();

        application.MapGet(TestPaths.PreAuthChain, () => Results.Ok("pre-auth-chain"))
            .AllowAnonymous();

        application.MapGet(TestPaths.Disabled, () => Results.Ok("disabled"))
            .AllowAnonymous()
            .DisableRateLimiting();

        application.MapGet(TestPaths.AllowRule, () => Results.Ok("allow-rule"))
            .AllowAnonymous();

        application.MapGet(TestPaths.StopRemainingRule, () => Results.Ok("stop-remaining-rule"))
            .AllowAnonymous();

        application.MapGet(TestPaths.ShortCircuitRule, () => Results.Ok("short-circuit-rule"))
            .AllowAnonymous();

        application.MapGet(TestPaths.PreAuthRejected, () => Results.Ok("pre-auth-rejected"))
            .AllowAnonymous();

        application.MapGet(TestPaths.PostAuthRejected, () => Results.Ok("post-auth-rejected"))
            .RequireAuthorization();

        application.MapGet(TestPaths.FixedWindow, () => Results.Ok("fixed-window"))
            .AllowAnonymous();

        application.MapGet(TestPaths.SlidingWindow, () => Results.Ok("sliding-window"))
            .AllowAnonymous();

        application.MapGet(TestPaths.CustomPartition, () => Results.Ok("custom-partition"))
            .AllowAnonymous();

        application.MapGet(TestPaths.ConcurrencyLimiter, async (HttpContext httpContext) =>
            {
                var partition = TestRequestValues.GetPartition(httpContext);
                if (!RateLimitConcurrencyCoordinator.TryGet(partition, out var probe))
                    return Results.Ok("concurrency-limiter");

                probe!.MarkEntered();
                await probe.WaitForReleaseAsync(httpContext.RequestAborted);
                return Results.Ok("concurrency-limiter");
            })
            .AllowAnonymous();

        application.MapGet(TestPaths.NativePolicy, () => Results.Ok("native-policy"))
            .AllowAnonymous()
            .RequireRateLimiting(TestPolicies.NativeStrict);

        application.MapGet(TestPaths.NativeRelaxedPolicy, () => Results.Ok("native-relaxed-policy"))
            .AllowAnonymous()
            .RequireRateLimiting(TestPolicies.NativeRelaxed);

        application.MapGet(TestPaths.DrnPolicy, () => Results.Ok("drn-policy"))
            .AllowAnonymous()
            .RequireRateLimiting(TestPolicies.DrnRuleStrict);

        application.MapGet(TestPaths.OtherDrnPolicy, () => Results.Ok("other-drn-policy"))
            .AllowAnonymous()
            .RequireRateLimiting(TestPolicies.OtherDrnRule);
    }
}

internal sealed class RateLimitingTestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(TestHeaders.UserId, out var userIdValues))
            return Task.FromResult(AuthenticateResult.NoResult());

        var userId = userIdValues.ToString();
        if (string.IsNullOrWhiteSpace(userId))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Name, userId),
            new(ClaimConventions.AuthenticationMethodReference, MfaClaimValues.Amr)
        };

        if (Request.Headers.TryGetValue(TestHeaders.TenantId, out var tenantIdValues))
        {
            var tenantId = tenantIdValues.ToString();
            if (!string.IsNullOrWhiteSpace(tenantId))
                claims.Add(new Claim(TestClaims.TenantId, tenantId));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name, ClaimTypes.Name, ClaimTypes.Role);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

//Todo test scoped rules
//Todo improve allow, deny and ShortCircuitOnMatch semantics

public sealed class HeaderPartitionPreAuthRateLimitRule : SingletonRateLimitRule
{
    public override int Order => -100;

    public override RateLimitRuleResult? EvaluatePreAuth(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(TestPaths.CustomPreAuth))
            return null;

        var partition = context.Request.Headers.TryGetValue(TestHeaders.PreAuthPartition, out var values)
            ? values.ToString()
            : "default";

        return RateLimitRuleResult.TokenBucket(
            $"pre-auth-header:{partition}",
            _ => TestRateLimitBuckets.Create(tokenLimit: 1));
    }
}

public sealed class ChainedPreAuthRateLimitRule : SingletonRateLimitRule
{
    public override int Order => -100;

    public override RateLimitRuleResult? EvaluatePreAuth(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(TestPaths.PreAuthChain))
            return null;

        var partition = TestRequestValues.GetPartition(context);
        return RateLimitRuleResult.TokenBucket(
            $"pre-auth-chain:{partition}",
            _ => TestRateLimitBuckets.Create(tokenLimit: 1));
    }
}

public sealed class TenantPostAuthRateLimitRule(IScopedUser scopedUser) : ScopedRateLimitRule
{
    public override int Order => -100;

    public override RateLimitRuleResult? EvaluatePostAuth(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(TestPaths.Chained))
            return null;

        var tenantId = scopedUser.GetClaimValue(TestClaims.TenantId);
        if (string.IsNullOrWhiteSpace(tenantId))
            return null;

        return RateLimitRuleResult.TokenBucket(
            $"tenant:{tenantId}",
            _ => TestRateLimitBuckets.Create(tokenLimit: 1));
    }
}

public sealed class AllowRouteRateLimitRule : SingletonRateLimitRule
{
    public override int Order => -200;
    public override bool ShortCircuitOnMatch => true;

    public override RateLimitRuleResult? EvaluatePreAuth(HttpContext context) => IsAllowRoute(context)
        ? RateLimitRuleResult.AllowRequest("allow-route")
        : null;

    public override RateLimitRuleResult? EvaluatePostAuth(HttpContext context) => IsAllowRoute(context)
        ? RateLimitRuleResult.AllowRequest("allow-route")
        : null;

    private static bool IsAllowRoute(HttpContext context) =>
        context.Request.Path.StartsWithSegments(TestPaths.AllowRule);
}

public sealed class StopRemainingPreAuthRateLimitRule : SingletonRateLimitRule
{
    public override int Order => -200;

    public override RateLimitRuleResult? EvaluatePreAuth(HttpContext context) =>
        IsStopRemainingRoute(context)
            ? CreateResult()
            : null;

    public override RateLimitRuleResult? EvaluatePostAuth(HttpContext context) =>
        IsStopRemainingRoute(context)
            ? CreateResult()
            : null;

    private static RateLimitRuleResult CreateResult() =>
        RateLimitRuleResult.CustomPartition(
            "stop-remaining-rule",
            RateLimitPartition.GetNoLimiter("stop-remaining-rule"),
            stopRemainingRules: true);

    private static bool IsStopRemainingRoute(HttpContext context) =>
        context.Request.Path.StartsWithSegments(TestPaths.StopRemainingRule);
}

public sealed class ShortCircuitPreAuthRateLimitRule : SingletonRateLimitRule
{
    public override int Order => -200;
    public override bool ShortCircuitOnMatch => true;

    public override RateLimitRuleResult? EvaluatePreAuth(HttpContext context) =>
        IsShortCircuitRoute(context)
            ? CreateResult()
            : null;

    public override RateLimitRuleResult? EvaluatePostAuth(HttpContext context) =>
        IsShortCircuitRoute(context)
            ? CreateResult()
            : null;

    private static RateLimitRuleResult CreateResult() =>
        RateLimitRuleResult.CustomPartition(
            "short-circuit-rule",
            RateLimitPartition.GetNoLimiter("short-circuit-rule"));

    private static bool IsShortCircuitRoute(HttpContext context) =>
        context.Request.Path.StartsWithSegments(TestPaths.ShortCircuitRule);
}

public sealed class PreAuthRejectedHeaderRateLimitRule : SingletonRateLimitRule
{
    public override int Order => -100;

    public override RateLimitRuleResult? EvaluatePreAuth(HttpContext context) =>
        context.Request.Path.StartsWithSegments(TestPaths.PreAuthRejected)
            ? RateLimitRuleResult.TokenBucket("pre-auth-rejected", _ => TestRateLimitBuckets.Create(tokenLimit: 1))
            : null;

    public override Task OnRejectedAsync(HttpContext context, RateLimitLease lease, CancellationToken cancellationToken = default)
    {
        context.Response.Headers[TestHeaders.RuleOnRejected] = TestHeaderValues.PreAuth;
        return Task.CompletedTask;
    }
}

public sealed class PostAuthRejectedHeaderRateLimitRule : SingletonRateLimitRule
{
    public override int Order => -100;

    public override RateLimitRuleResult? EvaluatePostAuth(HttpContext context) =>
        context.Request.Path.StartsWithSegments(TestPaths.PostAuthRejected)
            ? RateLimitRuleResult.TokenBucket("post-auth-rejected", _ => TestRateLimitBuckets.Create(tokenLimit: 1))
            : null;

    public override Task OnRejectedAsync(HttpContext context, RateLimitLease lease, CancellationToken cancellationToken = default)
    {
        context.Response.Headers[TestHeaders.RuleOnRejected] = TestHeaderValues.PostAuth;
        return Task.CompletedTask;
    }
}

public sealed class DrnPolicyPostAuthRateLimitRule : SingletonRateLimitRule
{
    public override int Order => -100;
    public override string PolicyName => TestPolicies.DrnRuleStrict;

    public override RateLimitRuleResult EvaluatePostAuth(HttpContext context) =>
        RateLimitRuleResult.TokenBucket("drn-policy-rule", _ => TestRateLimitBuckets.Create(tokenLimit: 1));
}

public sealed class FixedWindowRateLimitRule : SingletonRateLimitRule
{
    public override int Order => -100;

    public override RateLimitRuleResult? EvaluatePreAuth(HttpContext context) =>
        context.Request.Path.StartsWithSegments(TestPaths.FixedWindow)
            ? RateLimitRuleResult.FixedWindow("fixed-window", _ => TestRateLimitBuckets.CreateFixedWindow(permitLimit: 1))
            : null;
}

public sealed class SlidingWindowRateLimitRule : SingletonRateLimitRule
{
    public override int Order => -100;

    public override RateLimitRuleResult? EvaluatePreAuth(HttpContext context) =>
        context.Request.Path.StartsWithSegments(TestPaths.SlidingWindow)
            ? RateLimitRuleResult.SlidingWindow("sliding-window", _ => TestRateLimitBuckets.CreateSlidingWindow(permitLimit: 1))
            : null;
}

public sealed class CustomPartitionRateLimitRule : SingletonRateLimitRule
{
    public override int Order => -100;

    public override RateLimitRuleResult? EvaluatePreAuth(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(TestPaths.CustomPartition))
            return null;

        const string partitionKey = "custom-partition";
        var partition = RateLimitPartition.GetTokenBucketLimiter(partitionKey, _ => TestRateLimitBuckets.Create(tokenLimit: 1));
        return RateLimitRuleResult.CustomPartition(partitionKey, partition);
    }
}

public sealed class ConcurrencyRateLimitRule : SingletonRateLimitRule
{
    public override int Order => -100;

    public override RateLimitRuleResult? EvaluatePreAuth(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments(TestPaths.ConcurrencyLimiter))
            return null;

        var partition = TestRequestValues.GetPartition(context);
        return RateLimitRuleResult.ConcurrencyLimiter(
            $"concurrency:{partition}",
            _ => TestRateLimitBuckets.CreateConcurrency(permitLimit: 1));
    }
}

internal static class TestRateLimitBuckets
{
    public static TokenBucketRateLimiterOptions Create(int tokenLimit)
    {
        var options = new TokenBucketRateLimiterOptions();
        Configure(options, tokenLimit);
        return options;
    }

    public static void Configure(TokenBucketRateLimiterOptions options, int tokenLimit)
    {
        options.TokenLimit = tokenLimit;
        options.ReplenishmentPeriod = TimeSpan.FromSeconds(60);
        options.TokensPerPeriod = tokenLimit;
        options.QueueLimit = 0;
        options.AutoReplenishment = true;
    }

    public static FixedWindowRateLimiterOptions CreateFixedWindow(int permitLimit) => new()
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromSeconds(60),
        QueueLimit = 0,
        AutoReplenishment = true
    };

    public static SlidingWindowRateLimiterOptions CreateSlidingWindow(int permitLimit) => new()
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromSeconds(60),
        SegmentsPerWindow = 2,
        QueueLimit = 0,
        AutoReplenishment = true
    };

    public static ConcurrencyLimiterOptions CreateConcurrency(int permitLimit) => new()
    {
        PermitLimit = permitLimit,
        QueueLimit = 0
    };
}

internal static class TestRequestValues
{
    public static string GetPartition(HttpContext context) =>
        context.Request.Headers.TryGetValue(TestHeaders.PreAuthPartition, out var values) && !string.IsNullOrWhiteSpace(values.ToString())
            ? values.ToString()
            : "default";
}

public static class TestPaths
{
    public const string Anonymous = "/rate-limit/anonymous";
    public const string Authenticated = "/rate-limit/authenticated";
    public const string Chained = "/rate-limit/chained";
    public const string CustomPreAuth = "/rate-limit/custom-pre-auth";
    public const string PreAuthChain = "/rate-limit/pre-auth-chain";
    public const string Disabled = "/rate-limit/disabled";
    public const string AllowRule = "/rate-limit/allow-rule";
    public const string StopRemainingRule = "/rate-limit/stop-remaining-rule";
    public const string ShortCircuitRule = "/rate-limit/short-circuit-rule";
    public const string PreAuthRejected = "/rate-limit/pre-auth-rejected";
    public const string PostAuthRejected = "/rate-limit/post-auth-rejected";
    public const string FixedWindow = "/rate-limit/fixed-window";
    public const string SlidingWindow = "/rate-limit/sliding-window";
    public const string CustomPartition = "/rate-limit/custom-partition";
    public const string ConcurrencyLimiter = "/rate-limit/concurrency-limiter";
    public const string NativePolicy = "/rate-limit/native-policy";
    public const string NativeRelaxedPolicy = "/rate-limit/native-relaxed-policy";
    public const string DrnPolicy = "/rate-limit/drn-policy";
    public const string OtherDrnPolicy = "/rate-limit/other-drn-policy";
}

public static class TestHeaders
{
    public const string UserId = "X-Test-UserId";
    public const string TenantId = "X-Test-TenantId";
    public const string PreAuthPartition = "X-Test-PreAuth-Partition";
    public const string RuleOnRejected = "X-Test-RateLimit-Rule-OnRejected";
    public const string ConfiguredOnRejected = "X-Test-RateLimit-Configured-OnRejected";
}

public static class TestHeaderValues
{
    public const string True = "true";
    public const string PreAuth = "pre-auth";
    public const string PostAuth = "post-auth";
}

public sealed class RateLimitConcurrencyProbe(string id) : IDisposable
{
    private static readonly TimeSpan EnteredTimeout = TimeSpan.FromSeconds(5);
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitUntilEnteredAsync() => _entered.Task.WaitAsync(EnteredTimeout);

    internal void MarkEntered() => _entered.TrySetResult();

    internal Task WaitForReleaseAsync(CancellationToken cancellationToken) => _release.Task.WaitAsync(cancellationToken);

    public void Release() => _release.TrySetResult();

    public void Dispose()
    {
        Release();
        RateLimitConcurrencyCoordinator.Remove(id);
    }
}

public static class RateLimitConcurrencyCoordinator
{
    private static readonly ConcurrentDictionary<string, RateLimitConcurrencyProbe> Probes = new();

    public static RateLimitConcurrencyProbe Create(string id)
    {
        var probe = new RateLimitConcurrencyProbe(id);
        Probes[id] = probe;
        return probe;
    }

    internal static bool TryGet(string id, out RateLimitConcurrencyProbe? probe) => Probes.TryGetValue(id, out probe);

    internal static void Remove(string id) => Probes.TryRemove(id, out _);
}

internal static class TestClaims
{
    public const string TenantId = "tenant_id";
}

internal static class TestPolicies
{
    public const string NativeStrict = "native-strict";
    public const string NativeRelaxed = "native-relaxed";
    public const string DrnRuleStrict = "drn-rule-strict";
    public const string OtherDrnRule = "other-drn-rule";
}

internal static class TestAuthDefaults
{
    public const string AuthenticationScheme = "RateLimitingTest";
}