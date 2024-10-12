using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;

namespace Sample.Hosted.Auth.EndpointRouteBuilderExtensions.Endpoints;

public static class EndpointRouteBuilderResendConfirmationEmailExtensions
{
    public static void MapIdentityApiResendConfirmationEmail<TUser>(this IEndpointRouteBuilder endpointBuilder)
        where TUser : class, new()
    {
        ArgumentNullException.ThrowIfNull(endpointBuilder);

        endpointBuilder.MapPost("/resendConfirmationEmail", async Task<Ok>
            ([FromBody] ResendConfirmationEmailRequest resendRequest, HttpContext context, [FromServices] IServiceProvider sp) =>
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();
            if (await userManager.FindByEmailAsync(resendRequest.Email) is not { } user)
                return TypedResults.Ok();

            var confirmationService = sp.GetRequiredService<IdentityConfirmationService>();
            await confirmationService.SendConfirmationEmailAsync(user, userManager, context, resendRequest.Email);

            return TypedResults.Ok();
        }).AllowAnonymous();
    }
}