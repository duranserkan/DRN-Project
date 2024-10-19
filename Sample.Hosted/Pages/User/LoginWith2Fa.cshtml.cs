using System.ComponentModel.DataAnnotations;
using DRN.Framework.Hosting.Auth;
using DRN.Framework.Utils.Auth.MFA;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Sample.Hosted.Pages.User;

[Authorize(AuthPolicy.MFAExempt)]
public class LoginWith2Fa(SignInManager<IdentityUser> signInManager) : PageModel
{
    private const string InvalidCodeAttempts = nameof(InvalidCodeAttempts);

    [BindProperty] public Login2FaModel Login2FaModel { get; set; } = new();

    public void OnGet(bool rememberMe, string? returnUrl = null)
    {
        Login2FaModel.RememberMe = rememberMe;
        ViewData["ReturnUrl"] = returnUrl;
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!MFAFor.MFAInProgress)
            return LocalRedirect(PageFor.User.Login);

        if (!ModelState.IsValid)
            return Page();

        //Todo:remember client
        var result = await signInManager.TwoFactorAuthenticatorSignInAsync(Login2FaModel.TwoFactorCode, Login2FaModel.RememberMe, rememberClient: false);
        if (result.Succeeded)
        {
            ResetInvalidCodeAttempts(HttpContext);
            return LocalRedirect(returnUrl ?? PageFor.Root.Home);
        }

        if (result.IsLockedOut)
            return RedirectToPage("./Lockout");

        ModelState.AddModelError(string.Empty, "Invalid authenticator code.");

        var invalidCodeAttempt = TrackInvalidCodeAttempt(HttpContext);
        if (invalidCodeAttempt > 1)
            ModelState.AddModelError(string.Empty, "Invalid code again? Try logging out and back in.");

        return Page();
    }

    public int TrackInvalidCodeAttempt(HttpContext context)
    {
        var count = int.Parse(context.Request.Cookies[InvalidCodeAttempts] ?? "0");
        var updatedCount = count + 1;

        context.Response.Cookies.Append(InvalidCodeAttempts, updatedCount.ToString());

        return updatedCount;
    }

    public void ResetInvalidCodeAttempts(HttpContext context) => context.Response.Cookies.Delete(InvalidCodeAttempts);
}

public class Login2FaModel
{
    [Required]
    [StringLength(7, ErrorMessage = "The {0} must be at least {2} and at most {1} characters long.", MinimumLength = 6)]
    [DataType(DataType.Text)]
    [Display(Name = "Authenticator code")]
    public string TwoFactorCode { get; init; } = string.Empty;

    [Display(Name = "Remember me?")]
    public bool RememberMe { get; set; }
}