using DRN.Framework.Utils.DependencyInjection;

namespace DRN.Test.Tests.Utils.DependencyInjectionTests;

public class LifetimeContainerTests
{
    [Theory]
    [DataInlineContext]
    public void Services_Marked_By_Lifetime_Attributes_Should_Be_Added_To_ServiceProvider(TestContext context)
    {
        var containers = context.GetServices<LifetimeContainer>().ToArray();
        var utilsAssemblyContainer = containers.Single(c => c.Assembly == typeof(IAppSettings).Assembly);
        var lifetime = utilsAssemblyContainer.LifetimeAttributes.Single(l => l.ServiceType == typeof(IAppSettings) &&
                                                                             l.ImplementationType == typeof(AppSettings) &&
                                                                             l.ServiceLifetime == ServiceLifetime.Singleton);
        var appSettings = context.GetRequiredService<IAppSettings>();
    }
}