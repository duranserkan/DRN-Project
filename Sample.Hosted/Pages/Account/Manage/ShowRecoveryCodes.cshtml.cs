using Microsoft.AspNetCore.Mvc.RazorPages;
using Sample.Hosted.Auth;

namespace Sample.Hosted.Pages.Account.Manage;

[Authorize(AuthPolicy.MFAExempt)]
public class ShowRecoveryCodes : PageModel
{
    [TempData] public string[]? RecoveryCodes { get; set; } = [];

    public IActionResult OnGet()
    {
        if (RecoveryCodes != null && RecoveryCodes.Length == 0)
            return RedirectToPage("./EnableAuthenticator");

        return Page();
    }
}