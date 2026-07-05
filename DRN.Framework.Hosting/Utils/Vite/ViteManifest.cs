using System.Collections.Frozen;
using System.Text.Json;
using DRN.Framework.Hosting.Utils.Vite.Models;
using DRN.Framework.SharedKernel;
using DRN.Framework.SharedKernel.Extensions;
using DRN.Framework.Utils.Data.Encodings;
using DRN.Framework.Utils.Data.Hashing;
using DRN.Framework.Utils.DependencyInjection.Attributes;
using DRN.Framework.Utils.Extensions;
using Microsoft.AspNetCore.Hosting;

namespace DRN.Framework.Hosting.Utils.Vite;

public interface IViteManifest
{
    string ManifestRootPath { get; }
    ViteManifestWarmReport? WarmReport { get; }
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

    public ViteManifestWarmReport? WarmReport { get; internal set; }

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
        var canonicalManifestRoot = Path.GetFullPath(manifestRoot);
        var canonicalManifestFile = Path.GetFullPath(manifestFile);

        if (!canonicalManifestRoot.IsPathWithinDirectory(canonicalManifestFile))
            throw new ConfigurationException($"Vite manifest file is outside manifest root: {canonicalManifestFile}");

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
        var canonicalScriptDir = Path.GetFullPath(scriptDir);

        foreach (var (key, value) in manifest)
        {
            var fullFilePath = Path.GetFullPath(Path.Combine(scriptDir, value.File));

            if (!canonicalScriptDir.IsPathWithinDirectory(fullFilePath))
                throw new ConfigurationException($"Vite manifest at {manifestFile} references file outside its output directory: {value.File}");

            if (!File.Exists(fullFilePath))
                throw new ConfigurationException($"Vite manifest at {manifestFile} references missing file: {value.File} (resolved path: {fullFilePath})");

            var hash = fullFilePath.HashOfFile(HashAlgorithm.Sha256, ByteEncoding.Base64);

            cache[key] = value.FromCalculatedProperties(outputDir, hash);
        }
    }
}
