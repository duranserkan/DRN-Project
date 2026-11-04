using System.Diagnostics.CodeAnalysis;
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

    [SuppressMessage("Major Code Smell", "S2325", Justification = "Razor Pages excludes static methods from handler discovery.")]
    public IActionResult OnPostUnhandledException() =>
        throw new InvalidOperationException("Intentional unhandled server exception from the HTMX test page.");

    // POST methods in Razor Pages automatically validate CSRF tokens
    public IActionResult OnPostAuto(string message) =>
        ResultMessage(message, "Success (Automatic Razor Pages Validation)");

    // GET requests don't validate CSRF by default
    public IActionResult OnGetNoCsrfGet() => ResultMessage(string.Empty, "Not Required (GET Request)");

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

        return ResultMessage(query, "Success (Explicit Validation)");
    }

    // PUT method with automatic CSRF validation
    public IActionResult OnPut(string message) => ResultMessage(message, "Success (PUT with Automatic Validation)");

    // PATCH method with automatic CSRF validation
    public IActionResult OnPatch(string message) => ResultMessage(message, "Success (PATCH with Automatic Validation)");

    // DELETE method with automatic CSRF validation
    public IActionResult OnDelete(string message) => ResultMessage(message, "Success (DELETE with Automatic Validation)");

    private PartialViewResult ResultMessage(string message, string validation) => Partial(ResultMessageView, new HtmxResultMessage
    {
        Message = message,
        TokenValidation = validation,
        Timestamp = DateTime.Now.ToString(TimestampFormat)
    });
}

public sealed class HtmxResultMessage
{
    public string Message { get; init; } = string.Empty;

    [Sensitive(false, Justification = "Antiforgery validation outcome, not token material.")]
    public string TokenValidation { get; init; } = string.Empty;

    public string Timestamp { get; init; } = string.Empty;
}
