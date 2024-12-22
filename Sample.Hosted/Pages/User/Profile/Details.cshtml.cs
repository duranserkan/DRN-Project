using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sample.Domain.Users;

namespace Sample.Hosted.Pages.User.Profile;

[Authorize]
public class ProfileDetailsModel(UserManager<SampleUser> userManager) : PageModel
{
    public SampleUser? IdentityUser { get; set; }

    public async Task OnGetAsync()
    {
        // Retrieve the currently authenticated user
        IdentityUser = await userManager.GetUserAsync(HttpContext.User);
    }
}