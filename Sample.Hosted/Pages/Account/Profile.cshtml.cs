using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Sample.Hosted.Pages.Account;

[Authorize]
public class ProfileModel(UserManager<IdentityUser> userManager) : PageModel
{
    public IdentityUser? User { get; set; }

    public async Task OnGetAsync()
    {
        // Retrieve the currently authenticated user
        User = await userManager.GetUserAsync(HttpContext.User);
    }
}