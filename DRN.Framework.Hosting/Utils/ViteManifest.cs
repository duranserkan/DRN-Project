using System.Text.Json.Serialization;
using DRN.Framework.Hosting.BackgroundServices.StaticAssetPreWarm;
using DRN.Framework.SharedKernel;
using DRN.Framework.Utils.Data.Encodings;
using DRN.Framework.Utils.Data.Hashing;
using DRN.Framework.Utils.Data.Serialization;
using DRN.Framework.Utils.Concurrency;

namespace DRN.Framework.Hosting.Utils;

public static class ViteManifest
{
    private static string _manifestRootPath = "";
    private const string ViteBuildOutputPrefix = "buildwww/";
    private const string NodeModulesPrefix = "node_modules/";

    private static volatile Dictionary<string, ViteManifestItem>? _manifestCache;
    private static readonly Lock Lock = new();
    private static volatile ViteManifestPreWarmReport? _preWarmReport;
    private static int _preWarmClaimed;

    public static ViteManifestPreWarmReport? PreWarmReport => _preWarmReport;

    /// <summary>
    /// Atomically claims the right to create the pre-warm report.
    /// Returns <c>true</c> only for the first caller; all subsequent callers get <c>false</c> immediately
    /// without blocking — they should skip pre-warming entirely.
    /// </summary>
    internal static bool TryClaimPreWarm() => LockUtils.TryClaimLock(ref _preWarmClaimed);

    /// <summary>
    /// Sets the pre-warm report exactly once (write-once singleton).
    /// Should only be called by the instance that successfully claimed via <see cref="TryClaimPreWarm"/>.
    /// Thread-safe via <see cref="Interlocked.CompareExchange{T}"/>.
    /// </summary>
#pragma warning disable CS0420 // Interlocked provides full memory barrier
    internal static bool TrySetPreWarmReport(ViteManifestPreWarmReport report) => LockUtils.TrySetIfNull(ref _preWarmReport, report);
#pragma warning restore CS0420

    public static ViteManifestItem? GetManifestItem(string entryName)
    {
        if (_manifestCache == null)
            EnsureManifest();

        return _manifestCache!.TryGetValue(entryName, out var entry) ? entry : null;
    }

    /// <summary>
    /// Returns all parsed Vite manifest items across all discovered manifest.json files.
    /// Triggers lazy initialization if the manifest has not been loaded yet.
    /// </summary>
    public static IReadOnlyCollection<ViteManifestItem> GetAllManifestItems(string manifestRootPath = "")
    {
        if (string.IsNullOrEmpty(_manifestRootPath))
            _manifestRootPath = manifestRootPath;
            
        if (_manifestCache == null)
            EnsureManifest();

        return _manifestCache!.Values;
    }

    public static bool IsViteOrigin(string path) =>
        path.StartsWith(ViteBuildOutputPrefix, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(NodeModulesPrefix, StringComparison.OrdinalIgnoreCase);

    private static void EnsureManifest()
    {
        if (_manifestCache != null)
            return;

        lock (Lock)
        {
            if (_manifestCache != null)
                return;

            var cache = new Dictionary<string, ViteManifestItem>();
            var manifestFiles = Directory.GetFiles(_manifestRootPath, "manifest.json", SearchOption.AllDirectories);
            foreach (var manifestFile in manifestFiles)
                try
                {
                    var json = File.ReadAllText(manifestFile);
                    var manifest = json.Deserialize<Dictionary<string, ViteManifestItem>>();

                    if (manifest == null)
                        continue;

                    var manifestDir = Path.GetDirectoryName(manifestFile)!;
                    var scriptDir = Path.GetDirectoryName(manifestDir)!;
                    var relativeDir = Path.GetRelativePath(_manifestRootPath, scriptDir).Trim('/');

                    foreach (var (key, value) in manifest)
                    {
                        //ignore query parameters
                        var normalizedKey = key.Split('?')[0];

                        var fullFilePath = Path.Combine(scriptDir, value.File);
                        var hash = fullFilePath.HashOfFile(HashAlgorithm.Sha256, ByteEncoding.Base64UrlEncoded);

                        cache[normalizedKey] = value.FromCalculatedProperties($"/{relativeDir}/", hash);
                    }
                }
                catch (Exception e)
                {
                    throw new ConfigurationException($"Failed to parse Vite manifest at {manifestFile}", e);
                }

            _manifestCache = cache;
        }
    }
}

// Class to represent an entry in the manifest.json
public class ViteManifestItem
{
    private const string HashNotFoundPlaceholder = "HashNotFound";

    [JsonConstructor]
    private ViteManifestItem(string file, string src, string outputDir = "/", string hash = "")
    {
        Path = $"{outputDir}{file}";
        File = file;
        Src = src;
        OutputDir = outputDir;
        Hash = hash;
        Integrity = $"sha256-{hash}";

        var parts = file.Split('.');
        ShortHash = parts.Length >= 3 ? parts[^2] : HashNotFoundPlaceholder;
    }

    internal ViteManifestItem FromCalculatedProperties(string outputDir, string hash) => new(File, Src, outputDir, hash);

    public string Path { get; }
    public string File { get; }
    public string Src { get; }
    public string OutputDir { get; }
    public string Hash { get; }
    public string ShortHash { get; }
    public string Integrity { get; }
}

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

/// <summary>
/// Compression statistics grouped by algorithm (e.g. br, gzip, deflate, identity).
/// Contains the list of assets compressed with this algorithm.
/// </summary>
public class ViteManifestCompressionAlgorithmSummary
{
    public string Algorithm { get; }
    public int AssetCount { get; }
    public long OriginalBytes { get; }
    public long CompressedBytes { get; }
    public long SavedBytes { get; }
    public double CompressionRatio { get; }

    public IReadOnlyList<ViteManifestPreWarmAssetReport> Assets { get; }

    public ViteManifestCompressionAlgorithmSummary(string algorithm, int assetCount, long originalBytes, long compressedBytes,
        IReadOnlyList<ViteManifestPreWarmAssetReport> assets)
    {
        Algorithm = algorithm;
        AssetCount = assetCount;
        OriginalBytes = originalBytes;
        CompressedBytes = compressedBytes;
        SavedBytes = originalBytes - compressedBytes;
        CompressionRatio = originalBytes > 0 ? Math.Round(1.0 - (double)compressedBytes / originalBytes, 4) : 0;
        Assets = assets;
    }

    public override string ToString() =>
        $"{Algorithm}: {AssetCount} assets, {ViteManifestPreWarmReport.FormatBytes(SavedBytes)} saved ({CompressionRatio:P1})";
}

public class ViteManifestPreWarmAssetReport
{
    public string Path { get; }
    public int StatusCode { get; }
    public bool Success { get; }
    public string? Error { get; }

    public long OriginalBytes { get; }
    public long CompressedBytes { get; }
    public double CompressionRatio { get; }
    public string? ContentEncoding { get; }
    public string? ContentType { get; }

    public long DurationMs { get; }

    private ViteManifestPreWarmAssetReport(string path, int statusCode, long originalBytes, long compressedBytes,
        string? contentEncoding, string? contentType, long durationMs)
    {
        Path = path;
        StatusCode = statusCode;
        Success = true;
        OriginalBytes = originalBytes;
        CompressedBytes = compressedBytes;
        CompressionRatio = originalBytes > 0 ? Math.Round(1.0 - (double)compressedBytes / originalBytes, 4) : 0;
        ContentEncoding = contentEncoding;
        ContentType = contentType;
        DurationMs = durationMs;
    }

    private ViteManifestPreWarmAssetReport(string path, int statusCode, string? error, long durationMs)
    {
        Path = path;
        StatusCode = statusCode;
        Success = false;
        Error = error;
        DurationMs = durationMs;
    }


    public static ViteManifestPreWarmAssetReport Ok(string path, int statusCode,
        long originalBytes, long compressedBytes, string? contentEncoding, string? contentType, long durationMs)
        => new(path, statusCode, originalBytes, compressedBytes, contentEncoding, contentType, durationMs);

    public static ViteManifestPreWarmAssetReport Failed(string path, int statusCode, long durationMs)
        => new(path, statusCode, error: null, durationMs);

    public static ViteManifestPreWarmAssetReport Errored(string path, string error, long durationMs)
        => new(path, statusCode: 0, error, durationMs);

    public override string ToString() => Success
        ? $"{Path}: {ViteManifestPreWarmReport.FormatBytes(OriginalBytes)} → {ViteManifestPreWarmReport.FormatBytes(CompressedBytes)} ({CompressionRatio:P1}) [{ContentEncoding ?? "none"}] in {DurationMs}ms"
        : $"{Path}: FAILED ({Error ?? $"HTTP {StatusCode}"}) in {DurationMs}ms";
}