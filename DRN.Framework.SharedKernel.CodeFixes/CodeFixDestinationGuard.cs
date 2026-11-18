using Microsoft.CodeAnalysis;

namespace DRN.Framework.SharedKernel.CodeFixes;

internal static class CodeFixDestinationGuard
{
    internal static bool SafeDestination(string root, string destination)
    {
        try
        {
            var trimmedRoot = Path.TrimEndingDirectorySeparator(root);
            if (!ConventionGenerationPreflight.IsUnderDirectory(destination, trimmedRoot) || File.Exists(destination) ||
                Directory.Exists(destination) || new FileInfo(destination).LinkTarget is not null) return false;
            var destinationDirectory = Path.GetDirectoryName(destination)!;
            if (Directory.Exists(destinationDirectory) && Directory.EnumerateFileSystemEntries(destinationDirectory)
                .Any(entry => string.Equals(Path.GetFileName(entry), Path.GetFileName(destination), StringComparison.OrdinalIgnoreCase))) return false;
            return AreParentDirectoriesSafe(destinationDirectory, trimmedRoot);
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
    }

    internal static bool SafeDocumentDestination(Project project, string root, string destination, CancellationToken token) =>
        SafeDestination(root, destination) && !WorkspaceDestinationConflict(project.Solution, destination, project.Id, token);

    private static bool AreParentDirectoriesSafe(string destinationDirectory, string trimmedRoot)
    {
        for (var directory = new DirectoryInfo(destinationDirectory); directory is not null; directory = directory.Parent)
        {
            if (string.Equals(Path.TrimEndingDirectorySeparator(directory.FullName), trimmedRoot, ConventionGenerationPreflight.RootComparison))
                return !File.Exists(directory.FullName);
            if (File.Exists(directory.FullName) || directory.LinkTarget is not null || HasDirectoryCaseMismatch(directory))
                return false;
        }
        return false;
    }

    private static bool HasDirectoryCaseMismatch(DirectoryInfo directory) =>
        directory.Parent is { Exists: true } parent &&
        Directory.EnumerateFileSystemEntries(parent.FullName).Any(entry =>
            string.Equals(Path.GetFileName(entry), directory.Name, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(Path.GetFileName(entry), directory.Name, StringComparison.Ordinal));

    internal static bool TryFullPath(string path, out string fullPath)
    {
        fullPath = "";
        try
        {
            if (!Path.IsPathFullyQualified(path)) return false;
            fullPath = Path.GetFullPath(path);
            return true;
        }
        catch (ArgumentException) { return false; }
        catch (NotSupportedException) { return false; }
        catch (IOException) { return false; }
    }

    internal static bool WorkspaceDestinationConflict(Solution solution, string destination, ProjectId targetProjectId, CancellationToken token = default)
    {
        var destinationName = Path.GetFileName(destination);
        foreach (var project in solution.Projects)
        {
            if (ProjectConflictsWithDestination(project, destination, destinationName, targetProjectId, token))
                return true;
        }
        return false;
    }

    private static bool ProjectConflictsWithDestination(Project project, string destination, string destinationName,
        ProjectId targetProjectId, CancellationToken token)
    {
        var projectDirectory = project.FilePath is { } projectPath && TryFullPath(projectPath, out var fullProject)
            ? Path.GetDirectoryName(fullProject) : null;
        foreach (var document in project.Documents.Cast<TextDocument>().Concat(project.AdditionalDocuments).Concat(project.AnalyzerConfigDocuments))
        {
            token.ThrowIfCancellationRequested();
            if (DocumentConflictsWithDestination(project, document, projectDirectory, destination, destinationName, targetProjectId))
                return true;
        }
        return false;
    }

    private static bool DocumentConflictsWithDestination(Project project, TextDocument document, string? projectDirectory,
        string destination, string destinationName, ProjectId targetProjectId)
    {
        if (document.FilePath is not { } path)
            return string.Equals(document.Name, destinationName, StringComparison.OrdinalIgnoreCase);
        if (TryFullPath(path, out var existing) ||
            projectDirectory is not null && !Path.IsPathFullyQualified(path) &&
            TryFullPath(Path.Combine(projectDirectory, path), out existing))
            return WorkspacePathConflict(existing, destination);
        if (project.Id == targetProjectId) return true;
        var fallbackName = SafeFileName(path);
        return string.Equals(string.IsNullOrEmpty(fallbackName) ? document.Name : fallbackName,
            destinationName, StringComparison.OrdinalIgnoreCase);
    }

    private static string SafeFileName(string path)
    {
        try { return Path.GetFileName(path); }
        catch (ArgumentException) { return ""; }
    }

    internal static bool WorkspacePathConflict(string path, string destination)
    {
        if (!TryFullPath(path, out var existing)) return true;
        if (string.Equals(existing, destination, StringComparison.OrdinalIgnoreCase)) return true;
        if (destination.StartsWith(existing + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            existing.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return true;
        var existingParts = existing.Split(Path.DirectorySeparatorChar);
        var destinationParts = destination.Split(Path.DirectorySeparatorChar);
        for (var index = 0; index < Math.Min(existingParts.Length, destinationParts.Length) - 1; index++)
        {
            if (!string.Equals(existingParts[index], destinationParts[index], StringComparison.OrdinalIgnoreCase)) return false;
            var comparison = index == 0 ? ConventionGenerationPreflight.RootComparison : StringComparison.Ordinal;
            if (!string.Equals(existingParts[index], destinationParts[index], comparison)) return true;
        }
        return false;
    }
}
