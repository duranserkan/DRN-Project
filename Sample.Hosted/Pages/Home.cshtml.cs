using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Sample.Hosted.Pages;

[AllowAnonymous]
public class Home : PageModel
{
    public IActionResult OnGet()
    {
        return Page();
    }
}