using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sample.Domain.Identity;

namespace Sample.Hosted.Pages.Account;

[AllowAnonymous]
public class SetupModel(
    SignInManager<IdentityUser> signInManager,
    IUserAdminRepository userAdminRepository) : PageModel
{
    [BindProperty] public SetupInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var adminUserExists = await userAdminRepository.AnySystemAdminExistsAsync();
        if (adminUserExists)
            return RedirectToPage(PageFor.Home);

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var adminUserExists = await userAdminRepository.AnySystemAdminExistsAsync();
        if (adminUserExists)
            return RedirectToPage(PageFor.Home);

        var user = new IdentityUser { UserName = Input.Email, Email = Input.Email };
        var result = await userAdminRepository.CreateSystemAdminForInitialSetup(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Code, error.Description);
            return Page();
        }

        // Sign in the user to update the claims
        await signInManager.RefreshSignInAsync(user);

        return RedirectToPage(PageFor.Home);
    }
}

public class SetupInput
{
    [Required] [EmailAddress] public string Email { get; init; } = null!;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; init; } = null!;

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; init; } = null!;
}