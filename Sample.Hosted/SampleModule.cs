using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;

namespace Sample.Hosted;

public static class SampleModule
{
    private const string AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

    public static IServiceCollection AddSampleHostedServices(this IServiceCollection services, IAppSettings settings)
    {
        var development = settings.IsDevEnvironment;

        services.AddIdentityApiEndpoints<IdentityUser>(ConfigureIdentity(development));
        //.AddPersonalDataProtection<>()

        services.AddServicesWithAttributes();
        services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 1000 * 1024);

        return services;
    }

    //https://learn.microsoft.com/en-us/aspnet/core/security/?view=aspnetcore-8.0
    //https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity
    //https://learn.microsoft.com/en-us/aspnet/core/security/authentication/mfa?view=aspnetcore-8.0
    //https://learn.microsoft.com/en-us/aspnet/core/security/authorization/limitingidentitybyscheme?view=aspnetcore-8.0
    //https://learn.microsoft.com/en-us/aspnet/core/security/authentication/social/microsoft-logins?view=aspnetcore-8.0
    //https://stackoverflow.com/questions/52492666/what-is-the-point-of-configuring-defaultscheme-and-defaultchallengescheme-on-asp
    private static Action<IdentityOptions> ConfigureIdentity(bool development)
    {
        //IdentityConstants.BearerAndApplicationScheme; //ClaimsIdentity.DefaultIssuer; //ClaimTypes.Name;
        return options => //IdentityConstants.BearerAndApplicationScheme; //ClaimsIdentity.DefaultIssuer; //ClaimTypes.Name;
        {
            var user = options.User;
            user.RequireUniqueEmail = true;
            user.AllowedUserNameCharacters = AllowedUserNameCharacters;

            var lockout = options.Lockout;
            lockout.MaxFailedAccessAttempts = 3;
            lockout.AllowedForNewUsers = true;
            lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(1);

            var password = options.Password;
            password.RequireDigit = true;
            password.RequireUppercase = true;
            password.RequireLowercase = true;
            password.RequiredLength = 8;
            password.RequiredUniqueChars = 1;
            password.RequireNonAlphanumeric = true;

            if (development) return;

            var signIn = options.SignIn;
            signIn.RequireConfirmedAccount = true;
            signIn.RequireConfirmedEmail = true;
            signIn.RequireConfirmedPhoneNumber = true;
        };
    }
}