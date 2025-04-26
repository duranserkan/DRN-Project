using System.Text.Json;

namespace DRN.Test.Unit.Tests.Framework.Utils.Configurations;

public class ConfigurationDebugViewTests
{
    [Theory]
    [DataInlineUnit("testDb", "Server=127.0.0.1;Port=5432;Database=myDataBase;User Id=myUsername;Password=myPassword;")]
    public void ConfigurationDebugView_Should_List_Keys(UnitTestContext context, string name, string connectionString)
    {
        var connectionStrings = new ConnectionStringsCollection();
        connectionStrings.ConnectionStrings.Add(name, connectionString);
        context.AddToConfiguration(connectionStrings);

        var debugView = context.GetConfigurationDebugView();
        var debugViewJson = JsonSerializer.Serialize(debugView);
        debugViewJson.Should().NotBeNullOrWhiteSpace();
    }
}