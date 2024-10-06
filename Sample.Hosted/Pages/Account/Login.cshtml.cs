using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using DRN.Framework.Utils.Scope;
using Sample.Domain.Identity;
using Sample.Hosted.Auth.Claims;

namespace Sample.Hosted.Pages.Account;

[AllowAnonymous]
public class LoginModel(SignInManager<IdentityUser> signInManager, UserManager<IdentityUser> userManager) : PageModel
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

        var user = await userManager.FindByEmailAsync(Input.Email);
        if (user == null)
            return ReturnInvalidAttempt();

        var passwordValid = await userManager.CheckPasswordAsync(user, Input.Password);
        if (!passwordValid)
            return ReturnInvalidAttempt();

        var result = await signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);

        if (result.IsLockedOut)
            return RedirectToPage("./Lockout");

        if (result.RequiresTwoFactor)
        {
            await signInManager.SignInAsync(user, false, authenticationMethod: UserClaims.MFAInProgress);
            return RedirectToPage(PageFor.AccountLoginWith2Fa, new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
        }

        if (result.Succeeded) //force MFA to stay always secure
            return RedirectToPage(PageFor.AccountEnableAuthenticator);

        return ReturnInvalidAttempt();
    }

    private IActionResult ReturnInvalidAttempt()
    {
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