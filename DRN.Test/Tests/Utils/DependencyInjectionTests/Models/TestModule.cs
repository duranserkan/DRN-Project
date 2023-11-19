namespace DRN.Test.Tests.Utils.DependencyInjectionTests.Models;

public static class TestModule
{
    public static IServiceCollection AddTestModule(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddServicesWithAttributes();

        return serviceCollection;
    }
}