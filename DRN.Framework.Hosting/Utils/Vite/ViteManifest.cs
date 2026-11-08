using System.Collections.Frozen;
using System.Text.Json;
using DRN.Framework.Hosting.Utils.Vite.Models;
using DRN.Framework.SharedKernel;
using DRN.Framework.SharedKernel.Extensions;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Extensions;
using Microsoft.AspNetCore.Hosting;

namespace DRN.Framework.Hosting.Utils.Vite;

public interface IViteManifest
{
    string ManifestRootPath { get; }
    ViteManifestItem? GetManifestItem(string entryName);
    IReadOnlyCollection<ViteManifestItem> GetAllManifestItems();
}

[Singleton<IViteManifest>]
public class ViteManifest(IWebHostEnvironment environment) : IViteManifest
{
    private const string ManifestRootDefault = "wwwroot";
    private const string ViteManifestDirectoryName = ".vite";
    private const string ViteBuildOutputPrefix = "buildwww/";
    private const string NodeModulesPrefix = "node_modules/";

    private FrozenDictionary<string, ViteManifestItem>? _manifestCache;
    private readonly Lock _lock = new();

    public string ManifestRootPath => ResolveManifestRootPath();

    public ViteManifestItem? GetManifestItem(string entryName)
    {
        ViteManifestItem? entry;
        var cache = _manifestCache;
        if (cache != null)
            return cache.TryGetValue(entryName, out entry) ? entry : null;

        EnsureManifest();
        cache = Volatile.Read(ref _manifestCache);

        return cache != null && cache.TryGetValue(entryName, out entry) ? entry : null;
    }

    /// <summary>
    /// Returns all parsed Vite manifest items across all discovered .vite/manifest.json files.
    /// Triggers lazy initialization if the manifest has not been loaded yet.
    /// </summary>
    public IReadOnlyCollection<ViteManifestItem> GetAllManifestItems()
    {
        var cache = _manifestCache;
        if (cache != null) return cache.Values;

        EnsureManifest();
        cache = Volatile.Read(ref _manifestCache);

        return cache != null ? cache.Values : Array.Empty<ViteManifestItem>();
    }

    public static bool IsViteOrigin(string path) =>
        path.StartsWith(ViteBuildOutputPrefix, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(NodeModulesPrefix, StringComparison.OrdinalIgnoreCase);

    private void EnsureManifest()
    {
        if (Volatile.Read(ref _manifestCache) != null)
            return;

        lock (_lock)
        {
            if (Volatile.Read(ref _manifestCache) != null)
                return;

            var cache = new Dictionary<string, ViteManifestItem>();
            var manifestRoot = ManifestRootPath;
            var manifestFiles = GetManifestFiles(manifestRoot);

            foreach (var manifestFile in manifestFiles)
                try
                {
                    AddManifestFile(cache, manifestRoot, manifestFile);
                }
                catch (ConfigurationException)
                {
                    throw;
                }
                catch (Exception e)
                {
                    throw new ConfigurationException($"Failed to parse Vite manifest at {manifestFile}", e);
                }

            Volatile.Write(ref _manifestCache, cache.ToFrozenDictionary());
        }
    }

    private string ResolveManifestRootPath()
    {
        var manifestRoot = string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(environment.ContentRootPath, ManifestRootDefault)
            : environment.WebRootPath;

        return manifestRoot.NormalizeDirectoryPath();
    }

    private static string[] GetManifestFiles(string manifestRoot)
    {
        if (!Directory.Exists(manifestRoot))
            return [];

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.None,
            IgnoreInaccessible = true
        };

        return Directory.GetDirectories(manifestRoot, ViteManifestDirectoryName, options)
            .Select(manifestDir => Path.Combine(manifestDir, "manifest.json"))
            .Where(File.Exists)
            .ToArray();
    }

    private static void AddManifestFile(Dictionary<string, ViteManifestItem> cache, string manifestRoot, string manifestFile)
    {
        var canonicalManifestFile = manifestRoot.GetPathWithinDirectory(manifestFile);

        using var stream = File.OpenRead(canonicalManifestFile);
        var manifest = JsonSerializer.Deserialize<Dictionary<string, ViteManifestItem>>(stream);

        if (manifest == null)
            return;

        var manifestDir = Path.GetDirectoryName(canonicalManifestFile)!;
        var scriptDir = Path.GetDirectoryName(manifestDir)!;
        var relativeDir = Path.GetRelativePath(manifestRoot, scriptDir)
            .Replace(Path.DirectorySeparatorChar, '/')
            .Replace(Path.AltDirectorySeparatorChar, '/')
            .Trim('/');
        var outputDir = string.IsNullOrWhiteSpace(relativeDir) || relativeDir == "." ? "/" : $"/{relativeDir}/";

        Span<byte> digest = stackalloc byte[32];
        foreach (var (key, value) in manifest)
        {
            var originalPath = ResolveAssetPath(value.File);
            var sidecarPath = ResolveAssetPath(value.File + ".br");
            var deployedFile = value.BrotliOnly ? value.File + ".br" : value.File;
            var deployedPath = value.BrotliOnly ? sidecarPath : originalPath;

            if (!File.Exists(deployedPath))
                throw new ConfigurationException($"Vite manifest at {manifestFile} references missing file: {deployedFile} (resolved path: {deployedPath})");

            if (value.Hash is not { Length: 44 } || !Convert.TryFromBase64String(value.Hash, digest, out var written) || written != digest.Length)
                throw new ConfigurationException($"Vite manifest at {manifestFile} requires a base64 SHA-256 hash for {value.File}. Configure the frontend build to hash the uncompressed bytes and regenerate the manifest.");

            cache[key] = value.WithOutputDirectory(outputDir, value.BrotliOnly || File.Exists(sidecarPath));
        }

        string ResolveAssetPath(string file)
        {
            try
            {
                return scriptDir.GetPathWithinDirectory(file);
            }
            catch (ArgumentException e)
            {
                throw new ConfigurationException($"Vite manifest at {manifestFile} references file outside its output directory: {file}", e);
            }
        }
    }
}
