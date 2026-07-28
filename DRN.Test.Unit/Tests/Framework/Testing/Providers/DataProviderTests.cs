namespace DRN.Test.Unit.Tests.Framework.Testing.Providers;

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
    [DataInlineUnit("data.txt", "Atatürk")]
    [DataInlineUnit("alternateData.txt", "Father of Turks")]
    public void DataProvider_Should_Return_Test_Specific_Data(DrnTestContextUnit context, string dataPath, string data)
    {
        var folderLocation = context.MethodContext.GetTestFolderLocation();
        DataProvider.Get(dataPath, folderLocation).Data.Should().Be(data);
        context.GetData(dataPath).Data.Should().Be(data);
    }

    [Theory]
    [DataInlineUnit("data.txt", "Atatürk")]
    [DataInlineUnit("alternateData.txt", "Father of Turks")]
    [DataInlineUnit("globalData.txt", "Mustafa Kemal Atatürk's enlightenment ideals")]
    public void DrnTestContext_Should_Return_Test_Specific_Data(DrnTestContextUnit context, string dataPath, string data)
    {
        //data file can be found in the same folder with test file, in the global Data folder or Data folder that stays in the same folder with test file
        var dataResult = context.GetData(dataPath);

        dataResult.Data.Should().Be(data);
    }

    [Fact]
    public void DataProvider_Should_Prefer_Local_Convention_Data_Folder_Over_Global_Fallback()
    {
        var testDirectory = $"DataProviderTests_{Guid.NewGuid():N}";
        var localDataDir = Path.Combine(testDirectory, "Data");
        var testFileName = $"conflict_{Guid.NewGuid():N}.txt";
        var localFilePath = Path.Combine(localDataDir, testFileName);
        var globalFilePath = Path.Combine(DataProviderDataLookupDirectoryPaths.GlobalConventionDirectoryPath, testFileName);

        try
        {
            Directory.CreateDirectory(localDataDir);
            File.WriteAllText(localFilePath, "Local Content");
            File.WriteAllText(globalFilePath, "Global Content");

            var dataPathResult = DataProvider.GetDataPath(testFileName, testDirectory);
            dataPathResult.SelectedDirectory.Should().Be(localDataDir);
            dataPathResult.DataPath.Should().Be(localFilePath);

            var getResult = DataProvider.Get(testFileName, testDirectory);
            getResult.DataExists.Should().BeTrue();
            getResult.Data.Should().Be("Local Content");
        }
        finally
        {
            if (File.Exists(globalFilePath))
                File.Delete(globalFilePath);
            if (Directory.Exists(testDirectory))
                Directory.Delete(testDirectory, recursive: true);
        }
    }
}
