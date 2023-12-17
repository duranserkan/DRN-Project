namespace DRN.Test.Tests.Testing.Providers;

public class DataProviderTests
{
    [Fact]
    public void DataProvider_Should_Return_Data_From_Test_File()
    {
        DataProvider.Get("Test.txt").Should().Be("Foo");
    }

    [Theory]
    [DataInline("data.txt", "Atatürk")]
    [DataInline("alternateData.txt", "Father of Turks")]
    public void DataProvider_Should_Return_Test_Specific_Data(TestContext context, string dataPath, string data)
    {
        context.MethodContext.GetTestFolderLocation();
        DataProvider.Get(dataPath, context.MethodContext.GetTestFolderLocation()).Should().Be(data);
        context.GetData(dataPath).Should().Be(data);
    }

    [Theory]
    [DataInline("data.txt", "Atatürk")]
    [DataInline("alternateData.txt", "Father of Turks")]
    public void TestContext_Should_Return_Test_Specific_Data(TestContext context, string dataPath, string data)
    {
        //data file can be found in the same folder with test file, in the global Data folder or Data folder that stays in the same folder with test file
        context.GetData(dataPath).Should().Be(data);
    }
}