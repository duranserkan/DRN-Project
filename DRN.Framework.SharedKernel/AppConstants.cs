using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace DRN.Framework.SharedKernel;

public static class AppConstants
{
    private const string GoogleDnsIp = "8.8.4.4";
    public static readonly int ProcessId = Environment.ProcessId;
    public static readonly Guid ApplicationId = Guid.NewGuid();
    public static readonly string ApplicationName = Assembly.GetEntryAssembly()?.GetName().Name ?? "Entry Assembly Not Found";
    public static readonly string TempPath = GetTempPath();
    public static readonly string LocalIpAddress = GetLocalIpAddress();

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
        //how to get local IP address https://stackoverflow.com/posts/27376368/revisions
        using var dataGramSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
        dataGramSocket.Connect(GoogleDnsIp, 59999);
        var localEndPoint = dataGramSocket.LocalEndPoint as IPEndPoint;
        return localEndPoint?.Address.ToString() ?? string.Empty;
    }
}