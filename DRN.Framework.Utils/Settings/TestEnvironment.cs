namespace DRN.Framework.Utils.Settings;

public static class TestEnvironment
{
    public const string TestContextAddress = "http://localhost";
    public static bool DrnTestContextEnabled { get; internal set; }
}