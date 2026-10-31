using System.Diagnostics;
using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Middlewares.ApiErrors;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using DRN.Framework.Utils.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Middlewares.ExceptionHandler;

public class DrnExceptionHandlerTests
{
    [Theory]
    [DataInlineUnit(null, false, false)]
    [DataInlineUnit("/Errors/Server", false, true)]
    [DataInlineUnit("/Errors/Server", true, false)]
    public async Task Default_Handler_Should_Use_Configured_Page_Or_Redacted_Fallback(
        string? page, bool renderingFails, bool expectHtml)
    {
        var pageUtils = Substitute.For<IPageUtils>();
        var renderer = new ErrorPageRenderer(pageUtils,
            Options.Create(new DrnErrorPageOptions { ServerErrorPage = page }));
        using var services = new ServiceCollection().AddSingleton(renderer).BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            TraceIdentifier = "safe-reference",
            Response = { Body = body, StatusCode = StatusCodes.Status503ServiceUnavailable }
        };
        context.Request.Headers.Accept = "text/html";
        pageUtils.RenderPageAsync("/Pages/Errors/Server.cshtml", Arg.Any<ServerErrorPageModel>(), context)
            .Returns(_ => renderingFails
                ? Task.FromException<string>(new InvalidOperationException("private-render-error"))
                : Task.FromResult("<h1>Safe error</h1>"));
        var exception = new InvalidOperationException("private-original-error");
        var exceptionUtils = Substitute.For<IExceptionUtils>();
        exceptionUtils.CreateErrorPageModelAsync(context, exception).Returns(new DrnExceptionModel());
        using var diagnosticSource = new DiagnosticListener(nameof(DrnExceptionHandlerTests));
        var handler = new DrnExceptionHandler(Substitute.For<IScopedLog>(), Substitute.For<IAppSettings>(),
            exceptionUtils, [], Substitute.For<IExceptionPageContentProvider>(), diagnosticSource);

        await handler.HandleExceptionAsync(context, exception);

        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.Response.ContentType.Should().Be(expectHtml ? "text/html; charset=utf-8" : "text/plain; charset=utf-8");
        body.Position = 0;
        using var reader = new StreamReader(body);
        (await reader.ReadToEndAsync()).Should().Contain(expectHtml ? "Safe error" : "safe-reference")
            .And.NotContain("private-original-error").And.NotContain("private-render-error");
        await pageUtils.Received(page == null ? 0 : 1).RenderPageAsync(
            Arg.Any<string>(), Arg.Any<ServerErrorPageModel>(), context);
        await exceptionUtils.DidNotReceive().CreateErrorPageModelAsync(context, exception);
    }

    [Theory]
    [DataInlineUnit(false, false)]
    [DataInlineUnit(true, false)]
    [DataInlineUnit(false, true)]
    [DataInlineUnit(true, true)]
    public async Task Problem_Details_Should_Skip_Diagnostics_When_No_Post_Filter_Needs_Them(
        bool development, bool skipPostCreationFilter)
    {
        using var services = new ServiceCollection().AddSingleton(new DrnApiErrorResponse(
            Options.Create(new DrnApiErrorOptions { PathPrefixes = ["/Api"] }))).BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = body, StatusCode = StatusCodes.Status503ServiceUnavailable }
        };
        context.Request.Path = "/Api/test";
        var exception = new InvalidOperationException("private-original-error");
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.IsDevelopmentEnvironment.Returns(development);
        var exceptionUtils = Substitute.For<IExceptionUtils>();
        var filter = Substitute.For<IDrnExceptionFilter>();
        filter.HandlePreExceptionModelCreationAsync(context, exception)
            .Returns(new DrnExceptionFilterResult { SkipPostCreationFilter = true });
        using var diagnosticSource = new DiagnosticListener(nameof(DrnExceptionHandlerTests));
        var handler = new DrnExceptionHandler(Substitute.For<IScopedLog>(), appSettings,
            exceptionUtils, skipPostCreationFilter ? [filter] : [],
            Substitute.For<IExceptionPageContentProvider>(), diagnosticSource);

        await handler.HandleExceptionAsync(context, exception);

        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.Response.ContentType.Should().Be("application/problem+json");
        body.Length.Should().BeGreaterThan(0);
        await exceptionUtils.DidNotReceive().CreateErrorPageModelAsync(context, exception);
        await filter.Received(skipPostCreationFilter ? 1 : 0).HandlePreExceptionModelCreationAsync(context, exception);
        await filter.DidNotReceive().HandleExceptionAsync(context, exception, Arg.Any<DrnExceptionModel>());
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public async Task Problem_Details_Should_Preserve_Filter_Order_And_Populated_Model(bool development)
    {
        using var services = new ServiceCollection().AddSingleton(new DrnApiErrorResponse(
            Options.Create(new DrnApiErrorOptions { PathPrefixes = ["/Api"] }))).BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = body, StatusCode = StatusCodes.Status500InternalServerError }
        };
        context.Request.Path = "/Api/test";
        var exception = new InvalidOperationException("original");
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.IsDevelopmentEnvironment.Returns(development);
        var model = new DrnExceptionModel { Title = "diagnostics" };
        var calls = new List<string>();
        var exceptionUtils = Substitute.For<IExceptionUtils>();
        exceptionUtils.CreateErrorPageModelAsync(context, exception).Returns(_ =>
        {
            calls.Add("model");
            return model;
        });
        var first = Substitute.For<IDrnExceptionFilter>();
        var second = Substitute.For<IDrnExceptionFilter>();
        first.HandlePreExceptionModelCreationAsync(context, exception).Returns(_ =>
        {
            calls.Add("first-pre");
            return DrnExceptionFilterResult.Default;
        });
        second.HandlePreExceptionModelCreationAsync(context, exception).Returns(_ =>
        {
            calls.Add("second-pre");
            return DrnExceptionFilterResult.Default;
        });
        first.HandleExceptionAsync(context, exception, model).Returns(_ =>
        {
            calls.Add("first-post");
            return DrnExceptionFilterResult.Default;
        });
        second.HandleExceptionAsync(context, exception, model).Returns(_ =>
        {
            calls.Add("second-post");
            return DrnExceptionFilterResult.Default;
        });
        using var diagnosticSource = new DiagnosticListener(nameof(DrnExceptionHandlerTests));
        var handler = new DrnExceptionHandler(Substitute.For<IScopedLog>(), appSettings,
            exceptionUtils, [first, second], Substitute.For<IExceptionPageContentProvider>(), diagnosticSource);

        await handler.HandleExceptionAsync(context, exception);

        context.Response.ContentType.Should().Be("application/problem+json");
        calls.Should().Equal("first-pre", "second-pre", "model", "first-post", "second-post");
        await exceptionUtils.Received(1).CreateErrorPageModelAsync(context, exception);
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public async Task Development_Browser_And_Opted_Out_Responses_Should_Keep_Diagnostics(bool disableApiInPreFilter)
    {
        using var services = new ServiceCollection().AddSingleton(new DrnApiErrorResponse(
            Options.Create(new DrnApiErrorOptions { PathPrefixes = ["/Api"] }))).BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = body, StatusCode = StatusCodes.Status500InternalServerError }
        };
        context.Request.Path = disableApiInPreFilter ? "/Api/test" : "/Browser";
        var exception = new InvalidOperationException("original");
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.IsDevelopmentEnvironment.Returns(true);
        var model = new DrnExceptionModel();
        var exceptionUtils = Substitute.For<IExceptionUtils>();
        exceptionUtils.CreateErrorPageModelAsync(context, exception).Returns(model);
        var filter = Substitute.For<IDrnExceptionFilter>();
        filter.HandlePreExceptionModelCreationAsync(context, exception).Returns(_ =>
        {
            context.SetEndpoint(new Endpoint(null,
                new EndpointMetadataCollection(new DisableDrnProblemDetailsAttribute()), "Opted out"));
            return new DrnExceptionFilterResult { SkipPostCreationFilter = true };
        });
        var contentProvider = Substitute.For<IExceptionPageContentProvider>();
        contentProvider.CreateErrorContentResult(context, exception, model)
            .Returns(new ExceptionContentResult("text/html", "development diagnostics"));
        using var diagnosticSource = new DiagnosticListener(nameof(DrnExceptionHandlerTests));
        var handler = new DrnExceptionHandler(Substitute.For<IScopedLog>(), appSettings,
            exceptionUtils, disableApiInPreFilter ? [filter] : [], contentProvider, diagnosticSource);

        await handler.HandleExceptionAsync(context, exception);

        context.Response.ContentType.Should().Be("text/html");
        await exceptionUtils.Received(1).CreateErrorPageModelAsync(context, exception);
        await contentProvider.Received(1).CreateErrorContentResult(context, exception, model);
        await filter.DidNotReceive().HandleExceptionAsync(context, exception, Arg.Any<DrnExceptionModel>());
    }

    [Fact]
    public async Task Explicit_Model_Request_Should_Keep_Diagnostics_In_Production()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var exception = new InvalidOperationException("original");
        var model = new DrnExceptionModel();
        var exceptionUtils = Substitute.For<IExceptionUtils>();
        exceptionUtils.CreateErrorPageModelAsync(Arg.Any<HttpContext>(), exception).Returns(model);
        using var diagnosticSource = new DiagnosticListener(nameof(DrnExceptionHandlerTests));
        var handler = new DrnExceptionHandler(Substitute.For<IScopedLog>(), Substitute.For<IAppSettings>(),
            exceptionUtils, [], Substitute.For<IExceptionPageContentProvider>(), diagnosticSource);

        var result = await handler.GetExceptionPageModel(services, exception);

        result.ExceptionModel.Should().BeSameAs(model);
        await exceptionUtils.Received(1).CreateErrorPageModelAsync(result.HttpContext, exception);
    }

    [Fact]
    public async Task Production_Rendering_Failure_Should_Use_Redacted_Fallback_Without_Retrying_Override()
    {
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "safe-reference",
            Response = { Body = body, StatusCode = StatusCodes.Status503ServiceUnavailable }
        };
        var exception = new InvalidOperationException("private-original-error");
        var exceptionUtils = Substitute.For<IExceptionUtils>();
        exceptionUtils.CreateErrorPageModelAsync(context, exception).Returns(new DrnExceptionModel());
        using var diagnosticSource = new DiagnosticListener(nameof(DrnExceptionHandlerTests));
        var handler = new FailingProductionHandler(exceptionUtils, diagnosticSource);

        await handler.HandleExceptionAsync(context, exception);

        handler.RenderAttempts.Should().Be(1);
        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.Response.ContentType.Should().Be("text/plain; charset=utf-8");
        body.Position = 0;
        using var reader = new StreamReader(body);
        (await reader.ReadToEndAsync()).Should().Contain("safe-reference")
            .And.NotContain("private-original-error").And.NotContain("private-render-error");
    }

    private sealed class FailingProductionHandler(IExceptionUtils exceptionUtils, DiagnosticSource diagnosticSource)
        : DrnExceptionHandler(Substitute.For<IScopedLog>(), Substitute.For<IAppSettings>(), exceptionUtils,
            [], Substitute.For<IExceptionPageContentProvider>(), diagnosticSource)
    {
        public int RenderAttempts { get; private set; }

        protected override Task WriteProductionErrorResponseAsync(HttpContext context)
        {
            RenderAttempts++;
            throw new InvalidOperationException("private-render-error");
        }
    }

    [Fact]
    public async Task HandleExceptionAsync_Should_Not_Throw_When_Request_Is_Aborted_During_Error_Rendering()
    {
        using var requestCancellation = new CancellationTokenSource();
        await requestCancellation.CancelAsync();
        var context = new DefaultHttpContext { RequestAborted = requestCancellation.Token };
        var exception = new InvalidOperationException("original");
        var scopedLog = Substitute.For<IScopedLog>();
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.IsDevelopmentEnvironment.Returns(true);
        var exceptionUtils = Substitute.For<IExceptionUtils>();
        exceptionUtils.CreateErrorPageModelAsync(context, exception)
            .Returns(Task.FromException<DrnExceptionModel>(new OperationCanceledException(requestCancellation.Token)));

        using var diagnosticSource = new DiagnosticListener(nameof(DrnExceptionHandlerTests));
        var handler = new DrnExceptionHandler(
            scopedLog,
            appSettings,
            exceptionUtils,
            [],
            Substitute.For<IExceptionPageContentProvider>(),
            diagnosticSource);

        Func<Task> action = () => handler.HandleExceptionAsync(context, exception);

        await action.Should().NotThrowAsync();
        await exceptionUtils.Received(1).CreateErrorPageModelAsync(context, exception);
    }

    [Fact]
    public async Task HandleExceptionAsync_Should_Not_Throw_When_Request_Is_Aborted_During_Fallback_Write()
    {
        using var requestCancellation = new CancellationTokenSource();
        await using var responseBody = new AbortOnWriteStream(requestCancellation);
        var context = new DefaultHttpContext
        {
            RequestAborted = requestCancellation.Token,
            Response = { Body = responseBody }
        };
        var exception = new InvalidOperationException("original");
        var scopedLog = Substitute.For<IScopedLog>();
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.IsDevelopmentEnvironment.Returns(true);
        var exceptionUtils = Substitute.For<IExceptionUtils>();
        exceptionUtils.CreateErrorPageModelAsync(context, exception)
            .Returns(Task.FromException<DrnExceptionModel>(new InvalidOperationException("rendering")));

        using var diagnosticSource = new DiagnosticListener(nameof(DrnExceptionHandlerTests));
        var handler = new DrnExceptionHandler(
            scopedLog,
            appSettings,
            exceptionUtils,
            [],
            Substitute.For<IExceptionPageContentProvider>(),
            diagnosticSource);

        Func<Task> action = () => handler.HandleExceptionAsync(context, exception);

        await action.Should().NotThrowAsync();
        requestCancellation.IsCancellationRequested.Should().BeTrue();
        await exceptionUtils.Received(1).CreateErrorPageModelAsync(context, exception);
    }
}

public sealed class AbortOnWriteStream(CancellationTokenSource cancellation) : MemoryStream
{
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        cancellation.Cancel();
        return Task.FromCanceled(cancellation.Token);
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellation.Cancel();
        return ValueTask.FromCanceled(cancellation.Token);
    }
}
