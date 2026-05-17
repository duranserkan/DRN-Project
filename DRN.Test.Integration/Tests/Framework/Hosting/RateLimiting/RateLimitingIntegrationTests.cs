using System.Net;
using DRN.Test.Utils.RateLimiting;

namespace DRN.Test.Integration.Tests.Framework.Hosting.RateLimiting;

public class RateLimitingIntegrationTests(ITestOutputHelper outputHelper)
{
    [Theory]
    [DataInline]
    public async Task PreAuth_Default_Limiter_Should_Reject_When_Ip_Partition_Is_Exhausted(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 1, postAuthTokenLimit: 100);

        await AssertStatusAsync(client, TestPaths.Anonymous, HttpStatusCode.OK);
        await AssertStatusAsync(client, TestPaths.Anonymous, HttpStatusCode.TooManyRequests);
    }

    [Theory]
    [DataInline]
    public async Task PostAuth_Default_Limiter_Should_Partition_By_Authenticated_User(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 100, postAuthTokenLimit: 1);

        await AssertStatusAsync(client, TestPaths.Authenticated, HttpStatusCode.OK, userId: "alpha");
        await AssertStatusAsync(client, TestPaths.Authenticated, HttpStatusCode.TooManyRequests, userId: "alpha");
        await AssertStatusAsync(client, TestPaths.Authenticated, HttpStatusCode.OK, userId: "beta");
    }

    [Theory]
    [DataInline]
    public async Task PostAuth_Default_Limiter_Should_Fallback_To_Anonymous_Ip_Partition(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 100, postAuthTokenLimit: 1);

        await AssertStatusAsync(client, TestPaths.Anonymous, HttpStatusCode.OK);
        await AssertStatusAsync(client, TestPaths.Anonymous, HttpStatusCode.TooManyRequests);
    }

    [Theory]
    [DataInline]
    public async Task Chained_PostAuth_Limiter_Should_Reject_When_Custom_Tenant_Partition_Is_Exhausted(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 100, postAuthTokenLimit: 100);

        await AssertStatusAsync(client, TestPaths.Chained, HttpStatusCode.OK, userId: "alpha", tenantId: "shared");
        await AssertStatusAsync(client, TestPaths.Chained, HttpStatusCode.TooManyRequests, userId: "beta", tenantId: "shared");
        await AssertStatusAsync(client, TestPaths.Chained, HttpStatusCode.OK, userId: "gamma", tenantId: "other");
    }

    [Theory]
    [DataInline]
    public async Task Chained_PostAuth_Limiter_Should_Reject_When_Default_User_Partition_Is_Exhausted(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 100, postAuthTokenLimit: 1);

        await AssertStatusAsync(client, TestPaths.Chained, HttpStatusCode.OK, userId: "alpha", tenantId: "tenant-a");
        await AssertStatusAsync(client, TestPaths.Chained, HttpStatusCode.TooManyRequests, userId: "alpha", tenantId: "tenant-b");
    }

    [Theory]
    [DataInline]
    public async Task Custom_PreAuth_Rule_Should_Partition_By_Request_Header(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 100, postAuthTokenLimit: 100);

        await AssertStatusAsync(client, TestPaths.CustomPreAuth, HttpStatusCode.OK, preAuthPartition: "alpha");
        await AssertStatusAsync(client, TestPaths.CustomPreAuth, HttpStatusCode.TooManyRequests, preAuthPartition: "alpha");
        await AssertStatusAsync(client, TestPaths.CustomPreAuth, HttpStatusCode.OK, preAuthPartition: "beta");
    }

    [Theory]
    [DataInline]
    public async Task Chained_PreAuth_Limiter_Should_Continue_To_Default_Ip_Limiter_After_Custom_Rule(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 1, postAuthTokenLimit: 100);

        await AssertStatusAsync(client, TestPaths.PreAuthChain, HttpStatusCode.OK, preAuthPartition: "alpha");
        await AssertStatusAsync(client, TestPaths.PreAuthChain, HttpStatusCode.TooManyRequests, preAuthPartition: "beta");
    }

    [Theory]
    [DataInline]
    public async Task DisableRateLimiting_Metadata_Should_Bypass_Exhausted_PreAuth_And_PostAuth_Limiters(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 1, postAuthTokenLimit: 1);

        await AssertStatusAsync(client, TestPaths.Anonymous, HttpStatusCode.OK);
        await AssertStatusAsync(client, TestPaths.Anonymous, HttpStatusCode.TooManyRequests);
        await AssertStatusAsync(client, TestPaths.Disabled, HttpStatusCode.OK);
        await AssertStatusAsync(client, TestPaths.Disabled, HttpStatusCode.OK);
    }

    [Theory]
    [DataInline]
    public async Task Disabled_RateLimit_Config_Should_Bypass_All_Rate_Limiters(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 1, postAuthTokenLimit: 1, rateLimitDisabled: true);

        await AssertStatusAsync(client, TestPaths.Anonymous, HttpStatusCode.OK);
        await AssertStatusAsync(client, TestPaths.Anonymous, HttpStatusCode.OK);
        await AssertStatusAsync(client, TestPaths.Authenticated, HttpStatusCode.OK, userId: "alpha");
        await AssertStatusAsync(client, TestPaths.Authenticated, HttpStatusCode.OK, userId: "alpha");
    }

    [Theory]
    [DataInline]
    public async Task Allow_Rule_Should_Bypass_Exhausted_Default_Limiters_Without_Endpoint_Metadata(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 1, postAuthTokenLimit: 1);

        await AssertStatusAsync(client, TestPaths.Anonymous, HttpStatusCode.OK);
        await AssertStatusAsync(client, TestPaths.Anonymous, HttpStatusCode.TooManyRequests);
        await AssertStatusAsync(client, TestPaths.AllowRule, HttpStatusCode.OK);
        await AssertStatusAsync(client, TestPaths.AllowRule, HttpStatusCode.OK);
    }

    [Theory]
    [DataInline]
    public async Task Deny_Rule_Should_Reject_Immediately_Without_Consuming_Default_Quota(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 100, postAuthTokenLimit: 100);

        await AssertStatusAsync(client, TestPaths.DenyRule, HttpStatusCode.TooManyRequests, expectRetryAfter: false);
        await AssertStatusAsync(client, TestPaths.Anonymous, HttpStatusCode.OK);
    }

    [Theory]
    [DataInline]
    public async Task Scoped_PostAuth_Allow_Rule_Should_Use_Current_Request_Claims(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 100, postAuthTokenLimit: 1);

        await AssertStatusAsync(client, TestPaths.ScopedAllowRule, HttpStatusCode.OK, userId: "alpha", tenantId: "ordinary");
        await AssertStatusAsync(client, TestPaths.ScopedAllowRule, HttpStatusCode.TooManyRequests, userId: "alpha", tenantId: "ordinary");
        await AssertStatusAsync(client, TestPaths.ScopedAllowRule, HttpStatusCode.OK, userId: "alpha", tenantId: TestTenantValues.Trusted);
        await AssertStatusAsync(client, TestPaths.ScopedAllowRule, HttpStatusCode.OK, userId: "alpha", tenantId: TestTenantValues.Trusted);
    }

    [Theory]
    [DataInline]
    public async Task StopRemaining_Result_Should_Bypass_Exhausted_Default_Limiter_Without_Allowing(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 1, postAuthTokenLimit: 1);

        await AssertStatusAsync(client, TestPaths.Anonymous, HttpStatusCode.OK);
        await AssertStatusAsync(client, TestPaths.Anonymous, HttpStatusCode.TooManyRequests);
        await AssertStatusAsync(client, TestPaths.StopRemainingRule, HttpStatusCode.OK);
        await AssertStatusAsync(client, TestPaths.StopRemainingRule, HttpStatusCode.OK);
    }

    [Theory]
    [DataInline]
    public async Task ShortCircuit_Rule_Should_Bypass_Exhausted_Default_Limiter_Without_Allowing(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 1, postAuthTokenLimit: 1);

        await AssertStatusAsync(client, TestPaths.Anonymous, HttpStatusCode.OK);
        await AssertStatusAsync(client, TestPaths.Anonymous, HttpStatusCode.TooManyRequests);
        await AssertStatusAsync(client, TestPaths.ShortCircuitRule, HttpStatusCode.OK);
        await AssertStatusAsync(client, TestPaths.ShortCircuitRule, HttpStatusCode.OK);
    }

    [Theory]
    [DataInline]
    public async Task Rule_OnRejected_Should_Run_For_PreAuth_Rejections(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 100, postAuthTokenLimit: 100);

        await AssertStatusAsync(client, TestPaths.PreAuthRejected, HttpStatusCode.OK);
        await AssertStatusAsync(client, TestPaths.PreAuthRejected, HttpStatusCode.TooManyRequests,
            expectedHeaderName: TestHeaders.RuleOnRejected,
            expectedHeaderValue: TestHeaderValues.PreAuth);
    }

    [Theory]
    [DataInline]
    public async Task Rule_OnRejected_Should_Run_For_PostAuth_Rejections(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 100, postAuthTokenLimit: 100);

        await AssertStatusAsync(client, TestPaths.PostAuthRejected, HttpStatusCode.OK, userId: "alpha");
        await AssertStatusAsync(client, TestPaths.PostAuthRejected, HttpStatusCode.TooManyRequests,
            userId: "alpha",
            expectedHeaderName: TestHeaders.RuleOnRejected,
            expectedHeaderValue: TestHeaderValues.PostAuth);
    }

    [Theory]
    [DataInline]
    public async Task Native_Named_Policy_Should_Reject_Independently_Of_Drn_Global_Limiter(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 100, postAuthTokenLimit: 100);

        await AssertStatusAsync(client, TestPaths.NativePolicy, HttpStatusCode.OK);
        await AssertStatusAsync(client, TestPaths.NativePolicy, HttpStatusCode.TooManyRequests);
    }

    [Theory]
    [DataInline]
    public async Task Native_Named_Policy_Should_Compose_With_Drn_Global_Limiter(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 100, postAuthTokenLimit: 1);

        await AssertStatusAsync(client, TestPaths.NativeRelaxedPolicy, HttpStatusCode.OK, userId: "alpha");
        await AssertStatusAsync(client, TestPaths.NativeRelaxedPolicy, HttpStatusCode.TooManyRequests, userId: "alpha");
    }

    [Theory]
    [DataInline]
    public async Task Configured_RateLimiter_OnRejected_Should_Run_After_Drn_Wrapper(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 100, postAuthTokenLimit: 100);

        await AssertStatusAsync(client, TestPaths.NativePolicy, HttpStatusCode.OK);
        await AssertStatusAsync(client, TestPaths.NativePolicy, HttpStatusCode.TooManyRequests,
            expectedHeaderName: TestHeaders.ConfiguredOnRejected,
            expectedHeaderValue: TestHeaderValues.True);
    }

    [Theory]
    [DataInline]
    public async Task Native_Policy_Rejection_Should_Not_Run_Drn_Rule_OnRejected(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 100, postAuthTokenLimit: 100);

        await AssertStatusAsync(client, TestPaths.NativePolicy, HttpStatusCode.OK);
        using var response = await SendAsync(client, TestPaths.NativePolicy);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        response.Headers.Contains(TestHeaders.ConfiguredOnRejected).Should().BeTrue();
        response.Headers.Contains(TestHeaders.RuleOnRejected).Should().BeFalse();
    }

    [Theory]
    [DataInline]
    public async Task EnableRateLimiting_Metadata_Should_Not_Bypass_PreAuth_Global_Limiter(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 1, postAuthTokenLimit: 100);

        await AssertStatusAsync(client, TestPaths.NativeRelaxedPolicy, HttpStatusCode.OK);
        await AssertStatusAsync(client, TestPaths.NativeRelaxedPolicy, HttpStatusCode.TooManyRequests);
    }

    [Theory]
    [DataInline]
    public async Task Drn_Policy_Rule_Should_Run_Only_For_Matching_EnableRateLimiting_Metadata(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 100, postAuthTokenLimit: 100);

        await AssertStatusAsync(client, TestPaths.Anonymous, HttpStatusCode.OK);
        await AssertStatusAsync(client, TestPaths.Anonymous, HttpStatusCode.OK);
        await AssertStatusAsync(client, TestPaths.DrnPolicy, HttpStatusCode.OK);
        await AssertStatusAsync(client, TestPaths.DrnPolicy, HttpStatusCode.TooManyRequests);
    }

    [Theory]
    [DataInline]
    public async Task Drn_Policy_Rule_Should_Not_Run_For_Different_EnableRateLimiting_Metadata(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 100, postAuthTokenLimit: 100);

        await AssertStatusAsync(client, TestPaths.OtherDrnPolicy, HttpStatusCode.OK);
        await AssertStatusAsync(client, TestPaths.OtherDrnPolicy, HttpStatusCode.OK);
    }

    [Theory]
    [DataInline(TestPaths.FixedWindow)]
    [DataInline(TestPaths.SlidingWindow)]
    [DataInline(TestPaths.CustomPartition)]
    public async Task Partitioned_Rule_Results_Should_Reject_When_Partition_Is_Exhausted(DrnTestContext context, string path)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 100, postAuthTokenLimit: 100);

        await AssertStatusAsync(client, path, HttpStatusCode.OK);
        await AssertStatusAsync(client, path, HttpStatusCode.TooManyRequests, expectRetryAfter: null);
    }

    [Theory]
    [DataInline]
    public async Task ConcurrencyLimiter_Result_Should_Reject_Concurrent_Request(DrnTestContext context)
    {
        var client = await CreateClientAsync(context, preAuthTokenLimit: 100, postAuthTokenLimit: 100);
        var partition = Guid.NewGuid().ToString("N");
        using var probe = RateLimitConcurrencyCoordinator.Create(partition);
        var firstRequest = SendAsync(client, TestPaths.ConcurrencyLimiter, preAuthPartition: partition);

        try
        {
            await probe.WaitUntilEnteredAsync();
            await AssertStatusAsync(client, TestPaths.ConcurrencyLimiter, HttpStatusCode.TooManyRequests,
                preAuthPartition: partition,
                expectRetryAfter: false);
        }
        finally
        {
            probe.Release();
        }

        using var firstResponse = await firstRequest;
        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<HttpClient> CreateClientAsync(
        DrnTestContext context,
        int preAuthTokenLimit,
        int postAuthTokenLimit,
        bool rateLimitDisabled = false)
    {
        ConfigureRateLimits(context, preAuthTokenLimit, postAuthTokenLimit, rateLimitDisabled);
        return await context.ApplicationContext.CreateClientAsync<RateLimitingTestProgram>(outputHelper);
    }

    private static void ConfigureRateLimits(
        DrnTestContext context,
        int preAuthTokenLimit,
        int postAuthTokenLimit,
        bool rateLimitDisabled)
    {
        context.AddToConfiguration(new
        {
            DrnAppFeatures = new
            {
                DrnRateLimit = new
                {
                    Disabled = rateLimitDisabled,
                    TokenLimit = 100,
                    ReplenishmentSeconds = 60,
                    TokensPerPeriod = 100,
                    PreAuthTokenLimit = preAuthTokenLimit,
                    PreAuthReplenishmentSeconds = 60,
                    PreAuthTokensPerPeriod = preAuthTokenLimit,
                    PostAuthTokenLimit = postAuthTokenLimit,
                    PostAuthReplenishmentSeconds = 60,
                    PostAuthTokensPerPeriod = postAuthTokenLimit
                }
            }
        });
    }

    private static async Task AssertStatusAsync(
        HttpClient client,
        string path,
        HttpStatusCode expectedStatus,
        string? userId = null,
        string? tenantId = null,
        string? preAuthPartition = null,
        bool? expectRetryAfter = true,
        string? expectedHeaderName = null,
        string? expectedHeaderValue = null)
    {
        using var response = await SendAsync(client, path, userId, tenantId, preAuthPartition);

        response.StatusCode.Should().Be(expectedStatus);
        if (expectedStatus == HttpStatusCode.TooManyRequests && expectRetryAfter.HasValue)
            if (expectRetryAfter.Value)
                response.Headers.Contains("Retry-After").Should().BeTrue();
            else
                response.Headers.Contains("Retry-After").Should().BeFalse();
        
        if (!string.IsNullOrWhiteSpace(expectedHeaderName))
        {
            response.Headers.TryGetValues(expectedHeaderName, out var values).Should().BeTrue();
            if (!string.IsNullOrWhiteSpace(expectedHeaderValue))
                values.Should().Contain(expectedHeaderValue);
        }
    }

    private static Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        string path,
        string? userId = null,
        string? tenantId = null,
        string? preAuthPartition = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (!string.IsNullOrWhiteSpace(userId))
            request.Headers.Add(TestHeaders.UserId, userId);
        if (!string.IsNullOrWhiteSpace(tenantId))
            request.Headers.Add(TestHeaders.TenantId, tenantId);
        if (!string.IsNullOrWhiteSpace(preAuthPartition))
            request.Headers.Add(TestHeaders.PreAuthPartition, preAuthPartition);

        return client.SendAsync(request);
    }
}
