using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sample.Domain.Users;

namespace Sample.Hosted.Pages.Account;

[AllowAnonymous]
public class SetupModel(
    SignInManager<IdentityUser> signInManager,
    IUserRepository userRepository) : PageModel
{
    [BindProperty] public SetupInput Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync()
    {
        var adminUserExists = await userRepository.AnySystemAdminExistsAsync();
        if (adminUserExists)
            return RedirectToPage("/Index");

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var adminUserExists = await userRepository.AnySystemAdminExistsAsync();
        if (adminUserExists)
            return RedirectToPage("/Index");

        var user = new IdentityUser { UserName = Input.Email, Email = Input.Email };
        var result = await userRepository.CreateSystemAdminForInitialSetup(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(error.Code, error.Description);
            return Page();
        }

        await signInManager.SignInAsync(user, isPersistent: false);

        return RedirectToPage("/Index");
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