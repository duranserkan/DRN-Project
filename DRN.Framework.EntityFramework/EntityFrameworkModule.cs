using DRN.Framework.Utils.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.EntityFramework;

public static class EntityFrameworkModule
{
    public static IServiceCollection AddDrnDataProtectionContext(this IServiceCollection services)
    {
        services.AddServicesWithAttributes();
        return services;
    }
}