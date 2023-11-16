using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Utils.DependencyInjection;

public static class ServiceProviderExtensions
{
    public static void ValidateServicesAddedByAttributes(this IServiceProvider sp)
    {
        var containers = sp.GetServices<LifetimeContainer>().SelectMany(x => x.LifetimeAttributes);
        
        foreach (var lifetimeAttribute in containers)
        {
            sp.GetRequiredService(lifetimeAttribute.ServiceType);
        }
    }
}