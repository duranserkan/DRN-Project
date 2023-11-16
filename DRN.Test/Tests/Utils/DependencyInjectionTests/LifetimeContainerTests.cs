using DRN.Test.Tests.Utils.DependencyInjectionTests.Models;

namespace DRN.Test.Tests.Utils.DependencyInjectionTests;

public class LifetimeContainerTests
{
    [Theory]
    [DataInlineContext]
    public void Services_Marked_By_Lifetime_Attributes_Should_Be_Added_To_ServiceProvider(TestContext context)
    {
        context.ServiceCollection.AddTestModule();
        var containers = context.GetServices<LifetimeContainer>().ToArray();
        var utilsAssemblyContainer = containers.Single(c => c.Assembly == typeof(IAppSettings).Assembly);
        utilsAssemblyContainer.LifetimeAttributes.Single(l =>
            l.ServiceType == typeof(IAppSettings) && l.ImplementationType == typeof(AppSettings) && l.ServiceLifetime == ServiceLifetime.Singleton);
        context.GetRequiredService<Dependent>();

        context.ValidateServicesAddedByAttributes();
    }

    [Theory]
    [DataInlineContext]
    public void Service_Provider_Should_Throw_Exception_When_Service_Is_Not_Resolvable(TestContext context)
    {
        context.ServiceCollection.AddTestModule();
        context.ServiceCollection.RemoveAll(typeof(IIndependent));

        var action = () => context.ValidateServicesAddedByAttributes();
        action.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [DataInlineContext]
    public void Lifetime_Attributes_Should_Add_Multiple(TestContext context)
    {
        context.ServiceCollection.AddTestModule();

        var multipleInstances = context.GetServices<IMultiple>().ToArray();
        multipleInstances.Length.Should().Be(2);
    }
}