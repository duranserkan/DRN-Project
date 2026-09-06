using System.Net;
using DRN.Framework.Hosting.Endpoints;
using DRN.Test.Utils.Hosting;

namespace DRN.Test.Integration.Tests.Framework.Hosting;

public class CspIntegrationTests
{
    [Theory]
    [DataInline("/swagger/index.html", null, true)]
    [DataInline("/Swagger/index.html", null, true)]
    [DataInline("/swagger-ui", null, false)]
    [DataInline("/docs/swagger", null, false)]
    [DataInline("/", null, false)]
    [DataInline("/", CspFor.CspPolicySelf, false)]
    [DataInline("/", CspFor.CspPolicyInline, false)]
    [DataInline("/", "unknown-policy", false)]
    [DataInline("/", "", false)]
    [DataInline("/", CspFor.CspPolicySwagger, false)]
    [DataInline("/swagger/policy-probe", CspFor.CspPolicyInline, true)]
    public async Task Security_Policies_Should_Select_Default_Named_And_Swagger_Policies(
        DrnTestContext context, string path, string? policyName, bool isSwagger)
    {
        context.AddToConfiguration("DrnDevelopmentSettings:SkipValidation", "true");
        using var client = await context.ApplicationContext.CreateClientAsync<CspTestProgram>();
        var requestPath = policyName == null ? path : $"{path}?policy={policyName}";
        using var response = await client.GetAsync(requestPath);
        response.EnsureSuccessStatusCode();
        var csp = response.Headers.GetValues("Content-Security-Policy").Single();
        var styleSrc = csp.Split(';', StringSplitOptions.TrimEntries).Single(value => value.StartsWith("style-src "));
        var scriptSrc = csp.Split(';', StringSplitOptions.TrimEntries).Single(value => value.StartsWith("script-src "));
        csp.Split(';', StringSplitOptions.TrimEntries).Should().Contain("default-src 'none'")
            .And.Contain("object-src 'none'").And.Contain("script-src-attr 'none'");

        if (isSwagger)
            styleSrc.Should().Be("style-src 'self' 'unsafe-inline'");
        else
            styleSrc.Should().Contain("'self'").And.Contain("'nonce-").And.NotContain("'unsafe-inline'");

        if (isSwagger || policyName == CspFor.CspPolicySelf)
            scriptSrc.Should().Be("script-src 'self'");
        else if (policyName == CspFor.CspPolicyInline)
            scriptSrc.Should().Be("script-src 'self' 'unsafe-inline'");
        else
        {
            scriptSrc.Should().StartWith("script-src 'nonce-").And.NotContain("'self'").And.NotContain("'unsafe-inline'");
            var nonceSource = scriptSrc["script-src ".Length..];
            styleSrc.Should().Contain(nonceSource);
        }
    }

    [Theory]
    [DataInline]
    public async Task Named_Swagger_Policy_Replacement_Should_Not_Affect_Other_Policies(DrnTestContext context)
    {
        context.AddToConfiguration("DrnDevelopmentSettings:SkipValidation", "true");
        context.AddToConfiguration(CspTestProgram.ReplaceSwaggerPolicyKey, "true");
        using var client = await context.ApplicationContext.CreateClientAsync<CspTestProgram>();

        using var swagger = await client.GetAsync("/swagger/index.html");
        swagger.EnsureSuccessStatusCode();
        swagger.Headers.GetValues("Content-Security-Policy").Single()
            .Split(';', StringSplitOptions.TrimEntries).Should().Contain("script-src 'none'");

        using var self = await client.GetAsync($"/?policy={CspFor.CspPolicySelf}");
        self.EnsureSuccessStatusCode();
        self.Headers.GetValues("Content-Security-Policy").Single()
            .Split(';', StringSplitOptions.TrimEntries).Should().Contain("script-src 'self'");

        using var inline = await client.GetAsync($"/?policy={CspFor.CspPolicyInline}");
        inline.EnsureSuccessStatusCode();
        inline.Headers.GetValues("Content-Security-Policy").Single()
            .Split(';', StringSplitOptions.TrimEntries).Should().Contain("script-src 'self' 'unsafe-inline'");

        using var fallback = await client.GetAsync("/");
        fallback.EnsureSuccessStatusCode();
        fallback.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("script-src 'nonce-");
    }

    [Theory]
    [DataInline("swagger")]
    [DataInline("docs/swagger")]
    [DataInline("swagger-ui")]
    [DataInline("api-docs")]
    public async Task Swagger_Document_Should_Allow_Inline_Styles_And_Preserve_Caching(DrnTestContext context, string routePrefix)
    {
        context.AddToConfiguration("DrnDevelopmentSettings:SkipValidation", "true");
        context.AddToConfiguration(CspTestProgram.SwaggerRoutePrefixKey, routePrefix);
        using var client = await context.ApplicationContext.CreateClientAsync<CspTestProgram>();
        var documentPath = $"/{routePrefix}/index.html";
        using var first = await client.GetAsync(documentPath);
        first.EnsureSuccessStatusCode();
        var html = await first.Content.ReadAsStringAsync();
        var csp = first.Headers.GetValues("Content-Security-Policy").Single();
        csp.Split(';', StringSplitOptions.TrimEntries).Should().Contain("style-src 'self' 'unsafe-inline'")
            .And.Contain("script-src 'self'");
        html.Should().Contain("Custom API docs").And.Contain("content=\"preserved\"")
            .And.Contain("swagger-ui-bundle.js").And.NotContain("nonce=").And.NotContain("Reflect.apply");
        first.Headers.CacheControl!.MaxAge.Should().Be(TimeSpan.FromHours(1));
        first.Headers.CacheControl.NoStore.Should().BeFalse();

        using var request = new HttpRequestMessage(HttpMethod.Get, documentPath);
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);
        using var second = await client.SendAsync(request);
        second.StatusCode.Should().Be(HttpStatusCode.NotModified);
        second.Headers.GetValues("Content-Security-Policy").Single().Should().Be(csp);

        const string applicationPath = "/application-page";
        using var fallback = await client.GetAsync(applicationPath);
        fallback.EnsureSuccessStatusCode();
        fallback.Headers.GetValues("Content-Security-Policy").Single().Should()
            .Contain("script-src 'nonce-").And.Contain("style-src 'self' 'nonce-");

        using var inline = await client.GetAsync($"{applicationPath}?policy={CspFor.CspPolicyInline}");
        inline.EnsureSuccessStatusCode();
        inline.Headers.GetValues("Content-Security-Policy").Single().Should()
            .Contain("script-src 'self' 'unsafe-inline'").And.Contain("style-src 'self' 'nonce-");

        if (routePrefix.Length > 0)
        {
            using var outside = await client.GetAsync($"/{routePrefix}-other");
            outside.EnsureSuccessStatusCode();
            outside.Headers.GetValues("Content-Security-Policy").Single().Should().Contain("script-src 'nonce-");
        }
    }

    [Theory]
    [DataInline("/swagger/index.html")]
    [DataInline("/docs/swagger/index.html")]
    public async Task Disabled_Swagger_Should_Retain_Default_Csp(DrnTestContext context, string path)
    {
        context.AddToConfiguration("DrnDevelopmentSettings:SkipValidation", "true");
        context.AddToConfiguration(CspTestProgram.DisableSwaggerKey, "true");
        using var client = await context.ApplicationContext.CreateClientAsync<CspTestProgram>();
        using var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        response.Headers.GetValues("Content-Security-Policy").Single().Should()
            .Contain("script-src 'nonce-").And.Contain("style-src 'self' 'nonce-");
    }
}
