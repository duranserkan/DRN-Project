namespace DRN.Test.Tests.Testing.Providers;

public class DataProviderTests
{
    [Fact]
    public void DataProvider_Should_Return_Data_From_Test_File()
    {
        DataProvider.Get("Test.txt").Should().Be("Foo");
    }
}