using System.Collections;
using System.Text.Json;
using DRN.Framework.Utils.Logging.Diagnostics;
using Microsoft.Extensions.Logging;

namespace DRN.Framework.Utils.Logging;

public static class ILoggerExtensions
{
    public static void LogScoped(this ILogger logger, IScopedLog scopedLog)
    {
        try { LogScoped(logger, scopedLog, ResolveLogLevel(scopedLog)); }
        catch (Exception) { /* Foreign scope failures must not replace application failures. */ }
    }

    public static void LogScoped(this ILogger logger, IScopedLog scopedLog, LogLevel level)
        => LogScoped(logger, scopedLog, level, null);

    public static void LogScoped(this ILogger logger, IScopedLog scopedLog, LogLevel level, IDiagnosticSanitizer? sanitizer)
    {
        try
        {
            if (!logger.IsEnabled(level)) return;
            var native = scopedLog as ScopedLog;
            var policy = sanitizer ?? native?.Sanitizer ?? new DiagnosticSanitizer(new DiagnosticLogOptions());
            var snapshot = scopedLog.GetLogs();
            if (native == null || sanitizer != null && !ReferenceEquals(sanitizer, native.Sanitizer))
                snapshot = policy.SanitizeValue("", snapshot) as IReadOnlyDictionary<string, object>
                           ?? new Dictionary<string, object> { ["DiagnosticSnapshotUnavailable"] = true };
            var eventId = snapshot.TryGetValue("EventId", out var id) && id is int numeric
                ? new EventId(numeric, snapshot.GetValueOrDefault("EventName") as string) : default;
            foreach (var record in DiagnosticLogFormatter.Create(snapshot, policy.Options))
                logger.Log(level, eventId, new ScopedLogState(record), null, static (state, _) => state.ToString());
        }
        catch (Exception)
        {
            // A failed sink or diagnostic formatter must not replace the application's failure.
        }
    }

    private static LogLevel ResolveLogLevel(IScopedLog scopedLog)
    {
        if (scopedLog.HasException)
            return LogLevel.Error;

        if (scopedLog.HasWarning)
            return LogLevel.Warning;

        return LogLevel.Information;
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

        public override string ToString() => JsonSerializer.Serialize(logs);
    }
}
