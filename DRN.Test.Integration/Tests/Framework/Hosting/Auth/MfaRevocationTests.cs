using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.Extensions.Options;
using Sample.Domain.Users;
using Sample.Hosted;
using Sample.Hosted.Helpers;

namespace DRN.Test.Integration.Tests.Framework.Hosting.Auth;

public class MfaRevocationTests
{
    [Theory]
    [DataInline("administrative", true)]
    [DataInline("enable-factor", true)]
    [DataInline("disable-factor", true)]
    [DataInline("reset-key", true)]
    [DataInline("reset-password", true)]
    [DataInline("regenerate-recovery", false)]
    [DataInline("redeem-recovery", false)]
    public async Task Revocation_Should_Follow_Stamp_Cookie_And_Token_Boundaries(
        DrnTestContext context, string operation, bool rotatesStamp)
    {
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var clock = new ControlledTimeProvider(issuedAt);
        var interval = TimeSpan.FromMinutes(5);
        var application = context.ApplicationContext.CreateApplication<SampleProgram>(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<TimeProvider>(clock);
                services.PostConfigure<SecurityStampValidatorOptions>(options =>
                {
                    options.TimeProvider = clock;
                    options.ValidationInterval = interval;
                });
                services.PostConfigure<BearerTokenOptions>(IdentityConstants.BearerScheme, options => options.TimeProvider = clock);
            }));
        await context.ContainerContext.BindExternalDependenciesAsync();
        using var client = application.CreateClient();
        using var accountScope = application.Services.CreateScope();
        var manager = accountScope.ServiceProvider.GetRequiredService<UserManager<SampleUser>>();
        var signIn = accountScope.ServiceProvider.GetRequiredService<SignInManager<SampleUser>>();
        var email = $"revocation-{Guid.NewGuid():N}@example.com";
        var user = new SampleUser { UserName = email, Email = email };
        (await manager.CreateAsync(user)).Succeeded.Should().BeTrue();
        (await manager.ResetAuthenticatorKeyAsync(user)).Succeeded.Should().BeTrue();
        (await manager.SetTwoFactorEnabledAsync(user, operation != "enable-factor")).Succeeded.Should().BeTrue();
        var recoveryCodes = (await manager.GenerateNewTwoFactorRecoveryCodesAsync(user, 2))!.ToArray();
        var principal = await signIn.CreateUserPrincipalAsync(user);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim("amr", "mfa"));
        var originalStamp = await manager.GetSecurityStampAsync(user);
        var bearer = application.Services.GetRequiredService<IOptionsMonitor<BearerTokenOptions>>().Get(IdentityConstants.BearerScheme);
        var accessExpires = issuedAt.AddMinutes(10);
        var refreshExpires = issuedAt.AddMinutes(30);
        var access = bearer.BearerTokenProtector.Protect(new AuthenticationTicket(principal,
            new AuthenticationProperties { ExpiresUtc = accessExpires }, $"{IdentityConstants.BearerScheme}:AccessToken"));
        var refresh = bearer.RefreshTokenProtector.Protect(new AuthenticationTicket(principal,
            new AuthenticationProperties { ExpiresUtc = refreshExpires }, $"{IdentityConstants.BearerScheme}:RefreshToken"));

        (await AccessStatusAsync(client, access)).Should().Be(HttpStatusCode.OK);
        (await RefreshStatusAsync(client, refresh)).Should().Be(HttpStatusCode.OK);

        switch (operation)
        {
            case "administrative":
                (await manager.UpdateSecurityStampAsync(user)).Succeeded.Should().BeTrue();
                break;
            case "disable-factor":
                (await manager.SetTwoFactorEnabledAsync(user, false)).Succeeded.Should().BeTrue();
                break;
            case "enable-factor":
                (await manager.SetTwoFactorEnabledAsync(user, true)).Succeeded.Should().BeTrue();
                break;
            case "reset-key":
                (await manager.ResetAuthenticatorKeyAsync(user)).Succeeded.Should().BeTrue();
                break;
            case "reset-password":
                var reset = await manager.GeneratePasswordResetTokenAsync(user);
                (await manager.ResetPasswordAsync(user, reset, CredentialsProvider.GenerateCredentials().Password)).Succeeded.Should().BeTrue();
                break;
            case "regenerate-recovery":
                (await manager.GenerateNewTwoFactorRecoveryCodesAsync(user, 2)).Should().NotBeNull();
                break;
            case "redeem-recovery":
                (await manager.RedeemTwoFactorRecoveryCodeAsync(user, recoveryCodes[0])).Succeeded.Should().BeTrue();
                (await manager.RedeemTwoFactorRecoveryCodeAsync(user, recoveryCodes[0])).Succeeded.Should().BeFalse();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }

        var stampChanged = originalStamp != await manager.GetSecurityStampAsync(user);
        stampChanged.Should().Be(rotatesStamp);
        (await RefreshStatusAsync(client, refresh)).Should().Be(rotatesStamp ? HttpStatusCode.Unauthorized : HttpStatusCode.OK);

        // Cookie validation is strictly after the configured interval, not at its exact boundary.
        clock.UtcNow = issuedAt + interval;
        (await CookieAcceptedAsync(application.Services, principal, issuedAt)).Should().BeTrue();
        clock.UtcNow = clock.UtcNow.AddTicks(1);
        (await CookieAcceptedAsync(application.Services, principal, issuedAt)).Should().Be(!rotatesStamp);

        // A stamp change alone does not revoke an already-issued opaque bearer access token.
        clock.UtcNow = accessExpires.AddSeconds(-1);
        (await AccessStatusAsync(client, access)).Should().Be(HttpStatusCode.OK);
        clock.UtcNow = accessExpires;
        (await AccessStatusAsync(client, access)).Should().Be(HttpStatusCode.Unauthorized);
        clock.UtcNow = refreshExpires;
        (await RefreshStatusAsync(client, refresh)).Should().Be(HttpStatusCode.Unauthorized);
    }

    private static async Task<HttpStatusCode> AccessStatusAsync(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Get.Endpoint.User.Identity.ManagementController.GetInfo.RoutePattern!);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static async Task<HttpStatusCode> RefreshStatusAsync(HttpClient client, string token)
    {
        using var response = await client.PostAsJsonAsync(Get.Endpoint.User.Identity.LoginController.Refresh.RoutePattern!,
            new RefreshRequest { RefreshToken = token });
        return response.StatusCode;
    }

    private static async Task<bool> CookieAcceptedAsync(IServiceProvider services, ClaimsPrincipal principal, DateTimeOffset issuedAt)
    {
        using var scope = services.CreateScope();
        var http = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var accessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var previous = accessor.HttpContext;
        accessor.HttpContext = http;
        try
        {
            var options = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(IdentityConstants.ApplicationScheme);
            var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
            {
                IssuedUtc = issuedAt, ExpiresUtc = issuedAt.AddHours(1)
            }, IdentityConstants.ApplicationScheme);
            var validation = new CookieValidatePrincipalContext(http,
                new AuthenticationScheme(IdentityConstants.ApplicationScheme, null, typeof(CookieAuthenticationHandler)), options, ticket);
            await options.Events.ValidatePrincipal(validation);
            return validation.Principal != null;
        }
        finally
        {
            accessor.HttpContext = previous;
        }
    }

    private sealed class ControlledTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }
}
