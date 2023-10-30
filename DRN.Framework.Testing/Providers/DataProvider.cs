namespace DRN.Framework.Testing.Providers;

public static class DataProvider
{
    private static readonly string CallingAssemblyLocation = Path.GetDirectoryName(Assembly.GetCallingAssembly().Location) ?? "";
    private static readonly string DataFolder = Path.Combine(CallingAssemblyLocation, "Data");

    /// <summary>
    /// Gets the content of specified data file in the Data folder.
    /// </summary>
    /// <param name="pathRelativeToDataFolder">
    /// Path is relative Data folder including file extension. Data folder must be created in the root of the test Project. Make sure the data file is copied to output directory.
    /// </param>
    public static string Get(string pathRelativeToDataFolder) => File.ReadAllText(Path.Combine(DataFolder, pathRelativeToDataFolder));
}