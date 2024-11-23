using System.ComponentModel.DataAnnotations;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sample.Domain.Users;
using Sample.Hosted.Extensions;

namespace Sample.Hosted.Pages.User;

[AllowAnonymous]
public class LoginModel(SignInManager<SampleUser> signInManager, UserManager<SampleUser> userManager) : PageModel
{
    [BindProperty] public LoginInput Input { get; set; } = null!;

    public string? ReturnUrl { get; set; }

    public void OnGet(string? returnUrl = null)
    {
        if (ScopeContext.Authenticated)
        {
            RedirectToPage(PageFor.Root.Home);
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

        var userLoginValidation = await ValidateUserLoginAsync(user);
        if (userLoginValidation.LockedOut)
            return RedirectToPage(PageFor.User.Lockout);
        if (!userLoginValidation.PasswordValid)
            return ReturnInvalidAttempt();
        if (!userLoginValidation.TwoFactorEnabled) //enforce Mfa
            return await this.RedirectToEnableAuthenticator(signInManager, user);

        var result = await signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
        if (!result.RequiresTwoFactor)
            return ReturnInvalidAttempt();

        await signInManager.SignInAsync(user, false, authenticationMethod: MfaClaimValues.MfaInProgress);
        return RedirectToPage(PageFor.User.LoginWith2Fa, new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
    }

    public record UserLoginValidation(bool LockedOut, bool PasswordValid, bool TwoFactorEnabled);

    private async Task<UserLoginValidation> ValidateUserLoginAsync(SampleUser user)
    {
        var lockOutValidationTask = userManager.IsLockedOutAsync(user);
        var passwordCheckTask = userManager.CheckPasswordAsync(user, Input.Password);
        var twoFactorEnabledTask = userManager.GetTwoFactorEnabledAsync(user);
        await Task.WhenAll(lockOutValidationTask, passwordCheckTask, twoFactorEnabledTask);

        return new UserLoginValidation(lockOutValidationTask.Result, passwordCheckTask.Result, twoFactorEnabledTask.Result);
    }

    private PageResult ReturnInvalidAttempt()
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