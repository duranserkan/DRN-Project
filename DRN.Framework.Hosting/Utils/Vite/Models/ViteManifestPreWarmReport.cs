using DRN.Framework.Hosting.BackgroundServices.StaticAssetPreWarm;

namespace DRN.Framework.Hosting.Utils.Vite.Models;

/// <summary>
/// Immutable singleton report produced by <see cref="StaticAssetPreWarmService"/>.
/// Created once per process; subsequent application instances (e.g. integration test suites)
/// reuse the existing report without re-executing pre-warming.
/// </summary>
public class ViteManifestPreWarmReport
{
    public int TotalAssets { get; }
    public int PreWarmedAssets { get; }
    public int FailedAssets { get; }
    public long ElapsedMs { get; }
    public DateTimeOffset CreatedAt { get; }

    public long TotalOriginalBytes { get; }
    public long TotalCompressedBytes { get; }
    public double TotalCompressionRatio { get; }
    public string TotalOriginalFormatted { get; }
    public string TotalCompressedFormatted { get; }
    public string TotalSavedFormatted { get; }

    public IReadOnlyList<ViteManifestCompressionAlgorithmSummary> CompressionBreakdown { get; }

    public ViteManifestPreWarmReport(int totalAssets, int preWarmedAssets, long elapsedMs,
        IReadOnlyList<ViteManifestPreWarmAssetReport> assets)
    {
        TotalAssets = totalAssets;
        PreWarmedAssets = preWarmedAssets;
        FailedAssets = totalAssets - preWarmedAssets;
        ElapsedMs = elapsedMs;
        CreatedAt = DateTimeOffset.UtcNow;

        var succeeded = assets.Where(a => a.Success).ToList();
        TotalOriginalBytes = succeeded.Sum(a => a.OriginalBytes);
        TotalCompressedBytes = succeeded.Sum(a => a.CompressedBytes);
        TotalCompressionRatio = TotalOriginalBytes > 0
            ? Math.Round(1.0 - (double)TotalCompressedBytes / TotalOriginalBytes, 4)
            : 0;
        TotalOriginalFormatted = FormatBytes(TotalOriginalBytes);
        TotalCompressedFormatted = FormatBytes(TotalCompressedBytes);
        TotalSavedFormatted = FormatBytes(TotalOriginalBytes - TotalCompressedBytes);

        // Group by algorithm; assets without encoding go under "identity"
        CompressionBreakdown = succeeded
            .GroupBy(a => a.ContentEncoding ?? "identity")
            .Select(g => new ViteManifestCompressionAlgorithmSummary(
                g.Key,
                g.Count(),
                g.Sum(a => a.OriginalBytes),
                g.Sum(a => a.CompressedBytes),
                g.ToList()))
            .OrderByDescending(s => s.SavedBytes)
            .ToList();
    }

    public override string ToString()
    {
        var breakdown = string.Join(", ", CompressionBreakdown.Select(b => b.ToString()));
        return $"Pre-warmed {PreWarmedAssets}/{TotalAssets} assets in {ElapsedMs}ms " +
               $"| {TotalOriginalFormatted} → {TotalCompressedFormatted} (saved {TotalSavedFormatted}, {TotalCompressionRatio:P1}) " +
               $"| [{breakdown}]";
    }

    internal static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024.0):F2} MB"
    };
}