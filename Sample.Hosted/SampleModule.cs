using DRN.Framework.SharedKernel.Extensions;
using DRN.Framework.Hosting.Identity;
using DRN.Framework.Hosting.Middlewares.ExceptionHandler;
using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Sample.Application;
using Sample.Domain.Identity;
using Sample.Domain.Users;
using Sample.Hosted.Settings;
using Sample.Infra;
using Sample.Infra.DataProtection;

namespace Sample.Hosted;

//https://learn.microsoft.com/en-us/aspnet/core/security/
public static class SampleModule
{
    public static IServiceCollection AddSampleHostedServices(this IServiceCollection services, IAppSettings settings)
    {
        var securitySettings = new AppSecuritySettings(settings.Features ?? throw ExceptionFor.Configuration("DrnAppFeatures configuration is required for security settings."));

        services
            .AddSampleInfraServices()
            .AddSampleApplicationServices()
            .Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 1000 * 1024) // size limit
            .ConfigureCookieAuthenticationOptions(settings)
            .AddIdentityApiEndpoints<SampleUser>(ConfigureIdentity(settings))
            .AddSignInManager<DrnSignInManager<SampleUser>>();

        services.AddDrnIdentityMfaPolicies();
        services.Configure<DrnErrorPageOptions>(options =>
        {
            options.ServerErrorPage = Get.Page.Root.SomethingWentWrong;
            options.NotFoundPage = Get.Page.Root.NotFound;
            options.BadRequestPage = Get.Page.Root.BadRequest;
        });
        services.AddAuthorization(options =>
        {
            options.AddPolicy(UserClaims.SwaggerAccess, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => settings.IsDevelopmentEnvironment ||
                    context.User.IsInRole(UserRoles.SystemAdmin) ||
                    context.User.HasClaim(claim => claim.Type == UserClaims.SwaggerAccess &&
                        bool.TryParse(claim.Value, out var enabled) && enabled)));
            options.AddPolicy(UserClaims.TestPagesAccess, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => settings.IsDevelopmentEnvironment ||
                    context.User.IsInRole(UserRoles.SystemAdmin) ||
                    context.User.HasClaim(claim => claim.Type == UserClaims.TestPagesAccess &&
                        bool.TryParse(claim.Value, out var enabled) && enabled)));
        });

        //https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview
        //https://www.nuget.org/packages/Microsoft.AspNetCore.DataProtection.EntityFrameworkCore/
        services.AddDataProtection()
            .PersistKeysToDbContext<DrnDataProtectionContext>()
            .SetApplicationName(settings.GetAppSpecificName("DataProtection"))
            .AddKeyManagementOptions(options => options.XmlEncryptor = new SampleXmlEncryptor(securitySettings));

        //.AddPersonalDataProtection<>()
        services.AddServicesWithAttributes();

        return services;
    }

    private static IServiceCollection ConfigureCookieAuthenticationOptions(this IServiceCollection services, IAppSettings settings)
    {
        services.ConfigureApplicationCookie(options =>
        {
            options.ExpireTimeSpan = TimeSpan.FromHours(24);
            options.SlidingExpiration = true;
            options.ClaimsIssuer = settings.ApplicationName.ToPascalCase();
            options.Cookie.IsEssential = true;
            options.Cookie.Name = settings.GetAppSpecificName("Identity");
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            options.LoginPath = Get.Page.User.Login;
            options.LogoutPath = Get.Page.User.Logout;
            options.AccessDeniedPath = Get.Page.Root.AccessDenied;
            options.ReturnUrlParameter = Get.ViewDataKeys.ReturnUrl;
            var redirectToAccessDenied = options.Events.OnRedirectToAccessDenied;
            options.Events.OnRedirectToAccessDenied = async context =>
            {
                await redirectToAccessDenied(context);
                if (context.Response.StatusCode != StatusCodes.Status302Found)
                    return;

                var tempData = context.HttpContext.RequestServices.GetRequiredService<ITempDataDictionaryFactory>()
                    .GetTempData(context.HttpContext);
                tempData[Get.TempDataKeys.AccessDeniedRequestId] = context.HttpContext.TraceIdentifier;
                tempData.Save();
            };
        });

        return services;
    }

    private static Action<IdentityOptions> ConfigureIdentity(IAppSettings settings) => options =>
    {
        options.User = IdentitySettings.UserOptions;
        options.Password = IdentitySettings.PasswordOptions;
        options.Lockout = IdentitySettings.LockoutOptions;
        options.Tokens = IdentitySettings.TokenOptions;
        options.Stores = IdentitySettings.StoreOptions;

        var config = settings.Get<IdentityConfig>("Identity") ?? new IdentityConfig();
        options.SignIn.RequireConfirmedAccount = config.RequireConfirmedAccount;
        options.SignIn.RequireConfirmedEmail = config.RequireConfirmedEmail;
        options.SignIn.RequireConfirmedPhoneNumber = config.RequireConfirmedPhoneNumber;
    };
}
