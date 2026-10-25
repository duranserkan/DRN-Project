using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace DRN.Test.Unit.Tests.Framework.Hosting.Middlewares.ExceptionHandler;

public class AccessDeniedPageTests
{
    [Theory]
    [DataInlineUnit(302)]
    [DataInlineUnit(403)]
    [DataInlineUnit(401)]
    public async Task Reference_Preservation_Should_Await_Existing_Callback_And_Only_Save_For_Redirects(int status)
    {
        var factory = Substitute.For<ITempDataDictionaryFactory>();
        var tempData = Substitute.For<ITempDataDictionary>();
        var services = new ServiceCollection();
        services.AddSingleton(factory);
        await using var provider = services.BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = provider, TraceIdentifier = "denied-reference" };
        factory.GetTempData(context).Returns(tempData);
        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var options = new CookieAuthenticationOptions();
        options.Events.OnRedirectToAccessDenied = async redirect =>
        {
            calls++;
            callbackStarted.SetResult();
            await releaseCallback.Task;
            redirect.Response.StatusCode = status;
            if (status == 302)
                redirect.Response.Headers.Location = redirect.RedirectUri;
        };
        options.Events.PreserveAccessDeniedRequestId();
        var redirectContext = new RedirectContext<CookieAuthenticationOptions>(context,
            new AuthenticationScheme("test", null, typeof(CookieAuthenticationHandler)), options,
            new AuthenticationProperties(), "/gateway/AccessDenied");

        var redirectTask = options.Events.OnRedirectToAccessDenied(redirectContext);
        try
        {
            await callbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            redirectTask.IsCompleted.Should().BeFalse();
            factory.DidNotReceive().GetTempData(Arg.Any<HttpContext>());
        }
        finally
        {
            releaseCallback.TrySetResult();
        }
        await redirectTask.WaitAsync(TimeSpan.FromSeconds(5));

        calls.Should().Be(1);
        context.Response.StatusCode.Should().Be(status);
        if (status == 302)
        {
            context.Response.Headers.Location.ToString().Should().Be("/gateway/AccessDenied");
            tempData.Received()[AccessDeniedPageModel.RequestIdTempDataKey] = "denied-reference";
            tempData.Received(1).Save();
        }
        else
        {
            context.Response.Headers.Location.ToString().Should().BeEmpty();
            factory.DidNotReceive().GetTempData(Arg.Any<HttpContext>());
            tempData.DidNotReceive().Save();
        }
    }

    [Fact]
    public void Page_Should_Consume_Original_Reference_Once_And_Then_Use_Current_Request()
    {
        var provider = Substitute.For<ITempDataProvider>();
        var stored = new Dictionary<string, object> { [AccessDeniedPageModel.RequestIdTempDataKey] = "denied-reference" };
        provider.LoadTempData(Arg.Any<HttpContext>()).Returns(_ => new Dictionary<string, object>(stored));
        provider.When(value => value.SaveTempData(Arg.Any<HttpContext>(), Arg.Any<IDictionary<string, object>>()))
            .Do(call => stored = new Dictionary<string, object>(call.Arg<IDictionary<string, object>>()));

        var first = new DefaultHttpContext { TraceIdentifier = "redirect-reference" };
        var model = new AccessDeniedPageModel
        {
            PageContext = new PageContext { HttpContext = first },
            TempData = new TempDataDictionary(first, provider)
        };
        model.OnGet();
        model.RequestId.Should().Be("denied-reference");
        first.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
        model.TempData.Save();
        stored.Should().BeEmpty();

        var direct = new DefaultHttpContext { TraceIdentifier = "direct-reference" };
        direct.Request.QueryString = new QueryString("?RequestId=untrusted-reference");
        var directModel = new AccessDeniedPageModel
        {
            PageContext = new PageContext { HttpContext = direct },
            TempData = new TempDataDictionary(direct, provider)
        };
        directModel.OnGet();
        directModel.RequestId.Should().Be("direct-reference");
        direct.Response.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }
}
