using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Identity;

namespace DRN.Nexus.Hosted;

public static class NexusModule
{
    private const string AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

    public static IServiceCollection AddNexusServices(this IServiceCollection services, IAppSettings settings)
    {
        services.AddServicesWithAttributes();

        //https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity
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

        return services;
    }
}