using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Sample.Hosted.Pages.Account;

[Authorize]
public class ProfileEditModel(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
    : PageModel
{
    [BindProperty] public EditInput Input { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null)
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");

        Input = new EditInput
        {
            PhoneNumber = user.PhoneNumber ?? string.Empty
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var user = await userManager.GetUserAsync(User);
        if (user == null)
            return NotFound($"Unable to load user with ID '{userManager.GetUserId(User)}'.");

        user.PhoneNumber = Input.PhoneNumber;

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return Page();
        }

        // Sign in the user to update the claims
        await signInManager.RefreshSignInAsync(user);

        TempData["StatusMessage"] = "Your profile has been updated";
        return RedirectToPage("/Account/Profile");
    }
}

public class EditInput
{
    [Phone]
    [Display(Name = "Phone Number")]
    [Required]
    public string PhoneNumber { get; set; }
}