namespace DRN.Test.Tests.Utils.Configurations;

public class JsonSerializerConfigurationTests
{
    [Theory]
    [DataInlineContext("testDb", "Server=127.0.0.1;Port=5432;Database=myDataBase;User Id=myUsername;Password=myPassword;")]
    public void Object_Should_Be_Added_As_Configuration(TestContext context, string name, string connectionString)
    {
        var connectionStrings = new ConnectionStringsCollection();
        connectionStrings.ConnectionStrings.Add(name, connectionString);
        context.AddToConfiguration(connectionStrings);

        context.GetRequiredService<IAppSettings>().GetRequiredConnectionString(name).Should().Be(connectionString);
        context.GetRequiredService<IAppSettings>().GetRequiredConnectionString("Foo").Should().Be("Bar");
    }

    [Theory]
    [DataInlineContext("Foo", "Zoo")]
    public void Object_Should_Override_Previous_Configuration(TestContext context, string name, string connectionString)
    {
        var connectionStrings = new ConnectionStringsCollection();
        connectionStrings.ConnectionStrings.Add(name, connectionString);
        context.AddToConfiguration(connectionStrings);

        context.GetRequiredService<IAppSettings>().TryGetConnectionString(name, out var expectedString);
        expectedString.Should().Be(connectionString);
    }
}