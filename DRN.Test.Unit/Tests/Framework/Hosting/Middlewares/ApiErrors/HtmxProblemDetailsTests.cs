using System.Text.Json;
using DRN.Framework.Hosting.Middlewares.ApiErrors;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Middlewares.ApiErrors;

public class HtmxProblemDetailsTests
{
    [Theory]
    [DataInlineUnit(true, "true", false, 400, true)]
    [DataInlineUnit(false, "true", false, 400, false)]
    [DataInlineUnit(true, "", false, 400, false)]
    [DataInlineUnit(true, "false", false, 400, false)]
    [DataInlineUnit(true, "True", false, 400, false)]
    [DataInlineUnit(true, "true,true", false, 400, false)]
    [DataInlineUnit(true, "true", true, 400, false)]
    [DataInlineUnit(true, "true", false, 399, false)]
    [DataInlineUnit(true, "true", false, 599, true)]
    [DataInlineUnit(true, "true", false, 600, false)]
    public async Task Htmx_Formatting_Should_Be_Opt_In_Without_Reclassifying_Browser_Endpoints(
        bool enabled, string header, bool disabled, int status, bool expected)
    {
        await using var body = new MemoryStream();
        var context = BrowserRequest(body, header);
        context.Response.StatusCode = status;
        if (disabled)
            context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new DisableDrnProblemDetailsAttribute()), "disabled"));
        var response = CreateResponse(enabled);

        response.IsApiRequest(context).Should().BeFalse();
        (await response.TryWriteAsync(context)).Should().Be(expected);

        context.Response.StatusCode.Should().Be(status);
        context.Response.Headers.WWWAuthenticate.ToString().Should().Be("Bearer");
        context.Response.Headers.RetryAfter.ToString().Should().Be("7");
        if (!expected)
        {
            body.Length.Should().Be(0);
            context.Response.ContentType.Should().BeNull();
            return;
        }

        context.Response.ContentType.Should().Be("application/problem+json");
        using var json = JsonDocument.Parse(body.ToArray());
        json.RootElement.GetProperty("status").GetInt32().Should().Be(status);
        json.RootElement.GetProperty("requestId").GetString().Should().Be("partial-request");
        json.RootElement.GetProperty("instance").GetString().Should().Be("/gateway/browser/partial");
        json.RootElement.TryGetProperty("detail", out _).Should().BeFalse();
        json.RootElement.ToString().Should().NotContain("private-query");
    }

    [Fact]
    public void Multiple_Htmx_Header_Values_Should_Not_Enable_Problem_Formatting()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers["HX-Request"] = new StringValues(["true", "true"]);
        context.Response.StatusCode = 500;

        CreateResponse(true).CanHandle(context).Should().BeFalse();
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public async Task Api_Error_Formatting_Should_Not_Depend_On_Htmx_Options(bool enabled)
    {
        await using var body = new MemoryStream();
        var context = BrowserRequest(body, "false");
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new ApiControllerAttribute()), "api"));
        context.Response.StatusCode = 401;
        var response = CreateResponse(enabled);

        response.IsApiRequest(context).Should().BeTrue();
        (await response.TryWriteAsync(context)).Should().BeTrue();
        context.Response.ContentType.Should().Be("application/problem+json");
        context.Response.StatusCode.Should().Be(401);
        context.Response.Headers.WWWAuthenticate.ToString().Should().Be("Bearer");
    }

    [Theory]
    [DataInlineUnit(400)]
    [DataInlineUnit(422)]
    public async Task Htmx_Problem_Result_Should_Keep_Validation_And_Use_The_Problem_Status(int status)
    {
        await using var body = new MemoryStream();
        var context = BrowserRequest(body, "true");
        var problem = new ValidationProblemDetails(new Dictionary<string, string[]>
        {
            ["Input.Name"] = ["Enter a name."],
            [string.Empty] = ["Review the form."]
        })
        {
            Status = status,
            Detail = "Correct the highlighted fields."
        };
        var result = new ObjectResult(problem);
        var action = new ActionContext(context, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        var executing = new ResultExecutingContext(action, [], result, new object());

        new DrnApiProblemDetailsFilter(CreateResponse(true)).OnResultExecuting(executing);
        executing.Result.Should().NotBeSameAs(result);
        await executing.Result.ExecuteResultAsync(action);

        context.Response.StatusCode.Should().Be(status);
        context.Response.ContentType.Should().Be("application/problem+json");
        using var json = JsonDocument.Parse(body.ToArray());
        json.RootElement.GetProperty("errors").GetProperty("Input.Name")[0].GetString().Should().Be("Enter a name.");
        json.RootElement.GetProperty("errors").GetProperty(string.Empty)[0].GetString().Should().Be("Review the form.");
        json.RootElement.GetProperty("detail").GetString().Should().Be("Correct the highlighted fields.");
        json.RootElement.GetProperty("requestId").GetString().Should().Be("partial-request");
    }

    [Fact]
    public void Htmx_Filter_Should_Preserve_Explicit_Html_And_Custom_Json_Bodies()
    {
        var context = BrowserRequest(Stream.Null, "true");
        var action = new ActionContext(context, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        IActionResult[] results =
        [
            new ContentResult { StatusCode = 422, ContentType = "text/html", Content = "<p>Correct the form.</p>" },
            new ObjectResult(new { message = "Custom error contract" }) { StatusCode = 409 }
        ];
        var filter = new DrnApiProblemDetailsFilter(CreateResponse(true));

        foreach (var result in results)
        {
            var executing = new ResultExecutingContext(action, [], result, new object());
            filter.OnResultExecuting(executing);
            executing.Result.Should().BeSameAs(result);
        }
    }

    private static DefaultHttpContext BrowserRequest(Stream body, string header)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "partial-request", Response = { Body = body } };
        context.Request.PathBase = "/gateway";
        context.Request.Path = "/browser/partial";
        context.Request.QueryString = new QueryString("?private-query=secret");
        context.Request.Headers.Accept = "text/html";
        context.Request.Headers["HX-Request"] = header;
        context.SetEndpoint(new Endpoint(null, EndpointMetadataCollection.Empty, "browser"));
        context.Response.Headers.WWWAuthenticate = "Bearer";
        context.Response.Headers.RetryAfter = "7";
        return context;
    }

    private static DrnApiErrorResponse CreateResponse(bool enabled) => new(
        Options.Create(new DrnApiErrorOptions { HandleHtmxRequests = enabled }));
}
