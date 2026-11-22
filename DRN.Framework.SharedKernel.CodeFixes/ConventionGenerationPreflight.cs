using DRN.Framework.SharedKernel.Analyzers.Conventions;

namespace DRN.Framework.SharedKernel.CodeFixes;

internal static class ConventionGenerationPreflight
{
    internal static StringComparison RootComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    internal sealed record ProjectLayout(string DomainLayout, string DefaultDomain, bool HasExplicitDomain, bool Valid)
    {
        internal bool IsFoldersLayout => DomainLayout == ConventionNames.FoldersLayout;

        internal bool Matches(ProjectLayout other) =>
            Valid && other.Valid && IsFoldersLayout == other.IsFoldersLayout &&
            (IsFoldersLayout || !HasExplicitDomain && !other.HasExplicitDomain ||
             string.Equals(DefaultDomain, other.DefaultDomain, StringComparison.Ordinal));

        internal bool Matches(CodeFixSymbolFacts.OwnerKey owner) =>
            Valid && IsFoldersLayout == owner.HasFolderDomain &&
            (IsFoldersLayout || !HasExplicitDomain && !owner.HasExplicitDomain ||
             string.Equals(DefaultDomain, owner.Domain, StringComparison.Ordinal));
    }

    internal static ProjectLayout Layout(Project project)
    {
        var global = project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GlobalOptions;
        global.TryGetValue("build_property.MSBuildProjectName", out var msbuildName);
        var projectName = string.IsNullOrWhiteSpace(msbuildName) ? project.Name : msbuildName;
        global.TryGetValue("build_property.DrnDomainLayout", out var rawLayout);
        global.TryGetValue("build_property.DrnDomain", out var rawDomain);
        return ParseLayout(projectName, rawLayout ?? "", rawDomain ?? "");
    }

    internal static (ProjectLayout Layout, string RootNamespace, bool HasExplicitRootNamespace) MetadataFromAssembly(IAssemblySymbol assembly)
    {
        var metadata = new List<KeyValuePair<string, string>>();
        foreach (var attribute in assembly.GetAttributes())
        {
            if (attribute.AttributeClass is not null &&
                MetadataName(attribute.AttributeClass) == "System.Reflection.AssemblyMetadataAttribute" &&
                ConventionNames.TryReadAssemblyMetadata(attribute, out var entry))
                metadata.Add(entry);
        }
        var parsed = ConventionNames.ParseReferenceMetadata(assembly.Name, metadata);
        if (!parsed.Valid || parsed.HasExplicitRootNamespace && !CodeFixSymbolFacts.ValidNamespace(parsed.RootNamespace))
            return (new("", "", false, false), "", false);
        return (ParseLayout(parsed.ProjectName, parsed.DomainLayout, parsed.Domain),
            parsed.RootNamespace, parsed.HasExplicitRootNamespace);
    }

    private static ProjectLayout ParseLayout(string projectName, string rawLayout, string rawDomain)
    {
        var (role, problem) = ConventionNames.Read(projectName);
        if (problem.Length > 0) return new("", "", false, false);
        var (layout, domain, explicitDomain, errorKey, _, _) =
            ConventionNames.ParseDomainLayout(projectName, role, rawLayout, rawDomain);
        if (errorKey.Length > 0 && layout == ConventionNames.FoldersLayout)
            return new("", "", false, false);
        return new(layout, domain, explicitDomain, errorKey.Length == 0);
    }

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

    internal static string EntityStem(string name) => ConventionNames.EntityStem(name);

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
