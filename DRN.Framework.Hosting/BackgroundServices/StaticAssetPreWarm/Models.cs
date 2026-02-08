using DRN.Framework.Hosting.Utils;

namespace DRN.Framework.Hosting.BackgroundServices.StaticAssetPreWarm;

/// <summary>
/// Static class containing scope log key constants for pre-warm operations.
/// Centralizes log keys to ensure consistency and enable compile-time checking.
/// </summary>
public static class PreWarmScopeLogKeys
{
    public const string SkipReason = nameof(SkipReason);
    public const string ExistingReportCreatedAt = nameof(ExistingReportCreatedAt);
    public const string AssetCount = nameof(AssetCount);
    public const string Encodings = nameof(Encodings);
    public const string BaseAddress = nameof(BaseAddress);
    public const string MaxParallelism = nameof(MaxParallelism);
    public const string FailedRequests = nameof(FailedRequests);
    public const string ErroredRequests = nameof(ErroredRequests);
    public const string PreWarmReport = nameof(PreWarmReport);
    public const string PreWarmReportStatus = nameof(PreWarmReportStatus);
}

/// <summary>
/// Represents a single work item for pre-warming: an asset paired with an encoding.
/// </summary>
/// <param name="Item">The Vite manifest item to pre-warm.</param>
/// <param name="Encoding">The Accept-Encoding value to use for this request.</param>
public sealed record PreWarmWorkItem(ViteManifestItem Item, string Encoding);

/// <summary>
/// Contains validated context required to execute pre-warming.
/// </summary>
/// <param name="Items">The collection of manifest items to pre-warm.</param>
/// <param name="BaseAddress">The loopback base address for self-requests.</param>
public sealed record PreWarmContext(IReadOnlyCollection<ViteManifestItem> Items, string BaseAddress);