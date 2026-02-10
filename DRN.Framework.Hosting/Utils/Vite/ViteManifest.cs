using DRN.Framework.Hosting.Utils.Vite.Models;
using DRN.Framework.SharedKernel;
using DRN.Framework.Utils.Data.Encodings;
using DRN.Framework.Utils.Data.Hashing;
using DRN.Framework.Utils.Data.Serialization;
using DRN.Framework.Utils.DependencyInjection.Attributes;

namespace DRN.Framework.Hosting.Utils.Vite;

public interface IViteManifest
{
    ViteManifestPreWarmReport? PreWarmReport { get; }
    ViteManifestItem? GetManifestItem(string entryName);
    IReadOnlyCollection<ViteManifestItem> GetAllManifestItems(string manifestRootPath = "");
}

[Singleton<IViteManifest>]
public class ViteManifest : IViteManifest
{
    private string _manifestRootPath = "";
    private const string ViteBuildOutputPrefix = "buildwww/";
    private const string NodeModulesPrefix = "node_modules/";

    private volatile Dictionary<string, ViteManifestItem>? _manifestCache;
    private readonly Lock _lock = new();

    public ViteManifestPreWarmReport? PreWarmReport { get; internal set; }

    public ViteManifestItem? GetManifestItem(string entryName)
    {
        if (_manifestCache == null)
            EnsureManifest();

        return _manifestCache!.TryGetValue(entryName, out var entry) ? entry : null;
    }

    /// <summary>
    /// Returns all parsed Vite manifest items across all discovered manifest.json files.
    /// Triggers lazy initialization if the manifest has not been loaded yet.
    /// </summary>
    public IReadOnlyCollection<ViteManifestItem> GetAllManifestItems(string manifestRootPath = "")
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

    private void EnsureManifest()
    {
        if (_manifestCache != null)
            return;

        lock (_lock)
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