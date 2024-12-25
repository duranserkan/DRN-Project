using DRN.Framework.Utils.Settings;
using DRN.Nexus.Domain.User;
using DRN.Nexus.Hosted.Settings;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;

namespace DRN.Nexus.Hosted;

public static class NexusModule
{
    private const string AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
    
    public static IServiceCollection AddNexusHostedServices(this IServiceCollection services, IAppSettings settings)
    {
        services.AddIdentityApiEndpoints<NexusUser>(ConfigureIdentity(settings.IsDevEnvironment));
        //.AddPersonalDataProtection<>()

        services.AddServicesWithAttributes();
        services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 1000 * 1024);

        return services;
    }

    private static Action<IdentityOptions> ConfigureIdentity(bool development) => options =>
    {
        options.User = IdentitySettings.UserOptions;
        options.Password = IdentitySettings.PasswordOptions;
        options.Lockout = IdentitySettings.LockoutOptions;
        options.Tokens = IdentitySettings.TokenOptions;
        options.Stores = IdentitySettings.StoreOptions;
        options.ClaimsIdentity = IdentitySettings.ClaimsIdentityOptions;
        if (development) return;

        options.SignIn = IdentitySettings.SignInOptions;
    };
}