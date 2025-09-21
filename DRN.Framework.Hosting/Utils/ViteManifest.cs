using System.Text.Json;
using System.Text.Json.Serialization;
using DRN.Framework.SharedKernel;
using DRN.Framework.Utils.Extensions;

namespace DRN.Framework.Hosting.Utils;

public static class ViteManifest
{
    private static readonly string ManifestRootPath = Path.Combine("wwwroot");
    private static Dictionary<string, ViteManifestItem>? _manifestCache;
    private static readonly Lock Lock = new();

    public static ViteManifestItem? GetManifestItem(string entryName)
    {
        if (_manifestCache == null)
            EnsureManifest();

        return _manifestCache!.TryGetValue(entryName, out var entry) ? entry : null;
    }

    public static bool IsViteOrigin(string path) =>
        path.StartsWith("buildwww/", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("node_modules/", StringComparison.OrdinalIgnoreCase);

    private static void EnsureManifest()
    {
        if (_manifestCache != null)
            return;

        lock (Lock)
        {
            if (_manifestCache != null)
                return;

            var cache = new Dictionary<string, ViteManifestItem>();
            var manifestFiles = Directory.GetFiles(ManifestRootPath, "manifest.json", SearchOption.AllDirectories);
            foreach (var manifestFile in manifestFiles)
                try
                {
                    var json = File.ReadAllText(manifestFile);
                    var manifest = JsonSerializer.Deserialize<Dictionary<string, ViteManifestItem>>(json);

                    if (manifest == null)
                        continue;

                    var manifestDir = Path.GetDirectoryName(manifestFile)!;
                    var scriptDir = Path.GetDirectoryName(manifestDir)!;
                    var relativeDir = Path.GetRelativePath(ManifestRootPath, scriptDir).Trim('/');

                    foreach (var (key, value) in manifest)
                    {
                        //ignore query parameters
                        var normalizedKey = key.Split('?')[0];

                        var fullFilePath = Path.Combine(scriptDir, value.File);
                        var hash = fullFilePath.ComputeSha256HashBase64UrlEncoded();

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
        ShortHash = parts.Length >= 3 ? parts[^2] : "HashNotFound";
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