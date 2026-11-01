using DRN.Framework.Utils.Logging;
using DRN.Framework.Utils.Logging.Diagnostics;
using DRN.Framework.Utils.Settings;

namespace DRN.Framework.Hosting.RateLimiting;

internal static class RateLimitDiagnostics
{
    private static readonly DiagnosticCapturePolicy RejectionCapture = new();

    internal static void WriteRejection(IScopedLog log, RateLimitRulePhase phase, RateLimitRuleMatch? match,
        string? partitionKey, DrnRateLimitOptions options, IAppSecuritySettings securitySettings, string? ipAddress = null)
    {
        var prefix = phase + "RateLimitRejected";
        log.Add(prefix, true, exemptionReason: "Rate limit outcome flag.");
        log.Add(prefix + "Action", RateLimitTelemetry.GetActionTag(match), exemptionReason: "Rate limit action category.");
        if (phase == RateLimitRulePhase.PreAuth)
            AddIdentifier("Ip", ipAddress);
        log.Add(prefix + "Rule", match?.Rule.GetType().FullName ?? string.Empty, exemptionReason: "Rate limit rule type name.");
        AddIdentifier("Partition", partitionKey);

        // Identifiers receive no name exemption, including when plaintext mode is configured.
        void AddIdentifier(string suffix, string? value) => log.AddWithPolicy(prefix + suffix,
            RateLimitPartitionRedactor.Format(value, options, securitySettings), RejectionCapture);
    }
}
