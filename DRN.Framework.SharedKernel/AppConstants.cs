using System.Net;
using System.Net.Sockets;
using System.Reflection;
using DRN.Framework.SharedKernel.Extensions;

namespace DRN.Framework.SharedKernel;

public static class AppConstants
{
    public static int ProcessId { get; } = Environment.ProcessId;
    public static Guid AppInstanceId { get; } = Guid.NewGuid();

    public static string EntryAssemblyName { get; } = Assembly.GetEntryAssembly()?.GetName().Name ?? "Entry Assembly Not Found";
    public static string EntryAssemblyNameNormalized { get; } = EntryAssemblyName.ToPascalCase();
    public static string EntryAssemblyFullName { get; } = Assembly.GetEntryAssembly()?.GetName().FullName ?? "Entry Assembly Not Found";

    public static string LocalIpAddress { get; } = GetLocalIpAddress();

    /// <summary>
    /// Prefer AppData over using app constants for data paths
    /// </summary>
    public static string LocalAppDataPath { get; } = GetAppDataPath();

    public static string TempPath { get; } = GetTempPath(); //Attempts to clean directory at every startup

    public const string LocalAppDataPathEnvVariable = "DrnAppDataSettings__DataPath";
    public const string TempPathEnvVariable = "DrnAppDataSettings__TempPath";
    
    private static string GetTempPath()
    {
        var tempBase = Environment.GetEnvironmentVariable(TempPathEnvVariable);
        var tempFromEnv = GetAppSpecificDirectoryPath(tempBase);
        if (!string.IsNullOrEmpty(tempFromEnv))
            return tempFromEnv;

        tempBase = LocalAppDataPath;
        if (string.IsNullOrWhiteSpace(tempBase))
            return string.Empty;

        tempBase = Path.Combine(tempBase, "Temp");
        try
        {
            if (Directory.Exists(tempBase))
                Directory.Delete(tempBase, true);

            Directory.CreateDirectory(tempBase);
        }
        catch (Exception)
        {
            // ignored
        }

        return tempBase;
    }

    private static string GetAppDataPath()
    {
        var appDataFromEnv = Environment.GetEnvironmentVariable(LocalAppDataPathEnvVariable);
        if (!string.IsNullOrEmpty(appDataFromEnv))
            return appDataFromEnv;

        var folder = Environment.SpecialFolder.LocalApplicationData;
        var appSpecificPath = GetAppSpecificDirectoryPath(Environment.GetFolderPath(folder));

        return string.IsNullOrWhiteSpace(appSpecificPath)
            ? string.Empty
            : appSpecificPath;
    }

    internal static string GetAppSpecificDirectoryPath(string? rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            return string.Empty;

        var appDirectoryName = EntryAssemblyNameNormalized;
        if (string.IsNullOrWhiteSpace(appDirectoryName))
            return string.Empty;

        try
        {
            return Path.Combine(rootPath.NormalizeDirectoryPath(), appDirectoryName);
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static string GetLocalIpAddress()
    {
        //how to get local IP address https://stackoverflow.com/posts/27376368/revisions
        using var dataGramSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Unspecified);
        try
        {
            dataGramSocket.Connect("192.168.0.0", 59999);
        }
        catch (SocketException e)
        {
            _ = e;
            dataGramSocket.Connect("localhost", 59999);
        }

        var localEndPoint = dataGramSocket.LocalEndPoint as IPEndPoint;

        return localEndPoint?.Address.ToString() ?? string.Empty;
    }
}