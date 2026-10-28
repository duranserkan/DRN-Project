using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using DRN.Framework.Hosting.Utils.Vite;
using DRN.Test.Integration.Helpers;
using DRN.Test.Integration.Tests.Sample.Pages.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Sample.Hosted;
using Sample.Hosted.Helpers;

namespace DRN.Test.Integration.Tests.Sample.Pages;

public class ErrorPageTests
{
    private const string SensitiveMessage = "private-error-probe-password";

    [Theory]
    [DataInline("Staging", "")]
    [DataInline("Staging", "/gateway")]
    [DataInline("Production", "")]
    [DataInline("Production", "/gateway")]
    public async Task Error_Pages_Should_Preserve_Status_And_Redact_Details(
        DrnTestContext context, string environment, string pathBase)
    {
        context.AddToConfiguration("Environment", environment);
        context.AddToConfiguration("AllowedHosts", "localhost");
        var application = context.ApplicationContext.CreateApplication<SampleProgram>(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IStartupFilter>(new PathBaseStartupFilter(pathBase));
                services.Configure<MvcOptions>(options => options.Filters.Add(new ErrorProbeFilter()));
            }));
        await context.ContainerContext.BindExternalDependenciesAsync();
        using var client = application.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");
        var expectedHomePath = pathBase.Length == 0 ? "/" : pathBase;
        var bootstrap = application.Services.GetRequiredService<IViteManifest>()
            .GetManifestItem("buildwww/lib/bootstrap/bootstrap.scss");
        bootstrap.Should().NotBeNull();
        bootstrap.Path.Should().NotBeNullOrEmpty();
        var stylesheetPath = pathBase + bootstrap.Path;

        foreach (var (path, status, message) in new[]
                 {
                     ("/BadRequest", HttpStatusCode.BadRequest, "Bad request"),
                     ("/AccessDenied", HttpStatusCode.Forbidden, "You do not have permission"),
                     ("/NotFound", HttpStatusCode.NotFound, "Page not found"),
                     ("/SomethingWentWrong", HttpStatusCode.InternalServerError, "Please try again later")
                 })
        {
            using var response = await client.GetAsync(pathBase + path + "?ReturnUrl=https://untrusted.example/&message=" + SensitiveMessage);
            response.StatusCode.Should().Be(status);
            response.Headers.Location.Should().BeNull();
            response.Headers.CacheControl!.NoStore.Should().BeTrue();
            var html = await response.Content.ReadAsStringAsync();
            html.Should().Contain(message).And.Contain($"href=\"{expectedHomePath}\"")
                .And.Contain($"href=\"{stylesheetPath}\"")
                .And.Contain("href=\"#error-content\"")
                .And.Contain("aria-labelledby=\"error-title\"")
                .And.Contain("id=\"error-title\"")
                .And.Contain("aria-label=\"Recovery actions\"")
                .And.Contain("aria-label=\"Request reference\">error-page-test-reference</code>")
                .And.NotContain("untrusted.example").And.NotContain(SensitiveMessage)
                .And.NotContain("class=\"frame\"");
        }

        using var denied = await client.GetAsync(pathBase + "/?errorProbe=forbid403");
        denied.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        denied.Headers.Location.Should().NotBeNull();
        denied.Headers.Location!.AbsolutePath.Should().Be(pathBase + "/AccessDenied");
        using var accessDenied = await client.GetAsync(denied.Headers.Location);
        accessDenied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await accessDenied.Content.ReadAsStringAsync()).Should()
            .Contain("aria-label=\"Request reference\">denied-request-reference</code>");

        using var directAccessDenied = await client.GetAsync(pathBase + "/AccessDenied?RequestId=untrusted-reference");
        directAccessDenied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await directAccessDenied.Content.ReadAsStringAsync()).Should()
            .Contain("aria-label=\"Request reference\">error-page-test-reference</code>")
            .And.NotContain("denied-request-reference").And.NotContain("untrusted-reference");

        using var stylesheet = await client.GetAsync(stylesheetPath);
        stylesheet.StatusCode.Should().Be(HttpStatusCode.OK);
        stylesheet.Content.Headers.ContentType!.MediaType.Should().Be("text/css");

        foreach (var (probe, status) in new[]
                 {
                     ("throw", HttpStatusCode.InternalServerError),
                     ("503", HttpStatusCode.ServiceUnavailable),
                     ("599", (HttpStatusCode)599)
                 })
        {
            using var response = await client.GetAsync(pathBase + "/?errorProbe=" + probe);
            response.StatusCode.Should().Be(status);
            response.Headers.Location.Should().BeNull();
            response.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
            response.Headers.CacheControl!.NoStore.Should().BeTrue();
            var html = await response.Content.ReadAsStringAsync();
            html.Should().Contain("Something went wrong").And.Contain("error-page-test-reference")
                .And.Contain($"href=\"{expectedHomePath}\"")
                .And.Contain($"href=\"{stylesheetPath}\"")
                .And.NotContain(SensitiveMessage).And.NotContain("InvalidOperationException")
                .And.NotContain("class=\"frame\"");
        }

        foreach (var probe in new[] { "400", "throw400" })
        {
            using var badRequest = await client.GetAsync(pathBase + "/?errorProbe=" + probe);
            badRequest.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            badRequest.Headers.Location.Should().BeNull();
            badRequest.Headers.CacheControl!.NoStore.Should().BeTrue();
            (await badRequest.Content.ReadAsStringAsync()).Should().Contain("Bad request")
                .And.Contain("aria-label=\"Request reference\">error-page-test-reference</code>")
                .And.Contain($"href=\"{expectedHomePath}\"").And.NotContain(SensitiveMessage);
        }

        using var validation = await client.GetAsync(pathBase + "/?errorProbe=validation400");
        validation.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await validation.Content.ReadAsStringAsync()).Should().Be("Check the submitted value.");

        using var notFound = await client.GetAsync(pathBase + "/?errorProbe=404");
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
        notFound.Headers.Location.Should().BeNull();
        notFound.Headers.CacheControl!.NoStore.Should().BeTrue();
        (await notFound.Content.ReadAsStringAsync()).Should().Contain("Page not found")
            .And.Contain("aria-label=\"Request reference\">error-page-test-reference</code>")
            .And.Contain($"href=\"{expectedHomePath}\"").And.NotContain("Something went wrong");

        using var thrownNotFound = await client.GetAsync(pathBase + "/?errorProbe=throw404");
        thrownNotFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await thrownNotFound.Content.ReadAsStringAsync()).Should().Contain("Page not found")
            .And.Contain("aria-label=\"Request reference\">error-page-test-reference</code>")
            .And.NotContain(SensitiveMessage);

        // This anonymous API action returns NotFound outside Development, even when HTML is requested.
        using var apiNotFound = await client.GetAsync(pathBase + "/Api/Sample/Exception/NotFoundException");
        apiNotFound.Headers.Location.Should().BeNull();
        using var notFoundProblem = await ProblemDetailsAssertions.ReadProblemAsync(apiNotFound, 404);
        (await apiNotFound.Content.ReadAsStringAsync()).Should().NotContain("Page not found")
            .And.NotContain("<html");

        var registerRoute = Get.Endpoint.User.Identity.RegisterController.Register.RoutePattern;
        registerRoute.Should().NotBeNullOrWhiteSpace();
        var registerPath = pathBase + "/" + registerRoute.TrimStart('/');
        var invalidRegistration = new { Email = "invalid-email", Password = "Unused-password-123!" };
        using var jsonRequest = new HttpRequestMessage(HttpMethod.Post, registerPath);
        jsonRequest.Content = JsonContent.Create(invalidRegistration);
        jsonRequest.Headers.Accept.ParseAdd("application/json");
        using var jsonResponse = await client.SendAsync(jsonRequest);
        using var expectedDocument = await ProblemDetailsAssertions.ReadProblemAsync(jsonResponse, 400);
        var expectedProblem = expectedDocument.RootElement.Deserialize<ValidationProblemDetails>();
        expectedProblem.Should().NotBeNull();
        expectedProblem.Errors.Should().NotBeEmpty();

        // The client still requests text/html; API validation must retain its JSON payload.
        using var apiValidation = await client.PostAsJsonAsync(registerPath, invalidRegistration);
        apiValidation.Headers.Location.Should().BeNull();
        using var actualDocument = await ProblemDetailsAssertions.ReadProblemAsync(apiValidation, 400);
        var actualProblem = actualDocument.RootElement.Deserialize<ValidationProblemDetails>();
        actualProblem.Should().NotBeNull();
        actualProblem.Type.Should().Be(expectedProblem.Type);
        actualProblem.Title.Should().Be(expectedProblem.Title);
        actualProblem.Errors.Should().BeEquivalentTo(expectedProblem.Errors);
        (await apiValidation.Content.ReadAsStringAsync()).Should().NotContain("<html")
            .And.NotContain("id=\"error-content\"").And.NotContain(stylesheetPath);

        var loginPath = pathBase + "/" + Get.Endpoint.User.Identity.LoginController.Login.RoutePattern!.TrimStart('/');
        using var invalidLogin = await client.PostAsJsonAsync(loginPath,
            new LoginRequest { Email = "missing-api-error-user@example.com", Password = "Invalid-password-123!" });
        using var loginProblem = await ProblemDetailsAssertions.ReadProblemAsync(invalidLogin, 401);
        loginProblem.RootElement.GetProperty("detail").GetString().Should().Be("Invalid email or password.");

        var refreshPath = pathBase + "/" + Get.Endpoint.User.Identity.LoginController.Refresh.RoutePattern!.TrimStart('/');
        using var invalidRefresh = await client.PostAsJsonAsync(refreshPath, new RefreshRequest { RefreshToken = "invalid" });
        invalidRefresh.Headers.WwwAuthenticate.Should().Contain(value => value.Scheme == "Bearer");
        using var refreshProblem = await ProblemDetailsAssertions.ReadProblemAsync(invalidRefresh, 401);

        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        using var nonHtmlBadRequest = await client.GetAsync(pathBase + "/?errorProbe=400");
        nonHtmlBadRequest.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await nonHtmlBadRequest.Content.ReadAsStringAsync()).Should().BeEmpty();
        using var nonHtmlNotFound = await client.GetAsync(pathBase + "/?errorProbe=404");
        nonHtmlNotFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await nonHtmlNotFound.Content.ReadAsStringAsync()).Should().BeEmpty();
        using var nonHtml = await client.GetAsync(pathBase + "/?errorProbe=throw");
        nonHtml.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        nonHtml.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
        (await nonHtml.Content.ReadAsStringAsync()).Should().NotContain(SensitiveMessage)
            .And.NotContain("<html");
    }

    [Theory]
    [DataInline("Development", true)]
    [DataInline("Development", false)]
    [DataInline("Staging", true)]
    [DataInline("Staging", false)]
    [DataInline("Production", true)]
    [DataInline("Production", false)]
    public async Task Status_Page_Rendering_Failure_Should_Preserve_Status_And_Use_Redacted_Fallback(
        DrnTestContext context, string environment, bool missingPage)
    {
        context.AddToConfiguration("Environment", environment);
        context.AddToConfiguration("AllowedHosts", "localhost");
        var pageUtils = Substitute.For<IPageUtils>();
        pageUtils.RenderPageAsync(Arg.Any<string>(), Arg.Any<BadRequestPageModel>(), Arg.Any<HttpContext>())
            .Returns(_ => Task.FromException<string>(new InvalidOperationException(SensitiveMessage)));
        pageUtils.RenderPageAsync(Arg.Any<string>(), Arg.Any<NotFoundPageModel>(), Arg.Any<HttpContext>())
            .Returns(_ => Task.FromException<string>(new InvalidOperationException(SensitiveMessage)));
        pageUtils.RenderPageAsync(Arg.Any<string>(), Arg.Any<ServerErrorPageModel>(), Arg.Any<HttpContext>())
            .Returns(_ => Task.FromException<string>(new InvalidOperationException(SensitiveMessage)));
        var application = context.ApplicationContext.CreateApplication<SampleProgram>(builder =>
            builder.ConfigureServices(services =>
            {
                services.Configure<MvcOptions>(options => options.Filters.Add(new ErrorProbeFilter()));
                if (missingPage)
                    services.PostConfigure<DrnErrorPageOptions>(options =>
                    {
                        options.NotFoundPage = "/Errors/Missing";
                        options.BadRequestPage = "/Errors/Missing";
                        options.ServerErrorPage = "/Errors/Missing";
                    });
                else
                    services.AddSingleton(pageUtils);
            }));
        await context.ContainerContext.BindExternalDependenciesAsync();
        using var client = application.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");

        foreach (var status in new[] { HttpStatusCode.BadRequest, HttpStatusCode.NotFound, HttpStatusCode.ServiceUnavailable })
        {
            using var response = await client.GetAsync("/?errorProbe=" + (int)status);

            response.StatusCode.Should().Be(status);
            response.Headers.Location.Should().BeNull();
            response.Content.Headers.ContentType!.MediaType.Should().Be("text/plain");
            response.Headers.CacheControl!.NoStore.Should().BeTrue();
            (await response.Content.ReadAsStringAsync()).Should()
                .Be($"{status} {(int)status} TraceId: error-page-test-reference");
        }

        if (!missingPage)
        {
            await pageUtils.Received(1).RenderPageAsync(
                Arg.Any<string>(), Arg.Any<BadRequestPageModel>(), Arg.Any<HttpContext>());
            await pageUtils.Received(1).RenderPageAsync(
                Arg.Any<string>(), Arg.Any<NotFoundPageModel>(), Arg.Any<HttpContext>());
            await pageUtils.Received(1).RenderPageAsync(
                Arg.Any<string>(), Arg.Any<ServerErrorPageModel>(), Arg.Any<HttpContext>());
        }
    }

    private sealed class ErrorProbeFilter : IAsyncPageFilter
    {
        public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

        public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
        {
            context.HttpContext.TraceIdentifier = "error-page-test-reference";
            var probe = context.HttpContext.Request.Query["errorProbe"].ToString();
            if (probe.Length == 0)
            {
                await next();
                return;
            }

            if (probe == "throw")
                throw new InvalidOperationException(SensitiveMessage);
            if (probe == "throw404")
                throw new NotFoundException(SensitiveMessage);
            if (probe == "throw400")
                throw new ValidationException(SensitiveMessage);
            if (probe == "forbid403")
            {
                context.HttpContext.TraceIdentifier = "denied-request-reference";
                context.Result = new ForbidResult(IdentityConstants.ApplicationScheme);
                return;
            }
            if (probe == "validation400")
            {
                context.Result = new ContentResult
                {
                    StatusCode = StatusCodes.Status400BadRequest,
                    ContentType = "text/plain",
                    Content = "Check the submitted value."
                };
                return;
            }

            context.Result = new StatusCodeResult(int.Parse(probe));
        }
    }
}
