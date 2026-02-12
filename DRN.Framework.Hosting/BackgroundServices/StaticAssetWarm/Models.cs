using DRN.Framework.Hosting.Utils;
using DRN.Framework.Hosting.Utils.Vite;
using DRN.Framework.Hosting.Utils.Vite.Models;

namespace DRN.Framework.Hosting.BackgroundServices.StaticAssetWarm;

public static class WarmScopeLogKeys
{
    public const string SkipReason = nameof(SkipReason);
    public const string AssetCount = nameof(AssetCount);
    public const string Encodings = nameof(Encodings);
    public const string BaseAddress = nameof(BaseAddress);
    public const string FailedRequests = nameof(FailedRequests);
    public const string ErroredRequests = nameof(ErroredRequests);
    public const string ManifestRootPath = nameof(ManifestRootPath);
}

public sealed record WarmWorkItem(ViteManifestItem Item, string Encoding);

public sealed record WarmContext(IReadOnlyCollection<ViteManifestItem> Items, string BaseAddress);