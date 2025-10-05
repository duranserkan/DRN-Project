using System.Text.Json;

namespace DRN.Test.Unit.Tests.Framework.Utils.Settings;

public class ConnectionStringsCollectionTests
{
    [Theory]
    [DataInlineUnit("testDb", "Server=127.0.0.1;Port=5432;Database=myDataBase;User Id=myUsername;Password=myPassword;")]
    public void ConnectionStringsCollection_Should_Be_Serialized(string name, string connectionString)
    {
        var collection = new ConnectionStringsCollection();
        collection.ConnectionStrings.Add(name, connectionString);
        var json=JsonSerializer.Serialize(collection);

        var collection2 = JsonSerializer.Deserialize<ConnectionStringsCollection>(json)!;
        collection2.ConnectionStrings.TryGetValue(name, out var actual);

        actual.Should().Be(connectionString);
    }
}