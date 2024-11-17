using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Sample.Domain.Users;
using Sample.Hosted.Settings;

namespace Sample.Hosted;

public static class SampleModule
{
    public static IServiceCollection AddSampleHostedServices(this IServiceCollection services, IAppSettings settings)
    {
        var development = settings.IsDevEnvironment;

        services.AddIdentityApiEndpoints<SampleUser>(ConfigureIdentity(development));
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
            options.User = IdentitySettings.UserOptions;
            options.Lockout = IdentitySettings.LockoutOptions;
            options.Password = IdentitySettings.PasswordOptions;
            if (development) return;

            options.SignIn = IdentitySettings.SignInOptions;
        };
    }
}