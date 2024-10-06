using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Sample.Hosted.Auth;
using Sample.Hosted.Auth.Policies;

namespace Sample.Hosted;

public static class SampleModule
{
    private const string AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

    public static IServiceCollection AddSampleServices(this IServiceCollection services, IAppSettings settings)
    {
        services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 1000 * 1024);

        //https://learn.microsoft.com/en-us/aspnet/core/security/?view=aspnetcore-8.0
        //https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/microsoft-logins?view=aspnetcore-8.0
        //https://learn.microsoft.com/en-us/aspnet/core/security/authentication/mfa?view=aspnetcore-8.0
        //https://stackoverflow.com/questions/52492666/what-is-the-point-of-configuring-defaultscheme-and-defaultchallengescheme-on-asp
        //https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity
        //https://learn.microsoft.com/en-us/aspnet/core/security/authorization/limitingidentitybyscheme?view=aspnetcore-8.0
        //AuthenticationSchemeOptions, ConfigureApplicationCookie
        var development = settings.IsDevEnvironment;
        services.AddIdentityApiEndpoints<IdentityUser>(
            options => //IdentityConstants.BearerAndApplicationScheme; //ClaimsIdentity.DefaultIssuer; //ClaimTypes.Name;
            {
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = AllowedUserNameCharacters;

                options.Lockout.MaxFailedAccessAttempts = 3;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(1);

                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequiredLength = 8;
                options.Password.RequiredUniqueChars = 1;
                options.Password.RequireNonAlphanumeric = true;

                if (development) return;

                options.SignIn.RequireConfirmedAccount = true;
                options.SignIn.RequireConfirmedEmail = true;
                options.SignIn.RequireConfirmedPhoneNumber = true;
            });
        //.AddPersonalDataProtection<>()

        services.AddServicesWithAttributes();

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthPolicy.MFA, policy => policy.AddRequirements(new MFARequirement()));
            options.AddPolicy(AuthPolicy.MFAExempt, policy => policy.AddRequirements(new MFAExemptRequirement()));

            options.DefaultPolicy = options.GetPolicy(AuthPolicy.MFA)!;
            options.FallbackPolicy = options.GetPolicy(AuthPolicy.MFA)!;
        });

        return services;
    }
}