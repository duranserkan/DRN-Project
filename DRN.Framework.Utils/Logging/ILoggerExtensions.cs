using System.Collections;
using Microsoft.Extensions.Logging;

namespace DRN.Framework.Utils.Logging;

public static class ILoggerExtensions
{
    public static void LogScoped(this ILogger logger, IScopedLog scopedLog)
    {
        var level = scopedLog.HasException ? LogLevel.Error : scopedLog.HasWarning ? LogLevel.Warning : LogLevel.Information;
        if (!logger.IsEnabled(level))
            return;

        var eventId = scopedLog.Event?.Id ?? default;
        logger.Log(level, eventId, new ScopedLogState(scopedLog.GetLogs()), null,
            static (state, _) => state.ToString());
    }

    // A value-type state avoids the params array and preserves the structured logging contract.
    private readonly struct ScopedLogState(IReadOnlyDictionary<string, object> logs) : IReadOnlyList<KeyValuePair<string, object?>>
    {
        public int Count => 2;

        public KeyValuePair<string, object?> this[int index] => index switch
        {
            0 => new("@Logs", logs),
            1 => new("{OriginalFormat}", "{@Logs}"),
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            yield return this[0];
            yield return this[1];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        // Match LoggerExtensions' enumerable argument rendering; formatting remains provider-driven.
        public override string ToString() => string.Join(", ", logs);
    }
}
