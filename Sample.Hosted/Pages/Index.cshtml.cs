using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Sample.Hosted.Pages;

public class Index : PageModel
{
    public IActionResult OnGet()
    {
        return Page();
    }
}