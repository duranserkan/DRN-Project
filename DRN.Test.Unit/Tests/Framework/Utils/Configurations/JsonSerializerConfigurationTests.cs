using DRN.Framework.Utils.Configurations;

namespace DRN.Test.Unit.Tests.Framework.Utils.Configurations;

public class JsonSerializerConfigurationTests
{
    [Theory]
    [DataInlineUnit("testDb", "Server=127.0.0.1;Port=5432;Database=myDataBase;User Id=myUsername;Password=myPassword;", "Bar")]
    [DataInlineUnit("Foo", "Zoo", "Zoo")]
    public void Object_Should_Add_Or_Override_Configuration(DrnTestContextUnit context, string name, string connectionString, string expectedFoo)
    {
        var connectionStrings = new ConnectionStringsCollection();
        connectionStrings.ConnectionStrings.Add(name, connectionString);
        context.AddToConfiguration(connectionStrings);

        var settings = context.GetRequiredService<IAppSettings>();
        settings.GetRequiredConnectionString(name).Should().Be(connectionString);
        settings.GetRequiredConnectionString("Foo").Should().Be(expectedFoo);
        settings.TryGetConnectionString(name, out var expectedString);
        expectedString.Should().Be(connectionString);
    }

    [Fact]
    public void ObjectToJsonConfigurationSource_Should_Support_Multiple_Build_Calls()
    {
        var source = new ObjectToJsonConfigurationSource(new { TestKey = "TestValue" });
        var builder = new ConfigurationBuilder().Add(source);

        var config1 = builder.Build();
        config1["TestKey"].Should().Be("TestValue");

        var config2 = builder.Build();
        config2["TestKey"].Should().Be("TestValue");
    }
}
