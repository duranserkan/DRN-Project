namespace DRN.Framework.Testing.Providers;

public static class DataProvider
{
    public static readonly string ConventionDirectory = "Data";
    public static readonly string GlobalConventionLocation = Path.Combine(Directory.GetCurrentDirectory(), ConventionDirectory);

    /// <summary>
    /// Gets the content of specified data file in the Data folder.
    /// Data folder must be created in the root of the test Project or provided location.
    /// </summary>
    /// <param name="pathRelativeToDataFolder">
    /// Path is relative Data folder including file extension.
    /// Make sure the data file is copied to output directory.
    /// </param>
    /// <param name="dataFolderLocation">If not provided global convention location will be applied</param>
    public static string Get(string pathRelativeToDataFolder, string? dataFolderLocation = null)
        => File.ReadAllText(GetDataPath(pathRelativeToDataFolder, dataFolderLocation));

    public static string GetDataPath(string pathRelativeToDataFolder, string? dataFolderLocation = null)
    {
        var locationFound = CheckLocation(dataFolderLocation, pathRelativeToDataFolder);
        if (!locationFound && !string.IsNullOrWhiteSpace(dataFolderLocation))
        {
            //alternate location by convention
            dataFolderLocation = Path.Combine(dataFolderLocation, ConventionDirectory);
            locationFound = CheckLocation(dataFolderLocation, pathRelativeToDataFolder);
        }

        var selectedLocation = locationFound ? dataFolderLocation! : GlobalConventionLocation;
        var path = Path.Combine(selectedLocation, pathRelativeToDataFolder);

        return path;
    }

    private static bool CheckLocation(string? dataFolderLocation, string pathRelativeToDataFolder)
        => !string.IsNullOrWhiteSpace(dataFolderLocation) && File.Exists(Path.Combine(dataFolderLocation, pathRelativeToDataFolder));
}