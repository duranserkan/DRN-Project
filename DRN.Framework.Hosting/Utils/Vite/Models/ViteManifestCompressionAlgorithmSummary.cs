namespace DRN.Framework.Hosting.Utils.Vite.Models;

/// <summary>
/// Compression statistics grouped by algorithm (e.g. br, gzip, deflate, identity).
/// Contains the list of assets compressed with this algorithm.
/// </summary>
public class ViteManifestCompressionAlgorithmSummary(
    string algorithm,
    int assetCount,
    long originalBytes,
    long compressedBytes,
    IReadOnlyList<ViteManifestPreWarmAssetReport> assets)
{
    public string Algorithm { get; } = algorithm;
    public int AssetCount { get; } = assetCount;
    public long OriginalBytes { get; } = originalBytes;
    public long CompressedBytes { get; } = compressedBytes;
    public long SavedBytes { get; } = originalBytes - compressedBytes;
    public double CompressionRatio { get; } = originalBytes > 0 ? Math.Round(1.0 - (double)compressedBytes / originalBytes, 4) : 0;

    public IReadOnlyList<ViteManifestPreWarmAssetReport> Assets { get; } = assets;

    public override string ToString() =>
        $"{Algorithm}: {AssetCount} assets, {ViteManifestPreWarmReport.FormatBytes(SavedBytes)} saved ({CompressionRatio:P1})";
}