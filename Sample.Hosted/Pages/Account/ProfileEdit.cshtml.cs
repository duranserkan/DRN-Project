using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sample.Application.Services;
using Sample.Hosted.Claims;

namespace Sample.Hosted.Pages.Account;

[Authorize]
public class ProfileEditModel(IUserProfileService service, SignInManager<IdentityUser> signInManager)
    : PageModel
{
    [BindProperty] public UserProfileEditModel Input { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        Input = await service.GetUserProfileEditModelAsync(User);
        Input.SlimUI = ClaimContext.SlimUi;

        return Page();
    }


    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var result = await service.UpdateUserAsync(Input, User);

        if (!result.IdentityResult.Succeeded)
        {
            foreach (var error in result.IdentityResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return Page();
        }

        // Sign in the user to update the claims
        await signInManager.RefreshSignInAsync(result.IdentityUser);

        TempData["StatusMessage"] = "Your profile has been updated";
        return RedirectToPage(PageFor.AccountProfile);
    }
}