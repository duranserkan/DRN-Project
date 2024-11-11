using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Identity.Services;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting.Identity.Controllers;

public abstract class IdentityManagementControllerBase<TUser> : ControllerBase
    where TUser : IdentityUser, new()
{
    private readonly SignInManager<TUser> _signInManager;
    private readonly IIdentityConfirmationService _confirmationService;

    protected IdentityManagementControllerBase()
    {
        var sp = ScopeContext.Services;
        _signInManager = sp.GetRequiredService<SignInManager<TUser>>();
        _confirmationService = sp.GetRequiredService<IIdentityConfirmationService>();
    }

    public abstract ApiEndpoint EmailEndpoint { get; }

    [HttpPost(nameof(TwoFactorAuth))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(TwoFactorResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public virtual async Task<IResult> TwoFactorAuth([FromBody] TwoFactorRequest tfaRequest)
    {
        var userManager = _signInManager.UserManager;
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
            await _signInManager.ForgetTwoFactorClientAsync();

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
            IsMachineRemembered = await _signInManager.IsTwoFactorClientRememberedAsync(user)
        });
    }

    [HttpGet(nameof(GetInfo))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(InfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public virtual async Task<IResult> GetInfo()
    {
        var userManager = _signInManager.UserManager;
        if (await userManager.GetUserAsync(User) is not { } user)
            return TypedResults.NotFound();

        return TypedResults.Ok(await IdentityApiHelper.CreateInfoResponseAsync(user, userManager));
    }

    [HttpPost(nameof(PostInfo))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(InfoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public virtual async Task<IResult> PostInfo([FromBody] InfoRequest infoRequest)
    {
        var userManager = _signInManager.UserManager;
        if (await userManager.GetUserAsync(User) is not { } user)
        {
            return TypedResults.NotFound();
        }

        if (!string.IsNullOrEmpty(infoRequest.NewEmail) && !IdentityApiHelper.EmailAddressAttribute.IsValid(infoRequest.NewEmail))
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
                await _confirmationService.SendConfirmationEmailAsync(user, userManager, HttpContext, EmailEndpoint, infoRequest.NewEmail, isChange: true);
        }

        return TypedResults.Ok(await IdentityApiHelper.CreateInfoResponseAsync(user, userManager));
    }
}