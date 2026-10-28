using DRN.Framework.Hosting.Middlewares;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Middlewares;

public class HttpScopeMiddlewareTests
{
    [Theory]
    [DataInlineUnit(false, false, 302, 303)]
    [DataInlineUnit(true, false, 302, 303)]
    [DataInlineUnit(false, true, 302, 303)]
    [DataInlineUnit(true, true, 302, 303)]
    [DataInlineUnit(false, false, 307, 307)]
    [DataInlineUnit(true, false, 308, 308)]
    public async Task Redirect_Should_Be_Normalized_Before_Start_And_Logged_After_Completion(
        DrnTestContextUnit services, bool startInEndpoint, bool explicitCacheControl, int status, int expectedStatus)
    {
        var response = new LifecycleResponseFeature();
        var http = new DefaultHttpContext();
        http.Features.Set<IHttpResponseFeature>(response);
        http.Request.Protocol = "HTTP/1.1";
        http.Request.Path = "/redirect";
        var settings = services.GetRequiredService<IAppSettings>();
        var log = new ScopedLog(settings);
        var logger = Substitute.For<ILogger<HttpScopeMiddleware>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var middleware = new HttpScopeMiddleware(async context =>
        {
            context.Response.StatusCode = status;
            context.Response.Headers.Location = "/destination";
            if (explicitCacheControl)
                context.Response.Headers.CacheControl = "private, max-age=60";
            if (startInEndpoint)
                await response.StartAsync();
        });

        await middleware.InvokeAsync(http, log, Substitute.For<IScopedUser>(), services, logger,
            settings, Substitute.For<IDrnExceptionHandler>());

        logger.ReceivedCalls().Should().NotContain(call => call.GetMethodInfo().Name == "Log");
        if (!startInEndpoint)
        {
            http.Response.StatusCode.Should().Be(status);
            await response.StartAsync();
        }

        http.Response.StatusCode.Should().Be(expectedStatus);
        http.Response.Headers.Location.ToString().Should().Be("/destination");
        http.Response.Headers.CacheControl.ToString().Should().Be(explicitCacheControl
            ? "private, max-age=60"
            : "no-store, no-cache, must-revalidate");
        Action changeStatusAfterStart = () => http.Response.StatusCode = StatusCodes.Status200OK;
        changeStatusAfterStart.Should().Throw<InvalidOperationException>();
        await response.CompleteAsync();

        log.GetLogs()["Response_StatusCode"].Should().Be(expectedStatus);
        logger.ReceivedCalls().Count(call => call.GetMethodInfo().Name == "Log").Should().Be(1);
    }

    [Theory]
    [DataInlineUnit]
    public async Task Aborted_Request_Should_Log_Without_Completion_And_Not_Log_Twice(DrnTestContextUnit services)
    {
        using var cancellation = new CancellationTokenSource();
        var response = new LifecycleResponseFeature();
        var http = new DefaultHttpContext { RequestAborted = cancellation.Token };
        http.Features.Set<IHttpResponseFeature>(response);
        http.Request.Protocol = "HTTP/1.1";
        http.Request.Path = "/aborted";
        var settings = services.GetRequiredService<IAppSettings>();
        var log = new ScopedLog(settings);
        var logger = Substitute.For<ILogger<HttpScopeMiddleware>>();
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);
        var middleware = new HttpScopeMiddleware(context =>
        {
            context.Response.StatusCode = 400;
            cancellation.Cancel();
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(http, log, Substitute.For<IScopedUser>(), services, logger,
            settings, Substitute.For<IDrnExceptionHandler>());

        log.GetLogs()["Response_StatusCode"].Should().Be(400);
        logger.ReceivedCalls().Count(call => call.GetMethodInfo().Name == "Log").Should().Be(1);
        await response.CompleteAsync();
        logger.ReceivedCalls().Count(call => call.GetMethodInfo().Name == "Log").Should().Be(1);
    }

    [Theory]
    [DataInlineUnit]
    public async Task Started_Response_Failure_Should_Preserve_Status_And_Propagate_Original_Exception(DrnTestContextUnit services)
    {
        var response = new LifecycleResponseFeature();
        var http = new DefaultHttpContext();
        http.Features.Set<IHttpResponseFeature>(response);
        http.Request.Protocol = "HTTP/1.1";
        http.Request.Path = "/started";
        var settings = services.GetRequiredService<IAppSettings>();
        var log = new ScopedLog(settings);
        var exceptionHandler = Substitute.For<IDrnExceptionHandler>();
        var original = new InvalidOperationException("Original streaming failure");
        var middleware = new HttpScopeMiddleware(async context =>
        {
            context.Response.StatusCode = 202;
            await response.StartAsync();
            throw original;
        });

        Func<Task> invoke = () => middleware.InvokeAsync(http, log, Substitute.For<IScopedUser>(), services,
            Substitute.For<ILogger<HttpScopeMiddleware>>(), settings, exceptionHandler);

        (await invoke.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(original);
        http.Response.StatusCode.Should().Be(202);
        await exceptionHandler.DidNotReceive().HandleExceptionAsync(Arg.Any<HttpContext>(), Arg.Any<Exception>());
    }

    // Model server callback ordering and reject status changes after headers are sent.
    private sealed class LifecycleResponseFeature : HttpResponseFeature, IHttpResponseFeature
    {
        private readonly Stack<(Func<object, Task> Callback, object State)> _starting = new();
        private readonly Stack<(Func<object, Task> Callback, object State)> _completed = new();
        private bool _started;

        public override bool HasStarted => _started;
        // The base property is not virtual, so enforce the guard through the feature contract.
        int IHttpResponseFeature.StatusCode
        {
            get => base.StatusCode;
            set
            {
                if (_started)
                    throw new InvalidOperationException("Response already started.");
                base.StatusCode = value;
            }
        }

        public override void OnStarting(Func<object, Task> callback, object state) => _starting.Push((callback, state));
        public override void OnCompleted(Func<object, Task> callback, object state) => _completed.Push((callback, state));

        public async Task StartAsync()
        {
            while (_starting.TryPop(out var entry))
                await entry.Callback(entry.State);
            _started = true;
        }

        public async Task CompleteAsync()
        {
            while (_completed.TryPop(out var entry))
                await entry.Callback(entry.State);
        }
    }
}
