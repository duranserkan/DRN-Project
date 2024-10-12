using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.Options;

namespace Sample.Hosted.Auth.EndpointRouteBuilderExtensions.Endpoints;

public static class EndpointRouteBuilderRegisterExtensions
{
    // Validate the email address using DataAnnotations like the UserValidator does when RequireUniqueEmail = true.
    private static readonly EmailAddressAttribute EmailAddressAttribute = new();

    public static void MapIdentityApiRegister<TUser>(this IEndpointRouteBuilder endpointBuilder)
        where TUser : class, new()
    {
        ArgumentNullException.ThrowIfNull(endpointBuilder);

        // NOTE: We cannot inject UserManager<TUser> directly because the TUser generic parameter is currently unsupported by RDG.
        // https://github.com/dotnet/aspnetcore/issues/47338
        endpointBuilder.MapPost("/register", async Task<Results<Ok, ValidationProblem>>
            ([FromBody] RegisterRequest registration, HttpContext context, [FromServices] IServiceProvider sp) =>
        {
            var userManager = sp.GetRequiredService<UserManager<TUser>>();

            if (!userManager.SupportsUserEmail)
                throw new NotSupportedException($"{nameof(MapIdentityApiRegister)} requires a user store with email support.");

            var userStore = sp.GetRequiredService<IUserStore<TUser>>();
            var emailStore = (IUserEmailStore<TUser>)userStore;
            var email = registration.Email;

            if (string.IsNullOrEmpty(email) || !EmailAddressAttribute.IsValid(email))
                return IdentityApiHelper.CreateValidationProblem(IdentityResult.Failed(userManager.ErrorDescriber.InvalidEmail(email)));

            var user = new TUser();
            await userStore.SetUserNameAsync(user, email, CancellationToken.None);
            await emailStore.SetEmailAsync(user, email, CancellationToken.None);
            var result = await userManager.CreateAsync(user, registration.Password);

            if (!result.Succeeded)
                return IdentityApiHelper.CreateValidationProblem(result);

            var confirmationService = sp.GetRequiredService<IdentityConfirmationService>();
            await confirmationService.SendConfirmationEmailAsync(user, userManager, context, email);

            return TypedResults.Ok();
        }).AllowAnonymous();
    }
}