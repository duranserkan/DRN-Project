using DRN.Framework.SharedKernel.Attributes;
using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Utils.Settings;

//todo: explore Feature flags
/// <summary>
/// Values from DrnAppFeatures section
/// </summary>
[Config(validateAnnotations: true, errorOnUnknownConfiguration: false)]
public class DrnAppFeatures
{
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
    
    public int MaxRequestBufferingSize { get; init; } = 0;
}