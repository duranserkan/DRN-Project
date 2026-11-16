using DRN.Framework.SharedKernel.Analyzers.Conventions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DRN.Framework.SharedKernel.CodeFixes;

internal static class ConventionGenerationPreflight
{
    internal static StringComparison RootComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    internal static (string Role, string Namespace) Identity(Project project)
    {
        var global = project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GlobalOptions;
        global.TryGetValue("build_property.MSBuildProjectName", out var msbuildName);
        var projectName = string.IsNullOrWhiteSpace(msbuildName) ? project.Name : msbuildName;
        var (role, problem) = ConventionNames.Read(projectName);
        if (problem.Length > 0 || role.Length == 0) return ("", "");
        global.TryGetValue("build_property.RootNamespace", out var ns);
        return (role, string.IsNullOrEmpty(ns) ? projectName : ns);
    }

    internal static string DirectoryPrefix(string directory)
    {
        var trimmed = Path.TrimEndingDirectorySeparator(directory);
        return trimmed.EndsWith(Path.DirectorySeparatorChar) ? trimmed : trimmed + Path.DirectorySeparatorChar;
    }

    internal static bool IsUnderDirectory(string path, string directory) =>
        path.StartsWith(DirectoryPrefix(directory), RootComparison);

    internal static string EntityStem(string name) =>
        name.Length > "Entity".Length && name.EndsWith("Entity", StringComparison.Ordinal) ? name[..^"Entity".Length] : name;

    internal static bool ValidNamespaceSegment(string segment) =>
        SyntaxFacts.IsValidIdentifier(segment) && !segment.StartsWith('@') && SyntaxFacts.GetKeywordKind(segment) == SyntaxKind.None;

    private const string InputsFileName = "DrnConventionInputs.g.cs";

    internal static bool IsInputsDocument(TextDocument document) =>
        LastSegment(document.FilePath ?? document.Name) == InputsFileName ||
        LastSegment(document.Name) == InputsFileName;

    internal static string MetadataName(INamedTypeSymbol type)
    {
        var typeParts = new Stack<string>();
        INamespaceSymbol? outermostNamespace = null;
        for (var current = type; current is not null; current = current.ContainingType)
        {
            typeParts.Push(current.MetadataName);
            if (current.ContainingType is null) outermostNamespace = current.ContainingNamespace;
        }
        var namespaceParts = new Stack<string>();
        for (var ns = outermostNamespace; ns is not null && !ns.IsGlobalNamespace; ns = ns.ContainingNamespace)
            namespaceParts.Push(ns.MetadataName);
        var qualifiedType = string.Join("+", typeParts);
        return namespaceParts.Count == 0 ? qualifiedType : string.Join(".", namespaceParts) + "." + qualifiedType;
    }

    private static string LastSegment(string value) => value.Replace('\\', '/').Split('/')[^1];

    internal static async Task<CompilationUnitSyntax?> ReadInputs(Project project, bool required, CancellationToken token)
    {
        var files = project.AdditionalDocuments.Where(IsInputsDocument).Take(2).ToArray();
        if (files.Length == 0) return required ? null : SyntaxFactory.CompilationUnit();
        if (files.Length != 1) return null;
        var root = ConventionInputSyntax.Parse(await files[0].GetTextAsync(token), files[0].FilePath ?? files[0].Name, token, out _);
        return root is not null && ConventionInputSyntax.SupportsGeneration(root, token) ? root : null;
    }

    internal static bool SymbolCollision(Compilation compilation, string ns, string name, CancellationToken token)
    {
        INamespaceSymbol[] parents = [compilation.GlobalNamespace];
        foreach (var rawSegment in string.IsNullOrEmpty(ns) ? [] : ns.Split('.'))
        {
            var segment = rawSegment.TrimStart('@');
            token.ThrowIfCancellationRequested();
            var matches = parents.SelectMany(parent => parent.GetMembers())
                .Where(member => string.Equals(member.Name, segment, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Any(member => member is INamedTypeSymbol)) return true;
            parents = matches.OfType<INamespaceSymbol>().ToArray();
            if (parents.Length == 0) return false;
        }
        var targetName = name.TrimStart('@');
        return parents.SelectMany(parent => parent.GetMembers()).Any(member =>
            string.Equals(member.Name, targetName, StringComparison.OrdinalIgnoreCase));
    }
}
