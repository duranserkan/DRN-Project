using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Sample.Hosted.Pages.Test;
//todo: test put, patch and delete methods 
public class Csrf(IAntiforgery antiforgery) : PageModel
{
    public void OnGet()
    {
    }

    // POST methods in Razor Pages automatically validate CSRF tokens
    public IActionResult OnPostAutoPost(string message)
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
           await antiforgery.ValidateRequestAsync(HttpContext);
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
}