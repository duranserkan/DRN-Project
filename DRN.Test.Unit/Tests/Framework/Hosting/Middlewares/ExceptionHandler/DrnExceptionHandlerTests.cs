using DRN.Framework.Utils.Logging;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using System.Diagnostics;
using DRN.Framework.Hosting;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Middlewares.ApiErrors;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
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
            Options.Create(new DrnPageOptions { ServerErrorPage = page }));
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
        context.User = DiagnosticsUser();
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
        using var services = DiagnosticsServices().AddSingleton(new DrnApiErrorResponse(
            Options.Create(new DrnApiErrorOptions { PathPrefixes = ["/Api"] }))).BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = body, StatusCode = StatusCodes.Status500InternalServerError }
        };
        context.Request.Path = disableApiInPreFilter ? "/Api/test" : "/Browser";
        context.Response.Headers.CacheControl = "public, max-age=600";
        context.User = DiagnosticsUser();
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute()), "Public"));
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
        context.Response.Headers.CacheControl.ToString().Should().Be("no-store");
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

    [Theory]
    [DataInlineUnit(false, false)]
    [DataInlineUnit(true, false)]
    [DataInlineUnit(false, true)]
    public async Task Rendering_Failure_Should_Withhold_Diagnostic_Logs_Without_Viewer_Access(bool development, bool hasViewerGrant)
    {
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "safe-reference",
            Response = { Body = body, StatusCode = StatusCodes.Status503ServiceUnavailable }
        };
        if (hasViewerGrant) context.User = DiagnosticsUser();
        var exception = new InvalidOperationException("private-original-error");
        var exceptionUtils = Substitute.For<IExceptionUtils>();
        exceptionUtils.CreateErrorPageModelAsync(context, exception).Returns(new DrnExceptionModel());
        using var diagnosticSource = new DiagnosticListener(nameof(DrnExceptionHandlerTests));
        var settings = Substitute.For<IAppSettings>();
        settings.IsDevelopmentEnvironment.Returns(development);
        var handler = new FailingProductionHandler(settings, exceptionUtils, diagnosticSource);

        await handler.HandleExceptionAsync(context, exception);

        handler.RenderAttempts.Should().Be(1);
        await exceptionUtils.DidNotReceive().CreateErrorPageModelAsync(context, exception);
        context.Response.StatusCode.Should().Be(StatusCodes.Status503ServiceUnavailable);
        context.Response.ContentType.Should().Be("text/plain; charset=utf-8");
        body.Position = 0;
        using var reader = new StreamReader(body);
        (await reader.ReadToEndAsync()).Should().Contain("safe-reference")
            .And.NotContain("private-original-error").And.NotContain("private-render-error");
    }

    private sealed class FailingProductionHandler(IAppSettings settings, IExceptionUtils exceptionUtils, DiagnosticSource diagnosticSource)
        : DrnExceptionHandler(Substitute.For<IScopedLog>(), settings, exceptionUtils,
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
        using var services = DiagnosticsServices().BuildServiceProvider();
        using var requestCancellation = new CancellationTokenSource();
        await requestCancellation.CancelAsync();
        var context = new DefaultHttpContext { RequestServices = services, RequestAborted = requestCancellation.Token, User = DiagnosticsUser() };
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
        using var services = DiagnosticsServices().BuildServiceProvider();
        using var requestCancellation = new CancellationTokenSource();
        await using var responseBody = new AbortOnWriteStream(requestCancellation);
        var context = new DefaultHttpContext
        {
            RequestAborted = requestCancellation.Token,
            RequestServices = services,
            User = DiagnosticsUser(),
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

    [Fact]
    public async Task Diagnostic_Json_Fallback_Should_Override_Public_Cache_Control()
    {
        using var services = DiagnosticsServices().BuildServiceProvider();
        using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services, User = DiagnosticsUser(), Response = { Body = body, StatusCode = 500 } };
        context.Response.Headers.CacheControl = "public, max-age=600";
        var exception = new InvalidOperationException("original");
        var model = new DrnExceptionModel();
        var utils = Substitute.For<IExceptionUtils>();
        utils.CreateErrorPageModelAsync(context, exception).Returns(model);
        var renderer = Substitute.For<IExceptionPageContentProvider>();
        renderer.CreateErrorContentResult(context, exception, model)
            .Returns(Task.FromException<ExceptionContentResult>(new InvalidOperationException("rendering")));
        var settings = Substitute.For<IAppSettings>();
        settings.IsDevelopmentEnvironment.Returns(true);
        var log = Substitute.For<IScopedLog>();
        log.GetLogs().Returns(new Dictionary<string, object> { ["Diagnostic"] = "fallback-probe" });
        using var source = new DiagnosticListener(nameof(DrnExceptionHandlerTests));
        var handler = new DrnExceptionHandler(log, settings, utils, [], renderer, source);

        await handler.HandleExceptionAsync(context, exception);

        context.Response.Headers.CacheControl.ToString().Should().Be("no-store");
        context.Response.ContentType.Should().StartWith("application/json");
        body.Position = 0;
        using var reader = new StreamReader(body);
        (await reader.ReadToEndAsync()).Should().Contain("fallback-probe");
    }

    private static ClaimsPrincipal DiagnosticsUser() => new(new ClaimsIdentity(
        [new Claim("amr", "mfa"), new Claim("TestPagesAccess", "true")], "Test"));

    private static IServiceCollection DiagnosticsServices() =>
        new ServiceCollection().AddLogging().AddAuthorizationCore(options =>
            options.AddPolicy(DeveloperExceptionAccess.Policy, policy => policy.RequireAuthenticatedUser()));

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public async Task Host_Policy_Denial_Or_Failure_Should_Withhold_Model_And_Fallback_Logs(bool policyThrows)
    {
        var registrations = DiagnosticsServices();
        registrations.Configure<AuthorizationOptions>(options => options.AddPolicy(DeveloperExceptionAccess.Policy, policy =>
        {
            policy.RequireClaim("HostDiagnosticAccess");
            if (policyThrows)
                policy.RequireAssertion(_ => Task.FromException<bool>(new InvalidOperationException("policy unavailable")));
        }));
        using var services = registrations.BuildServiceProvider();
        using var body = new MemoryStream();
        var context = new DefaultHttpContext
        {
            RequestServices = services, User = DiagnosticsUser(), TraceIdentifier = "safe-reference",
            Response = { Body = body, StatusCode = StatusCodes.Status503ServiceUnavailable }
        };
        var settings = Substitute.For<IAppSettings>();
        settings.IsDevelopmentEnvironment.Returns(true);
        var utils = Substitute.For<IExceptionUtils>();
        using var source = new DiagnosticListener(nameof(DrnExceptionHandlerTests));
        var handler = new FailingProductionHandler(settings, utils, source);
        var exception = new InvalidOperationException("private");

        await handler.HandleExceptionAsync(context, exception);

        await utils.DidNotReceive().CreateErrorPageModelAsync(context, exception);
        handler.RenderAttempts.Should().Be(1);
        context.Response.ContentType.Should().Be("text/plain; charset=utf-8");
        body.Position = 0;
        using var reader = new StreamReader(body);
        (await reader.ReadToEndAsync()).Should().Be("ServiceUnavailable 503 TraceId: safe-reference");
    }

    [Theory]
    [DataInlineUnit(false)]
    [DataInlineUnit(true)]
    public async Task Diagnostics_Should_Use_The_Policy_Scheme_And_Preserve_The_Request_User(bool authenticatedByScheme)
    {
        const string scheme = "Diagnostics";
        var authentication = Substitute.For<IAuthenticationService>();
        var registrations = DiagnosticsServices().AddSingleton(authentication);
        registrations.Configure<AuthorizationOptions>(options => options.AddPolicy(DeveloperExceptionAccess.Policy,
            policy => policy.AddAuthenticationSchemes(scheme).RequireAuthenticatedUser()));
        using var services = registrations.BuildServiceProvider();
        var original = DiagnosticsUser();
        var context = new DefaultHttpContext { RequestServices = services, User = original };
        authentication.AuthenticateAsync(context, scheme).Returns(authenticatedByScheme
            ? AuthenticateResult.Success(new AuthenticationTicket(DiagnosticsUser(), scheme))
            : AuthenticateResult.NoResult());
        var settings = Substitute.For<IAppSettings>();
        settings.IsDevelopmentEnvironment.Returns(true);

        (await DeveloperExceptionAccess.AllowsAsync(settings, context)).Should().Be(authenticatedByScheme);
        context.User.Should().BeSameAs(original);
    }

    [Fact]
    public async Task Diagnostics_Should_Use_The_Last_Successful_Scheme_As_Primary()
    {
        const string diagnostics = "Diagnostics";
        const string secondary = "Secondary";
        var authentication = Substitute.For<IAuthenticationService>();
        var registrations = DiagnosticsServices().AddSingleton(authentication);
        registrations.Configure<AuthorizationOptions>(options => options.AddPolicy(DeveloperExceptionAccess.Policy,
            policy => policy.AddAuthenticationSchemes(diagnostics, secondary)
                .RequireAssertion(auth => auth.User.Identity?.AuthenticationType == secondary && auth.User.Identities.Count() == 2)));
        using var services = registrations.BuildServiceProvider();
        var original = new ClaimsPrincipal();
        var context = new DefaultHttpContext { RequestServices = services, User = original };
        var subject = new Claim(ClaimTypes.NameIdentifier, "viewer");
        var diagnosticsIdentity = new ClaimsIdentity([subject, new Claim("amr", "mfa"), new Claim("TestPagesAccess", "true")], diagnostics);
        var diagnosticsUser = new ClaimsPrincipal(new[] { new ClaimsIdentity(), diagnosticsIdentity });
        var secondaryUser = new ClaimsPrincipal(new ClaimsIdentity([subject], secondary));
        authentication.AuthenticateAsync(context, diagnostics).Returns(AuthenticateResult.Success(new AuthenticationTicket(diagnosticsUser, diagnostics)));
        authentication.AuthenticateAsync(context, secondary).Returns(AuthenticateResult.Success(new AuthenticationTicket(secondaryUser, secondary)));
        var settings = Substitute.For<IAppSettings>();
        settings.IsDevelopmentEnvironment.Returns(true);

        (await DeveloperExceptionAccess.AllowsAsync(settings, context)).Should().BeTrue();
        context.User.Should().BeSameAs(original);
        original.Identities.Should().BeEmpty();
    }

    [Fact]
    public async Task Diagnostics_Should_Preserve_Final_Scheme_Identities_Like_AspNetCore()
    {
        const string diagnostics = "Diagnostics";
        var authentication = Substitute.For<IAuthenticationService>();
        var registrations = DiagnosticsServices().AddSingleton(authentication);
        registrations.Configure<AuthorizationOptions>(options => options.AddPolicy(DeveloperExceptionAccess.Policy,
            policy => policy.AddAuthenticationSchemes(diagnostics)
                .RequireAssertion(auth => auth.User.Identity?.IsAuthenticated == false && auth.User.Identities.Count() == 2)));
        using var services = registrations.BuildServiceProvider();
        var original = new ClaimsPrincipal();
        var context = new DefaultHttpContext { RequestServices = services, User = original };
        var subject = new Claim(ClaimTypes.NameIdentifier, "viewer");
        var diagnosticsIdentity = new ClaimsIdentity([subject, new Claim("amr", "mfa"), new Claim("TestPagesAccess", "true")], diagnostics);
        var diagnosticsUser = new ClaimsPrincipal(new[] { new ClaimsIdentity(), diagnosticsIdentity });
        authentication.AuthenticateAsync(context, diagnostics).Returns(AuthenticateResult.Success(new AuthenticationTicket(diagnosticsUser, diagnostics)));
        var settings = Substitute.For<IAppSettings>();
        settings.IsDevelopmentEnvironment.Returns(true);

        (await DeveloperExceptionAccess.AllowsAsync(settings, context)).Should().BeTrue();
        context.User.Should().BeSameAs(original);
        original.Identities.Should().BeEmpty();
    }

    [Theory]
    [DataInlineUnit(false, false)]
    [DataInlineUnit(true, false)]
    [DataInlineUnit(false, true)]
    public async Task Development_Error_Should_Withhold_Diagnostics_Without_Mfa_And_Permission(bool mfa, bool permission)
    {
        var identity = new ClaimsIdentity(mfa || permission ? "Test" : null);
        if (mfa) identity.AddClaim(new Claim("amr", "mfa"));
        if (permission) identity.AddClaim(new Claim(ClaimTypes.Role, "SystemAdmin"));
        using var services = DiagnosticsServices().BuildServiceProvider();
        using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services, User = new ClaimsPrincipal(identity), Response = { Body = body, StatusCode = 500 } };
        context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute()), "Public"));
        var settings = Substitute.For<IAppSettings>();
        settings.IsDevelopmentEnvironment.Returns(true);
        var utils = Substitute.For<IExceptionUtils>();
        using var source = new DiagnosticListener(nameof(DrnExceptionHandlerTests));
        var handler = new DrnExceptionHandler(Substitute.For<IScopedLog>(), settings, utils, [], Substitute.For<IExceptionPageContentProvider>(), source);
        var exception = new InvalidOperationException("private");

        await handler.HandleExceptionAsync(context, exception);

        context.Response.ContentType.Should().Be("text/plain; charset=utf-8");
        await utils.DidNotReceive().CreateErrorPageModelAsync(context, exception);
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
