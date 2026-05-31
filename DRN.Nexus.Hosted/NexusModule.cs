using DRN.Framework.Utils.Settings;
using DRN.Nexus.Application;
using DRN.Nexus.Domain.User;
using DRN.Nexus.Hosted.Settings;
using DRN.Nexus.Infra;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;

namespace DRN.Nexus.Hosted;

public static class NexusModule
{
    public static IServiceCollection AddNexusHostedServices(this IServiceCollection services, IAppSettings settings)
    {
        services
            .AddNexusInfraServices()
            .AddNexusApplicationServices()
            .Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 1000 * 1024) // size limit
            //.ConfigureCookieAuthenticationOptions(settings) //todo: update with nexus implementation
            .AddIdentityApiEndpoints<NexusUser>(ConfigureIdentity(settings));
        //.AddPersonalDataProtection<>() //todo: enable personal data protection

        services.AddServicesWithAttributes();

        return services;
    }


    private static Action<IdentityOptions> ConfigureIdentity(IAppSettings settings) => options =>
    {
        options.User = IdentitySettings.UserOptions;
        options.Password = IdentitySettings.PasswordOptions;
        options.Lockout = IdentitySettings.LockoutOptions;
        options.Tokens = IdentitySettings.TokenOptions;
        options.Stores = IdentitySettings.StoreOptions;
        options.ClaimsIdentity = IdentitySettings.ClaimsIdentityOptions;

        var config = settings.Get<NexusIdentityConfig>("Identity") ?? new NexusIdentityConfig();
        options.SignIn.RequireConfirmedAccount = config.RequireConfirmedAccount;
        options.SignIn.RequireConfirmedEmail = config.RequireConfirmedEmail;
        options.SignIn.RequireConfirmedPhoneNumber = config.RequireConfirmedPhoneNumber;
    };
}