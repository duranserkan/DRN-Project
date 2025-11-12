namespace DRN.Test.Unit.Tests.Framework.Utils.Configurations;

public class JsonSerializerConfigurationTests
{
    [Theory]
    [DataInlineUnit("testDb", "Server=127.0.0.1;Port=5432;Database=myDataBase;User Id=myUsername;Password=myPassword;")]
    public void Object_Should_Be_Added_As_Configuration(DrnTestContextUnit context, string name, string connectionString)
    {
        var connectionStrings = new ConnectionStringsCollection();
        connectionStrings.ConnectionStrings.Add(name, connectionString);
        context.AddToConfiguration(connectionStrings);

        context.GetRequiredService<IAppSettings>().GetRequiredConnectionString(name).Should().Be(connectionString);
        context.GetRequiredService<IAppSettings>().GetRequiredConnectionString("Foo").Should().Be("Bar");
    }

    [Theory]
    [DataInlineUnit("Foo", "Zoo")]
    public void Object_Should_Override_Previous_Configuration(DrnTestContextUnit context, string name, string connectionString)
    {
        var connectionStrings = new ConnectionStringsCollection();
        connectionStrings.ConnectionStrings.Add(name, connectionString);
        context.AddToConfiguration(connectionStrings);

        context.GetRequiredService<IAppSettings>().TryGetConnectionString(name, out var expectedString);
        expectedString.Should().Be(connectionString);
    }
}