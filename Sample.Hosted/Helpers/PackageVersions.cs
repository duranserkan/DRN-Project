using System.Text.Json;

namespace Sample.Hosted.Helpers;

/// <summary>
/// Provides cached, read-once access to frontend package versions from package.json.
/// Parsed lazily on first access; values never change at runtime.
/// </summary>
public sealed class PackageVersions
{
    public static PackageVersions Instance { get; } = Parse();

    public string Dotnet { get; } = Environment.Version.ToString();
    public string React { get; private init; } = "19";
    public string Bootstrap { get; private init; } = "5.3";
    public string Tailwind { get; private init; } = "4.2";
    public string Vite { get; private init; } = "8";

    private PackageVersions() { }

    private static PackageVersions Parse()
    {
        var versions = new PackageVersions();
        try
        {
            var packageJsonPath = Path.Combine(Environment.CurrentDirectory, "package.json");
            if (!File.Exists(packageJsonPath))
                return versions;

            using var doc = JsonDocument.Parse(File.ReadAllText(packageJsonPath));

            return new PackageVersions
            {
                React = ReadVersion(doc, "dependencies", "react") ?? versions.React,
                Bootstrap = ReadVersion(doc, "dependencies", "bootstrap") ?? versions.Bootstrap,
                Tailwind = ReadVersion(doc, "dependencies", "tailwindcss") ?? versions.Tailwind,
                Vite = ReadVersion(doc, "devDependencies", "vite") ?? versions.Vite
            };
        }
        catch
        {
            // Graceful degradation: return hardcoded defaults if parsing fails
            return versions;
        }
    }

    private static string? ReadVersion(JsonDocument doc, string section, string package)
    {
        if (!doc.RootElement.TryGetProperty(section, out var sectionElement))
            return null;
        if (!sectionElement.TryGetProperty(package, out var versionElement))
            return null;

        return versionElement.GetString()?
            .Replace("^", "")
            .Replace("~", "");
    }
}
