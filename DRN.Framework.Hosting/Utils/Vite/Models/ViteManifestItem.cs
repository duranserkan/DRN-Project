using System.Text.Json.Serialization;

namespace DRN.Framework.Hosting.Utils.Vite.Models;

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