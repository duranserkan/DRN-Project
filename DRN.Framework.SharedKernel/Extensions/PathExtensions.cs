namespace DRN.Framework.SharedKernel.Extensions;

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

    /// <summary>
    /// Combines the directory path with the provided path segments, ensuring that the resolved path stays physically 
    /// and logically within the directory. This method validates against parent directory traversal and symbolic link traversal.
    /// </summary>
    public static string GetPathWithinDirectory(this string directoryPath, params string[] segments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentNullException.ThrowIfNull(segments);

        if (segments.Length == 0)
            return directoryPath.NormalizeDirectoryPath();

        foreach (var segment in segments)
            if (string.IsNullOrWhiteSpace(segment))
                throw new ArgumentException("Path segments must not be null, empty, or whitespace.", nameof(segments));

        var combinedPath = segments.Aggregate(directoryPath, Path.Combine);
        var fullPath = Path.GetFullPath(combinedPath);

        if (!directoryPath.IsPathWithinDirectory(fullPath))
            throw new ArgumentException($"Resolved path '{fullPath}' must stay within directory '{directoryPath}'.", nameof(segments));

        return HasSymbolicLinkTraversal(directoryPath, fullPath)
            ? throw new ArgumentException($"Resolved path '{fullPath}' must stay physically within directory '{directoryPath}'.", nameof(segments))
            : fullPath;
    }

    private static bool HasSymbolicLinkTraversal(string directoryPath, string fullPath)
    {
        var normalizedRootPath = directoryPath.NormalizeDirectoryPath();
        var currentPath = fullPath.NormalizeDirectoryPath();

        while (!string.Equals(currentPath, normalizedRootPath, PathComparison))
        {
            if (IsSymbolicLinkOrReparsePoint(currentPath))
                return true;

            var parent = Directory.GetParent(currentPath);
            if (parent is null)
                return true;

            currentPath = parent.FullName;
        }

        return false;
    }

    private static bool IsSymbolicLinkOrReparsePoint(string path)
    {
        try
        {
            var attributes = Directory.Exists(path)
                ? new DirectoryInfo(path).Attributes
                : new FileInfo(path).Attributes;
                
            return (attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }
}