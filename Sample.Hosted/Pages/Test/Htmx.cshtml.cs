using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sample.Domain.Identity;

namespace Sample.Hosted.Pages.Test;

[Authorize(Roles = RoleFor.SystemAdmin)]
public class Csrf(IAntiforgery antiForgery) : PageModel
{
    public void OnGet()
    {
    }

    // POST methods in Razor Pages automatically validate CSRF tokens
    public IActionResult OnPostAuto(string message)
    {
        var model = new
        {
            Message = message,
            TokenValidation = "Success (Automatic Razor Pages Validation)",
            Timestamp = DateTime.Now.ToString("HH:mm:ss")
        };
        return Partial("_ResultMessage", model);
    }

    // GET requests don't validate CSRF by default
    public IActionResult OnGetNoCsrfGet()
    {
        var model = new
        {
            Message = string.Empty,
            TokenValidation = "Not Required (GET Request)",
            Timestamp = DateTime.Now.ToString("HH:mm:ss")
        };
        return Partial("_ResultMessage", model);
    }

    // Manually validate the token
    public async Task<IActionResult> OnGetExplicitValidationAsync(string query)
    {
        try
        {
            await antiForgery.ValidateRequestAsync(HttpContext);
        }
        catch (AntiforgeryValidationException e)
        {
            _ = e;
            return BadRequest("Invalid CSRF token.");
        }

        var model = new
        {
            Message = query,
            TokenValidation = "Success (Explicit Validation)",
            Timestamp = DateTime.Now.ToString("HH:mm:ss")
        };
        return Partial("_ResultMessage", model);
    }

    // PUT method with automatic CSRF validation
    public IActionResult OnPut(string message)
    {
        var model = new
        {
            Message = message,
            TokenValidation = "Success (PUT with Automatic Validation)",
            Timestamp = DateTime.Now.ToString("HH:mm:ss")
        };
        return Partial("_ResultMessage", model);
    }

    // PATCH method with automatic CSRF validation
    public IActionResult OnPatch(string message)
    {
        var model = new
        {
            Message = message,
            TokenValidation = "Success (PATCH with Automatic Validation)",
            Timestamp = DateTime.Now.ToString("HH:mm:ss")
        };
        return Partial("_ResultMessage", model);
    }

    // DELETE method with automatic CSRF validation
    public IActionResult OnDelete(string message)
    {
        var model = new
        {
            Message = message,
            TokenValidation = "Success (DELETE with Automatic Validation)",
            Timestamp = DateTime.Now.ToString("HH:mm:ss")
        };
        return Partial("_ResultMessage", model);
    }
}