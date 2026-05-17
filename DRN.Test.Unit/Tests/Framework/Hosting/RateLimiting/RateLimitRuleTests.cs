using System.Net;
using System.Security.Claims;
using System.Threading.RateLimiting;
using DRN.Framework.Hosting.DrnProgram;
using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.Hosting.RateLimiting;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Sample.Hosted.Helpers;

namespace DRN.Test.Unit.Tests.Framework.Hosting.RateLimiting;

public class RateLimitRuleTests
{
    [Theory]
    [DataInlineUnit]
    public void Default_Rules_Should_Register_Through_SingletonRule_Base_Class(DrnTestContextUnit context)
    {
        context.ServiceCollection.AddSingleton(new DrnAppFeatures());
        context.ServiceCollection.AddServicesWithAttributes(typeof(DefaultPreAuthRateLimitRule).Assembly);

        var rules = context.GetServices<ISingletonRateLimitRule>().ToArray();

        rules.Should().ContainSingle(rule => rule is DefaultPreAuthRateLimitRule);
        rules.Should().ContainSingle(rule => rule is DefaultPostAuthRateLimitRule);
    }

    [Theory]
    [DataInlineUnit]
    public void Default_Rules_Should_Run_After_Application_Rules(DrnTestContextUnit _)
    {
        var features = new DrnAppFeatures();

        new DefaultPreAuthRateLimitRule(features).Order.Should().Be(int.MaxValue);
        new DefaultPostAuthRateLimitRule(features).Order.Should().Be(int.MaxValue);
    }

    [Theory]
    [DataInlineUnit]
    public void TokenBucket_Result_Should_Compose_By_Default(DrnTestContextUnit _)
    {
        var result = RateLimitRuleResult.TokenBucket("tenant:alpha", _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 10,
            ReplenishmentPeriod = TimeSpan.FromSeconds(60),
            TokensPerPeriod = 10,
            QueueLimit = 0,
            AutoReplenishment = true
        });

        result.PartitionKey.Should().Be("tenant:alpha");
        result.Action.Should().Be(RateLimitRuleAction.Limit);
        result.StopRemainingRules.Should().BeFalse();
    }

    [Theory]
    [DataInlineUnit]
    public void Allow_Result_Should_Stop_Remaining_Rules(DrnTestContextUnit _)
    {
        var result = RateLimitRuleResult.AllowRequest("health");

        result.Action.Should().Be(RateLimitRuleAction.Allow);
        result.StopRemainingRules.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task Deny_Result_Should_Reject_And_Stop_Remaining_Rules(DrnTestContextUnit _)
    {
        var result = RateLimitRuleResult.DenyRequest("blocked", TimeSpan.FromSeconds(10));
        await using var limiter = result.Partition.Factory(result.Partition.PartitionKey);

        using var lease = await limiter.AcquireAsync();

        result.Action.Should().Be(RateLimitRuleAction.Deny);
        result.StopRemainingRules.Should().BeTrue();
        lease.IsAcquired.Should().BeFalse();
        lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter).Should().BeTrue();
        retryAfter.Should().Be(TimeSpan.FromSeconds(10));
    }

    [Theory]
    [DataInlineUnit]
    public void Default_PostAuth_Rule_Should_Partition_By_Authenticated_User_Id(DrnTestContextUnit _)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "user-1"), new Claim(ClaimTypes.Name, "alice")],
                authenticationType: "Test"))
        };

        var result = new DefaultPostAuthRateLimitRule(new DrnAppFeatures()).EvaluatePostAuth(context);

        result.Should().NotBeNull();
        result!.PartitionKey.Should().Be("user:Test:user-1");
    }

    [Theory]
    [DataInlineUnit]
    public void Default_PostAuth_Rule_Should_Not_Partition_By_Mutable_User_Name(DrnTestContextUnit _)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "alice@example.test")],
                authenticationType: "Test"))
        };
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

        var result = new DefaultPostAuthRateLimitRule(new DrnAppFeatures()).EvaluatePostAuth(context);

        result.Should().NotBeNull();
        result!.PartitionKey.Should().Be("ip:127.0.0.1");
    }

    [Theory]
    [DataInlineUnit]
    public void Scoped_Claim_Rule_Can_Extend_PostAuth_Partitioning(DrnTestContextUnit context)
    {
        var accountId = Guid.NewGuid().ToString("N");
        var tenantId = Guid.NewGuid().ToString("N");
        var claimsIdentity = new ClaimsIdentity([new Claim(AccountIdClaimType, accountId), new Claim(TenantIdClaimType, tenantId)], authenticationType: "Test");
        
        ScopeContext.InitializeForTest(context, scopedUser: ScopedUser.FromClaimsPrincipal(new ClaimsPrincipal(claimsIdentity)));
        
        Get.RateLimit.AccountPartition.Should().Be($"account:{accountId}");
        Get.RateLimit.TenantPartition.Should().Be($"tenant:{tenantId}");
        
        var result = new AccountRateLimitRule(new DrnAppFeatures()).EvaluatePostAuth(new DefaultHttpContext());
        result.Should().NotBeNull();
        result.PartitionKey.Should().Be(Get.RateLimit.AccountPartition);
    }

    [Theory]
    [DataInlineUnit]
    public void Default_PreAuth_Rule_Should_Partition_By_Remote_Ip(DrnTestContextUnit _)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

        var result = new DefaultPreAuthRateLimitRule(new DrnAppFeatures()).EvaluatePreAuth(context);

        result.Should().NotBeNull();
        result!.PartitionKey.Should().Be("ip:127.0.0.1");
    }

    [Theory]
    [DataInlineUnit]
    public void Phase_Specific_TokenBucket_Options_Should_Override_Shared_Defaults(DrnTestContextUnit _)
    {
        var features = new DrnAppFeatures
        {
            RateLimit = new DrnRateLimitOptions
            {
                TokenLimit = 100,
                ReplenishmentSeconds = 60,
                TokensPerPeriod = 100,
                PreAuthTokenLimit = 20,
                PreAuthReplenishmentSeconds = 10,
                PreAuthTokensPerPeriod = 20,
                PostAuthTokenLimit = 200,
                PostAuthReplenishmentSeconds = 120,
                PostAuthTokensPerPeriod = 200
            }
        };

        var preAuth = RateLimitTokenBucketOptions.CreatePreAuth(features.RateLimit);
        var postAuth = RateLimitTokenBucketOptions.CreatePostAuth(features.RateLimit);

        preAuth.TokenLimit.Should().Be(20);
        preAuth.ReplenishmentPeriod.Should().Be(TimeSpan.FromSeconds(10));
        preAuth.TokensPerPeriod.Should().Be(20);
        postAuth.TokenLimit.Should().Be(200);
        postAuth.ReplenishmentPeriod.Should().Be(TimeSpan.FromSeconds(120));
        postAuth.TokensPerPeriod.Should().Be(200);
    }

    [Theory]
    [DataInlineUnit]
    public void DrnRateLimit_Config_Key_Should_Bind_To_RateLimit_Property(DrnTestContextUnit _)
    {
        var appSettings = AppSettings.Development(new
        {
            DrnAppFeatures = new
            {
                DrnRateLimit = new
                {
                    Disabled = true,
                    TokenLimit = 7,
                    ReplenishmentSeconds = 11,
                    TokensPerPeriod = 13,
                    PreAuthTokenLimit = 17,
                    PartitionLogMode = "PlainText"
                }
            }
        });

        appSettings.Features.RateLimit.Disabled.Should().BeTrue();
        appSettings.Features.RateLimit.TokenLimit.Should().Be(7);
        appSettings.Features.RateLimit.ReplenishmentSeconds.Should().Be(11);
        appSettings.Features.RateLimit.TokensPerPeriod.Should().Be(13);
        appSettings.Features.RateLimit.PreAuthTokenLimit.Should().Be(17);
        appSettings.Features.RateLimit.PartitionLogMode.Should().Be(RateLimitPartitionLogMode.PlainText);
    }

    [Theory]
    [DataInlineUnit]
    public void Sample_Development_RateLimit_Settings_Should_Bind_And_Produce_Expected_TokenBucket_Options(DrnTestContextUnit _)
    {
        // Mirrors exact values from Sample.Hosted/appsettings.Development.json
        var appSettings = AppSettings.Development(new
        {
            DrnAppFeatures = new
            {
                DrnRateLimit = new
                {
                    TokenLimit = 20,
                    ReplenishmentSeconds = 30,
                    TokensPerPeriod = 20,
                    PreAuthTokenLimit = 200,
                    PreAuthReplenishmentSeconds = 30,
                    PreAuthTokensPerPeriod = 200
                }
            }
        });

        // Verify config binding
        var rateLimit = appSettings.Features.RateLimit;
        rateLimit.Disabled.Should().BeFalse();
        rateLimit.TokenLimit.Should().Be(20);
        rateLimit.ReplenishmentSeconds.Should().Be(30);
        rateLimit.TokensPerPeriod.Should().Be(20);
        rateLimit.PreAuthTokenLimit.Should().Be(200);
        rateLimit.PreAuthReplenishmentSeconds.Should().Be(30);
        rateLimit.PreAuthTokensPerPeriod.Should().Be(200);

        // PostAuth* defaults to 0 → inherits shared values
        rateLimit.PostAuthTokenLimit.Should().Be(0);
        rateLimit.PostAuthReplenishmentSeconds.Should().Be(0);
        rateLimit.PostAuthTokensPerPeriod.Should().Be(0);

        // Verify pre-auth token bucket uses explicit pre-auth overrides
        var preAuth = RateLimitTokenBucketOptions.CreatePreAuth(rateLimit);
        preAuth.TokenLimit.Should().Be(200);
        preAuth.ReplenishmentPeriod.Should().Be(TimeSpan.FromSeconds(30));
        preAuth.TokensPerPeriod.Should().Be(200);
        preAuth.QueueLimit.Should().Be(0);
        preAuth.AutoReplenishment.Should().BeTrue();

        // Verify post-auth token bucket inherits shared values (PostAuth* = 0)
        var postAuth = RateLimitTokenBucketOptions.CreatePostAuth(rateLimit);
        postAuth.TokenLimit.Should().Be(20);
        postAuth.ReplenishmentPeriod.Should().Be(TimeSpan.FromSeconds(30));
        postAuth.TokensPerPeriod.Should().Be(20);
        postAuth.QueueLimit.Should().Be(0);
        postAuth.AutoReplenishment.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task PreAuth_Middleware_Should_Honor_DisableRateLimiting_Endpoint_Metadata(DrnTestContextUnit _)
    {
        var nextCalled = false;
        var limiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            throw new InvalidOperationException("Rate limiter should not run for disabled endpoints."));
        var middleware = new PreAuthRateLimitingMiddleware(httpContext =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, new DrnPreAuthRateLimiter(limiter), CreateTelemetry(), new DrnAppFeatures(), CreateSecuritySettings());
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(
            httpContext => Task.CompletedTask,
            new EndpointMetadataCollection(new DisableRateLimitingAttribute()),
            "rate-limit-disabled"));

        await middleware.InvokeAsync(context, Substitute.For<IScopedLog>());

        nextCalled.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task PreAuth_Middleware_Should_Run_For_EnableRateLimiting_Endpoint_Metadata(DrnTestContextUnit _)
    {
        var limiterCalled = false;
        var nextCalled = false;
        var limiter = PartitionedRateLimiter.Create<HttpContext, string>(_ =>
        {
            limiterCalled = true;
            return RateLimitPartition.GetNoLimiter("enabled");
        });
        var middleware = new PreAuthRateLimitingMiddleware(httpContext =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        }, new DrnPreAuthRateLimiter(limiter), CreateTelemetry(), new DrnAppFeatures(), CreateSecuritySettings());
        var context = new DefaultHttpContext();
        context.SetEndpoint(new Endpoint(
            httpContext => Task.CompletedTask,
            new EndpointMetadataCollection(new EnableRateLimitingAttribute("strict")),
            "rate-limit-enabled"));

        await middleware.InvokeAsync(context, Substitute.For<IScopedLog>());

        limiterCalled.Should().BeTrue();
        nextCalled.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task PreAuth_Rejection_Log_Should_Redact_Ip_And_Partition(DrnTestContextUnit _)
    {
        const string rawPartition = "api-key:secret-value";
        const string rawIp = "203.0.113.10";
        var rule = new SensitivePreAuthRule();
        var result = RateLimitRuleResult.DenyRequest(rawPartition);
        var limiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
        {
            httpContext.SetRateLimitRuleMatch(rule, result);
            return result.Partition;
        });
        var middleware = new PreAuthRateLimitingMiddleware(
            _ => throw new InvalidOperationException("Rejected request should not reach next middleware."),
            new DrnPreAuthRateLimiter(limiter),
            CreateTelemetry(),
            new DrnAppFeatures(),
            CreateSecuritySettings());
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(rawIp);
        var scopedLog = Substitute.For<IScopedLog>();

        await middleware.InvokeAsync(context, scopedLog);

        context.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        scopedLog.Received().Add("PreAuthRateLimitRejectedIp", Arg.Is<object>(value => IsRedacted(value, rawIp)));
        scopedLog.Received().Add("PreAuthRateLimitRejectedPartition", Arg.Is<object>(value => IsRedacted(value, rawPartition)));
        scopedLog.DidNotReceive().Add("PreAuthRateLimitRejectedIp", rawIp);
        scopedLog.DidNotReceive().Add("PreAuthRateLimitRejectedPartition", rawPartition);
    }

    [Theory]
    [DataInlineUnit]
    public void Partition_Log_Format_Should_Allow_Explicit_PlainText_Audit_Mode(DrnTestContextUnit _)
    {
        const string rawPartition = "tenant:known-customer";
        var options = new DrnRateLimitOptions { PartitionLogMode = RateLimitPartitionLogMode.PlainText };

        var formatted = RateLimitPartitionRedactor.Format(rawPartition, options, CreateSecuritySettings());

        formatted.Should().Be(rawPartition);
    }

    [Theory]
    [DataInlineUnit]
    public async Task PreAuth_RuleChain_Should_Not_Evaluate_Scoped_Rules(DrnTestContextUnit _)
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedRateLimitRule, ThrowingPreAuthScopedRule>();
        services.AddSingleton<RateLimitRuleRegistry>();
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<RateLimitRuleRegistry>();
        var limiter = RateLimitRuleChainFactory.Create(
            registry,
            RateLimitRulePhase.PreAuth,
            static (rule, httpContext) => rule.EvaluatePreAuth(httpContext),
            includeScopedRules: false);
        using var scope = provider.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };

        using var lease = await limiter.AcquireAsync(context);

        lease.IsAcquired.Should().BeTrue();
    }

    [Theory]
    [DataInlineUnit]
    public async Task PostAuth_Scoped_Rules_Should_Evaluate_All_Matching_Rules(DrnTestContextUnit _)
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedRateLimitRule, FirstComposedScopedRule>();
        services.AddScoped<IScopedRateLimitRule, SecondComposedScopedRule>();
        services.AddSingleton<RateLimitRuleRegistry>();
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<RateLimitRuleRegistry>();
        var limiter = RateLimitRuleChainFactory.Create(
            registry,
            RateLimitRulePhase.PostAuth,
            static (rule, httpContext) => rule.EvaluatePostAuth(httpContext));
        using var scope = provider.CreateScope();
        var context = CreateScopedRuleContext(scope.ServiceProvider);

        using var lease = await limiter.AcquireAsync(context);

        lease.IsAcquired.Should().BeTrue();
        GetRuleHits(context).Should().Equal("first", "second");
    }

    [Theory]
    [DataInlineUnit]
    public async Task PostAuth_Scoped_ShortCircuit_Rule_Should_Run_First_For_Same_Order_And_Stop_On_Match(DrnTestContextUnit _)
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedRateLimitRule, NormalScopedRule>();
        services.AddScoped<IScopedRateLimitRule, SameOrderShortCircuitScopedRule>();
        services.AddSingleton<RateLimitRuleRegistry>();
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<RateLimitRuleRegistry>();
        var limiter = RateLimitRuleChainFactory.Create(
            registry,
            RateLimitRulePhase.PostAuth,
            static (rule, httpContext) => rule.EvaluatePostAuth(httpContext));
        using var scope = provider.CreateScope();
        var context = CreateScopedRuleContext(scope.ServiceProvider);

        using var lease = await limiter.AcquireAsync(context);

        lease.IsAcquired.Should().BeTrue();
        GetRuleHits(context).Should().Equal("short");
    }

    [Theory]
    [DataInlineUnit]
    public async Task PostAuth_Scoped_ShortCircuit_Rule_Should_Continue_When_Not_Matched(DrnTestContextUnit _)
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedRateLimitRule, NoMatchShortCircuitScopedRule>();
        services.AddScoped<IScopedRateLimitRule, NormalScopedRule>();
        services.AddSingleton<RateLimitRuleRegistry>();
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<RateLimitRuleRegistry>();
        var limiter = RateLimitRuleChainFactory.Create(
            registry,
            RateLimitRulePhase.PostAuth,
            static (rule, httpContext) => rule.EvaluatePostAuth(httpContext));
        using var scope = provider.CreateScope();
        var context = CreateScopedRuleContext(scope.ServiceProvider);

        using var lease = await limiter.AcquireAsync(context);

        lease.IsAcquired.Should().BeTrue();
        GetRuleHits(context).Should().Equal("short-null", "normal");
    }

    [Theory]
    [DataInlineUnit]
    public async Task PostAuth_Same_Order_Singleton_ShortCircuit_Rule_Should_Run_First_And_Stop_On_Match(DrnTestContextUnit _)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonRateLimitRule, NormalSingletonRule>();
        services.AddSingleton<ISingletonRateLimitRule, SameOrderShortCircuitSingletonRule>();
        services.AddSingleton<RateLimitRuleRegistry>();
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<RateLimitRuleRegistry>();
        var limiter = RateLimitRuleChainFactory.Create(
            registry,
            RateLimitRulePhase.PostAuth,
            static (rule, httpContext) => rule.EvaluatePostAuth(httpContext));
        using var scope = provider.CreateScope();
        var context = CreateScopedRuleContext(scope.ServiceProvider);

        using var lease = await limiter.AcquireAsync(context);

        lease.IsAcquired.Should().BeTrue();
        GetRuleHits(context).Should().Equal("singleton-short");
    }

    [Theory]
    [DataInlineUnit]
    public async Task PostAuth_Deny_Rule_Should_Reject_And_Stop_Remaining_Rules(DrnTestContextUnit _)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonRateLimitRule, NormalSingletonRule>();
        services.AddSingleton<ISingletonRateLimitRule, SameOrderDenySingletonRule>();
        services.AddSingleton<RateLimitRuleRegistry>();
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<RateLimitRuleRegistry>();
        var limiter = RateLimitRuleChainFactory.Create(
            registry,
            RateLimitRulePhase.PostAuth,
            static (rule, httpContext) => rule.EvaluatePostAuth(httpContext));
        using var scope = provider.CreateScope();
        var context = CreateScopedRuleContext(scope.ServiceProvider);

        using var lease = await limiter.AcquireAsync(context);

        lease.IsAcquired.Should().BeFalse();
        GetRuleHits(context).Should().Equal("singleton-deny");
        context.GetRateLimitRuleMatch()!.Result.Action.Should().Be(RateLimitRuleAction.Deny);
    }

    [Theory]
    [DataInlineUnit]
    public async Task PostAuth_Same_Order_Scoped_ShortCircuit_Rule_Should_Run_Before_Singleton_Normal_Rule(DrnTestContextUnit _)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonRateLimitRule, NormalSingletonRule>();
        services.AddScoped<IScopedRateLimitRule, SameOrderShortCircuitScopedRule>();
        services.AddSingleton<RateLimitRuleRegistry>();
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<RateLimitRuleRegistry>();
        var limiter = RateLimitRuleChainFactory.Create(
            registry,
            RateLimitRulePhase.PostAuth,
            static (rule, httpContext) => rule.EvaluatePostAuth(httpContext));
        using var scope = provider.CreateScope();
        var context = CreateScopedRuleContext(scope.ServiceProvider);

        using var lease = await limiter.AcquireAsync(context);

        lease.IsAcquired.Should().BeTrue();
        GetRuleHits(context).Should().Equal("short");
    }

    [Theory]
    [DataInlineUnit]
    public async Task PostAuth_Policy_Rule_Should_Run_Only_For_Matching_EnableRateLimiting_Metadata(DrnTestContextUnit _)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonRateLimitRule, StrictPolicySingletonRule>();
        services.AddSingleton<RateLimitRuleRegistry>();
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<RateLimitRuleRegistry>();
        var limiter = RateLimitRuleChainFactory.Create(
            registry,
            RateLimitRulePhase.PostAuth,
            static (rule, httpContext) => rule.EvaluatePostAuth(httpContext));
        var globalContext = CreateScopedRuleContext(provider);
        var strictContext = CreateScopedRuleContext(provider);
        SetRateLimitPolicy(strictContext, StrictPolicyName);

        using var globalLease = await limiter.AcquireAsync(globalContext);
        using var strictLease = await limiter.AcquireAsync(strictContext);

        globalLease.IsAcquired.Should().BeTrue();
        strictLease.IsAcquired.Should().BeTrue();
        GetRuleHits(globalContext).Should().BeEmpty();
        GetRuleHits(strictContext).Should().Equal("strict-policy");
    }

    [Theory]
    [DataInlineUnit]
    public void Rule_Registry_Should_Reject_Blank_Policy_Name(DrnTestContextUnit _)
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISingletonRateLimitRule, BlankPolicySingletonRule>();
        services.AddSingleton<RateLimitRuleRegistry>();
        using var provider = services.BuildServiceProvider();

        Action act = () => provider.GetRequiredService<RateLimitRuleRegistry>();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*PolicyName*non-empty*");
    }

    [Theory]
    [DataInlineUnit]
    public async Task PostAuth_Scoped_Rules_Should_Preserve_Global_Order_With_Singleton_Rules(DrnTestContextUnit _)
    {
        var services = new ServiceCollection();
        services.AddScoped<IScopedRateLimitRule, LowOrderNoMatchScopedRule>();
        services.AddSingleton<ISingletonRateLimitRule, MidOrderSingletonRule>();
        services.AddScoped<IScopedRateLimitRule, HighOrderAllowScopedRule>();
        services.AddSingleton<RateLimitRuleRegistry>();
        await using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<RateLimitRuleRegistry>();
        var limiter = RateLimitRuleChainFactory.Create(
            registry,
            RateLimitRulePhase.PostAuth,
            static (rule, httpContext) => rule.EvaluatePostAuth(httpContext));
        using var scope = provider.CreateScope();
        var context = CreateScopedRuleContext(scope.ServiceProvider);

        using var lease = await limiter.AcquireAsync(context);

        lease.IsAcquired.Should().BeTrue();
        GetRuleHits(context).Should().Equal("low-null", "singleton", "high-allow");
    }

    [Theory]
    [DataInlineUnit]
    public void PostAuth_Options_Should_Preserve_AddRateLimiter_Customizations(DrnTestContextUnit _)
    {
        var services = new ServiceCollection();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status418ImATeapot;
            options.OnRejected = (_, _) => ValueTask.CompletedTask;
            options.AddTokenBucketLimiter("strict", opt =>
            {
                opt.TokenLimit = 10;
                opt.ReplenishmentPeriod = TimeSpan.FromMinutes(1);
                opt.TokensPerPeriod = 10;
                opt.QueueLimit = 0;
            });
        });
        services.AddSingleton<RateLimitRuleRegistry>();
        using var provider = services.BuildServiceProvider();

        var options = new RateLimiterOptionsTestProgram().CreatePostAuthOptions(provider);

        GetPolicyMap(options).Contains("strict").Should().BeTrue();
        options.RejectionStatusCode.Should().Be(StatusCodes.Status418ImATeapot);
        options.OnRejected.Should().NotBeNull();
        options.GlobalLimiter.Should().NotBeNull();
    }

    [Theory]
    [DataInlineUnit]
    public void Telemetry_Should_Expose_Rule_Action_Tag(DrnTestContextUnit _)
    {
        var context = new DefaultHttpContext();
        var result = RateLimitRuleResult.AllowRequest("health");
        context.SetRateLimitRuleMatch(new TelemetryAllowRule(), result);

        var tags = RateLimitTelemetry.CreateTags(context, RateLimitRulePhase.PostAuth, "acquired", context.GetRateLimitRuleMatch());

        tags.Should().Contain(tag => tag.Key == "drn.rate_limiting.action" && tag.Value != null && tag.Value.ToString() == "allow");
    }

    private sealed class AccountRateLimitRule(DrnAppFeatures features) : ScopedRateLimitRule
    {
        public override RateLimitRuleResult? EvaluatePostAuth(HttpContext context)
        {
            var partitionKey = Get.RateLimit.AccountPartition;
            if (partitionKey == null)
                return null;

            return RateLimitRuleResult.TokenBucket(partitionKey, _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = features.RateLimit.TokenLimit,
                ReplenishmentPeriod = TimeSpan.FromSeconds(features.RateLimit.ReplenishmentSeconds),
                TokensPerPeriod = features.RateLimit.TokensPerPeriod,
                QueueLimit = 0,
                AutoReplenishment = true
            });
        }
    }

    private const string StrictPolicyName = "strict";
    private const string AccountIdClaimType = "AccountId";
    private const string TenantIdClaimType = "TenantId";
    private const string RuleHitsKey = "__rate_limit_rule_hits";

    private static RateLimitTelemetry CreateTelemetry()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();
        return new RateLimitTelemetry(provider);
    }

    private static IAppSecuritySettings CreateSecuritySettings() => new AppSecuritySettings(new DrnAppFeatures());

    private static DefaultHttpContext CreateScopedRuleContext(IServiceProvider serviceProvider)
    {
        var context = new DefaultHttpContext { RequestServices = serviceProvider };
        context.Items[RuleHitsKey] = new List<string>();

        return context;
    }

    private static void SetRateLimitPolicy(HttpContext context, string policyName) =>
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new EnableRateLimitingAttribute(policyName)),
            $"rate-limit-{policyName}"));

    private static List<string> GetRuleHits(HttpContext context) => (List<string>)context.Items[RuleHitsKey]!;

    private static void AddRuleHit(HttpContext context, string hit) => GetRuleHits(context).Add(hit);

    private static bool IsRedacted(object? value, string rawValue) =>
        value is string text
        && text.StartsWith("blake3-keyed:", StringComparison.Ordinal)
        && !text.Contains(rawValue, StringComparison.Ordinal);

    private static IDictionary GetPolicyMap(RateLimiterOptions options)
    {
        var property = typeof(RateLimiterOptions).GetProperty("PolicyMap", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        property.Should().NotBeNull("ASP.NET Core RateLimiterOptions should retain configured policy registrations");

        return (IDictionary)property!.GetValue(options)!;
    }

    private static RateLimitRuleResult TestTokenBucket(string partitionKey) =>
        RateLimitRuleResult.TokenBucket(partitionKey, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 10,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            TokensPerPeriod = 10,
            QueueLimit = 0,
            AutoReplenishment = true
        });

    private sealed class ThrowingPreAuthScopedRule : ScopedRateLimitRule
    {
        public override RateLimitRuleResult? EvaluatePreAuth(HttpContext context) =>
            throw new InvalidOperationException("Pre-auth limiter must not evaluate scoped rules.");
    }

    private sealed class SensitivePreAuthRule : SingletonRateLimitRule
    {
    }

    private sealed class TelemetryAllowRule : SingletonRateLimitRule
    {
    }

    private sealed class FirstComposedScopedRule : ScopedRateLimitRule
    {
        public override RateLimitRuleResult? EvaluatePostAuth(HttpContext context)
        {
            AddRuleHit(context, "first");
            return TestTokenBucket("first");
        }
    }

    private sealed class SecondComposedScopedRule : ScopedRateLimitRule
    {
        public override RateLimitRuleResult? EvaluatePostAuth(HttpContext context)
        {
            AddRuleHit(context, "second");
            return TestTokenBucket("second");
        }
    }

    private sealed class NormalScopedRule : ScopedRateLimitRule
    {
        public override RateLimitRuleResult? EvaluatePostAuth(HttpContext context)
        {
            AddRuleHit(context, "normal");
            return TestTokenBucket("normal");
        }
    }

    private sealed class SameOrderShortCircuitScopedRule : ScopedRateLimitRule
    {
        public override bool ShortCircuitOnMatch => true;

        public override RateLimitRuleResult? EvaluatePostAuth(HttpContext context)
        {
            AddRuleHit(context, "short");
            return RateLimitRuleResult.AllowRequest("short");
        }
    }

    private sealed class NoMatchShortCircuitScopedRule : ScopedRateLimitRule
    {
        public override bool ShortCircuitOnMatch => true;

        public override RateLimitRuleResult? EvaluatePostAuth(HttpContext context)
        {
            AddRuleHit(context, "short-null");
            return null;
        }
    }

    private sealed class LowOrderNoMatchScopedRule : ScopedRateLimitRule
    {
        public override int Order => -100;

        public override RateLimitRuleResult? EvaluatePostAuth(HttpContext context)
        {
            AddRuleHit(context, "low-null");
            return null;
        }
    }

    private sealed class MidOrderSingletonRule : SingletonRateLimitRule
    {
        public override RateLimitRuleResult? EvaluatePostAuth(HttpContext context)
        {
            AddRuleHit(context, "singleton");
            return TestTokenBucket("singleton");
        }
    }

    private sealed class StrictPolicySingletonRule : SingletonRateLimitRule
    {
        public override string? PolicyName => StrictPolicyName;

        public override RateLimitRuleResult? EvaluatePostAuth(HttpContext context)
        {
            AddRuleHit(context, "strict-policy");
            return TestTokenBucket("strict-policy");
        }
    }

    private sealed class BlankPolicySingletonRule : SingletonRateLimitRule
    {
        public override string? PolicyName => " ";
    }

    private sealed class NormalSingletonRule : SingletonRateLimitRule
    {
        public override RateLimitRuleResult? EvaluatePostAuth(HttpContext context)
        {
            AddRuleHit(context, "singleton-normal");
            return TestTokenBucket("singleton-normal");
        }
    }

    private sealed class SameOrderShortCircuitSingletonRule : SingletonRateLimitRule
    {
        public override bool ShortCircuitOnMatch => true;

        public override RateLimitRuleResult? EvaluatePostAuth(HttpContext context)
        {
            AddRuleHit(context, "singleton-short");
            return RateLimitRuleResult.AllowRequest("singleton-short");
        }
    }

    private sealed class SameOrderDenySingletonRule : SingletonRateLimitRule
    {
        public override bool ShortCircuitOnMatch => true;

        public override RateLimitRuleResult? EvaluatePostAuth(HttpContext context)
        {
            AddRuleHit(context, "singleton-deny");
            return RateLimitRuleResult.DenyRequest("singleton-deny");
        }
    }

    private sealed class HighOrderAllowScopedRule : ScopedRateLimitRule
    {
        public override int Order => 100;
        public override bool ShortCircuitOnMatch => true;

        public override RateLimitRuleResult? EvaluatePostAuth(HttpContext context)
        {
            AddRuleHit(context, "high-allow");
            return RateLimitRuleResult.AllowRequest("high-allow");
        }
    }

    private sealed class RateLimiterOptionsTestProgram : DrnProgramBase<RateLimiterOptionsTestProgram>, IDrnProgram
    {
        static Task IDrnProgram.Main(string[] args) => Task.CompletedTask;

        public RateLimiterOptions CreatePostAuthOptions(IServiceProvider serviceProvider) =>
            CreatePostAuthRateLimiterOptions(serviceProvider, AppSettings.Development());

        protected override Task AddServicesAsync(WebApplicationBuilder builder, IAppSettings appSettings, IScopedLog scopedLog) =>
            Task.CompletedTask;
    }
}
