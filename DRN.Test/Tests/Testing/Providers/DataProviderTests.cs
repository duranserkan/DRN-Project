namespace DRN.Test.Tests.Testing.Providers;

public class DataProviderTests
{
    [Fact]
    public void DataProvider_Should_Return_Data_From_Test_File()
    {
        DataProvider.Get("Test.txt").Should().Be("Foo");
    }

    [Theory]
    [DataInlineContext("data.txt", "Atatürk")]
    [DataInlineContext("alternateData.txt", "Father of Turks")]
    public void SettingsProvider_Should_Return_Test_Specific_IConfiguration_Instance(TestContext context, string dataPath, string data)
    {
        context.MethodContext.GetTestFolderLocation();
        DataProvider.Get(dataPath, context.MethodContext.GetTestFolderLocation()).Should().Be(data);
        context.GetData(dataPath).Should().Be(data);
    }
}