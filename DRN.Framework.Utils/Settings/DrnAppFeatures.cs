using System.ComponentModel.DataAnnotations;
using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using Microsoft.Extensions.Configuration;

namespace DRN.Framework.Utils.Settings;

//todo: explore Feature flags
/// <summary>
/// Values from DrnAppFeatures section
/// </summary>
[Config(validateAnnotations: true, errorOnUnknownConfiguration: false)]
public class DrnAppFeatures : IValidatableObject
{
    private const string RateLimitConfigurationKey = "DrnRateLimit";

    public static string GetKey(string shortKey) => $"{nameof(DrnAppFeatures)}:{shortKey}";

    /// <summary>
    /// Shows which test started the application when the application is created with DrnTestContext for integration tests.
    /// </summary>
    public string? ApplicationStartedBy { get; init; }

    public bool SeedData { get; init; }

    [SecureKey(MinLength = 58)]
    public string SeedKey { get; init; } = "Peace at home! Peace in the world! - Mustafa Kemal Atatürk (1931)";
    public string InternalRequestHttpVersion { get; init; } = "1.1";
    public string InternalRequestProtocol { get; init; } = "http";

    /// <summary>
    /// Not used for production or development but kept around to collect MonotonicDateTime Behavior Data
    /// </summary>
    public bool UseMonotonicDateTimeProvider { get; init; } = false;

    /// <summary>
    /// When true, disables request body buffering entirely. Use for high-throughput services
    /// (e.g., file upload endpoints) where even small buffering is undesirable.
    /// </summary>
    public bool DisableRequestBuffering { get; init; } = false;

    /// <summary>
    /// Values below 10,000 bytes will be ignored
    /// </summary>
    public int MaxRequestBufferingSize { get; init; } = 0;

    /// <summary>
    /// DRN Hosting rate limiting settings. Bound from the DrnAppFeatures:DrnRateLimit configuration key.
    /// </summary>
    [Required]
    [ConfigurationKeyName(RateLimitConfigurationKey)]
    public DrnRateLimitOptions RateLimit { get; init; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var validationResults = new List<ValidationResult>();
        var rateLimitContext = new ValidationContext(RateLimit);
        Validator.TryValidateObject(RateLimit, rateLimitContext, validationResults, true);

        foreach (var result in validationResults)
        {
            var memberNames = result.MemberNames.Select(member => $"{RateLimitConfigurationKey}.{member}");
            yield return new ValidationResult($"{RateLimitConfigurationKey}: {result.ErrorMessage}", memberNames);
        }
    }
}

/// <summary>
/// Rate limiting defaults and phase-specific overrides for DRN Hosting.
/// </summary>
public sealed class DrnRateLimitOptions
{
    /// <summary>
    /// When true, disables both pre-auth and post-auth rate limiting layers entirely.
    /// </summary>
    public bool Disabled { get; init; } = false;

    /// <summary>
    /// Controls how rate-limit partition values are written to structured logs.
    /// Defaults to keyed hashing so logs can be correlated without exposing raw IPs,
    /// API keys, tenant hints, or service identifiers.
    /// </summary>
    public RateLimitPartitionLogMode PartitionLogMode { get; init; } = RateLimitPartitionLogMode.KeyedHash;

    /// <summary>
    /// Maximum number of tokens in the token bucket. Represents the burst capacity.
    /// Shared by both pre-auth (IP) and post-auth (user) rate limiters.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int TokenLimit { get; init; } = 100;

    /// <summary>
    /// Token replenishment period in seconds. Tokens are added every this many seconds.
    /// Shared by both pre-auth (IP) and post-auth (user) rate limiters.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int ReplenishmentSeconds { get; init; } = 60;

    /// <summary>
    /// Number of tokens added per replenishment period.
    /// Shared by both pre-auth (IP) and post-auth (user) rate limiters.
    /// </summary>
    [Range(1, int.MaxValue)]
    public int TokensPerPeriod { get; init; } = 100;

    /// <summary>
    /// Pre-auth token bucket burst capacity. Defaults to 1000 (B2B-friendly coarse limit) to avoid
    /// throttling legitimate users behind shared NAT/VPN/CDN egress addresses. Set to 0 to inherit <see cref="TokenLimit"/>.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int PreAuthTokenLimit { get; init; } = 1000;

    /// <summary>
    /// Pre-auth token replenishment period in seconds. Defaults to 60. Set to 0 to inherit <see cref="ReplenishmentSeconds"/>.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int PreAuthReplenishmentSeconds { get; init; } = 60;

    /// <summary>
    /// Pre-auth tokens added per replenishment period. Defaults to 1000 (B2B-friendly coarse limit). Set to 0 to inherit <see cref="TokensPerPeriod"/>.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int PreAuthTokensPerPeriod { get; init; } = 1000;

    /// <summary>
    /// Optional post-auth token bucket burst capacity. 0 inherits <see cref="TokenLimit"/>.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int PostAuthTokenLimit { get; init; } = 0;

    /// <summary>
    /// Optional post-auth token replenishment period in seconds. 0 inherits <see cref="ReplenishmentSeconds"/>.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int PostAuthReplenishmentSeconds { get; init; } = 0;

    /// <summary>
    /// Optional post-auth tokens added per replenishment period. 0 inherits <see cref="TokensPerPeriod"/>.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int PostAuthTokensPerPeriod { get; init; } = 0;
}

/// <summary>
/// Structured logging mode for rate-limit partition values.
/// </summary>
public enum RateLimitPartitionLogMode
{
    /// <summary>
    /// Log a deterministic keyed hash. Recommended default for production.
    /// </summary>
    KeyedHash,

    /// <summary>
    /// Log the raw partition value. Use only for controlled development or dedicated audit sinks.
    /// </summary>
    PlainText
}
