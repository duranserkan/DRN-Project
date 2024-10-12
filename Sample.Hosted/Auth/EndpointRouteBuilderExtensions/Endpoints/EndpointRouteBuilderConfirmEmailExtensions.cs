using System.Text;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace Sample.Hosted.Auth.EndpointRouteBuilderExtensions.Endpoints;

public static class EndpointRouteBuilderConfirmEmailExtensions
{
    public static void MapIdentityApiConfirmEmail<TUser>(this IEndpointRouteBuilder endpointBuilder)
        where TUser : class, new()
    {
        ArgumentNullException.ThrowIfNull(endpointBuilder);

        var emailEndpoint = endpointBuilder.ServiceProvider.GetRequiredService<IIdentityEmailConfirmationEndpoint>();
        endpointBuilder.MapGet("/confirmEmail", async Task<Results<ContentHttpResult, UnauthorizedHttpResult>>
                ([FromQuery] string userId, [FromQuery] string code, [FromQuery] string? changedEmail, [FromServices] IServiceProvider sp) =>
            {
                var userManager = sp.GetRequiredService<UserManager<TUser>>();
                if (await userManager.FindByIdAsync(userId) is not { } user)
                {
                    // We could respond with a 404 instead of a 401 like Identity UI, but that feels like unnecessary information.
                    return TypedResults.Unauthorized();
                }

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
                {
                    result = await userManager.ConfirmEmailAsync(user, code);
                }
                else
                {
                    // As with Identity UI, email and username are one and the same. So when we update the email,
                    // we need to update the username.
                    result = await userManager.ChangeEmailAsync(user, changedEmail, code);

                    if (result.Succeeded)
                    {
                        result = await userManager.SetUserNameAsync(user, changedEmail);
                    }
                }

                if (!result.Succeeded)
                    return TypedResults.Unauthorized();

                return TypedResults.Text("Thank you for confirming your email.");
            })
            .AllowAnonymous()
            .Add(endpointBuilder =>
            {
                var finalPattern = ((RouteEndpointBuilder)endpointBuilder).RoutePattern.RawText;
                emailEndpoint.Name = $"{nameof(MapIdentityApiConfirmEmail)}-{finalPattern}";
                endpointBuilder.Metadata.Add(new EndpointNameMetadata(emailEndpoint.Name));
            });
    }
}