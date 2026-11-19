using System.Security.Claims;
using System.Text.Json;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler.Utils.Models;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Test.Integration.Helpers;
using DRN.Test.Integration.Tests.Sample.Pages.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Sample.Domain.SampleIdentity;
using SampleUser = Sample.Domain.SampleIdentity.SampleUser;

namespace DRN.Test.Integration.Tests.Sample.Pages;

public class DeveloperToolsAuthorizationTests
{
    private const string HostPreviewGrant = "HostPreviewAccess";

    [Theory]
    [DataInline("Development", "", null, true, true)]
    [DataInline("Development", "/gateway", UserRoles.SystemAdmin, true, true)]
    [DataInline("Development", "/gateway", UserClaims.TestPagesAccess, true, true)]
    [DataInline("Staging", "/gateway", UserRoles.SystemAdmin, true, true)]
    [DataInline("Production", "/gateway", UserRoles.SystemAdmin, true, true)]
    [DataInline("Staging", "/gateway", UserClaims.SwaggerAccess, true, false)]
    [DataInline("Staging", "/gateway", UserClaims.TestPagesAccess, false, true)]
    [DataInline("Production", "/gateway", UserClaims.SwaggerAccess, true, false)]
    [DataInline("Production", "/gateway", UserClaims.TestPagesAccess, false, true)]
    [DataInline("Staging", "/gateway", null, false, false)]
    [DataInline("Production", "/gateway", null, false, false)]
    public Task Mfa_Login_Should_Enforce_Developer_Tool_Policies_And_Preserve_Prefixed_Links(
        DrnTestContext context, string environment, string pathBase, string? grant, bool swaggerAllowed, bool testPagesAllowed)
        => VerifyDeveloperToolsAsync(context, environment, pathBase, grant, swaggerAllowed, testPagesAllowed);

    [Theory]
    [DataInline(HostPreviewGrant, "Identity.BearerAndApplication")]
    [DataInline(UserClaims.TestPagesAccess, "Identity.BearerAndApplication")]
    [DataInline(UserClaims.TestPagesAccess, "Identity.Bearer")]
    public Task Preview_Endpoints_And_Navigation_Should_Require_Both_Host_And_Built_In_Access(
        DrnTestContext context, string grant, string scheme)
        => VerifyDeveloperToolsAsync(context, "Development", "/gateway", grant, true, true, scheme);

    private static async Task VerifyDeveloperToolsAsync(DrnTestContext context, string environment, string pathBase,
        string? grant, bool swaggerAllowed, bool testPagesAllowed, string? previewScheme = null)
    {
        context.AddToConfiguration("Environment", environment);
        context.AddToConfiguration("AllowedHosts", "localhost");
        // Keep the multi-request authorization flow independent of token-bucket quotas.
        context.AddToConfiguration("DrnAppFeatures:DrnRateLimit:Disabled", "true");
        var output = TestContext.Current.TestOutputHelper;
        output?.WriteLine($"Developer tools authorization: environment={environment}, pathBase='{pathBase}', grant='{grant ?? "none"}'");
        var application = context.ApplicationContext.CreateApplicationCore<SampleProgram>(output, builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IStartupFilter>(new PathBaseStartupFilter(pathBase));
                if (previewScheme != null)
                    services.PostConfigure<AuthorizationOptions>(options => options.AddPolicy(Get.Policy.DeveloperException, policy =>
                        policy.AddAuthenticationSchemes(previewScheme).RequireClaim(HostPreviewGrant)
                            .RequireAssertion(check => check.Resource is Microsoft.AspNetCore.Http.HttpContext http &&
                                                       http.Request.PathBase.Value == pathBase)));
            }));
        await context.ContainerContext.BindExternalDependenciesAsync();
        application.Server.PreserveExecutionContext = true;
        using var client = application.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        application.Services.GetRequiredService<IAppSettings>().Environment.ToString().Should().Be(environment);
        var cookieAccepted = previewScheme != IdentityConstants.BearerScheme;
        var previewAllowed = cookieAccepted && environment == "Development" && grant is UserRoles.SystemAdmin or UserClaims.TestPagesAccess;
        var modelPath = "/" + Get.Endpoint.Sample.Exception.GetErrorPageModel.Path().TrimStart('/');
        var toolPaths = new[]
        {
            ("/swagger/index.html", swaggerAllowed), ("/swagger/swagger-ui.css", swaggerAllowed),
            ("/swagger/swagger-ui-bundle.js", swaggerAllowed), ("/swagger/v1/swagger.json", swaggerAllowed),
            ("/swagger/v1/swagger.yaml", swaggerAllowed), (Get.Page.Test.Htmx, testPagesAllowed),
            (Get.Page.Test.DeveloperView, previewAllowed), (modelPath, previewAllowed)
        };
        foreach (var (path, _) in toolPaths)
        {
            using var denied = await client.GetAsync(pathBase + path);
            denied.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.SeeOther);
            (await denied.Content.ReadAsStringAsync()).Should().NotContain("Developer diagnostics preview.")
                .And.NotContain("class=\"frame\"");
        }
        using var scope = application.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<SampleUser>>();
        var (user, credentials, key) = await PageTestHelper.CreateMfaUserAsync(manager);
        var email = credentials.EmailAddress;
        if (grant != null)
        {
            var claim = grant == UserRoles.SystemAdmin
                ? new Claim(manager.Options.ClaimsIdentity.RoleClaimType, UserRoles.SystemAdmin)
                : new Claim(grant, bool.TrueString);
            (await manager.AddClaimAsync(user, claim)).Succeeded.Should().BeTrue();
        }
        if (previewScheme != null && grant != HostPreviewGrant)
            (await manager.AddClaimAsync(user, new Claim(HostPreviewGrant, bool.TrueString))).Succeeded.Should().BeTrue();

        var loginPath = pathBase + Get.Page.User.Login;
        var landingPage = testPagesAllowed ? Get.Page.Test.Htmx : Get.Page.Root.Home;
        var returnUrl = $"{pathBase}{landingPage}?tag=one&tag=two&next=%2Fitems%3Fx%3D1#details";
        var token = await PageTestHelper.GetAntiforgeryTokenAsync(client, loginPath);
        using var loginForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = credentials.Password,
            ["Input.ReturnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = token
        });
        using var loginResponse = await client.PostAsync(loginPath, loginForm);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        var challengePath = loginResponse.Headers.Location!.OriginalString;
        challengePath.Should().StartWith(pathBase + Get.Page.User.LoginWith2Fa + "?");

        foreach (var (path, _) in toolPaths)
        {
            using var denied = await client.GetAsync(pathBase + path);
            denied.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.SeeOther);
            (await denied.Content.ReadAsStringAsync()).Should().NotContain("Developer diagnostics preview.")
                .And.NotContain("class=\"frame\"");
        }

        token = await PageTestHelper.GetAntiforgeryTokenAsync(client, challengePath);
        foreach (var (errorPage, expectedStatus) in new[]
                 {
                     (Get.Page.Root.AccessDenied, HttpStatusCode.Forbidden),
                     (Get.Page.Root.NotFound, HttpStatusCode.NotFound),
                     (Get.Page.Root.SomethingWentWrong, HttpStatusCode.InternalServerError)
                 })
        {
            using var errorResponse = await client.GetAsync(pathBase + errorPage);
            errorResponse.StatusCode.Should().Be(expectedStatus);
            errorResponse.Headers.Location.Should().BeNull();
        }
        using var challengeForm = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Login2FaModel.TwoFactorCode"] = TotpUtils.GenerateTotpCode(key),
            ["__RequestVerificationToken"] = token
        });
        using var challengeResponse = await client.PostAsync(challengePath, challengeForm);
        challengeResponse.StatusCode.Should().Be(HttpStatusCode.SeeOther);
        challengeResponse.Headers.Location!.OriginalString.Should().Be(returnUrl);

        using var missingRequest = new HttpRequestMessage(HttpMethod.Get, pathBase + "/missing-page-probe");
        missingRequest.Headers.Accept.ParseAdd("text/html");
        using var missingResponse = await client.SendAsync(missingRequest);
        missingResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        missingResponse.Headers.Location.Should().BeNull();
        (await missingResponse.Content.ReadAsStringAsync()).Should().Contain("Page not found");

        using var landingResponse = await client.GetAsync(challengeResponse.Headers.Location);
        landingResponse.EnsureSuccessStatusCode();
        var html = await landingResponse.Content.ReadAsStringAsync();
        html.Should().Contain($"name=\"drn-app-base\" content=\"{pathBase}/\"")
            .And.Contain($"href=\"{pathBase}{Get.Page.Root.Home}\"")
            .And.Contain($"src=\"{pathBase}{Get.Endpoint.User.Profile.ProfilePicture.ControllerRoute}/{user.Id}?v=")
            .And.NotContain("hx-post=\"~/");
        html.Contains($"href=\"{pathBase}{Get.Page.Root.Swagger}\"").Should().Be(swaggerAllowed);
        html.Contains($"href=\"{pathBase}{Get.Page.Test.DeveloperView}\"").Should().Be(previewAllowed);
        if (testPagesAllowed)
        {
            html.Should().Contain($"hx-post=\"{pathBase}{Get.Page.Test.Htmx}?handler=Auto\"");
            const string menuLabel = "aria-labelledby=\"navbarTestPages\"";
            var menuStart = html.IndexOf(menuLabel, StringComparison.Ordinal);
            menuStart.Should().BeGreaterThanOrEqualTo(0);
            var menuEnd = html.IndexOf("</ul>", menuStart, StringComparison.Ordinal);
            html[menuStart..menuEnd].Contains("dropdown-divider").Should().Be(previewAllowed);
        }

        foreach (var (path, allowed) in toolPaths)
        {
            using var response = await client.GetAsync(pathBase + path);
            if (!cookieAccepted && (path == Get.Page.Test.DeveloperView || path == modelPath))
            {
                if (path == modelPath)
                    response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
                else
                    response.StatusCode.Should().BeOneOf(HttpStatusCode.Unauthorized, HttpStatusCode.SeeOther);
                (await response.Content.ReadAsStringAsync()).Should().NotContain("Developer diagnostics preview.")
                    .And.NotContain("class=\"frame\"");
                continue;
            }
            // Identity's default cookie/bearer composite forwards forbid responses to the bearer handler.
            response.StatusCode.Should().Be(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden,
                $"the {environment} policy should enforce the user's permission for {path}");
            if (allowed && path == modelPath)
            {
                response.Headers.CacheControl!.NoStore.Should().BeTrue();
                var model = await response.Content.ReadFromJsonAsync<DrnExceptionModel>();
                model.Should().NotBeNull();
                model!.RequestPath.Should().Be(pathBase + modelPath);
            }
            if (allowed && path == "/swagger/v1/swagger.json")
            {
                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                document.RootElement.GetProperty("security")[0].GetProperty("bearer").GetArrayLength().Should().Be(0);
                var paths = document.RootElement.GetProperty("paths");
                foreach (var anonymousPath in new[] { "/Api/Sample/Private/anonymous", "/Api/Sample/WeatherForecast/Nexus" })
                    paths.GetProperty(anonymousPath).GetProperty("get").GetProperty("security").GetArrayLength().Should().Be(0);
                paths.GetProperty("/Api/Sample/Private").GetProperty("get")
                    .TryGetProperty("security", out _).Should().BeFalse();
            }
            if (allowed) continue;

            response.Headers.Location.Should().BeNull();
            (await response.Content.ReadAsStringAsync()).Should().NotContain("Developer diagnostics preview.")
                .And.NotContain("class=\"frame\"");
            using var accessDenied = await client.GetAsync(pathBase + Get.Page.Root.AccessDenied);
            accessDenied.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            accessDenied.Headers.Location.Should().BeNull();
            (await accessDenied.Content.ReadAsStringAsync()).Should().Contain("You do not have permission")
                .And.NotContain("Developer diagnostics preview.")
                .And.NotContain("class=\"frame\"");
        }
        if (testPagesAllowed)
        {
            foreach (var htmx in new[] { false, true })
            {
                string? problemType = null;
                foreach (var manual in new[] { false, true })
                {
                    var path = pathBase + Get.Page.Test.Htmx + (manual
                        ? "?handler=ExplicitValidation&query=private-query-probe"
                        : "?handler=Auto");
                    using var request = new HttpRequestMessage(manual ? HttpMethod.Get : HttpMethod.Post, path);
                    request.Headers.Accept.ParseAdd("text/html");
                    if (htmx) request.Headers.Add("HX-Request", "true");
                    using var rejected = await client.SendAsync(request);
                    rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
                    rejected.Headers.Location.Should().BeNull();
                    if (htmx)
                    {
                        using var problem = await ProblemDetailsAssertions.ReadProblemAsync(rejected, 400);
                        var type = problem.RootElement.GetProperty("type").GetString();
                        if (problemType != null) type.Should().Be(problemType);
                        problemType = type;
                        problem.RootElement.ToString().Should().NotContain("private-query-probe")
                            .And.NotContain("AntiforgeryValidationException");
                        if (manual)
                            problem.RootElement.GetProperty("detail").GetString().Should()
                                .Be("The request could not be verified. Refresh the page and try again.");
                    }
                    else
                    {
                        rejected.Content.Headers.ContentType!.MediaType.Should().Be("text/html");
                        (await rejected.Content.ReadAsStringAsync()).Should().Contain("Bad request")
                            .And.Contain("Request reference").And.NotContain("private-query-probe");
                    }
                }
            }
        }
        if (previewAllowed)
        {
            using var preview = await client.GetAsync(pathBase + Get.Page.Test.DeveloperView);
            (await preview.Content.ReadAsStringAsync()).Should().Contain("Developer diagnostics preview.")
                .And.Contain("class=\"frame\"")
                .And.Contain("DeveloperView.OnGetAsync()");
            preview.Headers.CacheControl!.NoStore.Should().BeTrue();
        }
    }
}
