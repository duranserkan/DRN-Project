using System.Security.Cryptography;
using System.Text;
using DRN.Framework.Utils.Auth;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Sample.Hosted;

namespace DRN.Test.Integration.Tests.Sample.Pages;

public class NotificationShellTests
{
    [Theory]
    [DataInline]
    public async Task Rendered_Owner_Should_Use_Only_Stable_Authenticated_Account_Ids(DrnTestContext context)
    {
        var application = context.ApplicationContext.CreateApplication<SampleProgram>();
        await context.ContainerContext.BindExternalDependenciesAsync();

        foreach (var (authenticated, userId) in new (bool, string?)[]
                 {
                     (false, null), (false, "account-a"),
                     (true, null), (true, ""), (true, " \t"),
                     (true, "account-a"), (true, "account-b")
                 })
            await VerifyOwnerAsync(application.Services, authenticated, userId);
    }

    private static async Task VerifyOwnerAsync(IServiceProvider provider, bool authenticated, string? userId)
    {
        await using var scope = provider.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var user = Substitute.For<IScopedUser>();
        user.Authenticated.Returns(authenticated);
        user.Id.Returns(userId);
        ScopeContext.InitializeForTest(services, scopedUser: user);
        var engine = services.GetRequiredService<IRazorViewEngine>();
        var expectedOwner = "anonymous";
        if (authenticated)
            expectedOwner = string.IsNullOrWhiteSpace(userId)
                ? string.Empty
                : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId)));

        foreach (var traceId in new[] { "first-request", "second-request" })
        {
            var httpContext = new DefaultHttpContext { RequestServices = services, TraceIdentifier = traceId };
            httpContext.Request.PathBase = "/gateway";
            var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor());
            var view = engine.GetView(null, "/Pages/Shared/_NotificationShell.cshtml", isMainPage: false);
            view.Success.Should().BeTrue();
            using var writer = new StringWriter();
            var viewContext = new ViewContext(actionContext, view.View,
                new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary()),
                new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>()),
                writer, new HtmlHelperOptions());

            await view.View.RenderAsync(viewContext);

            writer.ToString().Should().Contain($"data-notification-owner=\"{expectedOwner}\"",
                    "authenticated={0}, userId='{1}', traceId='{2}' must preserve the account boundary",
                    authenticated, userId ?? "<null>", traceId)
                .And.Contain("data-notification-app=\"/gateway/\"");
        }
    }
}
