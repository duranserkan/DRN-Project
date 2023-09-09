namespace DRN.Framework.Testing;

public static class DataProvider
{
    private static readonly string CallingAssemblyLocation = Path.GetDirectoryName(Assembly.GetCallingAssembly().Location) ?? "";
    private static readonly string DataFolder = Path.Combine(CallingAssemblyLocation, "Data");

    /// <summary>
    /// Gets test data content
    /// </summary>
    /// <param name="pathRelativeToDataFolder">
    /// Data path of test data relative to Data folder created in test project. Make sure the file is copied to output directory.
    /// </param>
    public static string Get(string pathRelativeToDataFolder) => File.ReadAllText(Path.Combine(DataFolder, pathRelativeToDataFolder));
}