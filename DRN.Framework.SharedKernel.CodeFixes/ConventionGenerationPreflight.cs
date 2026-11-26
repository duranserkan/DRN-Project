using DRN.Framework.SharedKernel.Analyzers.Conventions;

namespace DRN.Framework.SharedKernel.CodeFixes;

internal static class ConventionGenerationPreflight
{
    internal static StringComparison RootComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    internal readonly record struct ProjectMetadata(string Role, string Namespace, ProjectLayout Layout, string ProjectName = "");

    internal sealed record ProjectLayout(string DomainLayout, string DefaultDomain, bool HasExplicitDomain, bool Valid)
    {
        internal bool IsFoldersLayout => DomainLayout == ConventionNames.FoldersLayout;

        internal bool TryResolveTargetOwner(CodeFixSymbolFacts.OwnerKey sourceOwner, out CodeFixSymbolFacts.OwnerKey targetOwner)
        {
            targetOwner = default!;
            if (!Valid || !ValidNamespaceSegment(sourceOwner.Subdomain)) return false;
            if (IsFoldersLayout)
            {
                if (!ValidNamespaceSegment(sourceOwner.Domain)) return false;
                targetOwner = new(sourceOwner.Domain, sourceOwner.Subdomain, HasFolderDomain: true, HasExplicitDomain: false);
            }
            else
            {
                targetOwner = new(DefaultDomain, sourceOwner.Subdomain, HasFolderDomain: false, HasExplicitDomain);
            }
            return sourceOwner.MatchesAcrossLayers(targetOwner);
        }
    }

    internal sealed record AssemblyConventionMetadata(ProjectLayout Layout, string ProjectName, string RootNamespace,
        bool HasExplicitProjectName, bool HasExplicitRootNamespace, bool HasExplicitDomainLayout, bool HasExplicitDomain,
        bool HasRoleOverrides, bool HasMetadataError, List<KeyValuePair<string, string>> Metadata)
    {
        private bool MatchesProducerLayout(CodeFixSymbolFacts.OwnerKey owner, ProjectMetadata? loadedProject)
        {
            if (HasMetadataError || HasRoleOverrides) return false;
            if (!Layout.Valid) return CanUseAbsentMetadataFallback(loadedProject);
            if (loadedProject is { } loaded && !MatchesLoadedIdentity(loaded)) return false;

            var isFoldersLayout = loadedProject?.Layout.IsFoldersLayout ?? owner.HasFolderDomain;
            if (HasExplicitDomainLayout && Layout.IsFoldersLayout != isFoldersLayout) return false;
            if (!HasExplicitDomain) return true;
            return !isFoldersLayout && Layout.DefaultDomain == owner.Domain;
        }

        private bool MatchesLoadedIdentity(ProjectMetadata loaded) =>
            (!HasExplicitProjectName || ProjectName == loaded.ProjectName) &&
            (!HasExplicitRootNamespace || RootNamespace == loaded.Namespace);

        // Loaded projects can supply a layout when the assembly omits explicit identity and layout metadata.
        private bool CanUseAbsentMetadataFallback(ProjectMetadata? loadedProject) =>
            loadedProject is { Layout.Valid: true } &&
            !HasExplicitProjectName && !HasExplicitRootNamespace && !HasExplicitDomainLayout && !HasExplicitDomain;

        internal bool TryValidateProducerOwnership(string entityName, CodeFixSymbolFacts.OwnerKey owner,
            out bool hasExplicitDomain, ProjectMetadata? loadedProject = null)
        {
            hasExplicitDomain = false;
            if (!MatchesProducerLayout(owner, loadedProject)) return false;
            if (!TryReadEntityOwnershipRecord(entityName, out var hasRecord, out var domain, out var subdomain, out var kind))
                return false;
            if (!hasRecord)
            {
                hasExplicitDomain = Layout.HasExplicitDomain;
                return true;
            }
            if (subdomain is null || subdomain != owner.Subdomain) return false;
            if (kind is not (null or "" or ConventionInputSyntax.EntityField)) return false;
            if (string.IsNullOrEmpty(domain))
            {
                hasExplicitDomain = Layout.HasExplicitDomain;
                return true;
            }
            if (domain != owner.Domain) return false;
            hasExplicitDomain = true;
            return true;
        }

        private bool TryReadEntityOwnershipRecord(string entityName, out bool hasRecord,
            out string? domain, out string? subdomain, out string? kind)
        {
            var prefix = ConventionNames.OwnershipMetadataPrefix + entityName + ".";
            domain = null;
            subdomain = null;
            string? reason = null;
            kind = null;
            hasRecord = false;
            foreach (var (key, value) in Metadata)
            {
                if (!key.StartsWith(prefix, StringComparison.Ordinal)) continue;
                var field = key[prefix.Length..];
                hasRecord = true;
                var valid = field switch
                {
                    ConventionInputSyntax.DomainField => TrySetField(ref domain, value),
                    ConventionInputSyntax.SubdomainField => TrySetField(ref subdomain, value),
                    ConventionInputSyntax.ReasonField => TrySetField(ref reason, value),
                    ConventionInputSyntax.KindField => TrySetField(ref kind, value),
                    _ => false
                };
                if (!valid) return false;
            }
            return true;
        }

        private static bool TrySetField(ref string? field, string value)
        {
            if (field is not null) return field == value;
            field = value;
            return true;
        }
    }

    internal static ProjectMetadata ReadProject(Project project)
    {
        var global = project.AnalyzerOptions.AnalyzerConfigOptionsProvider.GlobalOptions;
        global.TryGetValue("build_property.MSBuildProjectName", out var msbuildName);
        var projectName = string.IsNullOrWhiteSpace(msbuildName) ? project.Name : msbuildName;
        var (role, problem) = ConventionNames.Read(projectName);
        if (problem.Length > 0) return new("", "", new("", "", false, false), projectName);
        global.TryGetValue("build_property.DrnDomainLayout", out var rawLayout);
        global.TryGetValue("build_property.DrnDomain", out var rawDomain);
        var layout = ParseLayoutForRole(projectName, role, rawLayout ?? "", rawDomain ?? "");
        if (role.Length == 0) return new("", "", layout, projectName);
        global.TryGetValue("build_property.RootNamespace", out var ns);
        return new(role, string.IsNullOrEmpty(ns) ? projectName : ns, layout, projectName);
    }

    internal static AssemblyConventionMetadata MetadataFromAssembly(IAssemblySymbol assembly)
    {
        var metadata = new List<KeyValuePair<string, string>>();
        var hasRoleOverrides = false;
        foreach (var attribute in assembly.GetAttributes())
        {
            var isAssemblyMetadata = attribute.AttributeClass is { Name: "AssemblyMetadataAttribute" } attributeClass &&
                MetadataName(attributeClass) == "System.Reflection.AssemblyMetadataAttribute";
            if (!isAssemblyMetadata || !ConventionNames.TryReadAssemblyMetadata(attribute, out var entry))
                continue;
            if (entry.Key.StartsWith(ConventionNames.RoleMetadataPrefix, StringComparison.Ordinal))
                hasRoleOverrides = true;
            metadata.Add(entry);
        }
        var parsed = ConventionNames.ParseReferenceMetadata(assembly.Name, metadata);
        var hasInvalidMetadata = !parsed.Valid || hasRoleOverrides ||
            (parsed.HasExplicitRootNamespace && !CodeFixSymbolFacts.ValidNamespace(parsed.RootNamespace));
        if (hasInvalidMetadata)
            return new(new("", "", false, false), "", "", false, false, false, false, hasRoleOverrides, HasMetadataError: true, metadata);

        var (role, problem) = ConventionNames.Read(parsed.ProjectName);
        if (problem.Length > 0)
            return new(new("", "", false, false), parsed.ProjectName, parsed.RootNamespace,
                parsed.HasExplicitProjectName, parsed.HasExplicitRootNamespace,
                parsed.HasExplicitDomainLayout, parsed.HasExplicitDomain,
                HasRoleOverrides: false, HasMetadataError: parsed.HasExplicitProjectName, metadata);

        var layout = ParseLayoutForRole(parsed.ProjectName, role, parsed.DomainLayout, parsed.Domain);
        return new(layout, parsed.ProjectName, parsed.RootNamespace,
            parsed.HasExplicitProjectName, parsed.HasExplicitRootNamespace,
            parsed.HasExplicitDomainLayout, parsed.HasExplicitDomain,
            HasRoleOverrides: false, HasMetadataError: false, metadata);
    }

    private static ProjectLayout ParseLayoutForRole(string projectName, string role, string rawLayout, string rawDomain)
    {
        var (layout, domain, explicitDomain, errorKey, _, _) =
            ConventionNames.ParseDomainLayout(projectName, role, rawLayout, rawDomain);
        if (errorKey.Length > 0 && layout == ConventionNames.FoldersLayout)
            return new("", "", false, false);
        return new(layout, domain, explicitDomain, errorKey.Length == 0);
    }

    internal static (string Role, string Namespace) Identity(Project project)
    {
        var info = ReadProject(project);
        return (info.Role, info.Namespace);
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

    internal static string MetadataName(INamedTypeSymbol type) => ConventionNames.MetadataName(type);

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
