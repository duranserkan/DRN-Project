using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Sample.Hosted.Pages.User;

[Authorize]
public class ProfileModel(UserManager<IdentityUser> userManager) : PageModel
{
    public IdentityUser? IdentityUser { get; set; }

    public async Task OnGetAsync()
    {
        // Retrieve the currently authenticated user
        IdentityUser = await userManager.GetUserAsync(HttpContext.User);
    }
}