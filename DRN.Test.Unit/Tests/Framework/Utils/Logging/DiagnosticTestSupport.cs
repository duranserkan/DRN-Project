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

    internal static DiagnosticSanitizer Policy(params string[] fields) => new(new DiagnosticLogOptions
    {
        MaxProcessingMilliseconds = 100,
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
}
