using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Sample.Hosted.Pages;

public class Home : PageModel
{
    public IActionResult OnGet()
    {
        return Page();
    }
}