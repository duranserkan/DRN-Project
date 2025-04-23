namespace DRN.Framework.Hosting.Utils;

public static class ResourceExtractor
{
    public static void CopyWwwrootResourcesToDirectory(string targetDirectory)
    {
        var hostingAssembly = typeof(ResourceExtractor).Assembly;
        // Get all embedded resource names
        var resourceNames = hostingAssembly.GetManifestResourceNames();
        var rootNamespace = hostingAssembly.GetName().Name;
        var wwwrootPrefix = $"{rootNamespace}.wwwroot.";

        foreach (var resourceName in resourceNames)
        {
            // Skip resources not in wwwroot
            if (!resourceName.StartsWith(wwwrootPrefix))
                continue;

            // Convert a resource name to a relative path
            var relativePathSegments = resourceName
                .Substring(wwwrootPrefix.Length)
                .Split('.');

            var relativePath = string.Join('.', string.Join(Path.DirectorySeparatorChar, relativePathSegments[..^1]), relativePathSegments[^1]);
            var targetPath = Path.Combine(targetDirectory, relativePath);
            var targetDir = Path.GetDirectoryName(targetPath)!;

            // Create directory structure
            Directory.CreateDirectory(targetDir);

            // Extract resource to file
            using var resourceStream = hostingAssembly.GetManifestResourceStream(resourceName);
            using var fileStream = File.Create(targetPath);
            resourceStream!.CopyTo(fileStream);
        }
    }
}