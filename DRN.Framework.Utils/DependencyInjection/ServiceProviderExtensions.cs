using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Utils.DependencyInjection;

public static class ServiceProviderExtensions
{
    public static void ValidateServicesAddedByAttributes(this IServiceProvider sp, bool validateFrameworkAssemblies = false)
    {
        var containers = sp.GetServices<LifetimeContainer>()
            .Where(x => !x.FrameworkAssembly || validateFrameworkAssemblies).SelectMany(x => x.LifetimeAttributes);
        
        foreach (var lifetimeAttribute in containers)
        {
            sp.GetRequiredService(lifetimeAttribute.ServiceType);
        }
    }
}