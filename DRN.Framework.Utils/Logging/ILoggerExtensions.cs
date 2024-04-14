using Microsoft.Extensions.Logging;

namespace DRN.Framework.Utils.Logging;

public static class ILoggerExtensions
{
    public static void LogScoped(this ILogger logger, IScopedLog scopedLog)
    {
        if (scopedLog.HasException)
            logger.LogError("{@Logs}", scopedLog.Logs);
        else if (scopedLog.HasWarning)
            logger.LogWarning("{@Logs}", scopedLog.Logs);
        else
            logger.LogInformation("{@Logs}", scopedLog.Logs);
    }
}