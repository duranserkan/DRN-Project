using DRN.Framework.Hosting.Auth;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Sample.Hosted.Pages.User.Management;

[Authorize(AuthPolicy.MfaExempt)]
public class ShowRecoveryCodes : PageModel
{
    [TempData] public string[] RecoveryCodes { get; set; } = [];

    public IActionResult OnGet()
    {
        if (RecoveryCodes.Length == 0)
            return RedirectToPage(PageFor.Root.Home);

        return Page();
    }
}