using System.Net;
using System.Security.Claims;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Test.Integration.Helpers;
using DRN.Test.Integration.Tests.Sample.Pages.Helpers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Sample.Domain.Identity;
using Sample.Domain.Users;
using Sample.Hosted;
using Sample.Hosted.Helpers;

namespace DRN.Test.Integration.Tests.Sample.Pages;

public class DeveloperToolsAuthorizationTests
{
    [Theory]
    [DataInline("Development", "", null, true, true)]
    [DataInline("Development", "/gateway", UserRoles.SystemAdmin, true, true)]
    [DataInline("Staging", "/gateway", UserRoles.SystemAdmin, true, true)]
    [DataInline("Production", "/gateway", UserRoles.SystemAdmin, true, true)]
    [DataInline("Staging", "/gateway", UserClaims.SwaggerAccess, true, false)]
    [DataInline("Staging", "/gateway", UserClaims.TestPagesAccess, false, true)]
    [DataInline("Production", "/gateway", UserClaims.SwaggerAccess, true, false)]
    [DataInline("Production", "/gateway", UserClaims.TestPagesAccess, false, true)]
    [DataInline("Staging", "/gateway", null, false, false)]
    [DataInline("Production", "/gateway", null, false, false)]
    public async Task Mfa_Login_Should_Enforce_Developer_Tool_Policies_And_Preserve_Prefixed_Links(
        DrnTestContext context, string environment, string pathBase, string? grant, bool swaggerAllowed, bool testPagesAllowed)
    {
        context.AddToConfiguration("Environment", environment);
        context.AddToConfiguration("AllowedHosts", "localhost");
        // Keep the multi-request authorization flow independent of token-bucket quotas.
        context.AddToConfiguration("DrnAppFeatures:DrnRateLimit:Disabled", "true");
        var output = TestContext.Current.TestOutputHelper;
        output?.WriteLine($"Developer tools authorization: environment={environment}, pathBase='{pathBase}', grant='{grant ?? "none"}'");
        var application = context.ApplicationContext.CreateApplicationCore<SampleProgram>(output, builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter>(new PathBaseStartupFilter(pathBase))));
        await context.ContainerContext.BindExternalDependenciesAsync();
        application.Server.PreserveExecutionContext = true;
        using var client = application.CreateClient(new()
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });
        application.Services.GetRequiredService<IAppSettings>().Environment.ToString().Should().Be(environment);
        var toolPaths = new[]
        {
            ("/swagger/index.html", swaggerAllowed), ("/swagger/swagger-ui.css", swaggerAllowed),
            ("/swagger/swagger-ui-bundle.js", swaggerAllowed), ("/swagger/v1/swagger.json", swaggerAllowed),
            ("/swagger/v1/swagger.yaml", swaggerAllowed), (Get.Page.Test.Htmx, testPagesAllowed),
            (Get.Page.Test.DeveloperView, testPagesAllowed)
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
            .And.Contain($"src=\"{pathBase}{Get.Endpoint.User.PP.ControllerRoute}/{user.Id}?v=")
            .And.NotContain("hx-post=\"~/");
        html.Contains($"href=\"{pathBase}{Get.Page.Root.Swagger}\"").Should().Be(swaggerAllowed);
        html.Contains($"href=\"{pathBase}{Get.Page.Test.DeveloperView}\"").Should().Be(testPagesAllowed);
        if (testPagesAllowed)
            html.Should().Contain($"hx-post=\"{pathBase}{Get.Page.Test.Htmx}?handler=Auto\"");

        foreach (var (path, allowed) in toolPaths)
        {
            using var response = await client.GetAsync(pathBase + path);
            // Identity's default cookie/bearer composite forwards forbid responses to the bearer handler.
            response.StatusCode.Should().Be(allowed ? HttpStatusCode.OK : HttpStatusCode.Forbidden,
                $"the {environment} policy should enforce the user's permission for {path}");
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

            using var preview = await client.GetAsync(pathBase + Get.Page.Test.DeveloperView);
            (await preview.Content.ReadAsStringAsync()).Should().Contain("Developer diagnostics preview.")
                .And.Contain("class=\"frame\"")
                .And.Contain("DeveloperView.OnGetAsync()");
            preview.Headers.CacheControl!.NoStore.Should().BeTrue();
        }
    }
}
