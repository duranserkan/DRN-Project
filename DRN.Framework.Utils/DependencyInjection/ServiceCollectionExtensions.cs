using System.Collections.Concurrent;
using System.Reflection;
using DRN.Framework.Utils.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace DRN.Framework.Utils.DependencyInjection;

public static class ServiceCollectionExtensions
{
    private static readonly ConcurrentDictionary<string, LifetimeContainer> ContainerDictionary = new();

    /// <summary>
    /// This method scans implementations with LifetimeAttributes and adds them to the service collection
    /// Method needs to be called from assembly to scan or caller method should provide assembly to override default
    /// </summary>
    public static LifetimeContainer AddServicesWithAttributes(this IServiceCollection sc, Assembly? assembly = null)
    {
        if (Assembly.GetCallingAssembly() != typeof(AppSettings).Assembly) sc.AddDrnUtils();
        assembly ??= Assembly.GetCallingAssembly();

        var container = LifetimeSpecifiedTypes(sc, assembly);
        AddAttributeSpecifiedModules(sc, assembly);

        return container;
    }

    private static LifetimeContainer LifetimeSpecifiedTypes(IServiceCollection sc, Assembly assembly)
    {
        var container = ContainerDictionary.GetOrAdd(assembly.FullName!, x =>
        {
            var lifetimeAttributes = assembly.GetTypes()
                .Where(type => LifetimeAttribute.HasLifetime(type) && !HasServiceCollectionModuleAttribute.HasServiceCollectionModule(type))
                .Select(LifetimeAttribute.GetLifetime).ToArray();
            var container = new LifetimeContainer(assembly, lifetimeAttributes);

            return container;
        });

        container.AddLifetimesToServiceCollection(sc);

        return container;
    }

    private static void AddAttributeSpecifiedModules(IServiceCollection sc, Assembly assembly)
    {
        var moduleTypes = assembly.GetTypes().Where(HasServiceCollectionModuleAttribute.HasServiceCollectionModule).Distinct().ToArray();
        foreach (var moduleType in moduleTypes)
        {
            var moduleAttribute = HasServiceCollectionModuleAttribute.GetModuleAttribute(moduleType);
            var methodInfoProperty = moduleAttribute.GetType().GetProperty(nameof(HasServiceCollectionModuleAttribute.ModuleMethodInfo),
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)!;
            var methodInfo = (MethodInfo)methodInfoProperty.GetValue(null)!;
            methodInfo.Invoke(null, [sc, assembly]);
        }
    }
}