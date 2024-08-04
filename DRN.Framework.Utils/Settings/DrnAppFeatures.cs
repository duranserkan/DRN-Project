namespace DRN.Framework.Utils.Settings;

/// <summary>
/// Values from DrnAppFeatures section
/// </summary>
public class DrnAppFeatures
{
    public static string GetKey(string shortKey) => $"{nameof(DrnAppFeatures)}:{shortKey}";

    /// <summary>
    /// When true application dependencies are not validated. Mostly, used by Test Contexts.
    /// </summary>
    public bool SkipValidation { get; init; }

    /// <summary>
    /// When application is created to obtain configuration and registered services, it should mark as temporary. Mostly, used by Test Contexts.
    /// </summary>
    public bool TemporaryApplication { get; init; }

    /// <summary>
    /// In dev environment, Launch context launches external dependencies with required configuration for rapid application development
    /// </summary>
    public bool LaunchExternalDependencies { get; init; }

    /// <summary>
    /// When true in dev environment, after registered services validated, database migrations will be applied automatically for rapid application development
    /// </summary>
    public bool AutoMigrateDevEnvironment { get; init; }

    public string InternalRequestHttpVersion { get; init; } = "1.1";
    public string InternalRequestProtocol { get; init; } = "http";
    public bool UseHttpRequestLogger { get; init; } = false;

    public bool SocketsHttp2UnencryptedSupport { get; init; } =
        AppContext.TryGetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", out var enabled) && enabled;

    public string NexusAddress { get; init; } = "nexus";
}