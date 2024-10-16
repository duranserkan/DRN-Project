using System.ComponentModel.DataAnnotations;
using DRN.Framework.Hosting.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Sample.Hosted.Pages.User;

[Authorize(AuthPolicy.MFAExempt)]
public class LoginWith2Fa(SignInManager<IdentityUser> signInManager) : PageModel
{
    [BindProperty] public Login2FaModel Login2FaModel { get; set; } = null!;

    public bool RememberMe { get; set; }

    public void OnGet(bool rememberMe, string? returnUrl = null)
    {
        RememberMe = rememberMe;
        ViewData["ReturnUrl"] = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(bool rememberMe, string? returnUrl = null)
    {
        if (!ModelState.IsValid)
            return Page();

        //Todo:remember client
        var result = await signInManager.TwoFactorAuthenticatorSignInAsync(Login2FaModel.TwoFactorCode, rememberMe, rememberClient: false);
        if (result.Succeeded)
            return LocalRedirect(returnUrl ?? PageFor.Root.Home);

        if (result.IsLockedOut)
            return RedirectToPage("./Lockout");

        ModelState.AddModelError(string.Empty, "Invalid authenticator code.");
        return Page();
    }
}

public class Login2FaModel
{
    [Required]
    [StringLength(7, ErrorMessage = "The {0} must be at least {2} and at most {1} characters long.", MinimumLength = 6)]
    [DataType(DataType.Text)]
    [Display(Name = "Authenticator code")]
    public string TwoFactorCode { get; init; } = string.Empty;
}