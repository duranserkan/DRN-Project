using System.Net;
using System.Text.Json;
using DRN.Test.Utils.Hosting;
using DRN.Test.Utils.Hosting.Auth;
using Swashbuckle.AspNetCore.SwaggerUI;

namespace DRN.Test.Integration.Tests.Framework.Hosting;

public class SwaggerUiIntegrationTests
{
    [Theory]
    [DataInline("", false)]
    [DataInline("/gateway", false)]
    [DataInline("/gateway", true)]
    public async Task UI_Should_Resolve_Custom_Document_And_Preserve_Authorization_And_PathBase(
        DrnTestContext context, string pathBase, bool namedPolicy)
    {
        context.AddToConfiguration("DrnDevelopmentSettings:SkipValidation", "true");
        context.AddToConfiguration(SwaggerUiTestProgram.NamedPolicyKey, namedPolicy.ToString());
        using var client = await context.ApplicationContext.CreateClientAsync<SwaggerUiTestProgram>(
            clientOptions: new() { AllowAutoRedirect = false });
        var paths = new[] { "/docs/swagger/index.html", "/docs/swagger/openapi-document.json", "/specs/v2/openapi.json" };
        foreach (var path in paths)
        {
            using var anonymous = await client.GetAsync(pathBase + path);
            anonymous.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        client.DefaultRequestHeaders.Add(NonDefaultExemptValues.CookieHeader, "password");
        foreach (var path in paths)
        {
            using var passwordOnly = await client.GetAsync(pathBase + path);
            passwordOnly.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        client.DefaultRequestHeaders.Remove(NonDefaultExemptValues.CookieHeader);
        client.DefaultRequestHeaders.Add(NonDefaultExemptValues.CookieHeader, "mfa");

        using var redirect = await client.GetAsync(pathBase + paths[1]);
        redirect.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        redirect.Headers.Location!.OriginalString.Should().Be(pathBase + paths[2]);
        using var document = await client.GetAsync(redirect.Headers.Location);
        document.EnsureSuccessStatusCode();
        using var json = JsonDocument.Parse(await document.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("info").GetProperty("title").GetString().Should().Be("Generator API");

        var apiPaths = json.RootElement.GetProperty("paths");
        var queryRequestDescription = apiPaths.GetProperty("/swagger-pagination/query-request").GetProperty("get")
            .GetProperty("description").GetString();
        queryRequestDescription.Should().Contain("flattened query fields").And.Contain("cursor's previous-page number")
            .And.NotContain("10 pages").And.NotContain("ceiling of 100");
        apiPaths.GetProperty("/swagger-pagination/post-query-request").GetProperty("post")
            .GetProperty("description").GetString().Should().Be(queryRequestDescription);
        var queryInfo = apiPaths.GetProperty("/swagger-pagination/query-info").GetProperty("get");
        queryInfo.GetProperty("description").GetString().Should().Contain("previous page's pagination metadata")
            .And.Contain("outer first and last IDs").And.NotContain("10 pages");
        queryInfo.GetProperty("parameters").EnumerateArray().Select(parameter => parameter.GetProperty("name").GetString())
            .Should().Contain("previous.FirstId").And.Contain("previous.Request.PageNumber")
            .And.NotContain("FirstId").And.NotContain("previous.previous.FirstId");
        var bodyInfoDescription = apiPaths.GetProperty("/swagger-pagination/body-info").GetProperty("post")
            .GetProperty("description").GetString();
        bodyInfoDescription.Should().StartWith("Application continuation policy.")
            .And.Contain("metadata object as the request body").And.Contain("`destination` in the query string")
            .And.NotContain("jumpTo").And.NotContain("10 pages");
        apiPaths.GetProperty("/swagger-pagination/body-request").GetProperty("post")
            .GetProperty("description").GetString().Should().Contain("pagination settings as the request body")
            .And.Contain("required fields");
        apiPaths.GetProperty("/swagger-pagination/result").GetProperty("get")
            .GetProperty("description").GetString().Should().Contain("response contains page items and pagination metadata")
            .And.Contain("outer boundary IDs");
        apiPaths.GetProperty("/swagger-pagination/unrelated").GetProperty("get")
            .GetProperty("description").GetString().Should().Be("Unrelated operation.");

        foreach (var (route, allowsNull) in new[] { ("nullable", true), ("required", false), ("required-nullable", false) })
        {
            var endpoint = "/swagger-int64/" + route;
            var alternatives = apiPaths.GetProperty(endpoint).GetProperty("post").GetProperty("requestBody")
                .GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("oneOf");
            alternatives.EnumerateArray().Any(schema => schema.GetProperty("type").GetString() == "null")
                .Should().Be(allowsNull);
            using var body = new StringContent("null", System.Text.Encoding.UTF8, "application/json");
            using var result = await client.PostAsync(pathBase + endpoint, body);
            result.StatusCode.Should().Be(allowsNull ? HttpStatusCode.OK : HttpStatusCode.BadRequest);

            using var validBody = new StringContent("42", System.Text.Encoding.UTF8, "application/json");
            using var validResult = await client.PostAsync(pathBase + endpoint, validBody);
            validResult.StatusCode.Should().Be(HttpStatusCode.OK);
            (await validResult.Content.ReadAsStringAsync()).Should().Be("42");
        }

        var page = new Uri(client.BaseAddress!, pathBase + paths[0]);
        using var ui = await client.GetAsync(page);
        ui.EnsureSuccessStatusCode();
        (await ui.Content.ReadAsStringAsync()).Should().Contain("Generator API (v2)")
            .And.Contain("<link rel=\"icon\" href=\"../../images/icon.svg\">");
        new Uri(page, "../../images/icon.svg").AbsolutePath.Should().Be(pathBase + "/images/icon.svg");
        ui.Headers.CacheControl!.MaxAge.Should().Be(new SwaggerUIOptions().CacheLifetime);
        ui.Headers.CacheControl.NoStore.Should().BeFalse();
        using var conditionalRequest = new HttpRequestMessage(HttpMethod.Get, page);
        conditionalRequest.Headers.IfNoneMatch.Add(ui.Headers.ETag!);
        using var cached = await client.SendAsync(conditionalRequest);
        cached.StatusCode.Should().Be(HttpStatusCode.NotModified);
        using var initialization = await client.GetAsync(pathBase + "/docs/swagger/index.js");
        initialization.EnsureSuccessStatusCode();
        (await initialization.Content.ReadAsStringAsync()).Should().Contain("./openapi-document.json");
    }

    [Theory]
    [DataInline]
    public async Task Disabled_UI_Should_Keep_The_Document_Endpoint(DrnTestContext context)
    {
        context.AddToConfiguration("DrnDevelopmentSettings:SkipValidation", "true");
        context.AddToConfiguration(SwaggerUiTestProgram.UiEnabledKey, "false");
        using var client = await context.ApplicationContext.CreateClientAsync<SwaggerUiTestProgram>();
        client.DefaultRequestHeaders.Add(NonDefaultExemptValues.CookieHeader, "mfa");
        using var ui = await client.GetAsync("/docs/swagger/index.html");
        ui.StatusCode.Should().Be(HttpStatusCode.NotFound);
        ui.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("script-src 'nonce-");
        using var document = await client.GetAsync("/specs/v2/openapi.json");
        document.EnsureSuccessStatusCode();
    }
}
