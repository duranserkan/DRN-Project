using System.ComponentModel.DataAnnotations;
using System.Text;
using DRN.Framework.Hosting.Auth;
using DRN.Framework.Utils.Auth.MFA;
using DRN.Framework.Utils.Scope;
using Flurl;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QRCoder;
using Sample.Domain.Users;
using Sample.Hosted.Extensions;

namespace Sample.Hosted.Pages.User.Management;

[Authorize(AuthPolicy.MfaExempt)]
public class EnableAuthenticator(SignInManager<SampleUser> signInManager, UserManager<SampleUser> userManager) : PageModel
{
    [BindProperty] public QrCodeVerifyModel QrCodeVerify { get; set; } = null!;

    public string SharedKey { get; set; } = string.Empty;
    public string AuthenticatorUri { get; set; } = string.Empty;
    public string[] RecoveryCodes { get; set; } = [];
    public bool HasRecoveryCodes => RecoveryCodes.Length > 0;

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null)
            return this.ReturnLogoutPage();

        return await LoadSharedKeyAndQrCodeUriAsync(user);
    }

    public async Task<IActionResult> OnPostVerifyAsync()
    {
        if (!MfaFor.MfaSetupRequired)
            return LocalRedirect(Get.Page.User.Login);

        var user = await userManager.GetUserAsync(User);
        if (user == null)
            return this.ReturnLogoutPage();

        if (!ModelState.IsValid)
            return await LoadSharedKeyAndQrCodeUriAsync(user);

        var verificationCode = QrCodeVerify.Code.Replace(" ", string.Empty).Replace("-", string.Empty);
        var is2FaTokenValid = await userManager.VerifyTwoFactorTokenAsync(user, userManager.Options.Tokens.AuthenticatorTokenProvider, verificationCode);

        if (!is2FaTokenValid)
        {
            ModelState.AddModelError("Input.Code", "Verification code is invalid.");
            return await LoadSharedKeyAndQrCodeUriAsync(user);
        }

        await userManager.SetTwoFactorEnabledAsync(user, true);

        var recoveryCodes = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        RecoveryCodes = recoveryCodes?.ToArray() ?? [];

        await signInManager.SignOutAsync();

        return Page();
    }

    public string GenerateQrCodeImageAsBase64()
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(AuthenticatorUri, QRCodeGenerator.ECCLevel.Q);

        var qrCode = new Base64QRCode(qrCodeData);

        return qrCode.GetGraphic(20);
    }

    private async Task<IActionResult> LoadSharedKeyAndQrCodeUriAsync(SampleUser user)
    {
        var unformattedKey = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(unformattedKey))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            unformattedKey = await userManager.GetAuthenticatorKeyAsync(user);
        }

        var secret = unformattedKey!.ToLowerInvariant();
        SharedKey = FormatKey(secret);
        AuthenticatorUri = GenerateQrCodeUri(user.Email!, secret);

        return Page();
    }

    private static string GenerateQrCodeUri(string email, string secret)
    {
        var issuer = ScopeContext.Settings.ApplicationName;
        var otpAuthUri = $"otpauth://totp"
            .AppendPathSegment($"{issuer}:{email}")
            .SetQueryParam("secret", secret)
            .SetQueryParam("issuer", issuer)
            .SetQueryParam("digits", 6);

        return otpAuthUri;
    }

    private static string FormatKey(string unformattedKey)
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

        return result.ToString();
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