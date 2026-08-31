using System.Diagnostics;
using DRN.Framework.Hosting.Utils.Vite;
using DRN.Framework.Hosting.Utils.Vite.Models;
using Microsoft.Extensions.Configuration;

namespace DRN.Framework.Testing.Contexts.Application;

internal static class ApplicationContextHelper
{
    private static readonly string[] AddressSuffixes = ["Address", "Url", "Uri"];

    public static ITestOutputHelper? ResolveOutputHelper(ITestOutputHelper? supplied = null, bool debuggerOnly = true)
    {
        if (debuggerOnly && !Debugger.IsAttached)
            return null;

        return supplied ?? TestContext.Current.TestOutputHelper;
    }

    public static void DiscoverConfiguredAddresses(IConfiguration configuration, Type entryPointType, List<string> addresses)
    {
        var typeName = entryPointType.Name;
        var shortName = ApplicationContextRouterHandler.GetShortName(typeName);

        var kestrelEndpoints = configuration.GetSection("Kestrel:Endpoints");
        if (kestrelEndpoints.Exists())
        {
            foreach (var endpoint in kestrelEndpoints.GetChildren())
            {
                var url = endpoint["Url"];
                if (!string.IsNullOrWhiteSpace(url))
                    addresses.Add(url);
            }
        }

        foreach (var kvp in configuration.AsEnumerable())
        {
            if (string.IsNullOrWhiteSpace(kvp.Value))
                continue;

            var key = kvp.Key;
            var isAddressKey = key.EndsWith("Address", StringComparison.OrdinalIgnoreCase) ||
                               key.EndsWith("Url", StringComparison.OrdinalIgnoreCase) ||
                               key.EndsWith("Uri", StringComparison.OrdinalIgnoreCase);

            if (!isAddressKey)
                continue;

            var segments = key.Split(':');
            if (MatchesAddressKey(segments, typeName, shortName))
                addresses.Add(kvp.Value);
        }
    }

    private static bool MatchesAddressKey(string[] segments, string typeName, string shortName)
    {
        if (segments.Length == 0)
            return false;

        var leaf = segments[^1];
        if (MatchesAddressSegment(leaf, typeName, shortName))
            return true;

        if (segments.Length > 1 && AddressSuffixes.Any(suffix => leaf.Equals(suffix, StringComparison.OrdinalIgnoreCase)))
        {
            var parent = segments[^2];
            return IsExactMatch(parent, typeName, shortName);
        }

        return false;
    }

    private static bool MatchesAddressSegment(string segment, string typeName, string shortName)
    {
        foreach (var suffix in AddressSuffixes)
        {
            if (segment.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && segment.Length > suffix.Length)
            {
                var prefix = segment[..^suffix.Length];
                if (IsExactMatch(prefix, typeName, shortName))
                    return true;
            }
        }

        return false;
    }

    private static bool IsExactMatch(string value, string typeName, string shortName) =>
        value.Equals(typeName, StringComparison.OrdinalIgnoreCase) ||
        (!string.IsNullOrEmpty(shortName) && value.Equals(shortName, StringComparison.OrdinalIgnoreCase));

    public static string ResolveWebRootPath(Type programType)
    {
        var assemblyName = programType.Assembly.GetName().Name ?? string.Empty;
        var baseDir = AppContext.BaseDirectory;

        var current = new DirectoryInfo(baseDir);
        while (current != null)
        {
            var candidate = Path.Combine(current.FullName, assemblyName, "wwwroot");
            if (Directory.Exists(candidate))
                return candidate;

            current = current.Parent;
        }

        return string.Empty;
    }
}

internal sealed class EmptyViteManifest : IViteManifest
{
    public string ManifestRootPath => string.Empty;
    public ViteManifestWarmReport? WarmReport => null;
    public ViteManifestItem? GetManifestItem(string entryName) => null;
    public IReadOnlyCollection<ViteManifestItem> GetAllManifestItems() => [];
}
