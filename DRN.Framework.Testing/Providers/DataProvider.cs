namespace DRN.Framework.Testing.Providers;

public static class DataProvider
{
    /// <summary>
    /// Gets the content of the specified data file from the Data directory.
    /// The Data directory must exist at the root of the test project or a provided location.
    /// </summary>
    /// <param name="pathRelativeToDataDirectory">
    /// Path relative to the Data directory, including file extension.
    /// Ensure the data file is copied to the output directory.
    /// </param>
    /// <param name="dataDirectoryPath">
    /// Optional custom path to the Data directory. If not provided, a global convention path is used.
    /// </param>
    /// <param name="conventionDirectory">
    /// Optional path that overrides the default convention directory for locating the Data directory.
    /// </param>
    public static DataProviderResult Get(string pathRelativeToDataDirectory, string? dataDirectoryPath = null, string? conventionDirectory = null)
    {
        var location = GetDataPath(pathRelativeToDataDirectory, dataDirectoryPath, conventionDirectory);
        var data = location.DataExists ? File.ReadAllText(location.DataPath) : null;

        return new DataProviderResult(data, location);
    }

    public static DataProviderResultDataPath GetDataPath(string pathRelativeToDataDirectory, string? dataDirectoryPath = null, string? conventionDirectory = null)
    {
        var lookupDirectoryPaths = new DataProviderDataLookupDirectoryPaths(dataDirectoryPath ?? string.Empty, conventionDirectory);

        var locationFound = CheckLocation(lookupDirectoryPaths.TestDirectory, pathRelativeToDataDirectory);
        if (!locationFound && !string.IsNullOrWhiteSpace(dataDirectoryPath))
        {
            dataDirectoryPath = Path.Combine(dataDirectoryPath, lookupDirectoryPaths.TestDataDirectory);
            locationFound = CheckLocation(dataDirectoryPath, pathRelativeToDataDirectory);
        }

        var selectedDirectory = locationFound ? dataDirectoryPath! : lookupDirectoryPaths.GlobalDataDirectory;
        var path = Path.Combine(selectedDirectory, pathRelativeToDataDirectory);

        return new DataProviderResultDataPath(path, selectedDirectory, lookupDirectoryPaths);
    }

    private static bool CheckLocation(string dataDirectoryLocation, string pathRelativeToDataDirectory)
        => !string.IsNullOrWhiteSpace(dataDirectoryLocation) && File.Exists(Path.Combine(dataDirectoryLocation, pathRelativeToDataDirectory));
}

public class DataProviderResult(string? data, DataProviderResultDataPath dataPath)
{
    /// <summary>
    /// Make sure the data file is copied to output directory.
    /// </summary>
    public string? Data { get; } = data;

    /// <summary>
    /// Make sure the data file is copied to output directory.
    /// </summary>
    public DataProviderResultDataPath DataPath { get; } = dataPath;

    /// <summary>
    /// Make sure the data file is copied to output directory.
    /// </summary>
    public bool DataExists { get; } = dataPath.DataExists;
}

public class DataProviderResultDataPath(string dataPath, string selectedDirectory, DataProviderDataLookupDirectoryPaths directoryLookupPaths)
{
    public string DataPath { get; } = dataPath;
    public bool DataExists { get; } = File.Exists(dataPath);

    public string SelectedDirectory { get; } = selectedDirectory;
    public DataProviderDataLookupDirectoryPaths DirectoryLookupPaths { get; } = directoryLookupPaths;
}

public class DataProviderDataLookupDirectoryPaths
{
    public static readonly string ConventionDirectory = "Data";
    public static readonly string GlobalConventionDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), ConventionDirectory);

    public DataProviderDataLookupDirectoryPaths(string testDirectory, string? conventionDirectory)
    {
        var selectedConvention = string.IsNullOrWhiteSpace(conventionDirectory)
            ? ConventionDirectory
            : conventionDirectory;

        TestDirectory = testDirectory;
        TestDataDirectory = Path.Combine(TestDirectory, selectedConvention);
        GlobalDataDirectory = Path.Combine(Directory.GetCurrentDirectory(), selectedConvention);
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