using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Identity;

namespace DRN.Nexus.Hosted;

public static class NexusModule
{
    private const string AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

    public static IServiceCollection AddNexusHostedServices(this IServiceCollection services, IAppSettings settings)
    {
        services.AddServicesWithAttributes();

        var development = settings.IsDevEnvironment;
        services.AddIdentityApiEndpoints<IdentityUser>(ConfigureIdentity(development));
        //.AddPersonalDataProtection<>()

        return services;
    }

    //https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity
    private static Action<IdentityOptions> ConfigureIdentity(bool development)
    {
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