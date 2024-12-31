// This file is licensed to you under the MIT license.

using System.Text;
using System.Text.Encodings.Web;
using DRN.Framework.Hosting.Identity.Services;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting.Identity.Controllers;

public abstract class IdentityPasswordControllerBase<TUser> : ControllerBase
    where TUser : IdentityUser, new()
{
    private readonly UserManager<TUser> _userManager;
    private readonly IEmailSender<TUser> _emailSender;

    protected IdentityPasswordControllerBase()
    {
        var sp = ScopeContext.Services;
        _userManager = sp.GetRequiredService<UserManager<TUser>>();
        _emailSender = sp.GetRequiredService<IEmailSender<TUser>>();
    }

    [HttpPost(nameof(Reset))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public virtual async Task<IResult> Reset([FromBody] ResetPasswordRequest resetRequest)
    {
        if (!ModelState.IsValid)
            return TypedResults.BadRequest();
        
        var user = await _userManager.FindByEmailAsync(resetRequest.Email);

        if (user is null || !(await _userManager.IsEmailConfirmedAsync(user)))
        {
            // Don't reveal that the user does not exist or is not confirmed, so don't return a 200
            // if we had returned a 400 for an invalid code given a valid user email.
            return IdentityApiHelper.CreateValidationProblem(IdentityResult.Failed(_userManager.ErrorDescriber.InvalidToken()));
        }

        IdentityResult result;
        try
        {
            var code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(resetRequest.ResetCode));
            result = await _userManager.ResetPasswordAsync(user, code, resetRequest.NewPassword);
        }
        catch (FormatException)
        {
            result = IdentityResult.Failed(_userManager.ErrorDescriber.InvalidToken());
        }

        if (!result.Succeeded)
            return IdentityApiHelper.CreateValidationProblem(result);

        return TypedResults.Ok();
    }

    [HttpPost(nameof(Forgot))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public virtual async Task<IResult> Forgot([FromBody] ForgotPasswordRequest resetRequest)
    {
        if (!ModelState.IsValid)
            return TypedResults.BadRequest();
        
        var user = await _userManager.FindByEmailAsync(resetRequest.Email);

        if (user is not null && await _userManager.IsEmailConfirmedAsync(user))
        {
            var code = await _userManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

            await _emailSender.SendPasswordResetCodeAsync(user, resetRequest.Email, HtmlEncoder.Default.Encode(code));
        }

        // Don't reveal that the user does not exist or is not confirmed, so don't return a 200
        return TypedResults.Ok();
    }
}