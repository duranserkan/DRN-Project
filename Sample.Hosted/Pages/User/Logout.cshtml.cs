using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sample.Domain.Users;

namespace Sample.Hosted.Pages.User;

[AllowAnonymous]
public class LogoutModel(SignInManager<SampleUser> signInManager) : PageModel
{
    public IActionResult OnGet()
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return RedirectToPage(PageFor.Root.Home);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.Identity?.IsAuthenticated ?? false)
            await signInManager.SignOutAsync();
        return RedirectToPage(PageFor.Root.Home);
    }
}