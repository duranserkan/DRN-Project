namespace DRN.Test.Tests.Framework.Testing.Providers;

public class DataProviderTests
{
    [Fact]
    public void DataProvider_Should_Return_Data_From_Test_File()
    {
        var dataPath = DataProvider.GetDataPath("Test.txt");
        dataPath.Should().NotBeNull();

        var dataResult = DataProvider.Get("Test.txt");
        dataResult.Data.Should().Be("Foo");
        File.ReadAllText(dataPath.DataPath).Should().Be( dataResult.Data);
    }

    [Theory]
    [DataInline("data.txt", "Atatürk")]
    [DataInline("alternateData.txt", "Father of Turks")]
    public void DataProvider_Should_Return_Test_Specific_Data(DrnTestContext context, string dataPath, string data)
    {
        var folderLocation = context.MethodContext.GetTestFolderLocation();
        DataProvider.Get(dataPath, folderLocation).Data.Should().Be(data);
        context.GetData(dataPath).Data.Should().Be(data);
    }

    [Theory]
    [DataInline("data.txt", "Atatürk")]
    [DataInline("alternateData.txt", "Father of Turks")]
    [DataInline("globalData.txt", "Mustafa Kemal Atatürk's enlightenment ideals")]
    public void DrnTestContext_Should_Return_Test_Specific_Data(DrnTestContext context, string dataPath, string data)
    {
        //data file can be found in the same folder with test file, in the global Data folder or Data folder that stays in the same folder with test file
        var dataResult = context.GetData(dataPath);

        dataResult.Data.Should().Be(data);
    }
}