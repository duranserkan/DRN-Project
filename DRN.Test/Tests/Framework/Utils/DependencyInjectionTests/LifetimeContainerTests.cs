using DRN.Nexus.Application;
using DRN.Nexus.Infra;
using DRN.Test.Tests.Framework.Utils.DependencyInjectionTests.Models;
using Sample.Application;
using Sample.Infra;

namespace DRN.Test.Tests.Framework.Utils.DependencyInjectionTests;

public class LifetimeContainerTests
{
    [Theory]
    [DataInline]
    public void Services_Marked_By_Lifetime_Attributes_Should_Be_Added_To_ServiceProvider(TestContext context)
    {
        context.ServiceCollection.AddTestModule();
        var containers = context.GetServices<LifetimeContainer>().ToArray();
        var utilsAssemblyContainer = containers.Single(c => c.Assembly == typeof(IAppSettings).Assembly);
        utilsAssemblyContainer.LifetimeAttributes.Single(l =>
            l.ServiceType == typeof(IAppSettings) && l.ImplementationType == typeof(AppSettings) && l.ServiceLifetime == ServiceLifetime.Singleton);

        context.GetRequiredService<Dependent>();
        context.GetRequiredKeyedService<IKeyed>(1);
        context.GetRequiredKeyedService<IKeyed>(2);
        context.GetRequiredKeyedService<IKeyed>("A");
        context.GetRequiredKeyedService<IKeyed>("B");
        context.GetKeyedServices<IKeyed>("Multiple").Count().Should().Be(2);
        context.GetRequiredKeyedService<IKeyed>(Keyed.First);
        context.GetRequiredKeyedService<IKeyed>(Keyed.Second);

        context.ValidateServices();
    }

    [Theory]
    [DataInline]
    public void Service_Provider_Should_Throw_Exception_When_Service_Is_Not_Resolvable(TestContext context)
    {
        context.ServiceCollection.AddTestModule();
        context.ServiceCollection.RemoveAll(typeof(IIndependent));

        var action = context.ValidateServices;
        action.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [DataInline]
    public void Service_Provider_Should_Throw_Exception_When_Keyed_Service_Is_Not_Resolvable(TestContext context)
    {
        context.ServiceCollection.AddTestModule();
        context.ServiceCollection.RemoveAll(typeof(IKeyedDependency));

        var action = context.ValidateServices;
        action.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [DataInline]
    public void Lifetime_Attributes_Should_Add_Multiple(TestContext context)
    {
        context.ServiceCollection.AddTestModule();
        context.GetServices<IMultiple>().ToArray().Length.Should().Be(2);
    }

    [Theory]
    [DataInline]
    public void Service_Provider_Should_Throw_Exception_When_One_Of_Multiple_Services_Is_Not_Resolvable(TestContext context)
    {
        context.ServiceCollection.AddTestModule();
        context.ServiceCollection.RemoveAll(typeof(IMultipleIndependent));

        var action = context.ValidateServices;
        action.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [DataInline]
    public void Validate_Sample_Dependencies(TestContext context)
    {
        context.ServiceCollection.AddSampleApplicationServices();
        context.ServiceCollection.AddSampleInfraServices(context.BuildConfigurationRoot());
        context.ValidateServices();
    }

    [Theory]
    [DataInline]
    public void Validate_Nexus_Dependencies(TestContext context)
    {
        context.ServiceCollection.AddNexusApplicationServices();
        context.ServiceCollection.AddNexusInfraServices();
        context.ValidateServices();
    }
}