using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QRCoder;
using Sample.Hosted.Auth;

namespace Sample.Hosted.Pages.Account.Manage;

[Authorize(AuthPolicy.MFAExempt)]
public class EnableAuthenticator(UserManager<IdentityUser> userManager) : PageModel
{
    [BindProperty] public QrCodeVerifyModel QrCodeVerify { get; set; } = null!;

    public string SharedKey { get; set; } = string.Empty;
    public string AuthenticatorUri { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        await LoadSharedKeyAndQrCodeUriAsync(user);

        return Page();
    }

    public async Task<IActionResult> OnPostVerifyAsync()
    {
        var user = await userManager.GetUserAsync(User);

        if (!ModelState.IsValid)
        {
            await LoadSharedKeyAndQrCodeUriAsync(user);
            return Page();
        }

        var verificationCode = QrCodeVerify.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        var is2FaTokenValid = await userManager.VerifyTwoFactorTokenAsync(user, userManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode);

        if (!is2FaTokenValid)
        {
            ModelState.AddModelError("Input.Code", "Verification code is invalid.");
            await LoadSharedKeyAndQrCodeUriAsync(user);
            return Page();
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);

        // Generate recovery codes
        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        TempData["RecoveryCodes"] = recoveryCodes.ToArray();

        return RedirectToPage("./ShowRecoveryCodes");
    }


    public string GenerateQrCodeImageAsBase64()
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(AuthenticatorUri, QRCodeGenerator.ECCLevel.Q);

        var qrCode = new Base64QRCode(qrCodeData);

        return qrCode.GetGraphic(20);
    }


    private async Task LoadSharedKeyAndQrCodeUriAsync(IdentityUser user)
    {
        var unformattedKey = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await userManager.GetAuthenticatorKeyAsync(user);
        }

        SharedKey = FormatKey(unformattedKey!);
        AuthenticatorUri = GenerateQrCodeUri(user.Email!, unformattedKey!);
    }

    private string GenerateQrCodeUri(string email, string unformattedKey)
    {
        const string issuer = "YourAppName";
        return string.Format(
            CultureInfo.InvariantCulture,
            "otpauth://totp/{0}:{1}?secret={2}&issuer={0}&digits=6",
            issuer,
            email,
            unformattedKey);
    }

    private string FormatKey(string unformattedKey)
    {
        var result = new StringBuilder();
        var currentPosition = 0;
        while (currentPosition + 4 < unformattedKey.Length)
        {
            result.Append(unformattedKey.AsSpan(currentPosition, 4)).Append(' ');
            currentPosition += 4;
        }

        if (currentPosition < unformattedKey.Length)
            result.Append(unformattedKey.AsSpan(currentPosition));

        return result.ToString().ToLowerInvariant();
    }
}

public class QrCodeVerifyModel
{
    [Required]
    [StringLength(7, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
    [DataType(DataType.Text)]
    [Display(Name = "Verification Code")]
    public string Code { get; init; } = string.Empty;
}