using System.Text.Json;
using DRN.Framework.SharedKernel.Json;
using DRN.Framework.Utils.Logging.Diagnostics;
using DRN.Framework.Utils.Settings;

namespace DRN.Test.Unit.Tests.Framework.Utils.Logging;

internal static class DiagnosticTestSupport
{
    // Projection assertions inspect numeric enums rather than the framework's string enum format.
    internal static readonly JsonSerializerOptions ProjectionJsonOptions = new();
    internal static readonly JsonSerializerOptions FixtureJsonOptions = new(JsonConventions.DefaultOptions)
    {
        PropertyNamingPolicy = null
    };

    internal static readonly TimeProvider FrozenTime = new FrozenTimeProvider();

    internal static DiagnosticSanitizer Sanitizer(DiagnosticLogOptions? options = null) =>
        new(options ?? new DiagnosticLogOptions(), FrozenTime);

    internal static DiagnosticSanitizer Policy(params string[] fields) => Sanitizer(new DiagnosticLogOptions
    {
        SensitiveFields = fields
    });

    internal static AppSettings Settings(string environment)
    {
        using var nexus = new NexusAppSettings { Keys = [new NexusKey(new string('A', 32)) { Default = true }] };
        return SettingsProvider.DevelopmentWithNexusSettings(nexus, new
        {
            Environment = environment,
            DrnAppFeatures = new DrnAppFeatures
            {
                SeedKey = "Diagnostic fixture seed for isolated tests only! Version (2026)"
            }
        });
    }

    private sealed class FrozenTimeProvider : TimeProvider
    {
        public override long GetTimestamp() => 0;
    }
}
