using System.Security.Claims;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Utils.Auth.MFA;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Sample.Domain.Users;
using Sample.Hosted;

namespace DRN.Test.Integration.Tests.Framework.Hosting.Auth;

public class CookieMfaRenewalTests
{
    [Theory]
    [DataInline]
    public async Task SecurityStamp_Renewal_Should_Preserve_Ephemeral_Custom_Mfa(DrnTestContext context)
    {
        var config = new MfaClaimConfig("permission", "mfa");
        var application = context.ApplicationContext.CreateApplication<SampleProgram>(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(config);
                services.PostConfigure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
            }));
        await context.ContainerContext.BindExternalDependenciesAsync();
        using var client = application.CreateClient();
        using var scope = application.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<SampleUser>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<SampleUser>>();
        var email = $"cookie-mfa-{Guid.NewGuid():N}@example.com";
        var user = new SampleUser { UserName = email, Email = email };
        (await userManager.CreateAsync(user)).Succeeded.Should().BeTrue();

        var principal = await signInManager.CreateUserPrincipalAsync(user);
        principal.HasClaim(config.ClaimType, config.ClaimValue).Should().BeFalse();
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.AddClaim(new Claim(config.ClaimType, config.ClaimValue));
        identity.AddClaim(new Claim(config.ClaimType, "admin"));
        identity.AddClaim(new Claim("amr", "pwd"));
        identity.AddClaim(new Claim("amr", "mfa"));
        identity.AddClaim(new Claim("auth_time", "1700000000", ClaimValueTypes.Integer64, "trusted-issuer"));

        var cookieOptions = scope.ServiceProvider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(IdentityConstants.ApplicationScheme);
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        var ticket = new AuthenticationTicket(principal, new AuthenticationProperties
        {
            IssuedUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
        }, IdentityConstants.ApplicationScheme);
        var renewal = new CookieValidatePrincipalContext(httpContext,
            new AuthenticationScheme(IdentityConstants.ApplicationScheme, null, typeof(CookieAuthenticationHandler)),
            cookieOptions, ticket);

        await cookieOptions.Events.ValidatePrincipal(renewal);

        renewal.ShouldRenew.Should().BeTrue();
        renewal.Principal.Should().NotBeNull();
        renewal.Principal.Should().NotBeSameAs(principal);
        renewal.Principal!.FindAll(config.ClaimType).Select(c => c.Value).Should().Equal(config.ClaimValue);
        renewal.Principal.FindAll("amr").Select(c => c.Value).Should().BeEquivalentTo("pwd", "mfa");
        var authenticationTime = renewal.Principal.FindAll("auth_time").Should().ContainSingle().Which;
        authenticationTime.Value.Should().Be("1700000000");
        authenticationTime.Issuer.Should().Be("trusted-issuer");
        MfaAuthorization.IsMfaSatisfied(renewal.Principal, config, new MfaExemptionOptions(), null, null)
            .Should().BeTrue();
    }
}
