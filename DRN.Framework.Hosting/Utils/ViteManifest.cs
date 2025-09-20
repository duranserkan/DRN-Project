using System.Text.Json;
using DRN.Framework.SharedKernel;

namespace DRN.Framework.Hosting.Utils;

public static class ViteManifest
{
    private static readonly string ManifestRootPath = Path.Combine("wwwroot");
    private static Dictionary<string, ViteManifestEntry>? _manifestCache;
    private static readonly Lock Lock = new();

    public static string? GetPath(string entryName)
    {
        if (_manifestCache == null)
            EnsureManifest();

        return _manifestCache!.TryGetValue(entryName, out var entry) ? entry.Path : null;
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

            var cache = new Dictionary<string, ViteManifestEntry>();
            var manifestFiles = Directory.GetFiles(ManifestRootPath, "manifest.json", SearchOption.AllDirectories);
            foreach (var manifestFile in manifestFiles)
            {
                try
                {
                    var json = File.ReadAllText(manifestFile);
                    var manifest = JsonSerializer.Deserialize<Dictionary<string, ViteManifestEntry>>(json);

                    if (manifest == null)
                        continue;

                    var manifestDir = Path.GetDirectoryName(manifestFile);
                    var scriptDir = Path.GetDirectoryName(manifestDir);
                    var relativeDir = Path.GetRelativePath(ManifestRootPath, scriptDir!).Trim('/');

                    foreach (var (key, value) in manifest)
                    {
                        //ignore query parameters
                        var normalizedKey = key.Split('?')[0];
                        cache[normalizedKey] = value.WithOutputDir($"/{relativeDir}/");
                    }
                }
                catch (Exception e)
                {
                    throw new ConfigurationException($"Failed to parse Vite manifest at {manifestFile}", e);
                }
            }

            _manifestCache = cache;
        }
    }
}

// Class to represent an entry in the manifest.json
public class ViteManifestEntry(string file, string src, string outputDir = "/")
{
    public ViteManifestEntry WithOutputDir(string outputDir) => new(File, Src, outputDir);
    public string Path { get; } = $"{outputDir}{file}";
    public string File { get; } = file;
    public string Src { get; } = src;
    public string OutputDir { get; } = outputDir;
}