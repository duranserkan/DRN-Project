using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DRN.Framework.SharedKernel.Json;
using DRN.Test.Integration.Helpers;
using DRN.Test.Utils.Hosting.ApiErrors;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity.Data;
using Swashbuckle.AspNetCore.Swagger;

namespace DRN.Test.Integration.Tests.Framework.Hosting.ApiErrors;

public class ApiErrorResponseTests
{
    [Theory]
    [IsolatedHostingData]
    public async Task Scoped_Problem_Should_Flow_To_Errors_Without_Leaking_To_Later_Requests(DrnTestContext context)
    {
        using var testEnvironment = TestEnvironment.SetTestContextEnabledScope(true);
        Configure(context, "Production");
        var application = context.ApplicationContext.CreateApplication<ApiErrorTestProgram>();
        using var client = application.CreateClient(new() { AllowAutoRedirect = false });
        var root = "/gateway" + ApiErrorTestProgram.Root;

        foreach (var producer in new[] { "status", "typed", "throw", "scoped-conflict", "exception-for" })
        {
            using var response = await client.GetAsync(root + "/scoped/" + producer + "?secret=hidden");
            using var problem = await AssertProblemAsync(response, 409);
            problem.RootElement.GetProperty("detail").GetString().Should().Be(producer == "typed" ? "Explicit detail" : "Public detail");
            problem.RootElement.GetProperty("code").GetString().Should().Be("public_code");
            problem.RootElement.GetProperty("instance").GetString().Should().Be(root + "/scoped/" + producer);
            if (producer != "typed") problem.RootElement.GetProperty("title").GetString().Should().Be("Public title");
        }

        using var plain = await client.GetAsync(root + "/throw/409");
        using var plainProblem = await AssertProblemAsync(plain, 409);
        plainProblem.RootElement.TryGetProperty("detail", out _).Should().BeFalse();
        plainProblem.RootElement.TryGetProperty("code", out _).Should().BeFalse();
    }

    [Theory]
    [IsolatedHostingData]
    public async Task Controller_And_Action_Opt_Out_Should_Skip_Drn_Formatting_And_Swagger_Defaults(DrnTestContext context)
    {
        using var testEnvironment = TestEnvironment.SetTestContextEnabledScope(true);
        Configure(context, "Production");
        var application = context.ApplicationContext.CreateApplication<ApiErrorTestProgram>();
        using var client = application.CreateClient(new() { AllowAutoRedirect = false });
        var root = ApiErrorTestProgram.Root;
        var document = application.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");

        foreach (var path in new[] { "/opt-out-action", "/opt-out-controller/status" })
        {
            using var response = await client.GetAsync(root + path);
            response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
            (await response.Content.ReadAsStringAsync()).Should().BeEmpty();
            document.Paths[root + path].Operations![HttpMethod.Get].Responses!.Should().NotContainKey("400");
        }

        foreach (var path in new[] { "/opt-out-controller/mvc", "/opt-out-controller/typed" })
        {
            using var response = await client.GetAsync(root + path);
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            problem.RootElement.GetProperty("detail").GetString().Should().Be("Public message");
            problem.RootElement.TryGetProperty("requestId", out _).Should().BeFalse();
            document.Paths[root + path].Operations![HttpMethod.Get].Responses!.Should().NotContainKey("400");
        }

        using var failure = await client.GetAsync(root + "/opt-out-controller/throw");
        failure.StatusCode.Should().Be(HttpStatusCode.Conflict);
        failure.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        (await failure.Content.ReadAsStringAsync()).Should().NotContain(ApiErrorTestProgram.Secret);

        using var enabled = await client.GetAsync(root + "/typed/503");
        using var enabledProblem = await AssertProblemAsync(enabled, 503);
    }

    [Theory]
    [IsolatedHostingData]
    public void Swagger_Should_Describe_Api_Problems_And_Preserve_Custom_Contracts(DrnTestContext context)
    {
        using var testEnvironment = TestEnvironment.SetTestContextEnabledScope(true);
        Configure(context, "Production");
        var application = context.ApplicationContext.CreateApplication<ApiErrorTestProgram>();
        var document = application.Services.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        var root = ApiErrorTestProgram.Root;

        foreach (var path in new[] { "/unhandled", "/minimal" })
        {
            var responses = document.Paths[root + path].Operations![HttpMethod.Get].Responses!;
            responses.Keys.Should().NotContain(new[] { "401", "403", "429", "500", "5XX" });
            var content = responses["400"].Content!;
            content.Keys.Should().Equal("application/problem+json");
            var schema = content["application/problem+json"].Schema!;
            schema.Required.Should().Contain("requestId");
            schema.Properties!.Should().ContainKey("requestId");
            responses["400"].Description.Should().Contain("4xx (400-499)").And.Contain("5xx (500-599)");
        }

        var validation = document.Paths[root + "/validation"].Operations![HttpMethod.Post].Responses!["400"];
        validation.Content!.Keys.Should().Equal("application/problem+json");
        validation.Content["application/problem+json"].Schema!.AllOf.Should().ContainSingle();
        document.Components!.Schemas!["ValidationProblemDetails"].Properties!.Should().ContainKey("errors");

        var custom = document.Paths[root + "/custom"].Operations![HttpMethod.Get].Responses!["400"];
        custom.Content!.Keys.Should().Equal("text/plain");
        custom.Content["text/plain"].Schema!.Type.Should().Be(Microsoft.OpenApi.JsonSchemaType.String);
        document.Paths[root + "/browser"].Operations![HttpMethod.Get].Responses!.Should().NotContainKey("400");
        document.Paths[root + "/status/{status}"].Operations![HttpMethod.Get].Responses!["503"]
            .Content!.Keys.Should().Equal("application/problem+json");
        document.Paths[root + "/status/{status}"].Operations![HttpMethod.Head].Responses!.Should().NotContainKey("400");
    }

    [Theory]
    [IsolatedHostingData("Development", "text/html", "")]
    [IsolatedHostingData("Staging", "application/json", "/gateway")]
    [IsolatedHostingData("Production", "*/*", "")]
    [IsolatedHostingData("Production", "", "/gateway")]
    public async Task Mvc_Typed_And_Exception_Responses_Should_Share_The_Contract(
        DrnTestContext context, string environment, string accept, string pathBase)
    {
        using var testEnvironment = TestEnvironment.SetTestContextEnabledScope(true);
        Configure(context, environment);
        var application = context.ApplicationContext.CreateApplication<ApiErrorTestProgram>();
        using var client = application.CreateClient(new() { AllowAutoRedirect = false });
        if (accept.Length > 0) client.DefaultRequestHeaders.Accept.ParseAdd(accept);
        var root = pathBase + ApiErrorTestProgram.Root;

        foreach (var status in new[] { 400, 401, 403, 404, 405, 406, 408, 409, 410, 415, 422, 429, 499, 500, 502, 503, 504, 599 })
        {
            string? expectedType = null;
            foreach (var producer in new[] { "status", "typed", "throw" })
            {
                using var response = await client.GetAsync($"{root}/{producer}/{status}");
                using var problem = await AssertProblemAsync(response, status);
                var type = problem.RootElement.GetProperty("type").GetString();
                if (expectedType != null) type.Should().Be(expectedType);
                expectedType = type;
            }
        }

        using var unhandled = await client.GetAsync(root + "/unhandled");
        using var unhandledProblem = await AssertProblemAsync(unhandled, 500);
        using var minimal = await client.GetAsync(root + "/minimal");
        using var minimalProblem = await AssertProblemAsync(minimal, 404);
        using var malformed = await client.PostAsync(root + "/validation", new StringContent("{", Encoding.UTF8, "application/json"));
        using var malformedProblem = await AssertProblemAsync(malformed, 400);
        malformedProblem.RootElement.GetProperty("errors").EnumerateObject().Should().NotBeEmpty();
        AssertCustomization(malformedProblem, "/validation");
        using var invalid = await client.PostAsJsonAsync(root + "/validation", new { Name = "" });
        using var invalidProblem = await AssertProblemAsync(invalid, 400);
        invalidProblem.RootElement.GetProperty("errors").GetProperty("Name").GetArrayLength().Should().BeGreaterThan(0);
        AssertCustomization(invalidProblem, "/validation");
        using var typedValidation = await client.GetAsync(root + "/typed-validation");
        using var typedProblem = await AssertProblemAsync(typedValidation, 400);
        typedProblem.RootElement.GetProperty("errors").GetProperty("Name")[0].GetString().Should().Be("Name is required.");
        AssertCustomization(typedProblem, "/typed-validation");
        using var publicError = await client.GetAsync(root + "/problem");
        using var publicProblem = await AssertProblemAsync(publicError, 401);
        publicProblem.RootElement.GetProperty("detail").GetString().Should().Be("Public message");
        publicProblem.RootElement.GetProperty("code").GetString().Should().Be("invalid_login");
        AssertCustomization(publicProblem, "/problem");
        using var mvcError = await client.GetAsync(root + "/mvc-problem");
        using var mvcProblem = await AssertProblemAsync(mvcError, 401);
        mvcProblem.RootElement.GetProperty("detail").GetString().Should().Be("Public message");
        AssertCustomization(mvcProblem, "/mvc-problem");
    }

    [Theory]
    [IsolatedHostingData(false)]
    [IsolatedHostingData(true)]
    public async Task Auth_Rate_Limits_And_Custom_Responses_Should_Keep_Their_Semantics(DrnTestContext context, bool reject503)
    {
        using var testEnvironment = TestEnvironment.SetTestContextEnabledScope(true);
        Configure(context, "Production");
        context.AddToConfiguration("ApiErrorProbe:Reject503", reject503 ? "true" : "false");
        var application = context.ApplicationContext.CreateApplication<ApiErrorTestProgram>();
        using var client = application.CreateClient(new() { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        var root = ApiErrorTestProgram.Root;

        foreach (var path in new[] { "/challenge", "/protected", "/missing" })
        {
            using var challenged = await client.GetAsync(root + path);
            using var problem = await AssertProblemAsync(challenged, 401);
            challenged.Headers.WwwAuthenticate.Should().Contain(value => value.Scheme == "Bearer");
        }
        using var forbidden = await client.GetAsync(root + "/forbid");
        using var forbiddenProblem = await AssertProblemAsync(forbidden, 403);

        foreach (var (path, seconds, status) in new[] { ("/pre-limit", 7, 429), ("/post-limit", 11, reject503 ? 503 : 429) })
        {
            using var rejected = await client.GetAsync(root + path);
            using var problem = await AssertProblemAsync(rejected, status);
            rejected.Headers.RetryAfter!.Delta.Should().Be(TimeSpan.FromSeconds(seconds));
            rejected.Headers.GetValues("X-Rejection-Callback").Should().Equal("preserved");
        }

        using var browser = await client.GetAsync(root + "/browser");
        browser.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        browser.Headers.Location!.OriginalString.Should().Contain("/Account/Login");
        using var custom = await client.GetAsync(root + "/custom");
        custom.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        custom.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        (await custom.Content.ReadAsStringAsync()).Should().Be("consumer body");
        using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, root + "/status/503"));
        head.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await head.Content.ReadAsByteArrayAsync()).Should().BeEmpty();

        using var tokenResponse = await client.GetAsync(root + "/token");
        tokenResponse.EnsureSuccessStatusCode();
        var token = await tokenResponse.Content.ReadFromJsonAsync<AccessTokenResponse>(new JsonSerializerOptions(JsonConventions.DefaultOptions)
        {
            PropertyNamingPolicy = JsonNamingPolicy.KebabCaseLower
        });
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.AccessToken);
        using var methodRejected = await client.PostAsync(root + "/minimal", null);
        using var methodProblem = await AssertProblemAsync(methodRejected, 405);
        methodRejected.Content.Headers.Allow.Should().Contain("GET");
        using var missing = await client.GetAsync(root + "/missing");
        using var missingProblem = await AssertProblemAsync(missing, 404);
        using var nonApiMissing = await client.GetAsync(root + "ary/missing");
        nonApiMissing.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await nonApiMissing.Content.ReadAsStringAsync()).Should().BeEmpty();
    }

    private static void AssertCustomization(JsonDocument problem, string path)
    {
        problem.RootElement.GetProperty("customizationCount").GetInt32().Should().Be(1);
        problem.RootElement.GetProperty("requestPath").GetString().Should().Be(ApiErrorTestProgram.Root + path);
        var probe = problem.RootElement.GetProperty("serializationProbe");
        probe.GetProperty("displayName").GetString().Should().Be("public");
        probe.GetProperty("largeId").GetString().Should().Be("9223372036854775807");
        probe.GetProperty("kind").GetString().Should().Be("Monday");
    }

    private static async Task<JsonDocument> AssertProblemAsync(HttpResponseMessage response, int status)
    {
        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain(ApiErrorTestProgram.Secret).And.NotContain("<html").And.NotContain("stackTrace");
        return await ProblemDetailsAssertions.ReadProblemAsync(response, status);
    }

    private static void Configure(DrnTestContext context, string environment)
    {
        context.AddToConfiguration("Environment", environment);
        context.AddToConfiguration("AllowedHosts", "localhost");
        context.AddToConfiguration(new { DrnAppFeatures = new { DrnRateLimit = new
        {
            Disabled = false, PreAuthTokenLimit = 1000, PreAuthTokensPerPeriod = 1000,
            PostAuthTokenLimit = 1000, PostAuthTokensPerPeriod = 1000
        } } });
    }
}
