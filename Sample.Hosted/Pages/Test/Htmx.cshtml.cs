using DRN.Framework.SharedKernel.Attributes;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sample.Domain.Identity;

namespace Sample.Hosted.Pages.Test;

[Authorize(Policy = UserClaims.TestPagesAccess)]
public class Htmx(IAntiforgery antiForgery) : PageModel
{
    private const string TimestampFormat = "HH:mm:ss";
    private const string ResultMessageView = "_ResultMessage";

    public void OnGet()
    {
        // Intentionally empty: GET request initializes default page view.
    }

    public IActionResult OnPostUnhandledException() =>
        throw new InvalidOperationException("Intentional unhandled server exception from the HTMX test page.");

    // POST methods in Razor Pages automatically validate CSRF tokens
    public IActionResult OnPostAuto(string message)
    {
        var model = new HtmxResultMessage
        {
            Message = message,
            TokenValidation = "Success (Automatic Razor Pages Validation)",
            Timestamp = DateTime.Now.ToString(TimestampFormat)
        };
        return Partial(ResultMessageView, model);
    }

    // GET requests don't validate CSRF by default
    public IActionResult OnGetNoCsrfGet()
    {
        var model = new HtmxResultMessage
        {
            Message = string.Empty,
            TokenValidation = "Not Required (GET Request)",
            Timestamp = DateTime.Now.ToString(TimestampFormat)
        };
        return Partial(ResultMessageView, model);
    }

    // Manually validate the token
    public async Task<IActionResult> OnGetExplicitValidationAsync(string query)
    {
        try
        {
            await antiForgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            ScopeContext.Problem.Detail = "The request could not be verified. Refresh the page and try again.";
            return BadRequest();
        }

        var model = new HtmxResultMessage
        {
            Message = query,
            TokenValidation = "Success (Explicit Validation)",
            Timestamp = DateTime.Now.ToString(TimestampFormat)
        };
        return Partial(ResultMessageView, model);
    }

    // PUT method with automatic CSRF validation
    public IActionResult OnPut(string message)
    {
        var model = new HtmxResultMessage
        {
            Message = message,
            TokenValidation = "Success (PUT with Automatic Validation)",
            Timestamp = DateTime.Now.ToString(TimestampFormat)
        };
        return Partial(ResultMessageView, model);
    }

    // PATCH method with automatic CSRF validation
    public IActionResult OnPatch(string message)
    {
        var model = new HtmxResultMessage
        {
            Message = message,
            TokenValidation = "Success (PATCH with Automatic Validation)",
            Timestamp = DateTime.Now.ToString(TimestampFormat)
        };
        return Partial(ResultMessageView, model);
    }

    // DELETE method with automatic CSRF validation
    public IActionResult OnDelete(string message)
    {
        var model = new HtmxResultMessage
        {
            Message = message,
            TokenValidation = "Success (DELETE with Automatic Validation)",
            Timestamp = DateTime.Now.ToString(TimestampFormat)
        };
        return Partial(ResultMessageView, model);
    }
}

public sealed class HtmxResultMessage
{
    public string Message { get; init; } = string.Empty;

    [Sensitive(false, Justification = "Antiforgery validation outcome, not token material.")]
    public string TokenValidation { get; init; } = string.Empty;

    public string Timestamp { get; init; } = string.Empty;
}
