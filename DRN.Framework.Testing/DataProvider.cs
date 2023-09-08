namespace DRN.Framework.Testing.Attributes;

public class DataProvider
{
    public static string CallingAssemblyLocation=Path.GetDirectoryName(Assembly.GetCallingAssembly().Location) ?? "";
}
