using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Sample.Hosted.Pages.Account;

[AllowAnonymous]
public class RegisterModel(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
    : PageModel
{
    [BindProperty] public RegisterInput Input { get; set; } = null!;

    public string? ReturnUrl { get; set; }

    public void OnGet(string? returnUrl = null) => ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!ModelState.IsValid) return Page();

        var user = new IdentityUser { UserName = Input.Email, Email = Input.Email };
        var result = await userManager.CreateAsync(user, Input.Password);

        if (result.Succeeded)
        {
            await signInManager.SignInAsync(user, isPersistent: false);
            return string.IsNullOrWhiteSpace(returnUrl)
                ? RedirectToPage(PageFor.Home)
                : LocalRedirect(returnUrl);
        }

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return Page();
    }
}

public class RegisterInput
{
    [Required] [EmailAddress] public string Email { get; init; } = string.Empty;

    [Required]
    [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}