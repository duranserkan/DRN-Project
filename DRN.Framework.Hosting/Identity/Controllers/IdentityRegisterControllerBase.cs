using System.Text;
using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Hosting.Identity.Services;
using DRN.Framework.Utils.Scope;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting.Identity.Controllers;

[ApiController]
[AllowAnonymous]
public abstract class IdentityRegisterControllerBase<TUser> : ControllerBase
    where TUser : IdentityUser, new()
{
    private readonly SignInManager<TUser> _signInManager;
    private readonly IUserStore<TUser> _userStore;
    private readonly IIdentityConfirmationService _confirmationService;

    protected IdentityRegisterControllerBase()
    {
        var sp = ScopeContext.Services;
        _signInManager = sp.GetRequiredService<SignInManager<TUser>>();
        _userStore = sp.GetRequiredService<IUserStore<TUser>>();
        _confirmationService = sp.GetRequiredService<IIdentityConfirmationService>();
    }

    public abstract ApiEndpoint EmailEndpoint { get; }

    [HttpPost(nameof(Register))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public virtual async Task<IResult> Register([FromBody] RegisterRequest registration)
    {
        var userManager = _signInManager.UserManager;
        if (!userManager.SupportsUserEmail)
            throw new NotSupportedException($"{GetType().FullName}.{nameof(Register)} requires a user store with email support.");

        var emailStore = (IUserEmailStore<TUser>)_userStore;
        var email = registration.Email;

        if (string.IsNullOrEmpty(email) || !IdentityApiHelper.EmailAddressAttribute.IsValid(email))
            return IdentityApiHelper.CreateValidationProblem(IdentityResult.Failed(userManager.ErrorDescriber.InvalidEmail(email)));

        var user = new TUser();
        await _userStore.SetUserNameAsync(user, email, CancellationToken.None);
        await emailStore.SetEmailAsync(user, email, CancellationToken.None);
        var result = await userManager.CreateAsync(user, registration.Password);

        if (!result.Succeeded)
            return IdentityApiHelper.CreateValidationProblem(result);

        await _confirmationService.SendConfirmationEmailAsync(user, userManager, HttpContext, EmailEndpoint, email);

        return TypedResults.Ok();
    }

    [HttpGet(nameof(ConfirmEmail))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public virtual async Task<IResult> ConfirmEmail([FromQuery] string userId, [FromQuery] string code, [FromQuery] string? changedEmail)
    {
        var userManager = _signInManager.UserManager;
        if (await userManager.FindByIdAsync(userId) is not { } user)
            return TypedResults.Unauthorized(); // responding with a 401 to prevent unnecessary information

        try
        {
            code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            return TypedResults.Unauthorized();
        }

        IdentityResult result;

        if (string.IsNullOrEmpty(changedEmail))
            result = await userManager.ConfirmEmailAsync(user, code);
        else
        {
            // As with Identity UI, email and username are one and the same. So when we update the email,
            // we need to update the username.
            result = await userManager.ChangeEmailAsync(user, changedEmail, code);
            if (result.Succeeded)
                result = await userManager.SetUserNameAsync(user, changedEmail);
        }

        if (!result.Succeeded)
            return TypedResults.Unauthorized();

        return TypedResults.Text("Thank you for confirming your email.");
    }

    [HttpPost(nameof(ResendConfirmationEmail))]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public virtual async Task<IResult> ResendConfirmationEmail([FromBody] ResendConfirmationEmailRequest resendRequest)
    {
        var userManager = _signInManager.UserManager;
        if (await _signInManager.UserManager.FindByEmailAsync(resendRequest.Email) is not { } user)
            return TypedResults.Ok();

        await _confirmationService.SendConfirmationEmailAsync(user, userManager, HttpContext, EmailEndpoint, resendRequest.Email);

        return TypedResults.Ok();
    }
}