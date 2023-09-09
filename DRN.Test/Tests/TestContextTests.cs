namespace DRN.Test.Tests;

public class TestContextTests
{
    [Theory]
    [TestContextData(99)]
    public void TextContext_Should_Be_Created_From_TestContextData(TestContext context, int inlineData, IMockable mockable)
    {
        var serviceProvider = context.InitServiceProvider();
        var appSettings = serviceProvider.GetRequiredService<IAppSettings>();
        appSettings.GetRequiredSection("AllowedHosts").Value.Should().Be("*");
        appSettings.TryGetSection("Bar", out _).Should().BeFalse();
        appSettings.GetRequiredConnectionString("Foo").Should().Be("Bar");
        appSettings.TryGetConnectionString("Bar", out _).Should().BeFalse();

        inlineData.Should().Be(99);
        mockable.Max.Returns(int.MaxValue);
        mockable.Max.Should().Be(int.MaxValue);
    }

    [Theory]
    [TestContextData]
    public void Existing_Dependency_Should_Be_Replaced_With_New_Implementation(TestContext context)
    {
        var serviceCollection = context.ServiceCollection;
        serviceCollection.AddTransient<IMockable,ToBeRemovedService>();
        serviceCollection.ReplaceTransient<IMockable,ReplacingService>(new ReplacingService());

        var serviceProvider = context.InitServiceProvider();

        serviceProvider.GetService<ToBeRemovedService>().Should().BeNull();
        serviceProvider.GetRequiredService<IMockable>().Should().BeOfType<ReplacingService>();
    }
}

public interface IMockable
{
    public int Max { get; set; }
}

public class ToBeRemovedService:IMockable
{
    public int Max { get; set; }
}

public class ReplacingService:IMockable
{
    public int Max { get; set; }
}