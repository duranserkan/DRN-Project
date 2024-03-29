using System.Text.Json;

namespace DRN.Test.Tests.Framework.Utils.Configurations;

public class ConfigurationDebugViewTests
{
    [Theory]
    [DataInline("testDb", "Server=127.0.0.1;Port=5432;Database=myDataBase;User Id=myUsername;Password=myPassword;")]
    public void ConfigurationDebugView_Should_List_Keys(TestContext context, string name, string connectionString)
    {
        var connectionStrings = new ConnectionStringsCollection();
        connectionStrings.ConnectionStrings.Add(name, connectionString);
        context.AddToConfiguration(connectionStrings);

        var debugView = context.GetConfigurationDebugView();
        var debugViewJson = JsonSerializer.Serialize(debugView);
        debugViewJson.Should().NotBeNullOrWhiteSpace();
    }
}