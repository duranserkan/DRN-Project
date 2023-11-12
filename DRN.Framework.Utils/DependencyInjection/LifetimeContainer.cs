using System.Reflection;

namespace DRN.Framework.Utils.DependencyInjection;

public class LifetimeContainer
{
    public Assembly Assembly { get; }
    public LifetimeAttribute[] LifetimeAttributes { get; }

    public LifetimeContainer(Assembly assembly, LifetimeAttribute[] lifetimeAttributes)
    {
        Assembly = assembly;
        LifetimeAttributes = lifetimeAttributes;
    }

    public void Validate(IServiceProvider serviceProvider)
    {

    }
}