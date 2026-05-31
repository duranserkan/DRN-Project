using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Sample.Domain.Users;
using Sample.Hosted.Extensions;
using Sample.Hosted.Settings;

namespace Sample.Hosted.Pages.User;

[AllowAnonymous]
public class RegisterModel(UserManager<SampleUser> userManager, SignInManager<SampleUser> signInManager, SampleIdentityConfig identityConfig)
    : PageModel
{
    [BindProperty] public RegisterInput Input { get; set; } = null!;

    public string? ReturnUrl { get; set; }

    public void OnGet(string? returnUrl = null) => ReturnUrl = returnUrl;

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        if (!ModelState.IsValid) return Page();

        var user = new SampleUser { UserName = Input.Email, Email = Input.Email };
        var result = await userManager.CreateAsync(user, Input.Password);

        if (result.Succeeded)
        {
            var requiredConfirmations = new List<string>();

            var isEmailConfirmationRequired = identityConfig.RequireConfirmedAccount || identityConfig.RequireConfirmedEmail; // DefaultUserConfirmation only checks email for RequireConfirmedAccount
            if (isEmailConfirmationRequired)
                requiredConfirmations.Add("email");

            var isPhoneConfirmationRequired = identityConfig.RequireConfirmedPhoneNumber;
            if (isPhoneConfirmationRequired)
                requiredConfirmations.Add("phone number");

            if (requiredConfirmations.Count > 0)
            {
                var requiredText = string.Join(" and ", requiredConfirmations);
                TempData[Get.TempDataKeys.StatusMessage] = $"Registration successful. Account confirmation is required. Please check your {requiredText} to confirm your account.";
                return RedirectToPage(Get.Page.User.Login);
            }
            return await this.RedirectToEnableAuthenticator(signInManager, user);
        }

        return this.ReturnPageWithUserRegisterErrors(result);
    }
}

public class RegisterInput
{
    private const string PasswordErrorMessage = "The {0} must be at least {2} and at max {1} characters long.";
    private const string PasswordConfirmationErrorMessage = "The password and confirmation password do not match.";

    [Required] [EmailAddress] public string Email { get; init; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [StringLength(100, ErrorMessage = PasswordErrorMessage, MinimumLength = IdentitySettings.MinPasswordLength)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Confirm password")]
    [Compare(nameof(Password), ErrorMessage = PasswordConfirmationErrorMessage)]
    public string ConfirmPassword { get; set; } = string.Empty;
}