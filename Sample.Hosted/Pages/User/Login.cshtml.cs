using System.ComponentModel.DataAnnotations;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sample.Domain.Users;
using Sample.Hosted.Extensions;
using Sample.Hosted.Helpers;

namespace Sample.Hosted.Pages.User;

[AllowAnonymous]
public class LoginModel(SignInManager<SampleUser> signInManager, UserManager<SampleUser> userManager) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = null!;

    public string? ReturnUrl { get; set; }

    public IActionResult OnGet(string? returnUrl = null)
    {
        if (ScopeContext.Authenticated)
            return RedirectToPage(Get.Page.Root.Home);

        ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : string.Empty;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var user = await userManager.FindByEmailAsync(Input.Email);
        if (user == null)
            return ReturnInvalidAttempt();

        var userLoginValidation = await ValidateUserLoginAsync(user);
        if (userLoginValidation.LockedOut)
            return RedirectToPage(Get.Page.User.Lockout);
        if (!userLoginValidation.PasswordValid)
            return ReturnInvalidAttempt();
        if (!userLoginValidation.TwoFactorEnabled) //enforce Mfa
            return await this.RedirectToEnableAuthenticator(signInManager, user);

        var result = await signInManager.PasswordSignInAsync(Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: false);
        if (!result.RequiresTwoFactor)
            return ReturnInvalidAttempt();

        await signInManager.SignInAsync(user, false, authenticationMethod: MfaClaimValues.MfaInProgress);

        Input.ReturnUrl = Url.IsLocalUrl(Input.ReturnUrl) ? Input.ReturnUrl : string.Empty;
        return RedirectToPage(Get.Page.User.LoginWith2Fa, new { Input.ReturnUrl, Input.RememberMe });
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
    public string? ReturnUrl { get; set; }

    [Required]
    [EmailAddress]
    public string Email { get; init; } = null!;

    [Required]
    [DataType(DataType.Password)]
    public string Password { get; init; } = null!;

    [Display(Name = "Remember me?")]
    public bool RememberMe { get; init; }
}