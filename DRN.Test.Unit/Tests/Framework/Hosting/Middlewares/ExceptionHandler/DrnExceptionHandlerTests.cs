using System.Diagnostics;
using DRN.Framework.Hosting.Endpoints;
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
