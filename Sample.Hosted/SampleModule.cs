using DRN.Framework.Utils.Extensions;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Sample.Application;
using Sample.Domain.Users;
using Sample.Hosted.Helpers;
using Sample.Hosted.Settings;
using Sample.Infra;

namespace Sample.Hosted;

public static class SampleModule
{
    public static IServiceCollection AddSampleHostedServices(this IServiceCollection services, IAppSettings settings)
    {
        services
            .AddSampleInfraServices()
            .AddSampleApplicationServices()
            .Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 1000 * 1024) // size limit
            .ConfigureCookieAuthenticationOptions(settings)
            .AddIdentityApiEndpoints<SampleUser>(ConfigureIdentity(settings.IsDevEnvironment));
        //.AddPersonalDataProtection<>() //todo: enable personal data protection

        services.AddServicesWithAttributes();

        return services;
    }

    private static IServiceCollection ConfigureCookieAuthenticationOptions(this IServiceCollection services, IAppSettings settings)
    {
        services.ConfigureApplicationCookie(options =>
        {
            options.ExpireTimeSpan = TimeSpan.FromHours(24);
            options.SlidingExpiration = true;
            options.ClaimsIssuer = settings.ApplicationName.ToPascalCase(); //todo: test the issuer, test valid issuer such as TokenValidationParameters.ValidIssuer
            options.Cookie.IsEssential = true;
            options.Cookie.Name = settings.GetAppSpecificName("Identity");
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.LoginPath = Get.Page.User.Login;
            options.LogoutPath = Get.Page.User.Logout;
            options.AccessDeniedPath = "/AccessDenied"; // Optional: for authorized-but-forbidden
            options.ReturnUrlParameter = Get.ViewDataKeys.ReturnUrl;
        });

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