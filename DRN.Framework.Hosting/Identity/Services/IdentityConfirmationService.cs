using System.Text;
using System.Text.Encodings.Web;
using DRN.Framework.Hosting.Endpoints;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Hosting.Identity.Services;

public interface IIdentityConfirmationService
{
    Task SendConfirmationEmailAsync<TUser>(TUser user, UserManager<TUser> userManager,
        HttpContext context, ApiEndpoint emailEndpoint, string email, bool isChange = false)
        where TUser : class, new();
}

[Scoped<IIdentityConfirmationService>]
public class IdentityConfirmationService(IServiceProvider serviceProvider, LinkGenerator linkGenerator) : IIdentityConfirmationService
{
    public async Task SendConfirmationEmailAsync<TUser>(TUser user, UserManager<TUser> userManager, HttpContext context, ApiEndpoint emailEndpoint,
        string email, bool isChange = false)
        where TUser : class, new()
    {
        var emailSender = serviceProvider.GetRequiredService<IEmailSender<TUser>>();

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

        var confirmEmailUrl = linkGenerator.GetUriByAction(context, emailEndpoint.ActionName, emailEndpoint.ControllerName, routeValues)!;

        //Todo: check user type. Can it be IdentityUser?
        await emailSender.SendConfirmationLinkAsync(user, email, HtmlEncoder.Default.Encode(confirmEmailUrl));
    }
}