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
    /// Prefer AppData over using app constants for data paths.
    /// Set DrnAppDataSettings__DataPath as a process environment variable to override;
    /// otherwise the system local application data location is used.
    /// </summary>
    public static string LocalAppDataPath { get; } = GetAppSpecificLocalDataPath();

    /// <summary>
    /// Resolved temp root path. Set DrnAppDataSettings__TempPath as a process
    /// environment variable to override; AppData owns directory creation and cleanup.
    /// </summary>
    public static string TempPath { get; } = GetTempPath();

    public const string LocalAppDataPathEnvVariable = "DrnAppDataSettings__DataPath";
    public const string TempPathEnvVariable = "DrnAppDataSettings__TempPath";

    private static string GetTempPath()
    {
        var tempBase = Environment.GetEnvironmentVariable(TempPathEnvVariable);
        var tempFromEnv = GetAppSpecificDirectoryPath(tempBase);
        if (!string.IsNullOrEmpty(tempFromEnv))
            return tempFromEnv;

        tempBase = GetLocalDataPath();
        if (string.IsNullOrWhiteSpace(tempBase))
            return string.Empty;

        tempBase = Path.Combine(tempBase, "Temp");
        var tempFromLocalData = GetAppSpecificDirectoryPath(tempBase);

        return tempFromLocalData;
    }

    internal static string GetLocalDataPath()
    {
        var appDataFromEnv = Environment.GetEnvironmentVariable(LocalAppDataPathEnvVariable);
        if (!string.IsNullOrEmpty(appDataFromEnv))
            return appDataFromEnv;

        var folder = Environment.SpecialFolder.LocalApplicationData;
        return Environment.GetFolderPath(folder);
    }

    private static string GetAppSpecificLocalDataPath()
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

    internal static string GetAppSpecificDirectoryPath(string? rootPath, string? assemblyName = null)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            return string.Empty;

        var appDirectoryName = assemblyName?.ToPascalCase() ?? EntryAssemblyNameNormalized;
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
