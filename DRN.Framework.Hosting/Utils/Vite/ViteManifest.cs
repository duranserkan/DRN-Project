using System.Collections.Frozen;
using System.Text.Json;
using DRN.Framework.Hosting.Utils.Vite.Models;
using DRN.Framework.SharedKernel;
using DRN.Framework.Utils.Data.Encodings;
using DRN.Framework.Utils.Data.Hashing;
using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Hosting.Utils.Vite;

public interface IViteManifest
{
    string ManifestRootPath { get; }
    ViteManifestWarmReport? WarmReport { get; }
    ViteManifestItem? GetManifestItem(string entryName);
    IReadOnlyCollection<ViteManifestItem> GetAllManifestItems();
}

[Singleton<IViteManifest>]
public class ViteManifest : IViteManifest
{
    private const string ManifestRootDefault = "wwwroot";
    private const string ViteManifestDirectoryName = ".vite";
    private const string ViteBuildOutputPrefix = "buildwww/";
    private const string NodeModulesPrefix = "node_modules/";

    private FrozenDictionary<string, ViteManifestItem>? _manifestCache;
    private readonly Lock _lock = new();

    public string ManifestRootPath
    {
        get;
        internal set
        {
            var manifestRootPath = string.IsNullOrWhiteSpace(value) ? ManifestRootDefault : value;
            if (string.Equals(field, manifestRootPath, StringComparison.Ordinal))
                return;

            lock (_lock)
            {
                if (string.Equals(field, manifestRootPath, StringComparison.Ordinal))
                    return;

                field = manifestRootPath;
                Volatile.Write(ref _manifestCache, null);
            }
        }
    } = ManifestRootDefault;

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
            var manifestRoots = GetManifestRootCandidates(ManifestRootPath);

            foreach (var manifestRoot in manifestRoots)
            {
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
            }

            Volatile.Write(ref _manifestCache, cache.ToFrozenDictionary());
        }
    }

    private static IReadOnlyCollection<string> GetManifestRootCandidates(string manifestRootPath)
    {
        var candidates = new List<string>();
        AddCandidate(candidates, manifestRootPath);
        AddCandidate(candidates, Path.Combine(manifestRootPath, ManifestRootDefault));
        AddCandidate(candidates, Path.Combine(AppContext.BaseDirectory, ManifestRootDefault));

        foreach (var contentRoot in GetStaticWebAssetContentRoots())
            AddCandidate(candidates, contentRoot);

        return candidates
            .Select(NormalizeDirectory)
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddCandidate(List<string> candidates, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        candidates.Add(path);
    }

    private static IReadOnlyCollection<string> GetStaticWebAssetContentRoots()
    {
        // Staging can run from build output while static assets are served from source roots.
        var roots = new List<string>();
        string[] runtimeManifestFiles;
        try
        {
            runtimeManifestFiles = Directory.GetFiles(AppContext.BaseDirectory, "*.staticwebassets.runtime.json", SearchOption.TopDirectoryOnly);
        }
        catch (Exception e)
        {
            _ = e;
            return roots;
        }

        foreach (var runtimeManifestFile in runtimeManifestFiles)
        {
            try
            {
                using var document = JsonDocument.Parse(File.ReadAllText(runtimeManifestFile));
                if (!document.RootElement.TryGetProperty("ContentRoots", out var contentRoots))
                    continue;

                foreach (var contentRoot in contentRoots.EnumerateArray())
                {
                    var path = contentRoot.GetString();
                    if (!string.IsNullOrWhiteSpace(path))
                        roots.Add(path);
                }
            }
            catch (Exception e)
            {
                _ = e;
            }
        }

        return roots;
    }

    private static string NormalizeDirectory(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath);
        return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static string[] GetManifestFiles(string manifestRoot)
    {
        try
        {
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
        catch (Exception e)
        {
            _ = e;
            return [];
        }
    }

    private static void AddManifestFile(Dictionary<string, ViteManifestItem> cache, string manifestRoot, string manifestFile)
    {
        using var stream = File.OpenRead(manifestFile);
        var manifest = JsonSerializer.Deserialize<Dictionary<string, ViteManifestItem>>(stream);

        if (manifest == null)
            return;

        var manifestDir = Path.GetDirectoryName(manifestFile)!;
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

            if (!IsPathWithinDirectory(canonicalScriptDir, fullFilePath))
                throw new ConfigurationException($"Vite manifest at {manifestFile} references file outside its output directory: {value.File}");

            if (!File.Exists(fullFilePath))
                throw new ConfigurationException($"Vite manifest at {manifestFile} references missing file: {value.File} (resolved path: {fullFilePath})");

            var hash = fullFilePath.HashOfFile(HashAlgorithm.Sha256, ByteEncoding.Base64);

            cache[key] = value.FromCalculatedProperties(outputDir, hash);
        }
    }

    private static bool IsPathWithinDirectory(string directoryPath, string filePath)
    {
        var relativePath = Path.GetRelativePath(directoryPath, filePath);
        return relativePath is not "." and not ".." &&
               !Path.IsPathRooted(relativePath) &&
               !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
               !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
    }
}
