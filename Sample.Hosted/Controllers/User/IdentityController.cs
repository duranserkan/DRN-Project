using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.Options;
using Sample.Hosted.Auth.EndpointRouteBuilderExtensions;

namespace Sample.Hosted.Controllers.User;

[ApiController]
[AllowAnonymous]
[Route("[controller]")]
public class IdentityController(
    SignInManager<IdentityUser> signInManager,
    IUserStore<IdentityUser> userStore,
    IdentityConfirmationService confirmationService,
    TimeProvider timeProvider,
    IOptionsMonitor<BearerTokenOptions> bearerTokenOptions) : ControllerBase
{
    private static readonly EmailAddressAttribute EmailAddressAttribute = new();

    [HttpPost(nameof(Register))]
    public async Task<IResult> Register([FromBody] RegisterRequest registration)
    {
        var userManager = signInManager.UserManager;
        if (!userManager.SupportsUserEmail)
            throw new NotSupportedException($"{nameof(IdentityController)}.{nameof(Register)} requires a user store with email support.");

        var emailStore = (IUserEmailStore<IdentityUser>)userStore;
        var email = registration.Email;

        if (string.IsNullOrEmpty(email) || !EmailAddressAttribute.IsValid(email))
            return IdentityApiHelper.CreateValidationProblem(IdentityResult.Failed(userManager.ErrorDescriber.InvalidEmail(email)));

        var user = new IdentityUser();
        await userStore.SetUserNameAsync(user, email, CancellationToken.None);
        await emailStore.SetEmailAsync(user, email, CancellationToken.None);
        var result = await userManager.CreateAsync(user, registration.Password);

        if (!result.Succeeded)
            return IdentityApiHelper.CreateValidationProblem(result);

        await confirmationService.SendConfirmationEmailAsync(user, userManager, HttpContext, email);

        return TypedResults.Ok();
    }

    [HttpPost(nameof(Login))]
    public async Task<IResult> Login([FromBody] LoginRequest login, [FromQuery] bool? useCookies, [FromQuery] bool? useSessionCookies)
    {
        var useCookieScheme = (useCookies == true) || (useSessionCookies == true);
        var isPersistent = (useCookies == true) && (useSessionCookies != true);
        signInManager.AuthenticationScheme = useCookieScheme ? IdentityConstants.ApplicationScheme : IdentityConstants.BearerScheme;

        var result = await signInManager.PasswordSignInAsync(login.Email, login.Password, isPersistent, lockoutOnFailure: true);
        if (result.RequiresTwoFactor)
        {
            if (!string.IsNullOrEmpty(login.TwoFactorCode))
                result = await signInManager.TwoFactorAuthenticatorSignInAsync(login.TwoFactorCode, isPersistent, rememberClient: isPersistent);
            else if (!string.IsNullOrEmpty(login.TwoFactorRecoveryCode))
                result = await signInManager.TwoFactorRecoveryCodeSignInAsync(login.TwoFactorRecoveryCode);
        }

        if (!result.Succeeded)
            return TypedResults.Problem(result.ToString(), statusCode: StatusCodes.Status401Unauthorized);

        // The signInManager already produced the needed response in the form of a cookie or bearer token.
        return TypedResults.Empty;
    }

    [HttpPost(nameof(Refresh))]
    public async Task<IResult> Refresh([FromBody] RefreshRequest refreshRequest)
    {
        var refreshTokenProtector = bearerTokenOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
        var refreshTicket = refreshTokenProtector.Unprotect(refreshRequest.RefreshToken);

        // Reject the /refresh attempt with a 401 if the token expired or the security stamp validation fails
        if (refreshTicket?.Properties?.ExpiresUtc is not { } expiresUtc ||
            timeProvider.GetUtcNow() >= expiresUtc ||
            await signInManager.ValidateSecurityStampAsync(refreshTicket.Principal) is not IdentityUser user)
        {
            return TypedResults.Challenge();
        }

        var newPrincipal = await signInManager.CreateUserPrincipalAsync(user);
        return TypedResults.SignIn(newPrincipal, authenticationScheme: IdentityConstants.BearerScheme);
    }
}