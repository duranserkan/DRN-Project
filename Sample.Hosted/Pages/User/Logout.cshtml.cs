using Microsoft.AspNetCore.Identity;
using SampleUser = Sample.Domain.SampleIdentity.Users.SampleUser;

namespace Sample.Hosted.Pages.User;

[AllowAnonymous]
public class LogoutModel(SignInManager<SampleUser> signInManager) : PageModel
{
    public IActionResult OnGet()
    {
        if (!(User.Identity?.IsAuthenticated ?? false))
            return RedirectToPage(Get.Page.Root.HomeAnonymous);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (User.Identity?.IsAuthenticated ?? false)
            await signInManager.SignOutAsync();
        return RedirectToPage(Get.Page.Root.HomeAnonymous);
    }
}
