using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Sample.Hosted.Auth.EndpointRouteBuilderExtensions;

namespace Sample.Hosted.Controllers.User.Identity;

[ApiController]
[Authorize]
[Route("Api/User/[controller]")]
public class IdentityManagementController(
    SignInManager<IdentityUser> signInManager,
    IdentityConfirmationService confirmationService) : ControllerBase
{
    private static readonly EmailAddressAttribute EmailAddressAttribute = new();

    [HttpPost(nameof(TwoFactorAuth))]
    public async Task<IResult> TwoFactorAuth([FromBody] TwoFactorRequest tfaRequest)
    {
        var userManager = signInManager.UserManager;
        if (await userManager.GetUserAsync(User) is not { } user)
            return TypedResults.NotFound();

        if (tfaRequest.Enable == true)
        {
            if (tfaRequest.ResetSharedKey)
                return IdentityApiHelper.CreateValidationProblem("CannotResetSharedKeyAndEnable",
                    "Resetting the 2fa shared key must disable 2fa until a 2fa token based on the new shared key is validated.");

            if (string.IsNullOrEmpty(tfaRequest.TwoFactorCode))
                return IdentityApiHelper.CreateValidationProblem("RequiresTwoFactor",
                    "No 2fa token was provided by the request. A valid 2fa token is required to enable 2fa.");

            if (!await userManager.VerifyTwoFactorTokenAsync(user, userManager.Options.Tokens.AuthenticatorTokenProvider, tfaRequest.TwoFactorCode))
                return IdentityApiHelper.CreateValidationProblem("InvalidTwoFactorCode",
                    "The 2fa token provided by the request was invalid. A valid 2fa token is required to enable 2fa.");

            await userManager.SetTwoFactorEnabledAsync(user, true);
        }
        else if (tfaRequest.Enable == false || tfaRequest.ResetSharedKey)
            await userManager.SetTwoFactorEnabledAsync(user, false);

        if (tfaRequest.ResetSharedKey)
            await userManager.ResetAuthenticatorKeyAsync(user);

        string[]? recoveryCodes = null;
        if (tfaRequest.ResetRecoveryCodes || (tfaRequest.Enable == true && await userManager.CountRecoveryCodesAsync(user) == 0))
        {
            var recoveryCodesEnumerable = await userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
            recoveryCodes = recoveryCodesEnumerable?.ToArray();
        }

        if (tfaRequest.ForgetMachine)
            await signInManager.ForgetTwoFactorClientAsync();

        var key = await userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await userManager.ResetAuthenticatorKeyAsync(user);
            key = await userManager.GetAuthenticatorKeyAsync(user);

            if (string.IsNullOrEmpty(key))
                throw new NotSupportedException("The user manager must produce an authenticator key after reset.");
        }

        return TypedResults.Ok(new TwoFactorResponse
        {
            SharedKey = key,
            RecoveryCodes = recoveryCodes,
            RecoveryCodesLeft = recoveryCodes?.Length ?? await userManager.CountRecoveryCodesAsync(user),
            IsTwoFactorEnabled = await userManager.GetTwoFactorEnabledAsync(user),
            IsMachineRemembered = await signInManager.IsTwoFactorClientRememberedAsync(user)
        });
    }

    [HttpGet(nameof(GetInfo))]
    public async Task<IResult> GetInfo()
    {
        var userManager = signInManager.UserManager;
        if (await userManager.GetUserAsync(User) is not { } user)
            return TypedResults.NotFound();

        return TypedResults.Ok(await IdentityApiHelper.CreateInfoResponseAsync(user, userManager));
    }

    [HttpPost(nameof(PostInfo))]
    public async Task<IResult> PostInfo([FromBody] InfoRequest infoRequest)
    {
        var userManager = signInManager.UserManager;
        if (await userManager.GetUserAsync(User) is not { } user)
        {
            return TypedResults.NotFound();
        }

        if (!string.IsNullOrEmpty(infoRequest.NewEmail) && !EmailAddressAttribute.IsValid(infoRequest.NewEmail))
        {
            return IdentityApiHelper.CreateValidationProblem(IdentityResult.Failed(userManager.ErrorDescriber.InvalidEmail(infoRequest.NewEmail)));
        }

        if (!string.IsNullOrEmpty(infoRequest.NewPassword))
        {
            if (string.IsNullOrEmpty(infoRequest.OldPassword))
            {
                return IdentityApiHelper.CreateValidationProblem("OldPasswordRequired",
                    "The old password is required to set a new password. If the old password is forgotten, use /resetPassword.");
            }

            var changePasswordResult = await userManager.ChangePasswordAsync(user, infoRequest.OldPassword, infoRequest.NewPassword);
            if (!changePasswordResult.Succeeded)
            {
                return IdentityApiHelper.CreateValidationProblem(changePasswordResult);
            }
        }

        if (!string.IsNullOrEmpty(infoRequest.NewEmail))
        {
            var email = await userManager.GetEmailAsync(user);

            if (email != infoRequest.NewEmail)
                await confirmationService.SendConfirmationEmailAsync(user, userManager, HttpContext, infoRequest.NewEmail, isChange: true);
        }

        return TypedResults.Ok(await IdentityApiHelper.CreateInfoResponseAsync(user, userManager));
    }
}