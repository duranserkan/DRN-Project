using System.Text;
using System.Text.Encodings.Web;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace DRN.Nexus.Hosted.Controllers.User.Identity.Utils;

[Scoped<IdentityConfirmationService>]
public class IdentityConfirmationService(IServiceProvider serviceProvider, LinkGenerator linkGenerator)
{
    public async Task SendConfirmationEmailAsync<TUser>(TUser user, UserManager<TUser> userManager, HttpContext context, string email, bool isChange = false)
        where TUser : class, new()
    {
        var emailSender = serviceProvider.GetRequiredService<IEmailSender<TUser>>();
        var emailEndpoint =  NexusEndpointFor.User.Identity.Confirmation.ConfirmEmail;

        var code = isChange
            ? await userManager.GenerateChangeEmailTokenAsync(user, email)
            : await userManager.GenerateEmailConfirmationTokenAsync(user);
        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

        var userId = await userManager.GetUserIdAsync(user);
        var routeValues = new RouteValueDictionary
        {
            ["userId"] = userId,
            ["code"] = code,
        };

        if (isChange) // This is validated by the /confirmEmail endpoint on change.
            routeValues.Add("changedEmail", email);

        var confirmEmailUrl = linkGenerator.GetUriByAction(context, emailEndpoint.ActionName, emailEndpoint.ControllerName, routeValues);

        await emailSender.SendConfirmationLinkAsync(user, email, HtmlEncoder.Default.Encode(confirmEmailUrl));
    }
}