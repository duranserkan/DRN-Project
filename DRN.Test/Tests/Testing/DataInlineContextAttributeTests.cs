namespace DRN.Test.Tests.Testing;

public class DataInlineContextAttributeTests
{
    /// <param name="context"> Provided by DataInlineContext even if it is not a compile time constant</param>
    /// <param name="autoInlinedDependency">DataInlineContext will provide implementation mocked by NSubstitute</param>
    [Theory]
    [DataInlineContext]
    public void DataInlineContextDemonstration(TestContext context, IMockable autoInlinedDependency)
    {
        autoInlinedDependency.Max.Returns(int.MaxValue); //dependency mocked by NSubstitute

        context.ServiceCollection.AddApplicationServices(); //you can add services, modules defined in hosted app, application, infrastructure layer etc..
        var serviceProvider = context.BuildServiceProvider(); //appsettings.json added by convention. Context and service provider will be disposed by xunit
        var dependentService = serviceProvider.GetRequiredService<DependentService>(); //context replaces actual dependencies with auto inlined dependencies

        dependentService.Max.Should().Be(int.MaxValue);
    }

    /// <param name="context"> Provided by DataInlineContext even if it is not a compile time constant</param>
    /// <param name="inlineData">Provided by DataInlineContext</param>
    /// <param name="autoInlinedData">DataInlineContext will provide missing data with the help of AutoFixture</param>
    /// <param name="autoInlinedMockable">DataInlineContext will provide implementation mocked by NSubstitute</param>
    [Theory]
    [DataInlineContext(99)]
    public void TextContext_Should_Be_Created_From_TestContextData(TestContext context, int inlineData, Guid autoInlinedData, IMockable autoInlinedMockable)
    {
        inlineData.Should().Be(99);
        autoInlinedData.Should().NotBeEmpty(); //guid generated by AutoFixture
        autoInlinedMockable.Max.Returns(int.MaxValue); //dependency mocked by NSubstitute

        context.ServiceCollection.AddApplicationServices(); //you can add services, modules defined in hosted app, application, infrastructure layer etc..
        var serviceProvider = context.BuildServiceProvider(); //appsettings.json added by convention. Context and service provider will be disposed by xunit
        serviceProvider.GetService<ToBeRemovedService>().Should().BeNull(); //Service provider behaviour demonstration

        var dependentService = serviceProvider.GetRequiredService<DependentService>();
        dependentService.Max.Should().Be(int.MaxValue);
    }

    [Theory]
    [DataInlineContext]
    public void TextContext_Should_Provide_AppSettings(TestContext context)
    {
        var serviceProvider = context.BuildServiceProvider(); //appsettings.json added by convention. Context and service provider will be disposed by xunit

        var appSettings = serviceProvider.GetRequiredService<IAppSettings>();
        appSettings.GetRequiredSection("AllowedHosts").Value.Should().Be("*");
        appSettings.TryGetSection("Bar", out _).Should().BeFalse();
        appSettings.GetRequiredConnectionString("Foo").Should().Be("Bar");
        appSettings.TryGetConnectionString("Bar", out _).Should().BeFalse();
    }
}

public static class ApplicationModule //Can be defined in Application Layer or in Hosted App
{
    public static void AddApplicationServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddTransient<IMockable, ToBeRemovedService>(); //will be removed by test context because test method requested mocked interface
        serviceCollection.AddTransient<DependentService>(); //dependent service uses IMockable and Max property returns dependency's Max value
    }
}