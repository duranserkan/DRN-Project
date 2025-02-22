using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Sample.Domain.Users;
using Sample.Hosted.Helpers;
using Sample.Hosted.Settings;

namespace Sample.Hosted;

public static class SampleModule
{
    public static IServiceCollection AddSampleHostedServices(this IServiceCollection services, IAppSettings settings)
    {
        services.AddIdentityApiEndpoints<SampleUser>(ConfigureIdentity(settings.IsDevEnvironment));
        //.AddPersonalDataProtection<>()
        
        services.ConfigureApplicationCookie(options =>
        {
            options.ExpireTimeSpan = TimeSpan.FromHours(24);
            options.SlidingExpiration = true;
            options.ClaimsIssuer = settings.ApplicationName;
            options.Cookie.IsEssential = true;
            options.Cookie.Name = $".{settings.ApplicationName.ToPascalCase()}.Identity.Application";
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.LoginPath = Get.Page.User.Login;
            options.LogoutPath = Get.Page.User.Logout;
        });

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