using System.Collections.Concurrent;
using System.Reflection;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Utils.DependencyInjection;

public static class ServiceCollectionExtensions
{
    private static readonly ConcurrentDictionary<string, DrnServiceContainer> ContainerDictionary = new();

    /// <summary>
    /// This method scans implementations with LifetimeAttributes and adds them to the service collection
    /// Method needs to be called from assembly to scan or caller method should provide assembly to override default
    /// </summary>
    public static DrnServiceContainer AddServicesWithAttributes(this IServiceCollection sc, Assembly? assembly = null)
    {
        if (Assembly.GetCallingAssembly() != typeof(AppSettings).Assembly) sc.AddDrnUtils();
        assembly ??= Assembly.GetCallingAssembly();

        var container = CreateDrnServiceContainer(assembly);
        container.AddServices(sc);

        return container;
    }

    private static DrnServiceContainer CreateDrnServiceContainer(Assembly assembly)
    {
        var container = ContainerDictionary.GetOrAdd(assembly.FullName!, x =>
        {
            var lifetimeAttributes = assembly.GetTypes()
                .Where(type => LifetimeAttribute.HasLifetime(type) && !HasServiceCollectionModuleAttribute.HasServiceCollectionModule(type))
                .Select(LifetimeAttribute.GetLifetime).ToArray();
            var container = new DrnServiceContainer(assembly, lifetimeAttributes);

            return container;
        });

        return container;
    }
}