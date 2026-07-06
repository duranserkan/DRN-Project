using DRN.Framework.SharedKernel.Extensions;

namespace DRN.Framework.Utils.Data.App;

public enum AppDataPathStatus
{
    Valid = 0,
    PathNotFound = 1,
    InvalidPath = 2,
    EmptyPath = 3
}

/// <summary>
/// Represents a resolved application data root path.
/// </summary>
public sealed record AppDataPathResult
{
    private static AppDataPathResult Empty() => new(string.Empty, false, AppDataPathStatus.EmptyPath);
    internal static AppDataPathResult Invalid() => new(string.Empty, false, AppDataPathStatus.InvalidPath);

    internal static AppDataPathResult From(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Empty();

        try
        {
            var normalizedPath = path.NormalizeDirectoryPath();
            var directoryExists = Directory.Exists(normalizedPath);
            var status = directoryExists ? AppDataPathStatus.Valid : AppDataPathStatus.PathNotFound;

            return new AppDataPathResult(normalizedPath, directoryExists, status);
        }
        catch (Exception)
        {
            return Invalid();
        }
    }

    private AppDataPathResult(string path, bool directoryExists, AppDataPathStatus status)
    {
        Path = path;
        DirectoryExists = directoryExists;
        Status = status;
    }

    /// <summary>
    /// The resolved file system path, or a sentinel value when no usable path can be resolved.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Whether <see cref="Path"/> exists as a directory at resolution time.
    /// </summary>
    public bool DirectoryExists { get; }

    /// <summary>
    /// The path resolution state.
    /// </summary>
    public AppDataPathStatus Status { get; }

    /// <summary>
    /// Resolves a child path and rejects traversal outside the application data root.
    /// </summary>
    public string GetPath(params string[] segments) => Status is AppDataPathStatus.EmptyPath or AppDataPathStatus.InvalidPath
        ? throw new InvalidOperationException($"Cannot resolve child paths for app data path status '{Status}'.")
        : Path.GetPathWithinDirectory(segments);
}