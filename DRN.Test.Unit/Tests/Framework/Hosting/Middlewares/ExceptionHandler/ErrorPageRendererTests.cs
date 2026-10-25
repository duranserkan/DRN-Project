using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Middlewares.ExceptionHandler;

public class ErrorPageRendererTests
{
    [Theory]
    [DataInlineUnit(500)]
    [DataInlineUnit(503)]
    [DataInlineUnit(599)]
    public async Task Server_Error_Page_Should_Use_Configured_Page_And_Preserve_Status(int status)
    {
        var pageUtils = Substitute.For<IPageUtils>();
        var options = Options.Create(new DrnErrorPageOptions { ServerErrorPage = "/Errors/Server" });
        var renderer = new ErrorPageRenderer(pageUtils, options);
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "error-reference",
            Response = { Body = body, StatusCode = status }
        };
        pageUtils.RenderPageAsync("/Pages/Errors/Server.cshtml",
            Arg.Is<ServerErrorPageModel>(model => model.RequestId == "error-reference"), context)
            .Returns("<h1>Configured error page</h1>");

        await renderer.WriteErrorAsync(context);

        context.Response.StatusCode.Should().Be(status);
        context.Response.Headers.Location.ToString().Should().BeEmpty();
        context.Response.ContentType.Should().Be("text/html; charset=utf-8");
        body.Position = 0;
        using var reader = new StreamReader(body);
        (await reader.ReadToEndAsync()).Should().Be("<h1>Configured error page</h1>");
        await pageUtils.Received(1).RenderPageAsync("/Pages/Errors/Server.cshtml",
            Arg.Is<ServerErrorPageModel>(model => model.RequestId == "error-reference"), context);
    }

    [Theory]
    [DataInlineUnit("text/html", false, true)]
    [DataInlineUnit("text/html;q=0", false, false)]
    [DataInlineUnit("application/json", false, false)]
    [DataInlineUnit("*/*", false, false)]
    [DataInlineUnit("text/html", true, false)]
    public void Html_Error_Pages_Should_Require_Html_Accept_And_Exclude_Api_Endpoints(
        string accept, bool api, bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Headers.Accept = accept;
        if (api)
            context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new ApiControllerAttribute()), "API"));

        ErrorPageRenderer.WantsHtml(context).Should().Be(expected);
    }

    [Theory]
    [DataInlineUnit(null, 500, false)]
    [DataInlineUnit("/Errors/Server", 403, false)]
    [DataInlineUnit("/Errors/Server", 500, true)]
    [DataInlineUnit("/Errors/Server", 503, true)]
    [DataInlineUnit("/Errors/Server", 599, true)]
    [DataInlineUnit("/Errors/Server", 600, false)]
    public void Rendering_Should_Be_Opt_In_And_Only_For_Server_Errors(string? page, int status, bool expected)
    {
        var renderer = new ErrorPageRenderer(Substitute.For<IPageUtils>(),
            Options.Create(new DrnErrorPageOptions { ServerErrorPage = page }));
        var context = new DefaultHttpContext();
        context.Response.StatusCode = status;
        context.Request.Headers.Accept = "text/html";

        renderer.CanRender(context).Should().Be(expected);
    }

    [Theory]
    [DataInlineUnit(400, null, "text/html", false, false)]
    [DataInlineUnit(400, "/Errors/BadRequest", "text/html", false, true)]
    [DataInlineUnit(400, "/Errors/BadRequest", "application/json", false, false)]
    [DataInlineUnit(400, "/Errors/BadRequest", "text/html", true, false)]
    [DataInlineUnit(404, null, "text/html", false, false)]
    [DataInlineUnit(404, "/Errors/Missing", "text/html", false, true)]
    [DataInlineUnit(404, "/Errors/Missing", "application/json", false, false)]
    [DataInlineUnit(404, "/Errors/Missing", "text/html", true, false)]
    public void Client_Error_Rendering_Should_Be_Opt_In_And_Exclude_Api_Requests(
        int status, string? page, string accept, bool api, bool expected)
    {
        var renderer = new ErrorPageRenderer(Substitute.For<IPageUtils>(),
            Options.Create(new DrnErrorPageOptions
            {
                BadRequestPage = status == 400 ? page : null,
                NotFoundPage = status == 404 ? page : null
            }));
        var context = new DefaultHttpContext();
        context.Response.StatusCode = status;
        context.Request.Headers.Accept = accept;
        if (api)
            context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new ApiControllerAttribute()), "API"));

        renderer.CanRender(context).Should().Be(expected);
    }

    [Fact]
    public async Task Not_Found_Rendering_Should_Use_Configured_Page_And_Preserve_404()
    {
        var pageUtils = Substitute.For<IPageUtils>();
        var renderer = new ErrorPageRenderer(pageUtils,
            Options.Create(new DrnErrorPageOptions { NotFoundPage = "/Errors/Missing" }));
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "not-found-reference",
            Response = { Body = body, StatusCode = StatusCodes.Status404NotFound }
        };
        pageUtils.RenderPageAsync("/Pages/Errors/Missing.cshtml",
                Arg.Is<NotFoundPageModel>(model => model.RequestId == "not-found-reference"), context)
            .Returns("<h1>Missing page</h1>");

        await renderer.WriteErrorAsync(context);

        context.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        context.Response.Headers.Location.ToString().Should().BeEmpty();
        context.Response.Headers.CacheControl.ToString().Should().Contain("no-store");
        body.Position = 0;
        using var reader = new StreamReader(body);
        (await reader.ReadToEndAsync()).Should().Be("<h1>Missing page</h1>");
    }
}
