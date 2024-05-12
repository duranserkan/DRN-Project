using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace DRN.Framework.SharedKernel;

public static class AppConstants
{
    public static int ProcessId { get; } = Environment.ProcessId;
    public static Guid ApplicationId { get; } = Guid.NewGuid();
    public static string EntryAssemblyName { get; } = Assembly.GetEntryAssembly()?.GetName().Name ?? "Entry Assembly Not Found";
    public static string TempPath { get; } = GetTempPath(); //Cleans directory at every startup
    public static string LocalIpAddress { get; } = GetLocalIpAddress();

    private static string GetTempPath()
    {
        var appSpecificTempPath = Path.Combine(Path.GetTempPath(), EntryAssemblyName);
        if (Directory.Exists(appSpecificTempPath)) Directory.Delete(appSpecificTempPath, true);
        Directory.CreateDirectory(appSpecificTempPath);

        return appSpecificTempPath;
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