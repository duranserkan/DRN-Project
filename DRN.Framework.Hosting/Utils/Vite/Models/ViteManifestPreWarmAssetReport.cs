namespace DRN.Framework.Hosting.Utils.Vite.Models;

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