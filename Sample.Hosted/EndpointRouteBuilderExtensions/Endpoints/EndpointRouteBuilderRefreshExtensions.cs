using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.Options;

namespace Sample.Hosted.EndpointRouteBuilderExtensions.Endpoints;

public static class EndpointRouteBuilderRefreshExtensions
{
    public static void MapIdentityApiRefresh<TUser>(this IEndpointRouteBuilder endpointBuilder)
        where TUser : class, new()
    {
        ArgumentNullException.ThrowIfNull(endpointBuilder);

        var timeProvider = endpointBuilder.ServiceProvider.GetRequiredService<TimeProvider>();
        var bearerTokenOptions = endpointBuilder.ServiceProvider.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>();

        endpointBuilder.MapPost("/refresh", async Task<Results<Ok<AccessTokenResponse>, UnauthorizedHttpResult, SignInHttpResult, ChallengeHttpResult>>
            ([FromBody] RefreshRequest refreshRequest, [FromServices] IServiceProvider sp) =>
        {
            var signInManager = sp.GetRequiredService<SignInManager<TUser>>();
            var refreshTokenProtector = bearerTokenOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
            var refreshTicket = refreshTokenProtector.Unprotect(refreshRequest.RefreshToken);

            // Reject the /refresh attempt with a 401 if the token expired or the security stamp validation fails
            if (refreshTicket?.Properties?.ExpiresUtc is not { } expiresUtc ||
                timeProvider.GetUtcNow() >= expiresUtc ||
                await signInManager.ValidateSecurityStampAsync(refreshTicket.Principal) is not TUser user)
            {
                return TypedResults.Challenge();
            }

            var newPrincipal = await signInManager.CreateUserPrincipalAsync(user);
            return TypedResults.SignIn(newPrincipal, authenticationScheme: IdentityConstants.BearerScheme);
        }).AllowAnonymous();
    }
}