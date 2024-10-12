using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.WebUtilities;

namespace Sample.Hosted.Auth.EndpointRouteBuilderExtensions.Endpoints;

public static class EndpointRouteBuilderForgotPasswordExtensions
{
    public static void MapIdentityApiForgotPassword<TUser>(this IEndpointRouteBuilder endpointBuilder)
        where TUser : class, new()
    {
        ArgumentNullException.ThrowIfNull(endpointBuilder);

        var emailSender = endpointBuilder.ServiceProvider.GetRequiredService<IEmailSender<TUser>>();

        endpointBuilder.MapPost("/forgotPassword", async Task<Results<Ok, ValidationProblem>>
            ([FromBody] ForgotPasswordRequest resetRequest, [FromServices] IServiceProvider sp) =>
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();
            var user = await userManager.FindByEmailAsync(resetRequest.Email);

            if (user is not null && await userManager.IsEmailConfirmedAsync(user))
            {
                var code = await userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                await emailSender.SendPasswordResetCodeAsync(user, resetRequest.Email, HtmlEncoder.Default.Encode(code));
            }

            // Don't reveal that the user does not exist or is not confirmed, so don't return a 200 if we would have
            // returned a 400 for an invalid code given a valid user email.
            return TypedResults.Ok();
        }).AllowAnonymous();
    }
}