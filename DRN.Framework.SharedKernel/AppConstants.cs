using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace DRN.Framework.SharedKernel;

public static class AppConstants
{
    private static readonly string GoogleDNSIP = "8.8.4.4";

    static AppConstants()
    {
        //how to get local IP address https://stackoverflow.com/posts/27376368/revisions
        using var dataGramSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
        dataGramSocket.Connect(GoogleDNSIP, 59999);
        var localEndPoint = dataGramSocket.LocalEndPoint as IPEndPoint;
        LocalIpAddress = localEndPoint?.Address.ToString() ?? string.Empty;
    }

    public static readonly string LocalIpAddress;
    public static readonly int ProcessId = Environment.ProcessId;
    public static readonly Guid ApplicationId = Guid.NewGuid();
    public static readonly string ApplicationName = Assembly.GetEntryAssembly()?.GetName().Name ?? "Entry Assembly Not Found";
    public static readonly string TempPath = Path.Combine(Path.GetTempPath(), ApplicationName);
}