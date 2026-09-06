using DRN.Framework.Utils.Logging;

namespace DRN.Test.Unit.Tests.Framework.Utils.DependencyInjection;

public class ServiceRegistrationAttributeTests
{
    [Theory]
    [DataInlineUnit(typeof(MultipleDerivedModules))]
    [DataInlineUnit(typeof(RepeatedModuleType))]
    public async Task Discovery_Should_Register_And_Validate_All_Distinct_Modules(Type moduleType)
    {
        var assembly = Substitute.For<Assembly>();
        assembly.GetTypes().Returns([moduleType, typeof(SingleModule), typeof(NoModule)]);
        var services = new ServiceCollection();
        var appSettings = Substitute.For<IAppSettings>();
        appSettings.Features.Returns(new DrnAppFeatures());
        appSettings.DevelopmentSettings.Returns(new DrnDevelopmentSettings());
        services.AddSingleton(appSettings);

        var container = services.AddServicesWithAttributes(assembly);
        var repeatedContainer = services.AddServicesWithAttributes(assembly);

        repeatedContainer.Should().BeSameAs(container);
        container.AttributeSpecifiedModules.Should().HaveCount(2);
        container.AttributeSpecifiedModules.SelectMany(module => module.ServiceDescriptors)
            .Select(descriptor => descriptor.ServiceType)
            .Should().BeEquivalentTo([typeof(FirstProbe), typeof(SecondProbe)]);
        services.Count(descriptor => descriptor.ServiceType == typeof(FirstProbe)).Should().Be(1);
        services.Count(descriptor => descriptor.ServiceType == typeof(SecondProbe)).Should().Be(1);

        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<FirstProbe>();
        var second = provider.GetRequiredService<SecondProbe>();
        first.ValidationCount.Should().Be(0);
        second.ValidationCount.Should().Be(0);

        // This regression targets module callbacks; unrelated Utils lifetimes are validated elsewhere.
        await provider.ValidateServicesAddedByAttributesAsync(ignore: _ => true);

        first.ValidationCount.Should().Be(1);
        second.ValidationCount.Should().Be(1);
    }

    [Fact]
    public void GetModuleAttributes_Should_Support_Zero_One_And_Multiple_Attributes()
    {
        ServiceRegistrationAttribute.GetModuleAttributes(typeof(NoModule)).Should().BeEmpty();
        ServiceRegistrationAttribute.GetModuleAttributes(typeof(SingleModule)).Should().ContainSingle();
        ServiceRegistrationAttribute.GetModuleAttribute(typeof(SingleModule)).Should().BeOfType<ProbeRegistrationAttribute>();
        var noModule = () => ServiceRegistrationAttribute.GetModuleAttribute(typeof(NoModule));
        var multipleModules = () => ServiceRegistrationAttribute.GetModuleAttribute(typeof(MultipleDerivedModules));
        noModule.Should().ThrowExactly<InvalidOperationException>();
        multipleModules.Should().ThrowExactly<InvalidOperationException>();
        ServiceRegistrationAttribute.GetModuleAttributes(typeof(MultipleDerivedModules))
            .Select(attribute => attribute.GetType())
            .Should().BeEquivalentTo([typeof(ProbeRegistrationAttribute), typeof(SecondRegistrationAttribute)]);
    }

    [ProbeRegistration(typeof(FirstProbe))]
    [SecondRegistration]
    public sealed class MultipleDerivedModules;

    [ProbeRegistration(typeof(FirstProbe))]
    [ProbeRegistration(typeof(SecondProbe))]
    public sealed class RepeatedModuleType;

    [ProbeRegistration(typeof(FirstProbe))]
    public sealed class SingleModule;

    public sealed class NoModule;

    public abstract class Probe
    {
        public int ValidationCount { get; set; }
    }

    public sealed class FirstProbe : Probe;
    public sealed class SecondProbe : Probe;

    public class ProbeRegistrationAttribute(Type serviceType) : ServiceRegistrationAttribute
    {
        public override void ServiceRegistration(IServiceCollection sc, Assembly? assembly)
            => sc.AddSingleton(serviceType);

        public override Task PostStartupValidationAsync(object service, IServiceProvider serviceProvider, IScopedLog? scopedLog = null)
        {
            ((Probe)service).ValidationCount++;
            return Task.CompletedTask;
        }
    }

    public sealed class SecondRegistrationAttribute() : ProbeRegistrationAttribute(typeof(SecondProbe));
}
