using Microsoft.Extensions.Logging;

namespace DRN.Framework.Utils.Logging;

public static class ILoggerExtensions
{
    public static void LogScoped(this ILogger logger, IScopedLog scopedLog)
    {
        if (scopedLog.HasException)
            logger.LogError("{@Logs}", scopedLog.GetLogs());
        else if (scopedLog.HasWarning)
            logger.LogWarning("{@Logs}", scopedLog.GetLogs());
        else
            logger.LogInformation("{@Logs}", scopedLog.GetLogs());
    }
}