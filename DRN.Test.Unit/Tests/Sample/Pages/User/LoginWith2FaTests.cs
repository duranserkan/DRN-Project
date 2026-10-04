using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sample.Hosted.Pages.User;

namespace DRN.Test.Unit.Tests.Sample.Pages.User;

public class LoginWith2FaTests
{
    [Fact]
    public async Task Missing_Mfa_State_Should_Use_Page_Routing_For_Both_Handlers()
    {
        var context = new DefaultHttpContext();
        context.Request.PathBase = "/api";
        var model = new LoginWith2Fa(null!) { PageContext = new PageContext { HttpContext = context } };

        model.OnGet(false).Should().BeOfType<RedirectToPageResult>().Which.PageName.Should().Be("/User/Login");
        (await model.OnPostAsync()).Should().BeOfType<RedirectToPageResult>().Which.PageName.Should().Be("/User/Login");
    }

    [Theory]
    [DataInlineUnit(null, 1)]
    [DataInlineUnit("0", 1)]
    [DataInlineUnit("1", 2)]
    [DataInlineUnit("not-a-number", 1)]
    [DataInlineUnit("-1", 1)]
    [DataInlineUnit("2147483647", 1)]
    public void TrackInvalidCodeAttempt_Should_Write_Incremented_Or_Reset_Attempt_Cookie(string? cookieValue, int expectedAttempts)
    {
        var model = new LoginWith2Fa(null!);
        var context = CreateContext(cookieValue);

        var attempts = model.TrackInvalidCodeAttempt(context);

        attempts.Should().Be(expectedAttempts);
        context.Response.Headers.SetCookie.ToString().Should().Contain($"InvalidCodeAttempts={expectedAttempts}");
        context.Response.Headers.SetCookie.ToString().Should().Contain("httponly");
        context.Response.Headers.SetCookie.ToString().Should().Contain("samesite=strict");
    }

    private static DefaultHttpContext CreateContext(string? cookieValue)
    {
        var context = new DefaultHttpContext();
        if (cookieValue != null)
            context.Request.Headers.Cookie = $"InvalidCodeAttempts={Uri.EscapeDataString(cookieValue)}";

        return context;
    }
}
