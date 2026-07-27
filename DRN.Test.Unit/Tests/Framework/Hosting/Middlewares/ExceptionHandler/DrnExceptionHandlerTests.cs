using System.Diagnostics;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using DRN.Framework.Utils.Logging;
using Microsoft.AspNetCore.Http;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Middlewares.ExceptionHandler;

public class DrnExceptionHandlerTests
{
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
