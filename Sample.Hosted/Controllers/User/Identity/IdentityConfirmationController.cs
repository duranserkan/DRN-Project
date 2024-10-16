using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.WebUtilities;
using Sample.Hosted.EndpointRouteBuilderExtensions;

namespace Sample.Hosted.Controllers.User.Identity;

[ApiController]
[AllowAnonymous]
[Route("Api/User/[controller]")]
public class IdentityConfirmationController(
    UserManager<IdentityUser> userManager,
    IdentityConfirmationService confirmationService) : ControllerBase
{
    [HttpGet(nameof(ConfirmEmail))]
    public async Task<IResult> ConfirmEmail([FromQuery] string userId, [FromQuery] string code, [FromQuery] string? changedEmail)
    {
        if (await userManager.FindByIdAsync(userId) is not { } user)
            return TypedResults.Unauthorized();  // responding with a 401 to prevent unnecessary information

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
    public async Task<IResult> ResendConfirmationEmail([FromBody] ResendConfirmationEmailRequest resendRequest)
    {
        if (await userManager.FindByEmailAsync(resendRequest.Email) is not { } user)
            return TypedResults.Ok();

        await confirmationService.SendConfirmationEmailAsync(user, userManager, HttpContext, resendRequest.Email);

        return TypedResults.Ok();
    }
}