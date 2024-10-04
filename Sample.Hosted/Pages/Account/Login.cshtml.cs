using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using DRN.Framework.Utils.Scope;

namespace Sample.Hosted.Pages.Account;

[AllowAnonymous]
public class LoginModel(SignInManager<IdentityUser> signInManager) : PageModel
{
    [BindProperty] public LoginInput Input { get; set; } = null!;

    public string? ReturnUrl { get; set; }

    public void OnGet(string? returnUrl = null)
    {
        if (ScopeContext.Authenticated)
        {
            RedirectToPage(PageFor.Home);
            return;
        }

        ReturnUrl = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!ModelState.IsValid) return Page();

        var result = await signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);

        if (result.Succeeded)
            return string.IsNullOrWhiteSpace(returnUrl)
                ? RedirectToPage(PageFor.Home)
                : LocalRedirect(returnUrl);

        if (result.IsLockedOut)
            return RedirectToPage("./Lockout");

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");

        return Page();
    }
}

public class LoginInput
{
    [Required] [EmailAddress] public string Email { get; init; } = null!;

    [Required] [DataType(DataType.Password)] public string Password { get; init; } = null!;

    [Display(Name = "Remember me?")] public bool RememberMe { get; init; }
}