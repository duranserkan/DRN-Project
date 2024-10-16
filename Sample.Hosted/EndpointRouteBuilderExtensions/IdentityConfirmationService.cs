using System.Text;
using System.Text.Encodings.Web;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace Sample.Hosted.EndpointRouteBuilderExtensions;

[Scoped<IdentityConfirmationService>]
public class IdentityConfirmationService(IServiceProvider serviceProvider)
{
    public async Task SendConfirmationEmailAsync<TUser>(TUser user, UserManager<TUser> userManager, HttpContext context, string email, bool isChange = false)
        where TUser : class, new()
    {
        var emailSender = serviceProvider.GetRequiredService<IEmailSender<TUser>>();
        var linkGenerator = serviceProvider.GetRequiredService<LinkGenerator>();
        var emailEndpoint = serviceProvider.GetRequiredService<IIdentityEmailConfirmationEndpoint>();

        if (emailEndpoint.Name is null)
            throw new NotSupportedException("No email confirmation endpoint was registered!");

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

        if (isChange)
        {
            // This is validated by the /confirmEmail endpoint on change.
            routeValues.Add("changedEmail", email);
        }

        var confirmEmailUrl = linkGenerator.GetUriByName(context, emailEndpoint.Name, routeValues)
                              ?? throw new NotSupportedException($"Could not find endpoint named '{emailEndpoint.Name}'.");

        await emailSender.SendConfirmationLinkAsync(user, email, HtmlEncoder.Default.Encode(confirmEmailUrl));
    }
}