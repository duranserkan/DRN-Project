namespace DRN.Test.Unit.Tests.Framework.Utils.DependencyInjection;

public class ServiceProviderIsolationTests
{
    [Fact]
    public void AddServicesWithAttributes_Should_Isolate_Containers_Between_Service_Collections()
    {
        var assembly = typeof(AppSettings).Assembly;
        var firstServices = new ServiceCollection();
        var secondServices = new ServiceCollection();

        var firstContainer = firstServices.AddServicesWithAttributes(assembly);
        var repeatedFirstContainer = firstServices.AddServicesWithAttributes(assembly);
        var secondContainer = secondServices.AddServicesWithAttributes(assembly);

        repeatedFirstContainer.Should().BeSameAs(firstContainer);
        secondContainer.Should().NotBeSameAs(firstContainer);
        secondContainer.LifetimeAttributes[0].Should().NotBeSameAs(firstContainer.LifetimeAttributes[0]);
        secondContainer.AttributeSpecifiedModules.Should().NotBeSameAs(firstContainer.AttributeSpecifiedModules);
        firstServices.Count(descriptor =>
                descriptor.ServiceType == typeof(DrnServiceContainer) &&
                descriptor.ImplementationInstance is DrnServiceContainer container &&
                ReferenceEquals(container.Assembly, assembly))
            .Should().Be(1);
    }

    [Fact]
    public async Task ValidateServicesAddedByAttributesAsync_Should_Validate_Each_Service_Provider()
    {
        using var firstProvider = CreateProvider(typeof(FirstValidProbe), typeof(SecondValidProbe));
        using var secondProvider = CreateProvider(typeof(FirstValidProbe), typeof(InvalidProbe));

        await firstProvider.ValidateServicesAddedByAttributesAsync();
        Func<Task> validation = () => secondProvider.ValidateServicesAddedByAttributesAsync();

        await validation.Should().ThrowAsync<InvalidOperationException>();
    }

    private static ServiceProvider CreateProvider(params Type[] implementationTypes)
    {
        var services = new ServiceCollection();
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.Features.Returns(new DrnAppFeatures());
        appSettings.DevelopmentSettings.Returns(new DrnDevelopmentSettings());
        services.AddSingleton(appSettings);

        foreach (var implementationType in implementationTypes)
            services.AddTransient(typeof(IProviderValidationProbe), implementationType);

        var lifetimeAttribute = new TransientAttribute<IProviderValidationProbe>(tryAdd: false)
        {
            ImplementationType = implementationTypes[0]
        };
        services.AddSingleton(new DrnServiceContainer(
            typeof(ServiceProviderIsolationTests).Assembly,
            [lifetimeAttribute]));

        return services.BuildServiceProvider();
    }

    public interface IProviderValidationProbe;

    public interface IMissingProbeDependency;

    public sealed class FirstValidProbe : IProviderValidationProbe;

    public sealed class SecondValidProbe : IProviderValidationProbe;

    public sealed class InvalidProbe(IMissingProbeDependency dependency) : IProviderValidationProbe
    {
        public IMissingProbeDependency Dependency { get; } = dependency;
    }
}
