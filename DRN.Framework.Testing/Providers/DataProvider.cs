using DRN.Framework.SharedKernel;

namespace DRN.Framework.Testing.Providers;

public static class DataProvider
{
    /// <summary>
    /// Gets the content of specified data file in the Data folder.
    /// Data folder must be created in the root of the test Project or provided location.
    /// </summary>
    /// <param name="pathRelativeToDataFolder">
    /// Path is relative Data folder including file extension.
    /// Make sure the data file is copied to output directory.
    /// </param>
    /// <param name="dataFolderLocation">If not provided global convention location will be applied</param>
    public static DataProviderResult Get(string pathRelativeToDataFolder, string? dataFolderLocation = null)
    {
        var location = GetDataPath(pathRelativeToDataFolder, dataFolderLocation);
        var data = location.DataExists ? File.ReadAllText(location.DataPath) : null;

        return new DataProviderResult(data, location);
    }

    public static DataProviderResultDataPath GetDataPath(string pathRelativeToDataFolder, string? dataFolderLocation = null)
    {
        var lookupDirectoryPaths = new DataProviderDataLookupDirectoryPaths(dataFolderLocation ?? "");

        var locationFound = CheckLocation(lookupDirectoryPaths.TestDirectory, pathRelativeToDataFolder);
        if (!locationFound && !string.IsNullOrWhiteSpace(dataFolderLocation))
        {
            dataFolderLocation = Path.Combine(dataFolderLocation, lookupDirectoryPaths.TestDataDirectory);
            locationFound = CheckLocation(dataFolderLocation, pathRelativeToDataFolder);
        }

        var selectedLocation = locationFound ? dataFolderLocation! : lookupDirectoryPaths.GlobalDataDirectory;
        var path = Path.Combine(selectedLocation, pathRelativeToDataFolder);

        return new DataProviderResultDataPath(path, lookupDirectoryPaths);
    }

    private static bool CheckLocation(string dataFolderLocation, string pathRelativeToDataFolder)
        => !string.IsNullOrWhiteSpace(dataFolderLocation) && File.Exists(Path.Combine(dataFolderLocation, pathRelativeToDataFolder));
}

public class DataProviderResult(string? data, DataProviderResultDataPath dataPath)
{
    /// <summary>
    /// Make sure the data file is copied to output directory.
    /// </summary>
    public string? Data { get; } = data;

    public DataProviderResultDataPath DataPath { get; } = dataPath;

    /// <summary>
    /// Make sure the data file is copied to output directory.
    /// </summary>
    public bool DataFound { get; } = dataPath.DataExists;
}

public class DataProviderResultDataPath(string dataPath, DataProviderDataLookupDirectoryPaths directoryLookupPaths)
{
    public string DataPath { get; } = dataPath;
    public bool DataExists { get; } = File.Exists(dataPath);
    public DataProviderDataLookupDirectoryPaths DirectoryLookupPaths { get; } = directoryLookupPaths;
}

public class DataProviderDataLookupDirectoryPaths
{
    public static readonly string ConventionDirectory = "Data";
    public static readonly string GlobalConventionLocation = Path.Combine(Directory.GetCurrentDirectory(), ConventionDirectory);

    public DataProviderDataLookupDirectoryPaths(string testDirectory)
    {
        TestDirectory = testDirectory;
        TestDataDirectory = Path.Combine(TestDirectory, ConventionDirectory);
        GlobalDataDirectory = GlobalConventionLocation;
    }

    /// <summary>
    /// Test Specific Location
    /// </summary>
    public string TestDirectory { get; }

    /// <summary>
    /// Alternate location by convention
    /// </summary>
    public string TestDataDirectory { get; }

    /// <summary>
    /// Global Alternate location by convention
    /// </summary>
    public string GlobalDataDirectory { get; }
}