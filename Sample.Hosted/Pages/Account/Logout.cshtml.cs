using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Sample.Hosted.Pages.Account;

public class LogoutModel(SignInManager<IdentityUser> signInManager) : PageModel
{
    public IActionResult OnGet()
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return RedirectToPage(PageFor.Home);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.Identity?.IsAuthenticated ?? false)
            await signInManager.SignOutAsync();
        return RedirectToPage(PageFor.Home);
    }
}