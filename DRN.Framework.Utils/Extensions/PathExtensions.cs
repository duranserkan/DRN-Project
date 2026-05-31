namespace DRN.Framework.Utils.Extensions;

public static class PathExtensions
{
    public static readonly StringComparison PathComparison = 
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    
    /// <summary>
    /// Resolves a directory path to a full path and removes trailing directory separators without trimming filesystem roots.
    /// </summary>
    public static string NormalizeDirectoryPath(this string path) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    /// <summary>
    /// Returns whether <paramref name="filePath"/> resolves under <paramref name="directoryPath"/> using full-path,
    /// path-segment-aware containment checks. This method does not resolve symbolic links.
    /// </summary>
    public static bool IsPathWithinDirectory(this string directoryPath, string filePath)
    {
        var normalizedDirectoryPath = directoryPath.NormalizeDirectoryPath();
        var fullFilePath = Path.GetFullPath(filePath);
        if (string.Equals(fullFilePath, normalizedDirectoryPath, PathComparison))
            return true;

        var directoryPrefix = Path.EndsInDirectorySeparator(normalizedDirectoryPath)
            ? normalizedDirectoryPath
            : $"{normalizedDirectoryPath}{Path.DirectorySeparatorChar}";

        return fullFilePath.StartsWith(directoryPrefix, PathComparison);
    }
}
