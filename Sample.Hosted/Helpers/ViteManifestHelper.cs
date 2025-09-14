using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sample.Hosted.Helpers;

// Example helper method (place in a suitable utility class or service)
// Requires: using System.Text.Json;
public static class ViteManifestHelper
{
    // Root directory containing manifest.json files (and subdirectories)
    private static readonly string ManifestRootPath = Path.Combine("wwwroot", "site-dist");
    private static Dictionary<string, ViteManifestEntry>? _manifestCache;
    private static readonly Lock Lock = new();

    public static string GetScriptTag(string entryName, string? nonce = null)
    {
        if (_manifestCache == null)
            EnsureManifest();

        return _manifestCache!.TryGetValue(entryName, out var entry)
            ? entry.ScriptPath(nonce)
            : $"<!-- Vite entry '{entryName}' not found -->";
    }

    public static string GetStyleTag(string entryName, string? nonce = null)
    {
        if (_manifestCache == null)
            EnsureManifest();

        return _manifestCache!.TryGetValue(entryName, out var entry)
            ? entry.StylePath(nonce)
            : $"<!-- Vite entry '{entryName}' not found -->";
    }

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
                        cache[key] =value.WithOutputDir( $"/{relativeDir}/");
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
    // Vite's base path is '/site-dist/', so the file should be served from there
    private readonly string _scriptPathPrefix = $"""<script src="/site-dist{outputDir}{file}" crossorigin="anonymous" """;
    private readonly string _stylePathPrefix = $"""<link rel="stylesheet" href="/site-dist{outputDir}{file}" crossorigin="anonymous" """;

    public string File { get; } = file;
    public string Src { get; } = src;
    public string OutputDir { get; } = outputDir;
    public string[]? Css { get; set; } // If the entry imports CSS
    public string[]? Imports { get; set; } // Imported chunks
    public string[]? DynamicImports { get; set; }

    public string ScriptPath(string? nonce)
        => $"{_scriptPathPrefix}{(!string.IsNullOrEmpty(nonce) ? $@" nonce=""{nonce}""></script>" : "></script>")}";

    public string StylePath(string? nonce)
        => $"{_stylePathPrefix}{(!string.IsNullOrEmpty(nonce) ? $@" nonce=""{nonce}"">" : ">")}";

    public ViteManifestEntry WithOutputDir(string outputDir) => new ViteManifestEntry(File, Src, outputDir);
}