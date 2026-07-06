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
    public static string EntryAssemblyFullName { get; } = Assembly.GetEntryAssembly()?.GetName().FullName ?? "Entry Assembly Not Found";
    public static string TempPath { get; } = GetTempPath(); //Attempts to clean directory at every startup

    /// <summary>
    /// Prefer AppData over using app constants for data paths
    /// </summary>
    public static string LocalAppDataPath { get; } = GetAppDataPath(Environment.SpecialFolder.LocalApplicationData);

    public static string LocalIpAddress { get; } = GetLocalIpAddress();

    private static string GetTempPath()
    {
        var appSpecificTempPath = GetAppSpecificDirectoryPath(Path.GetTempPath(), EntryAssemblyName);
        if (string.IsNullOrWhiteSpace(appSpecificTempPath))
            return Path.GetTempPath();

        try
        {
            if (Directory.Exists(appSpecificTempPath))
                Directory.Delete(appSpecificTempPath, true);

            Directory.CreateDirectory(appSpecificTempPath);
        }
        catch (Exception)
        {
            // ignored
        }

        return appSpecificTempPath;
    }

    private static string GetAppDataPath(Environment.SpecialFolder specialFolder)
    {
        var appSpecificPath = GetAppSpecificDirectoryPath(Environment.GetFolderPath(specialFolder), EntryAssemblyName);
        return string.IsNullOrWhiteSpace(appSpecificPath)
            ? string.Empty
            : appSpecificPath;
    }

    internal static string GetAppSpecificDirectoryPath(string rootPath, string entryAssemblyName)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
            return string.Empty;

        var appDirectoryName = entryAssemblyName.ToPascalCase();
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
