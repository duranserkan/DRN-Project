using System.Reflection;

namespace DRN.Framework.Utils.DependencyInjection;

public class LifetimeContainer
{
    public Assembly Assembly { get; }
    public LifetimeAttribute[] LifetimeAttributes { get; }
    public bool FrameworkAssembly { get; }

    public LifetimeContainer(Assembly assembly, LifetimeAttribute[] lifetimeAttributes)
    {
        Assembly = assembly;
        LifetimeAttributes = lifetimeAttributes;
        FrameworkAssembly = Assembly.FullName?.StartsWith("DRN.Framework") ?? false;
    }
}