using System.Text.Json;
using DRN.Framework.Hosting.Middlewares.ApiErrors;
using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Options;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Middlewares.ApiErrors;

public class DrnApiErrorResponseTests
{
    [Theory]
    [DataInlineUnit(399, false)]
    [DataInlineUnit(400, true)]
    [DataInlineUnit(409, true)]
    [DataInlineUnit(499, true)]
    [DataInlineUnit(500, true)]
    [DataInlineUnit(599, true)]
    [DataInlineUnit(600, false)]
    public void Supported_Statuses_Should_Cover_All_Client_And_Server_Errors(int status, bool expected) =>
        DrnApiErrorResponse.IsSupportedStatus(status).Should().Be(expected);

    [Theory]
    [DataInlineUnit("/Api/missing", false, false, true)]
    [DataInlineUnit("/api/missing", false, false, true)]
    [DataInlineUnit("/Apiary", false, false, false)]
    [DataInlineUnit("/browser", false, false, false)]
    [DataInlineUnit("/Api/browser", true, false, false)]
    [DataInlineUnit("/outside-prefix", true, true, true)]
    public void Classification_Should_Use_Metadata_Then_Unmatched_Path_Segments(string path, bool matched, bool api, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.PathBase = "/gateway";
        context.Request.Path = path;
        context.Request.Headers.Accept = "application/json";
        if (matched)
            context.SetEndpoint(new Endpoint(null,
                new EndpointMetadataCollection(api ? new object[] { new ApiControllerAttribute() } : Array.Empty<object>()), "probe"));

        CreateResponse().IsApiRequest(context).Should().Be(expected);
    }

    [Theory]
    [DataInlineUnit("/Api/probe", false, 405, "GET", true)]
    [DataInlineUnit("/Apiary/probe", false, 405, "GET", false)]
    [DataInlineUnit("/Api/probe", true, 405, "GET", false)]
    [DataInlineUnit("/Api/probe", false, 404, "GET", false)]
    [DataInlineUnit("/Api/probe", false, 405, "", false)]
    public void Method_Rejections_Should_Use_Prefixes_Without_Reclassifying_Application_Routes(
        string path, bool applicationRoute, int status, string allow, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.StatusCode = status;
        context.Response.Headers.Allow = allow;
        RequestDelegate handler = _ => Task.CompletedTask;
        context.SetEndpoint(applicationRoute
            ? new RouteEndpoint(handler, RoutePatternFactory.Parse(path), 0, EndpointMetadataCollection.Empty, "browser")
            : new Endpoint(handler, EndpointMetadataCollection.Empty, "rejection"));

        CreateResponse().IsApiRequest(context).Should().Be(expected);
    }

    [Theory]
    [DataInlineUnit("/Api", true)]
    [DataInlineUnit("/Api/v1", true)]
    [DataInlineUnit("/", false)]
    [DataInlineUnit("/Api/", false)]
    [DataInlineUnit("/Api/../other", false)]
    [DataInlineUnit("/Api?query", false)]
    [DataInlineUnit("/Api#fragment", false)]
    [DataInlineUnit("Api", false)]
    [DataInlineUnit(null, false)]
    public void Prefixes_Should_Reject_Ambiguous_Configuration(string? prefix, bool expected) =>
        DrnApiErrorOptions.IsValidPrefix(prefix).Should().Be(expected);

    [Theory]
    [DataInlineUnit(400)]
    [DataInlineUnit(401)]
    [DataInlineUnit(403)]
    [DataInlineUnit(404)]
    [DataInlineUnit(429)]
    [DataInlineUnit(500)]
    [DataInlineUnit(502)]
    [DataInlineUnit(503)]
    [DataInlineUnit(504)]
    [DataInlineUnit(599)]
    public async Task Generic_Errors_Should_Have_Consistent_Fields_And_Preserve_Headers(int status)
    {
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { TraceIdentifier = "request-reference", Response = { Body = body, StatusCode = status } };
        context.Request.Path = "/api/probe";
        context.Response.Headers.WWWAuthenticate = "Bearer";
        context.Response.Headers.RetryAfter = "7";
        context.Response.Headers.SetCookie = "probe=value; HttpOnly";

        await DrnApiErrorResponse.WriteAsync(context);

        context.Response.StatusCode.Should().Be(status);
        context.Response.ContentType.Should().Be("application/problem+json");
        context.Response.Headers.WWWAuthenticate.ToString().Should().Be("Bearer");
        context.Response.Headers.RetryAfter.ToString().Should().Be("7");
        context.Response.Headers.SetCookie.ToString().Should().Be("probe=value; HttpOnly");
        using var json = JsonDocument.Parse(body.ToArray());
        json.RootElement.GetProperty("status").GetInt32().Should().Be(status);
        json.RootElement.GetProperty("type").GetString().Should().NotBeNullOrEmpty();
        json.RootElement.GetProperty("title").GetString().Should().NotBeNullOrEmpty();
        json.RootElement.GetProperty("requestId").GetString().Should().Be("request-reference");
        json.RootElement.TryGetProperty("detail", out _).Should().BeFalse();
        json.RootElement.GetProperty("instance").GetString().Should().Be("/api/probe");
    }

    [Fact]
    public async Task Validation_Details_And_Public_Extensions_Should_Survive_Serialization()
    {
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { TraceIdentifier = "request-reference", Response = { Body = body } };
        var problem = new ValidationProblemDetails(new Dictionary<string, string[]> { ["Name"] = ["Name is required."] })
        { Status = 400, Detail = "Public validation detail" };
        problem.Extensions["code"] = "validation_failed";
        problem.Extensions["requestId"] = "incorrect-reference";

        await DrnApiErrorResponse.WriteAsync(context, problem);

        using var json = JsonDocument.Parse(body.ToArray());
        json.RootElement.GetProperty("errors").GetProperty("Name")[0].GetString().Should().Be("Name is required.");
        json.RootElement.GetProperty("detail").GetString().Should().Be("Public validation detail");
        json.RootElement.GetProperty("code").GetString().Should().Be("validation_failed");
        json.RootElement.GetProperty("requestId").GetString().Should().Be("request-reference");
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public async Task Scoped_Defaults_Should_Preserve_Explicit_Values_And_Reserved_Fields(DrnTestContextUnit services, bool explicitValues)
    {
        ScopeContext.InitializeForTest(services);
        ScopeContext.Problem.Type = "/problems/scoped";
        ScopeContext.Problem.Title = "Scoped title";
        ScopeContext.Problem.Detail = "Scoped detail";
        ScopeContext.Problem.Instance = "/scoped";
        ScopeContext.Problem.Extensions["code"] = "scoped_code";
        ScopeContext.Problem.Extensions["status"] = 200;
        ScopeContext.Problem.Extensions["requestId"] = "incorrect";
        ScopeContext.Problem.Extensions["errors"] = "incorrect";
        var problem = new ValidationProblemDetails(new Dictionary<string, string[]> { ["Name"] = ["Required"] })
        {
            Status = 409,
            Type = explicitValues ? "/problems/explicit" : null,
            Title = explicitValues ? "Explicit title" : null,
            Detail = explicitValues ? "Explicit detail" : null,
            Instance = explicitValues ? "/explicit" : null
        };
        if (explicitValues) problem.Extensions["code"] = "explicit_code";
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { TraceIdentifier = "request-reference", Response = { Body = body } };

        await DrnApiErrorResponse.WriteAsync(context, problem);

        using var json = JsonDocument.Parse(body.ToArray());
        var root = json.RootElement;
        root.GetProperty("type").GetString().Should().Be(explicitValues ? "/problems/explicit" : "/problems/scoped");
        root.GetProperty("title").GetString().Should().Be(explicitValues ? "Explicit title" : "Scoped title");
        root.GetProperty("detail").GetString().Should().Be(explicitValues ? "Explicit detail" : "Scoped detail");
        root.GetProperty("instance").GetString().Should().Be(explicitValues ? "/explicit" : "/scoped");
        root.GetProperty("code").GetString().Should().Be(explicitValues ? "explicit_code" : "scoped_code");
        root.GetProperty("status").GetInt32().Should().Be(409);
        root.GetProperty("requestId").GetString().Should().Be("request-reference");
        root.GetProperty("errors").GetProperty("Name")[0].GetString().Should().Be("Required");
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public async Task Unserializable_Extension_Should_Fall_Back_Without_Leaking_Consumer_Details(DrnTestContextUnit services, bool scoped)
    {
        ScopeContext.InitializeForTest(services);
        var log = ScopeContext.Log;
        log.AddException(new InvalidOperationException("Original request failure"));
        var originalExceptionLogs = log.GetLogs()
            .Where(entry => entry.Key.StartsWith("Exception", StringComparison.Ordinal)).ToDictionary();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { TraceIdentifier = "request-reference", Response = { Body = body, StatusCode = 503 } };
        var problem = new ProblemDetails { Status = 503, Detail = "private detail", Title = "private title" };
        problem.Extensions["unsupported"] = typeof(DrnApiErrorResponseTests);
        if (scoped)
        {
            ScopeContext.Problem.Detail = "private detail";
            ScopeContext.Problem.Title = "private title";
            ScopeContext.Problem.Extensions["unsupported"] = typeof(DrnApiErrorResponseTests);
        }

        await DrnApiErrorResponse.WriteAsync(context, scoped ? null : problem);

        using var json = JsonDocument.Parse(body.ToArray());
        json.RootElement.GetProperty("status").GetInt32().Should().Be(503);
        json.RootElement.GetProperty("requestId").GetString().Should().Be("request-reference");
        json.RootElement.ToString().Should().NotContain("private").And.NotContain("unsupported");
        log.HasWarning.Should().BeTrue();
        var logs = log.GetLogs();
        logs[ScopedLogConventions.KeyOfWarningMessage].Should()
            .Be("API Problem Details serialization failed. A generic response was returned.");
        logs.Should().NotContainKey(ScopedLogConventions.KeyOfWarningHasException);
        foreach (var (key, value) in originalExceptionLogs)
            logs[key].Should().Be(value);
    }

    [Theory]
    [DataInlineUnit("head")]
    [DataInlineUnit("cancelled")]
    [DataInlineUnit("started")]
    public async Task Lifecycle_Boundaries_Should_Not_Write_A_Body(string boundary)
    {
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { Response = { Body = body, StatusCode = 503 } };
        if (boundary == "head") context.Request.Method = "HEAD";
        if (boundary == "cancelled") context.RequestAborted = new CancellationToken(true);
        if (boundary == "started")
        {
            var feature = Substitute.For<IHttpResponseFeature>();
            feature.HasStarted.Returns(true);
            context.Features.Set(feature);
        }

        await DrnApiErrorResponse.WriteAsync(context);

        body.Length.Should().Be(0);
        if (boundary == "head")
        {
            context.Response.StatusCode.Should().Be(503);
            context.Response.ContentType.Should().Be("application/problem+json");
        }
    }

    private static DrnApiErrorResponse CreateResponse() => new(
        Options.Create(new DrnApiErrorOptions { PathPrefixes = ["/Api"] }));
}
