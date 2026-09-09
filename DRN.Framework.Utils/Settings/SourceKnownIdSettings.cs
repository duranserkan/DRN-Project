using DRN.Framework.SharedKernel.Domain;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.Extensions.Configuration;

namespace DRN.Framework.Utils.Settings;

/// <summary>Source-Known generation floor, validated unconditionally at host startup.</summary>
[Config(nameof(SourceKnownIdSettings))]
public sealed class SourceKnownIdSettings
{
    /// <summary>UTC replacing the C# minimum, earlier or later. Omit to use SharedKernel's source-maintained minimum.</summary>
    public string? MinimumUtc { get; init; }

    /// <summary>Process-wide UTC origin. An explicit value requires MinimumUtc in the same configuration.</summary>
    public string? DefaultEpoch { get; init; }

    public static SourceKnownGenerationTimePolicy Initialize(IConfiguration configuration)
    {
        var settings = configuration.GetSection(nameof(SourceKnownIdSettings))
            .Get<SourceKnownIdSettings>() ?? new SourceKnownIdSettings();
        return SourceKnownGenerationTime.Initialize(settings.MinimumUtc, settings.DefaultEpoch);
    }
}
