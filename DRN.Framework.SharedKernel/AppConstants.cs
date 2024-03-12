using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace DRN.Framework.SharedKernel;

public static class AppConstants
{
    public static int ProcessId { get; } = Environment.ProcessId;
    public static Guid Id { get; } = Guid.NewGuid();
    public static string ApplicationName { get; } = Assembly.GetEntryAssembly()?.GetName().Name ?? "Entry Assembly Not Found";
    public static string TempPath { get; } = GetTempPath();
    public static string LocalIpAddress { get; } = GetLocalIpAddress();

    private static string GetTempPath()
    {
        var appSpecificTempPath = Path.Combine(Path.GetTempPath(), ApplicationName);
        //Cleans directory in every startup
        if (Directory.Exists(appSpecificTempPath)) Directory.Delete(appSpecificTempPath, true);
        Directory.CreateDirectory(appSpecificTempPath);

        return appSpecificTempPath;
    }

    private static string GetLocalIpAddress()
    {
        var googleDnsIp = "8.8.4.4";
        //how to get local IP address https://stackoverflow.com/posts/27376368/revisions
        using var dataGramSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
        dataGramSocket.Connect(googleDnsIp, 59999);
        var localEndPoint = dataGramSocket.LocalEndPoint as IPEndPoint;
        return localEndPoint?.Address.ToString() ?? string.Empty;
    }
}