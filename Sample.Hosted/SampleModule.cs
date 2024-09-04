using DRN.Framework.Utils.Settings;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;

namespace Sample.Hosted;

public static class SampleModule
{
    public static IServiceCollection AddSampleServices(this IServiceCollection services, IAppSettings appSettings)
    {
        services.AddServicesWithAttributes();
        services.AddIdentityApiEndpoints<IdentityUser>()
            .AddDefaultTokenProviders();
        //https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity

        services.Configure<FormOptions>(options =>
        {
            // Set the maximum file size to 100KB (102,400 bytes)
            options.MultipartBodyLengthLimit = 1000 * 1024; // 1000 KB
        });

        return services;
    }
}