using System.Security.Claims;
using DRN.Framework.Hosting.Auth.Policies;
using DRN.Framework.Utils.Auth;
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
    [DataInline(false)]
    [DataInline(true)]
    public async Task Cookie_Renewal_Should_Preserve_Ephemeral_Custom_Mfa(DrnTestContext context, bool explicitRefresh)
    {
        var config = new AuthenticationClaimConfig { Subject = new("uid"), Mfa = new("permission", "mfa") };
        var application = context.ApplicationContext.CreateApplication<SampleProgram>(builder =>
            builder.ConfigureServices(services =>
            {
                services.AddSingleton(config);
                services.Configure<SecurityStampValidatorOptions>(options => options.OnRefreshingPrincipal = refresh =>
                {
                    var refreshedIdentity = (refresh.NewPrincipal?.Identity).Should().BeOfType<ClaimsIdentity>().Which;
                    refreshedIdentity.AddClaim(new Claim("custom-hook", "called"));
                    return Task.CompletedTask;
                });
                services.PostConfigure<SecurityStampValidatorOptions>(options =>
                    options.ValidationInterval = explicitRefresh ? TimeSpan.MaxValue : TimeSpan.Zero);
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
        principal.HasClaim(config.Mfa.ClaimType, config.Mfa.ClaimValue).Should().BeFalse();
        var identity = (ClaimsIdentity)principal.Identity!;
        identity.AddClaim(new Claim(config.Mfa.ClaimType, config.Mfa.ClaimValue));
        identity.AddClaim(new Claim(config.Mfa.ClaimType, "admin"));
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

        ClaimsPrincipal renewed;
        if (explicitRefresh)
        {
            httpContext.Request.Headers.Cookie = $"{cookieOptions.Cookie.Name}={cookieOptions.TicketDataFormat.Protect(ticket)}";
            signInManager.Context = httpContext;
            await signInManager.RefreshSignInAsync(user);
            httpContext.Response.Headers.SetCookie.ToString().Should().NotBeNullOrEmpty();
            renewed = httpContext.User;
        }
        else
        {
            await cookieOptions.Events.ValidatePrincipal(renewal);
            renewal.ShouldRenew.Should().BeTrue();
            renewal.Principal.Should().NotBeNull();
            renewed = renewal.Principal!;
            renewed.HasClaim("custom-hook", "called").Should().BeTrue();
        }
        renewed.Should().NotBeSameAs(principal);
        renewed.FindAll(config.Mfa.ClaimType).Select(c => c.Value).Should().Equal(config.Mfa.ClaimValue);
        renewed.FindAll("amr").Select(c => c.Value).Should().BeEquivalentTo("pwd", "mfa");
        var authenticationTime = renewed.FindAll("auth_time").Should().ContainSingle().Which;
        authenticationTime.Value.Should().Be("1700000000");
        authenticationTime.Issuer.Should().Be("trusted-issuer");
        MfaAuthorization.IsMfaSatisfied(renewed, config, new MfaExemptionOptions(), null, null)
            .Should().BeTrue();
    }
}
